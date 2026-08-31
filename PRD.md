# PRD — AI-Powered Tutoring Chat

**One-liner:** A curriculum-grounded chat interface that lets students ask questions and receive answers restricted to the material they have studied at their grade level.

---

## Behavioral Specification

**When a student sends a message:**
The system embeds the query, searches Qdrant filtered to grades 1 through N (where N is the student's enrolled grade), and injects the top-k matching curriculum chunks as context before calling the LLM. The LLM is instructed to answer only from the provided context.

**Response language:** The LLM is instructed to answer in the same language the student used in their question, not a fixed language — this applies to chat answers, mock exams (`POST /api/student/exam`), and practice questions (`POST /api/student/practice-questions`). The app UI itself is in English regardless; response language is independent of UI language. Language is determined by `LanguageDetectionService` (offline n-gram detection) rather than left to the model to infer, since inference alone was unreliable — see `CLAUDE.md`'s Language Policy section for the current implementation and its known limits with mock exam generation.

**When the LLM response contains a `<STEREO>…</STEREO>` block:**
The system extracts the JSON scene description and returns it as a structured `scene` field alongside the text response so the frontend can render an interactive 3D geometry visualisation. The frontend fetches the rendered visualisation via `POST /api/chat/scene-html`, which takes that extracted scene JSON and returns standalone Three.js HTML (`StereometryHtmlBuilder.Build`) for display in a sandboxed `<iframe srcdoc>`.

**When a student deletes a chat session (`DELETE /api/chat/sessions/{id}`):**
The session and its messages are removed if the caller owns it; 404 otherwise. Used by the chat UI's "Past Chats" list.

**When a student asks a question outside their curriculum:**
If Qdrant returns no relevant chunks, the LLM is called with an empty context and a refusal system prompt. It responds that it can only help with material the student has studied — it does not answer from general knowledge.

**When a student uploads a PDF:**
The file is ingested into a temporary session-only vector store (not persisted to the shared Qdrant collection). Subsequent queries in the same session include results from this temporary store alongside grade-filtered curriculum results.

**When the LLM response is slow:**
Responses stream token-by-token to the frontend via Server-Sent Events or chunked transfer encoding so the student sees partial output in real time.

**When the student's JWT expires mid-session:**
The client silently exchanges the refresh token for a new JWT and retries the request. The student never sees an auth error during normal use.

**After every message exchange:**
Both the user message and the assistant response are persisted to the `chat_messages` table. Each record is tagged with a detected subject (linked to a `Subject` entity, get-or-created by `SubjectResolutionService` from the LLM's classified subject string) and a topic string produced by a one-shot LLM classification call. On a session's first exchange, the session itself is locked onto that `Subject` folder and — if the student belongs to a class tagged with that same subject in the same school — onto that class's `ClassId` too; if the student is later removed from the class, their affected sessions are re-filed onto the class's subject folder rather than left pointing at a class they've left.

**When a teacher calls `GET /api/teacher/classes`:**
Returns every class the teacher is assigned to (via `ClassTeachers`), including the subjects they teach in each class and the enrolled student headcount.

**When a teacher calls `GET /api/teacher/classes/{classId}/struggles?days=N`:**
Returns the top 5 most-asked topics per subject for that class over the last N days (default 30, clamped to 1–365), derived from `chat_messages` rows where `role = "user"` posted by students enrolled in that class. Returns 404 if the teacher is not assigned to the requested class.

**When a teacher calls `GET /api/teacher/activity?days=N`:**
Returns the top 5 most active students (by question count in `chat_messages`) per class over the last N days (default 30, clamped to 1–365).

**When a non-teacher or non-school-admin calls any teacher endpoint:**
Returns 403 Forbidden. Unauthenticated requests receive 401 from the JWT middleware.

**When a school admin calls `GET /api/admin/classes`:**
Returns all classes in the caller's school (derived from `ApplicationUser.SchoolId` on the authenticated user) with the homeroom teacher's username and enrolled student count.

**When a school admin calls `POST /api/admin/classes`:**
Creates a new class in the caller's school, tagged with the given `subjectId` (required — must belong to the caller's school). An optional `homeroomTeacherId` may be supplied; if provided, the teacher must belong to the same school.

**When a school admin calls `GET /api/admin/classes/{classId}`:**
Returns the class's name, subject tag, homeroom teacher, full student roster, and teacher-subject assignments. 404 if the class doesn't belong to the caller's school.

**When a school admin calls `PUT /api/admin/classes/{classId}`:**
Renames the class and/or retags its `subjectId` (both required in the request body). The subject must belong to the caller's school.

**When a school admin calls `PUT /api/admin/classes/{classId}/homeroom`:**
Sets the homeroom teacher for the specified class. The teacher must belong to the same school as the caller.

**When a school admin calls `DELETE /api/admin/classes/{classId}/students/{userId}`:**
Removes the student from that one class membership (`ClassStudent` row) and re-files any of that student's chat sessions that pointed at the class onto the class's subject folder instead. The student must belong to the caller's school. There is no corresponding "assign student" endpoint for a SchoolAdmin — a student attaches to a class only via their own `ClassJoinCode` self-join (`POST /api/student/classes`), or, at the GlobalAdmin level, `POST /api/global-admin/classes/{classId}/students`.

**When a school admin calls `POST /api/admin/classes/{classId}/teachers`:**
Assigns a teacher to a class for a named subject. If the subject does not yet exist for the school it is created automatically. Duplicate assignments are rejected.

**When a school admin calls `DELETE /api/admin/classes/{classId}/teachers/{teacherId}/subjects/{subjectId}`:**
Removes that one teacher-subject assignment from the class. If it was the teacher's last assignment in the class and they were also its homeroom teacher, the homeroom assignment is cleared too.

**When a school admin calls `POST /api/admin/subjects`:**
Creates a new `Subject` in the caller's school. Rejected if a subject with that name already exists in the school.

**When a school admin calls `POST /api/admin/teachers/{teacherId}/subjects/{subjectId}`:**
Adds an existing subject to the teacher's teaching list. Rejected if the subject does not belong to the caller's school or is already assigned.

**When a school admin calls `DELETE /api/admin/teachers/{teacherId}/subjects/{subjectId}`:**
Removes a subject from the teacher's teaching list.

**When a school admin calls `GET /api/admin/subjects`:**
Returns all subjects registered for the caller's school.

**When a school admin calls `GET /api/admin/users`:**
Returns all users in the caller's school with their role, grade, and class information.

**When a non-SchoolAdmin calls any `/api/admin` endpoint:**
Returns 403 Forbidden. Unauthenticated requests receive 401 from the JWT middleware.

**When a student calls `POST /api/student/practice-questions`:**
Given the original question and the AI's answer from a prior chat exchange, generates exactly 3 short follow-up practice questions on the same topic via a one-shot LLM call. Both inputs are sanitised through `InputSanitizer.SanitizeUserInput()` before reaching the prompt. On any LLM/parsing failure, returns an empty list rather than an error.

**When a student calls `GET /api/student/weak-spots?days=N`:**
Returns the student's own most-asked topics (grouped by topic, with subject and count) over the last N days (default 7, clamped 1–365) where the topic was asked at least twice — the student-facing counterpart to the teacher's per-class struggles view.

**When a student calls `GET /api/student/history?limit=N`:**
Returns the student's own chat messages (default 50, clamped 1–200), oldest to newest, each with role, content, subject name, topic, and timestamp.

**When a student calls `POST /api/student/exam`:**
Generates a mock exam for the given topic, grade-filtered to the student's own grade (from the JWT, not the request body). If no curriculum material is found for the topic at that grade, returns a Bulgarian "no material found" message instead of a hallucinated exam. The generated exam is now also persisted as a `SavedExam` row; the response is `{ id, exam }` so the client can later fetch it again.

**When a student calls `GET /api/student/exams`:**
Returns the student's own saved exams (`{ id, topic, createdAt }` per row), most useful for a list screen; does not include the full exam content.

**When a student calls `GET /api/student/exams/{id}`:**
Returns one saved exam's full detail (`{ id, topic, content, createdAt }`) if it belongs to the calling student; 404 otherwise.

**When a non-Student calls any `/api/student` endpoint:**
Returns 403 Forbidden. Unauthenticated requests receive 401 from the JWT middleware.

**When a global admin calls `GET /api/global-admin/curriculum/grades/{grade}/files`:**
Returns the list of curriculum PDF file keys (e.g. `Math/algebra.pdf`) currently ingested into Qdrant for that grade.

**When a global admin calls `POST /api/global-admin/curriculum/grades/{grade}/files`:**
Accepts a multipart PDF upload plus an optional subject field, saves it under `Database/DataPdf/Grade{grade}/{subject}/`, and ingests it into Qdrant. Returns 409 if a file with the resulting key (`{subject}/{filename}`) already exists — use PUT to replace it instead.

**When a global admin calls `PUT /api/global-admin/curriculum/grades/{grade}/files/{fileKey}`:**
Replaces the content at that exact file key: deletes any existing chunks for it, then re-ingests the newly uploaded PDF. Succeeds whether or not the key existed before (idempotent upsert).

**When a global admin calls `DELETE /api/global-admin/curriculum/grades/{grade}/files/{fileKey}`:**
Removes the file's chunks from Qdrant. Returns 404 if the key is not found for that grade.

**When a non-Admin calls any `/api/global-admin/curriculum` endpoint:**
Returns 403 Forbidden. Unauthenticated requests receive 401 from the JWT middleware.

---

## Acceptance Criteria

*Checked items reflect that the route, role guard, and described behavior exist in the code as written (controller + service inspection). They are not a record of a fresh manual end-to-end run against a live server — see Definition of Done in `CLAUDE.md` for what a full sign-off requires.*

- [x] `POST /api/chat/message` exists, requires a valid JWT, and streams the LLM response
- [x] RAG context is grade-filtered: a grade-8 student never receives chunks from grade-9 or higher material
- [x] When the LLM output contains a `<STEREO>` block, the response includes a structured `scene` field with the extracted JSON; the text field contains the response with the block removed
- [x] Off-curriculum questions produce a polite refusal message, not a hallucinated answer
- [x] `POST /api/chat/upload` accepts a PDF, ingests it into a session-scoped temporary store, and affects all subsequent `/api/chat/message` calls in that session
- [x] Every chat exchange writes two rows to `chat_messages` — one with `role = "user"`, one with `role = "assistant"` — both with a populated `subject_id` and `topic`
- [x] The streaming response reaches the client in real time (tokens visible as they are generated, not batched at the end)
- [x] Token expiry during a session is handled transparently via refresh token exchange on the client side — implemented in `frontend/src/api/client.ts`; the 401→refresh→retry-once flow and its concurrent-request dedup were verified against the real endpoints (login/refresh response shapes match exactly), but not yet observed inside a live browser session with an actually-expired token
- [x] `GET /api/teacher/classes` requires a valid JWT with `role = Teacher` or `SchoolAdmin`; returns class list with subjects and student counts
- [x] `GET /api/teacher/classes/{classId}/struggles?days=30` returns per-subject topic frequency for the teacher's own classes only; returns 404 for classes not assigned to the caller
- [x] `GET /api/teacher/activity?days=30` returns top-5 most active students per class; `days` is clamped to 1–365
- [x] Student role (or any non-teacher role) JWT receives 403 on all three teacher endpoints; unauthenticated requests receive 401
- [x] `GET /api/admin/classes` requires a valid JWT with `role = SchoolAdmin`; returns class list with subject tag, homeroom teacher username, and student count
- [x] `POST /api/admin/classes` creates a class scoped to the caller's school, tagged with a `subjectId` that must belong to that school; optional homeroom teacher must belong to the same school
- [x] `GET /api/admin/classes/{classId}` returns class detail (name, subject, homeroom, roster, teacher assignments); 404 for a class outside the caller's school
- [x] `PUT /api/admin/classes/{classId}` renames/retags a class; rejects a subject not belonging to the caller's school
- [x] `PUT /api/admin/classes/{classId}/homeroom` sets the homeroom teacher; rejects teachers from other schools
- [x] `DELETE /api/admin/classes/{classId}/students/{userId}` removes the student from their class and re-files their sessions for that class onto its subject folder; there is no SchoolAdmin-level "assign student" endpoint
- [x] `POST /api/admin/classes/{classId}/teachers` assigns a teacher to a class for a subject; auto-creates the subject if needed
- [x] `DELETE /api/admin/classes/{classId}/teachers/{teacherId}/subjects/{subjectId}` removes that teacher-subject assignment; clears homeroom if it was their last assignment and they held it
- [x] `GET /api/admin/subjects` / `POST /api/admin/subjects` list and create subjects for the caller's school; POST rejects duplicate names within the school
- [x] `POST /api/admin/teachers/{teacherId}/subjects/{subjectId}` adds a subject to a teacher's list; rejects duplicates and cross-school subjects
- [x] `DELETE /api/admin/teachers/{teacherId}/subjects/{subjectId}` removes a subject from a teacher's list
- [x] `GET /api/admin/users` returns all users in the caller's school with role, grade, and class info
- [x] Non-SchoolAdmin JWT receives 403 on all `/api/admin` endpoints; unauthenticated requests receive 401
- [x] `POST /api/global-admin/schools` registers a new School entity (`{ name: string }`); rejects duplicate names
- [x] `GET /api/global-admin/users` returns all users across all schools with id, username, fullName, role, grade, and class name
- [x] `GET /api/global-admin/classes` returns all classes across all schools with id, name, homeroom teacher username, and student count
- [x] `POST /api/global-admin/classes` creates a class for any school — body: `{ schoolId: int, name: string, subjectId: int, homeroomTeacherId?: string }`
- [x] `DELETE /api/global-admin/classes/{classId}` deletes a class and unlinks all its students
- [x] `POST /api/global-admin/subjects` creates a subject for a given school — body: `{ schoolId: int, name: string }`; rejects duplicates within the same school
- [x] `DELETE /api/global-admin/subjects/{subjectId}` deletes a subject by ID
- [x] `POST /api/global-admin/classes/{classId}/students` assigns a student (by userId) to a class; rejects non-students
- [x] `POST /api/global-admin/classes/{classId}/teachers` assigns a teacher to a class for a subject — body: `{ schoolId: int, teacherId: string, subjectName: string }`; auto-creates the subject if it does not exist in that school
- [x] `PUT /api/global-admin/users/{userId}/role` promotes a user to SchoolAdmin and assigns them to a school — body: `{ schoolId: int }`
- [x] Non-Admin JWT receives 403 on all `/api/global-admin` endpoints; unauthenticated requests receive 401
- [x] `POST /api/student/practice-questions` returns exactly 3 practice questions; inputs are sanitised before reaching the LLM prompt; returns an empty list on failure rather than an error
- [x] `GET /api/student/weak-spots?days=N` returns the calling student's own most-asked topics only; `days` is clamped to 1–365
- [x] `GET /api/student/history?limit=N` returns the calling student's own chat messages only, oldest to newest; `limit` is clamped to 1–200
- [x] `POST /api/student/exam` generates an exam grade-filtered to the calling student's own grade (from JWT); returns a graceful fallback message when no material is found for the topic; persists the exam as a `SavedExam` and returns `{ id, exam }`
- [x] `GET /api/student/exams` returns the calling student's own saved exams only (`{ id, topic, createdAt }`); `GET /api/student/exams/{id}` returns full detail for one, 404 if it doesn't belong to the caller
- [x] `DELETE /api/chat/sessions/{id}` removes a session and its messages if owned by the caller; 404 otherwise
- [x] `POST /api/chat/scene-html` renders a `<STEREO>` scene JSON into standalone Three.js HTML for iframe display
- [x] Non-Student JWT receives 403 on all `/api/student` endpoints; unauthenticated requests receive 401
- [x] `GET /api/global-admin/curriculum/grades/{grade}/files` lists ingested file keys for that grade
- [x] `POST /api/global-admin/curriculum/grades/{grade}/files` ingests a new PDF; rejects with 409 if the file key already exists
- [x] `PUT /api/global-admin/curriculum/grades/{grade}/files/{fileKey}` replaces the content at that key, re-ingesting whether or not it previously existed
- [x] `DELETE /api/global-admin/curriculum/grades/{grade}/files/{fileKey}` removes the file's chunks; returns 404 if the key does not exist
- [x] Non-Admin JWT receives 403 on all `/api/global-admin/curriculum` endpoints; unauthenticated requests receive 401

---

## Edge Cases

**Math notation in messages or PDFs**
Pix2Text handles LaTeX at ingest time for formula-heavy textbook PDFs. The LLM must output math in LaTeX notation so the frontend can render it. No special preprocessing of user query text is needed.

**Grade-1 student**
RAG searches with an upper bound of grade 1 — no cross-grade content can appear. This is the lower bound of the filter and must not be treated as an edge case that skips filtering.

**Corrupt or image-only PDF upload**
PdfPig plain-text extraction runs on every page by default (fast, no OCR/language dependency). Pages that embed classic MathType/Equation-Editor formula fonts additionally get a Pix2Text formula-only pass appended after that page's prose (`PDFLoader.LoadTextWithSelectiveFormulaOcrAsync`, the path ingestion actually uses). Vision OCR (Ollama) is a whole-document fallback used only when Pix2Text itself is unavailable — a genuinely scanned, image-only PDF then relies on it. If the resulting text is empty, the upload endpoint returns a clear error message rather than silently storing empty chunks.

**PDF text spanning a page break**
Ingestion chunks each PDF page independently (`PDFLoader.ChunkPages`) rather than treating the whole document as one string, so a single ~400-char chunk never mixes content from two different pages — including a page's Pix2Text formula block, which is guaranteed to be chunked from that same page's own prose, never an adjacent page's. Pages with under 20 characters of cleaned text (a blank divider page, or a page whose only content was a bare page number) are skipped rather than merged into a neighboring page. Each stored chunk's Qdrant payload records its 1-indexed source page (`page`) for provenance.

**Malformed `<STEREO>` block from LLM**
If the regex extraction in `StereometryService.ExtractSceneJson()` finds no valid JSON, the `scene` field is `null` in the response. The text response is always returned regardless.

**Qdrant returns zero results**
The LLM is still called, but with an empty context block and the curriculum-restriction system prompt. The expected output is a refusal, not an attempt to answer from training data.

**ZhipuAI unavailable**
Not applicable currently: `ZhipuAIChatService` is an implemented but unregistered `IChatService` backend — `Program.cs` wires `OllamaChatService` as the only `IChatService`, and there is no automatic runtime failover between the two. Switching backends today requires editing that DI registration and redeploying; if automatic failover is wanted, it needs to be built.

**Message exceeding LLM context window**
Long messages are truncated or chunked before submission. The system must not crash or return a 500; it should truncate with a warning log.

**Bulgarian diacritics and Cyrillic text**
Embedding and LLM services handle UTF-8 natively. The only preprocessing required is the existing `CleanText()` sanitisation in `PDFLoader.cs` (null bytes, control characters). No transliteration or encoding conversion is needed.

**Uploading a curriculum file with a key that already exists**
`POST /api/global-admin/curriculum/grades/{grade}/files` rejects with 409 rather than silently overwriting — an admin must explicitly use PUT on the same file key to replace existing content, preventing accidental data loss from a duplicate upload.

**Replacing a curriculum file (updated textbook edition)**
`PUT /api/global-admin/curriculum/grades/{grade}/files/{fileKey}` deletes the old chunks for that key and re-ingests the new upload in one service call, avoiding a window where a student query would see neither the old nor the new content.

---

## Environment Prerequisites

The behavioral specification below assumes a working local LLM/OCR pipeline. Before testing: pull the Ollama models referenced by `Llm:OllamaModel`/`Llm:OllamaVisionModel`/`Llm:OllamaEmbedModel` (currently `todorov/bggpt`, `minicpm-v`, and `nomic-embed-text-v2-moe` — see `ollama list`), and start the `pix2text` container (`docker compose up -d pix2text`). See `README.md`'s "Local Environment Prerequisites" section.

---

## Frontend Integration Readiness

The backend behavioral spec above is implemented. A frontend scaffold now exists at `frontend/` (Vue 3 + Vite + TypeScript, see `README.md`'s "Frontend Integration To-Do") covering the API client plus login, registration, and forgot-password screens — `POST /api/auth/login`, `POST /api/auth/register`, and the full `forgot-password`/`verify-reset-code`/`reset-password` sequence (including its 404/429/400 error responses) are all exercised end-to-end from the UI; per-role dashboards, student chat (`frontend/src/components/chat/`), and saved mock exams are also built and wired end-to-end (see `README.md`'s "Complete" section). Teacher/SchoolAdmin/GlobalAdmin chat-adjacent UI and a Settings screen (all roles) are not yet built. Remaining items to attend to:

**Token refresh is implemented.** `POST /api/auth/refresh` (`{ refreshToken }` → `{ token, refreshToken }`) is now consumed by `frontend/src/api/client.ts`, which triggers it automatically on any `401`, retries the original request once, and dedups concurrent 401s behind a single in-flight refresh call so a burst of requests doesn't fire multiple simultaneous `/refresh` calls. Force-logout on a failed refresh is also implemented.

**Chat SSE frame contract** (`POST /api/chat/message`, `Content-Type: text/event-stream`), documented in code at `Controllers/ChatController.cs` above `SendMessage`, reproduced here so it doesn't require reading the controller:
- `data: {"sessionId":N}` — always first
- `data: {"token":"..."}` — zero or more, one per streamed token
- `data: {"done":true,"scene":<json|null>}` — end of stream; `scene` is the extracted `<STEREO>` JSON or `null`
- `data: {"title":"...","subject":"..."}` — only sent on the session's first exchange

Note this is a `POST` with a streaming response body, not a plain `GET`-based `EventSource` — the frontend needs a `fetch` + `ReadableStream` consumer (or an SSE library that supports POST), not the browser's native `EventSource`.

**Production CORS origin is still a placeholder.** `appsettings.json`'s `Cors:AllowedOrigins` is `REPLACE_WITH_FRONTEND_ORIGIN`; local dev already works (`appsettings.Local.json` allows `localhost:3000`/`:5173`), but the real frontend origin must be set via `Cors__AllowedOrigins__0` before any non-local deploy.

**No machine-readable API contract exists.** Swagger/OpenAPI is intentionally out of scope for this project (see `CLAUDE.md`). This document's Behavioral Specification and Acceptance Criteria are the contract; if the frontend team needs something more structured (Postman/Insomnia collection, hand-written `API.md`), that needs to be produced separately — it does not exist today.

---

## Security Baseline (established pre-web-conversion)

The following security controls are in place as of the initial web conversion. Future features must not regress them.

**Authentication & tokens**
- Refresh token revocation uses `IsolationLevel.RepeatableRead` (same as token exchange) — prevents concurrent-logout race condition
- Password reset uses a DB-stored 6-digit code (10-min expiry) delivered via SMTP email; the code is never returned in the HTTP response
- Reset code consumption (marking `IsUsed = true`) is protected by a `RepeatableRead` transaction to prevent race conditions on simultaneous submissions

**Rate limiting**
- Login: progressive delay per account (3 s → 10 s → 30 s → 60 s) + separate IP counter
- Registration: 2-minute cooldown per email
- Password reset request: 5-minute cooldown per email
- Reset code verification and reset-password: shared per-email counter, locked after 5 failed attempts; requesting a new code resets the counter
- Token refresh and logout: 20 requests per minute per IP (`IsGeneralApiThrottled`)
- Rate limit error responses are intentionally generic — they do not reveal wait durations

**Prompt injection**
- All user-supplied strings entering LLM prompts are sanitised through `InputSanitizer.SanitizeUserInput()`: max 2000 chars for questions, 300 chars for exam topics; null bytes and C0 control characters stripped

**CORS**
- Allowed headers restricted to `Content-Type` and `Authorization` only — no wildcard headers
- Allowed origins loaded from `Cors:AllowedOrigins` config — never hardcoded

**Reverse proxy**
- `UseForwardedHeaders()` is wired first in the pipeline so that `RemoteIpAddress` reflects the real client IP behind a proxy. Production deployments must populate `KnownProxies` with the actual proxy IP(s).

**Service registration**
- `RAGService` must be Scoped — its instance fields are per-user state
- `VisualisationService` is CLI-only and must never be exposed via HTTP
