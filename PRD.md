# PRD — AI-Powered Tutoring Chat

**One-liner:** A curriculum-grounded chat interface that lets students ask questions and receive answers restricted to the material they have studied at their grade level.

---

## Behavioral Specification

**When a student sends a message:**
The system embeds the query, searches Qdrant filtered to grades 1 through N (where N is the student's enrolled grade), and injects the top-k matching curriculum chunks as context before calling the LLM. The LLM is instructed to answer only from the provided context.

**When the LLM response contains a `<STEREO>…</STEREO>` block:**
The system extracts the JSON scene description and returns it as a structured `scene` field alongside the text response so the frontend can render an interactive 3D geometry visualisation.

**When a student asks a question outside their curriculum:**
If Qdrant returns no relevant chunks, the LLM is called with an empty context and a refusal system prompt. It responds that it can only help with material the student has studied — it does not answer from general knowledge.

**When a student uploads a PDF:**
The file is ingested into a temporary session-only vector store (not persisted to the shared Qdrant collection). Subsequent queries in the same session include results from this temporary store alongside grade-filtered curriculum results.

**When the LLM response is slow:**
Responses stream token-by-token to the frontend via Server-Sent Events or chunked transfer encoding so the student sees partial output in real time.

**When the student's JWT expires mid-session:**
The client silently exchanges the refresh token for a new JWT and retries the request. The student never sees an auth error during normal use.

**After every message exchange:**
Both the user message and the assistant response are persisted to the `chat_messages` table. Each record is tagged with a detected subject (linked to a `Subject` entity) and a topic string produced by a one-shot LLM classification call.

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
Creates a new class in the caller's school. An optional `homeroomTeacherId` may be supplied; if provided, the teacher must belong to the same school.

**When a school admin calls `PUT /api/admin/classes/{classId}/homeroom`:**
Sets the homeroom teacher for the specified class. The teacher must belong to the same school as the caller.

**When a school admin calls `POST /api/admin/classes/{classId}/students`:**
Assigns the given student (by userId) to the class. Rejected if the student already belongs to a different school.

**When a school admin calls `DELETE /api/admin/classes/{classId}/students/{userId}`:**
Removes the student from their class by setting `ClassId` to null. The student must belong to the caller's school.

**When a school admin calls `POST /api/admin/classes/{classId}/teachers`:**
Assigns a teacher to a class for a named subject. If the subject does not yet exist for the school it is created automatically. Duplicate assignments are rejected.

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
Generates a mock exam for the given topic, grade-filtered to the student's own grade (from the JWT, not the request body). If no curriculum material is found for the topic at that grade, returns a Bulgarian "no material found" message instead of a hallucinated exam.

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
- [x] `GET /api/admin/classes` requires a valid JWT with `role = SchoolAdmin`; returns class list with homeroom teacher username and student count
- [x] `POST /api/admin/classes` creates a class scoped to the caller's school; optional homeroom teacher must belong to the same school
- [x] `PUT /api/admin/classes/{classId}/homeroom` sets the homeroom teacher; rejects teachers from other schools
- [x] `POST /api/admin/classes/{classId}/students` assigns a student to a class; rejects students who belong to a different school
- [x] `DELETE /api/admin/classes/{classId}/students/{userId}` removes the student from their class
- [x] `POST /api/admin/classes/{classId}/teachers` assigns a teacher to a class for a subject; auto-creates the subject if needed
- [x] `POST /api/admin/teachers/{teacherId}/subjects/{subjectId}` adds a subject to a teacher's list; rejects duplicates and cross-school subjects
- [x] `DELETE /api/admin/teachers/{teacherId}/subjects/{subjectId}` removes a subject from a teacher's list
- [x] `GET /api/admin/subjects` returns all subjects for the caller's school
- [x] `GET /api/admin/users` returns all users in the caller's school with role, grade, and class info
- [x] Non-SchoolAdmin JWT receives 403 on all `/api/admin` endpoints; unauthenticated requests receive 401
- [x] `POST /api/global-admin/schools` registers a new School entity (`{ name: string }`); rejects duplicate names
- [x] `GET /api/global-admin/users` returns all users across all schools with id, username, fullName, role, grade, and class name
- [x] `GET /api/global-admin/classes` returns all classes across all schools with id, name, homeroom teacher username, and student count
- [x] `POST /api/global-admin/classes` creates a class for any school — body: `{ schoolId: int, name: string, homeroomTeacherId?: string }`
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
- [x] `POST /api/student/exam` generates an exam grade-filtered to the calling student's own grade (from JWT); returns a graceful fallback message when no material is found for the topic
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
The OCR fallback chain runs in order: Pix2Text → vision OCR (Ollama) → PdfPig plain text. If all three return empty text, the upload endpoint returns a clear error message rather than silently storing empty chunks.

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

The behavioral specification below assumes a working local LLM/OCR pipeline. Before testing: pull the Ollama models referenced by `Llm:OllamaModel`/`Llm:OllamaVisionModel`/`Llm:OllamaEmbedModel` (currently `minicpm-v` and `nomic-embed-text` — see `ollama list`), and start the `pix2text` container (`docker compose up -d pix2text`). See `README.md`'s "Local Environment Prerequisites" section.

---

## Frontend Integration Readiness

The backend behavioral spec above is implemented. A frontend scaffold now exists at `frontend/` (Vue 3 + Vite + TypeScript, see `README.md`'s "Frontend Integration To-Do") covering the API client plus login and registration screens (`POST /api/auth/login` and `POST /api/auth/register` are both exercised end-to-end from the UI); chat UI, dashboards, and most other screens are not yet built. Remaining items to attend to:

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
