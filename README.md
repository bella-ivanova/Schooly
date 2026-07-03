# StudyAssist (Schooly)

A curriculum-aware AI tutoring platform for Bulgarian school students. The AI is restricted to the material the student has already studied at their grade level, preventing it from answering questions outside the curriculum.

## Implementation Status

### Complete
- User authentication: login, register, logout, JWT access tokens + refresh tokens
- Password reset flow: 6-digit email code with 10-minute expiry, brute-force protection (5 attempts), and same-password check
- Progressive rate limiting: per-account and per-IP for login, per-email for registration and password reset; IP-based limit on token refresh and logout
- User roles: Student, Teacher, SchoolAdmin, Admin
- School class and subject management (CLI-only admin commands via `AdminUserService`)
- PDF ingestion pipeline: triple-OCR fallback (Pix2Text → vision OCR → PdfPig), text chunking, and embedding
- Qdrant vector DB integration with grade-filtered semantic search
- Ollama local LLM chat service with streaming
- ZhipuAI remote LLM chat service with streaming (fallback backend)
- RAG orchestration: grade-filtered retrieval + LLM call with context injection
- Stereometry 3D scene JSON schema and `<STEREO>` block extraction from LLM responses
- Exam generation service: mock exams generated from curriculum material via RAG
- Chat message persistence with subject and topic tagging
- Weak-spot detection: aggregates most-asked topics per student
- Database schema: 9 migrations applied (PostgreSQL)
- Security hardening pass: TOCTOU-safe refresh token revocation, prompt injection sanitisation, generic rate limit error messages, HSTS, CORS header restriction, ForwardedHeaders middleware

### In Progress
- Student chat HTTP endpoint (`POST /api/chat/message`) — service layer complete, controller not yet wired
- Teacher dashboard HTTP endpoint — `TeacherDashboardService` exists, not exposed via controller
- School admin HTTP endpoints — all admin operations are currently CLI-only

### Not Started
- PDF upload endpoint for students — `TempFileManager` and session-store ingestion exist in `RAGService`, no HTTP route yet
- Practice question HTTP endpoint — `PracticeQuestionService` exists, not wired
- Frontend application (Vue/React, separate repository, expected at `localhost:3000` or `:5173`)

## Production Deployment Notes

**Reverse proxy (required):** `UseForwardedHeaders()` is active. You must restrict which proxies are trusted — edit the `ForwardedHeadersOptions` block in `Program.cs` and add your specific proxy IP(s) to `KnownProxies`. Without this, any client can spoof `X-Forwarded-For` to bypass IP-based rate limiting.

**HSTS:** `UseHsts()` is active. The first response to each browser instructs it to refuse all future HTTP connections to this domain. Ensure TLS is configured before deploying.

**SMTP (required):** Password reset codes are delivered via email. Supply `Smtp__Host`, `Smtp__Username`, `Smtp__Password`, and `Smtp__From` via environment variables. The app refuses to start if placeholders are detected.

**Secrets:** All config keys in `appsettings.json` hold placeholder strings. Supply real values via environment variables (`Jwt__Secret`, `ConnectionStrings__DefaultConnection`, `TeacherRegistrationCode`, `Cors__AllowedOrigins__0`, `Smtp__Host`, `Smtp__Username`, `Smtp__Password`, `Smtp__From`). The app refuses to start if placeholders are detected.
