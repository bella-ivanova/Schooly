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
- ZhipuAI remote LLM chat service with streaming (fallback backend)
- RAG orchestration: grade-filtered retrieval + LLM call with context injection
- Stereometry 3D scene JSON schema and `<STEREO>` block extraction from LLM responses
- Exam generation service: mock exams generated from curriculum material via RAG
- Chat message persistence with subject and topic tagging
- Chat sessions: messages are grouped into `ChatSession`s with true multi-turn context (prior turns are replayed into the LLM call, not just displayed), an AI-generated title after the first exchange, and a subject-folder assignment locked from that same first exchange
- Weak-spot detection: aggregates most-asked topics per student, exposed to the student themselves via `GET /api/student/weak-spots?days=N` (distinct from the teacher-facing per-class `GET /api/teacher/classes/{classId}/struggles`)
- Database schema: 14 migrations defined (PostgreSQL); run `dotnet ef database update` before starting the server
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
