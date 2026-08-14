# StudyAssist (Schooly)

A curriculum-aware AI tutoring platform for Bulgarian school students. The AI is restricted to the material the student has already studied at their grade level, preventing it from answering questions outside the curriculum.

## Implementation Status

### Complete
- User authentication: login, register, logout, JWT access tokens + refresh tokens
- Password reset flow: 6-digit email code with 10-minute expiry, brute-force protection (5 attempts), and same-password check
- Progressive rate limiting: per-account and per-IP for login, per-email for registration and password reset; IP-based limit on token refresh and logout
- User roles: Student, Teacher, SchoolAdmin, Admin
- School entity management: `School` is a first-class entity (`Id`, `Name`, `CreatedAt`); `ApplicationUser`, `Class`, and `Subject` each reference it via a `SchoolId` int FK (no loose school-name strings); school creation via HTTP (`POST /api/global-admin/schools`); full CLI management via `AdminUserService`
- PDF ingestion pipeline: triple-OCR fallback (Pix2Text → vision OCR → PdfPig), text chunking, and embedding
- Qdrant vector DB integration with grade-filtered semantic search
- Ollama local LLM chat service with streaming
- ZhipuAI remote LLM chat service with streaming (`ZhipuAIChatService`, implements the same `IChatService` interface as Ollama) — implemented but not currently registered in `Program.cs`; switching backends means editing that DI registration, there is no automatic runtime failover
- RAG orchestration: grade-filtered retrieval + LLM call with context injection
- Stereometry 3D scene JSON schema and `<STEREO>` block extraction from LLM responses
- Exam generation service: mock exams generated from curriculum material via RAG
- Chat message persistence with subject and topic tagging
- Chat sessions: messages are grouped into `ChatSession`s with true multi-turn context (prior turns are replayed into the LLM call, not just displayed), an AI-generated title after the first exchange, and a subject-folder assignment locked from that same first exchange
- Weak-spot detection: aggregates most-asked topics per student, exposed to the student themselves via `GET /api/student/weak-spots?days=N` (distinct from the teacher-facing per-class `GET /api/teacher/classes/{classId}/struggles`)
- Database schema: 13 migrations defined (PostgreSQL), most recent is `AddChatSessions`; run `dotnet ef database update` before starting the server
- Security hardening pass: TOCTOU-safe refresh token revocation, prompt injection sanitisation, generic rate limit error messages, HSTS, CORS header restriction, ForwardedHeaders middleware
- Student chat: `POST /api/chat/message` — grade-filtered RAG, SSE token streaming, stereometry scene extraction, session-scoped chat-log persistence with multi-turn context and AI-generated titles; `GET /api/chat/sessions?subject=` (folder-filterable session list), `GET /api/chat/sessions/{id}/messages` (session transcript) — all require auth, no role restriction
- PDF session upload: `POST /api/chat/upload` — ingests a PDF into a per-request temporary vector store that affects subsequent chat queries
- Teacher dashboard HTTP endpoints: `GET /api/teacher/classes`, `GET /api/teacher/classes/{classId}/struggles?days=N`, `GET /api/teacher/activity?days=N` — requires Teacher or SchoolAdmin JWT
- School admin HTTP endpoints: `GET /api/admin/classes`, `POST /api/admin/classes`, `PUT /api/admin/classes/{classId}/homeroom`, `POST /api/admin/classes/{classId}/students`, `DELETE /api/admin/classes/{classId}/students/{userId}`, `POST /api/admin/classes/{classId}/teachers`, `POST /api/admin/teachers/{teacherId}/subjects/{subjectId}`, `DELETE /api/admin/teachers/{teacherId}/subjects/{subjectId}`, `GET /api/admin/subjects`, `GET /api/admin/users` — requires SchoolAdmin JWT
- Global admin HTTP endpoints: `POST /api/global-admin/schools`, `GET /api/global-admin/users`, `GET /api/global-admin/classes`, `POST /api/global-admin/classes`, `DELETE /api/global-admin/classes/{classId}`, `POST /api/global-admin/subjects`, `DELETE /api/global-admin/subjects/{subjectId}`, `POST /api/global-admin/classes/{classId}/students`, `POST /api/global-admin/classes/{classId}/teachers`, `PUT /api/global-admin/users/{userId}/role` — requires Admin JWT; endpoints that target a school accept `schoolId: int` (not a school name string)
- Curriculum file management HTTP endpoints: `GET /api/global-admin/curriculum/grades/{grade}/files`, `POST /api/global-admin/curriculum/grades/{grade}/files` (multipart upload, 409 on duplicate), `PUT /api/global-admin/curriculum/grades/{grade}/files/{fileKey}` (replace/re-ingest), `DELETE /api/global-admin/curriculum/grades/{grade}/files/{fileKey}` — requires Admin JWT; curriculum is grade-wide, not school-specific
- Student HTTP endpoints: `POST /api/student/practice-questions`, `GET /api/student/weak-spots?days=N`, `GET /api/student/history?limit=N`, `POST /api/student/exam` — requires Student JWT

### Not Started
- Frontend application (Vue/React, separate repository, expected at `localhost:3000` or `:5173`)

## Local Environment Prerequisites

Before chat, RAG, or math-OCR endpoints will work, the following must be running/pulled locally:

- **Ollama models:** `ollama pull minicpm-v` (chat + vision, matches `Llm:OllamaModel` / `Llm:OllamaVisionModel`) and `ollama pull nomic-embed-text` (embeddings, matches `Llm:OllamaEmbedModel`). Verify with `ollama list`.
- **pix2text (math OCR) container:** `docker compose up -d pix2text`. Verify with `docker logs studyassist-pix2text` — it should log `Uvicorn running` / `Application startup complete`, not an import traceback.

## Production Deployment Notes

**Reverse proxy (required):** `UseForwardedHeaders()` is active. You must restrict which proxies are trusted — edit the `ForwardedHeadersOptions` block in `Program.cs` and add your specific proxy IP(s) to `KnownProxies`. Without this, any client can spoof `X-Forwarded-For` to bypass IP-based rate limiting.

**HSTS:** `UseHsts()` is active. The first response to each browser instructs it to refuse all future HTTP connections to this domain. Ensure TLS is configured before deploying.

**SMTP (required):** Password reset codes are delivered via email. Supply `Smtp__Host`, `Smtp__Username`, `Smtp__Password`, and `Smtp__From` via environment variables. The app refuses to start if placeholders are detected.

**Secrets:** All config keys in `appsettings.json` hold placeholder strings. Supply real values via environment variables (`Jwt__Secret`, `ConnectionStrings__DefaultConnection`, `TeacherRegistrationCode`, `Cors__AllowedOrigins__0`, `Smtp__Host`, `Smtp__Username`, `Smtp__Password`, `Smtp__From`). The app refuses to start if placeholders are detected.

## Frontend Integration To-Do

The backend is ready to connect to; the frontend project itself doesn't exist yet. Before/while building it:

1. ~~Pin down the local API port.~~ Done — `Properties/launchSettings.json` fixes it: `dotnet run` serves the API at `http://localhost:5080`. HTTP-only is intentional: `Program.cs` calls `app.UseHttpsRedirection()` unconditionally, but that middleware silently no-ops when no HTTPS URL is bound, so local dev stays plain HTTP with no redirect and no dev-cert trust step required. (An HTTPS profile can be added later for whoever sets up production hosting.)
2. **Scaffold the frontend project** (React or Vue), with its dev server running on `localhost:3000` or `:5173` — both are already allowed in `appsettings.Local.json`'s `Cors:AllowedOrigins`.
3. **Build the API client**, covering:
   - JWT storage and `Authorization: Bearer` attachment
   - Silent refresh-token exchange on 401 (`POST /api/auth/refresh`) — the one still-open item in `PRD.md`'s Acceptance Criteria
   - A POST-capable SSE consumer for `POST /api/chat/message` (it streams a response body over POST, so the browser's native `EventSource` — GET-only — doesn't apply; use `fetch` + `ReadableStream`). Frame contract is documented in `PRD.md`'s "Frontend Integration Readiness" section and inline in `Controllers/ChatController.cs`.
   - Role-aware routing for the four `UserRole`s (Student, Teacher, SchoolAdmin, Admin) against their respective endpoint namespaces (`/api/student`, `/api/teacher`, `/api/admin`, `/api/global-admin`)
4. **Set the production CORS origin** via `Cors__AllowedOrigins__0` before deploying the frontend anywhere other than localhost — `appsettings.json` still holds a placeholder.
5. **Decide how the frontend team gets the API contract** — Swagger/OpenAPI is intentionally out of scope for this project, so there's no generated spec. Either hand off `PRD.md`'s Behavioral Specification directly or produce a separate collection/doc.
6. Use the "Local Environment Prerequisites" section above as the shared checklist for getting a fully working local stack (backend + Ollama + Qdrant + Pix2Text) before testing the frontend end-to-end.
