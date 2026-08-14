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
- AI response language: the tutor mirrors whatever language the student wrote their question in (chat answers, mock exams, practice questions) rather than defaulting to one fixed language, independent of the UI's English copy — enforced via explicit system-prompt instructions in `RAGService.cs`, `ExamService.cs`, and `PracticeQuestionService.cs` (see `CLAUDE.md`'s Language Policy section)
- Frontend scaffold: Vue 3 + Vite + TypeScript app at `frontend/` (in this repo, not a separate one). Typed API client (`frontend/src/api/`) covering login/register/refresh/logout/forgot-password/verify-reset-code/reset-password, a silent refresh-on-401 flow with concurrent-request dedup (`client.ts`), and a typed SSE frame parser + consumer for `POST /api/chat/message` (`sse.ts`/`chat.ts`, not yet wired to any chat UI). Pinia auth store (`frontend/src/stores/auth.ts`) persists the JWT/refresh token/user to `localStorage`. Three real screens exist: login (`frontend/src/views/LoginView.vue`), registration (`RegisterView.vue`), and a 3-step forgot-password flow (`ForgotPasswordView.vue` → `ForgotPasswordForm.vue`, exercising `POST /api/auth/forgot-password|verify-reset-code|reset-password` end-to-end, including its 404/429/400 error paths). Login has no role selector — the backend has no role field on `POST /api/auth/login` and derives role from the authenticated user's JWT; role selection lives on the register screen instead (a Student/Teacher toggle, `frontend/src/components/register/RoleTabs.vue`), which is where `POST /api/auth/register`'s required `role` field is actually collected, along with grade (via a dropdown, student) or a teacher registration code (teacher), plus a client-side-only "agree to Terms" checkbox gating submit. All three screens share a full-viewport split-panel layout (`components/shared/AuthShell.vue`: green branding panel + form panel, collapsing to one column on mobile) and an inline-SVG logo mark (`SchoolyMark.vue`); copy is in English (the app's target UI language going forward, matching a hand-made mockup at `~/Downloads/Schooly Auth Screens-selection.png`; other screens like `PlaceholderHomeView.vue` are still Bulgarian and not yet migrated). Styled from design tokens (colors/fonts/radii/shadows) extracted from a hand-made mockup at `~/Downloads/Schooly UI Mockups (standalone).html` — see `CLAUDE.md`'s Frontend Integration Notes for the token values and mockup screen inventory.

### Not Started
- Frontend: chat UI, per-role dashboards (Student/Teacher/SchoolAdmin/Admin), the `<STEREO>` 3D viewer, RAG source-citation UI, and full per-role router guards

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

The frontend now exists at `frontend/` (Vue 3 + Vite + TypeScript, in this repo). Status of each step:

1. ~~Pin down the local API port.~~ Done — `Properties/launchSettings.json` fixes it: `dotnet run` serves the API at `http://localhost:5080`. HTTP-only is intentional: `Program.cs` calls `app.UseHttpsRedirection()` unconditionally, but that middleware silently no-ops when no HTTPS URL is bound, so local dev stays plain HTTP with no redirect and no dev-cert trust step required. (An HTTPS profile can be added later for whoever sets up production hosting.)
2. ~~Scaffold the frontend project.~~ Done — `npm create vite@latest frontend -- --template vue-ts`, dev server on `:5173` (`npm run dev` from `frontend/`), already covered by `appsettings.Local.json`'s `Cors:AllowedOrigins`.
3. **API client** — mostly done:
   - ~~JWT storage and `Authorization: Bearer` attachment~~ — `frontend/src/api/tokenStorage.ts` + `client.ts`
   - ~~Silent refresh-token exchange on 401~~ — `frontend/src/api/client.ts`, with a module-level promise dedup so concurrent 401s trigger exactly one `POST /api/auth/refresh` call, not one per request
   - ~~POST-capable SSE consumer for `POST /api/chat/message`~~ — `frontend/src/api/sse.ts` (generic frame parser) + `chat.ts` (typed `streamChatMessage()`), matching the frame contract in `PRD.md`'s "Frontend Integration Readiness" section; not yet wired to any chat UI
   - **Still open**: role-aware routing for the four `UserRole`s against their respective endpoint namespaces (`/api/student`, `/api/teacher`, `/api/admin`, `/api/global-admin`) — the router currently only guards authenticated-vs-not, not per-role
4. **Set the production CORS origin** via `Cors__AllowedOrigins__0` before deploying the frontend anywhere other than localhost — `appsettings.json` still holds a placeholder.
5. **Decide how the frontend team gets the API contract** — Swagger/OpenAPI is intentionally out of scope for this project, so there's no generated spec. Either hand off `PRD.md`'s Behavioral Specification directly or produce a separate collection/doc.
6. Use the "Local Environment Prerequisites" section above as the shared checklist for getting a fully working local stack (backend + Ollama + Qdrant + Pix2Text) before testing the frontend end-to-end.
