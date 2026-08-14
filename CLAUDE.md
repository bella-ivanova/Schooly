# CLAUDE.md — StudyAssist Project Context

## Technology Stack

| Technology | Version | Why This Choice |
|---|---|---|
| ASP.NET Core | 10.0 | Target platform; .NET 10 for performance improvements and minimal API features |
| PostgreSQL + EF Core + Npgsql | 10.* | Relational data with migrations; PostgreSQL's `ON CONFLICT DO UPDATE` is required for atomic rate-limit upserts that EF Core cannot express |
| ASP.NET Identity | 10.* | PBKDF2 password hashing, password validation rules, and direct password change without reinventing them |
| MailKit | 4.* | SMTP email delivery for password reset codes |
| JWT Bearer (HS256) | 10.* / 8.* | Stateless auth with revocable refresh tokens; HS256 is sufficient for a single-server trust boundary |
| OllamaSharp | 5.4.24 | Local LLM inference — keeps student data on-premises; has native streaming support. `Llm:OllamaModel` and `Llm:OllamaVisionModel` are pinned to `minicpm-v` (not `llama3.2`/`llava`) because that's the multimodal model actually kept pulled locally — check `ollama list` before repointing config at an unpulled model name |
| ZhipuAI (HTTP) | — | Alternate `IChatService` implementation (`ZhipuAIChatService`) calling ZhipuAI's OpenAI-compatible API (`/v4/chat/completions`); **not currently wired** — `Program.cs` registers `OllamaChatService` as `IChatService` and there is no automatic runtime failover, so switching backends means editing that DI registration |
| Qdrant | 1.17.0 (client) | Vector DB for RAG; supports payload-based metadata filtering so grade-N students only receive grade ≤ N curriculum material. `1.17.0` pins the `Qdrant.Client` NuGet package — the server image in `docker-compose.yml` (`qdrant/qdrant`) has no version tag and pulls `:latest`, so the server itself isn't pinned |
| nomic-embed-text (Ollama) | — | 768-dim embeddings; dimension is hardcoded in `EmbeddingService.cs` and must match the Qdrant collection |
| UglyToad.PdfPig | 1.7.0-custom-5 | Fast plain-text extraction from machine-readable PDFs (primary OCR path) |
| PDFtoImage + Tesseract | 5.2.0 | Fallback OCR for image-heavy or scanned PDFs |
| Pix2Text (Docker, :8503) | — | Math OCR producing LaTeX output — required because Bulgarian math textbooks contain dense formula notation |
| SkiaSharp | 3.119.2 | Image manipulation in the OCR pipeline; macOS native assets are included because dev happens on Mac |
| Vue 3 + Vite + TypeScript | 3.* / 8.* / 6.* | Frontend framework (`frontend/`), scaffolded via `npm create vite@latest frontend -- --template vue-ts`; chosen over React per project decision |
| Pinia | 4.* | Frontend state management — auth state (JWT, refresh token, user, role) needs to be read from many unrelated places (router guards, every API call, future stores) |
| vue-router | 4.* | Frontend routing; `beforeEach` guard redirects unauthenticated requests to `/login` |
| @fontsource/fredoka, @fontsource/nunito | 5.* | Self-hosted fonts matching the hand-designed mockup (`Fredoka` for headings, `Nunito` for body) — no external Google Fonts network call at runtime |

## Directory Structure

```
Controllers/        HTTP endpoints only — no business logic, no DB access
  AuthController.cs             /api/auth — login, register, logout, token refresh, password reset
  ChatController.cs             /api/chat/message (SSE streaming, session-scoped multi-turn chat), /api/chat/upload (session PDF ingest), /api/chat/sessions (list/filter by subject folder), /api/chat/sessions/{id}/messages (transcript)
  TeacherDashboardController.cs /api/teacher — teacher class list, struggle topics, student activity
  SchoolAdminController.cs      /api/admin — school admin class, subject, and assignment management (SchoolAdmin role only)
  GlobalAdminController.cs      /api/global-admin — global admin school, class, subject, user, and role management across all schools (Admin role only), plus curriculum file list/upload/replace/delete per grade
  StudentController.cs          /api/student — student's own practice questions, weak spots, chat history, and mock exam generation (Student role only)
Services/           All business logic and external integrations
  AuthService.cs         User/token lifecycle (registration, login, JWT, refresh tokens, password reset)
  IEmailService.cs       Email abstraction interface (SmtpEmailService implements it via MailKit)
  SmtpEmailService.cs    SMTP email delivery — sends 6-digit password reset codes
  RateLimiter.cs         Brute-force protection — singleton, database-backed, survives restarts
  IChatService.cs        LLM abstraction interface (OllamaChatService and ZhipuAIChatService implement it); `SeedHistory` replays prior session turns into per-request in-memory state before the next call
  OllamaChatService.cs   `IChatService` implementation backed by local Ollama (OllamaSharp) — one-shot, streaming, GEOM/STEREO-filtered streaming, token streaming; this is the implementation currently registered for `IChatService` in `Program.cs`
  ZhipuAIChatService.cs  `IChatService` implementation calling ZhipuAI's HTTP API directly (streaming SSE + GEOM/STEREO filtering); implemented but not registered anywhere — not currently reachable at runtime
  RAGService.cs          Retrieval + LLM orchestration (embeds query → searches Qdrant → calls LLM); `AskStreamAsync` is the HTTP-facing entry point that yields tokens, `Ask` is the CLI entry point; `SeedHistory` passes through to `IChatService` for multi-turn session context
  EmbeddingService.cs    Text → 768-dim vector via Ollama nomic-embed-text
  QdrantService.cs       Vector DB CRUD (upsert, search with grade filter, delete by file)
  PDFLoader.cs           PDF → text via triple-method fallback (static utility)
  OCRService.cs          Calls the local Ollama vision model to OCR page images into plain text (fallback OCR path)
  MathOcrService.cs      Calls the local Pix2Text HTTP server (:8503) to OCR page images into text + LaTeX (primary math OCR path)
  PersistentVectorStore.cs JSON-file-backed in-memory vector store with cosine-similarity search — legacy/local alternative to Qdrant
  InputSanitizer.cs      Static helper stripping control chars/null bytes and enforcing max length on user-supplied strings before LLM prompt interpolation
  ChatLogService.cs      Chat message persistence (session-scoped via `sessionId`) + subject/topic tagging via LLM classification; sanitises input before prompt interpolation
  ChatSessionService.cs  Chat session lifecycle: creation, ownership checks, multi-turn history replay (`GetRecentTurnsAsync`), AI-generated titles (`GenerateTitleAsync`, one-shot LLM call), and subject-folder assignment locked from the session's first exchange
  StereometryService.cs  3D geometry JSON schema definition + <STEREO> block extraction from LLM output
  StereometryDetector.cs Static keyword-based heuristic (Bulgarian + English) to detect whether a question is about 3D solid geometry
  StereometryHtmlBuilder.cs Static builder that injects a JSON scene into an embedded Three.js HTML template to render interactive 3D stereometry visualisations
  ExamService.cs         Mock exam generation from curriculum material via RAG
  AdminUserService.cs    Global admin operations (school, class, subject, teacher, user management across all schools); typed methods back `GlobalAdminController`, parameterless wrappers back the CLI menu; console wrappers resolve school names to School entity IDs internally; also wraps `RAGService`'s curriculum file list/upload/delete methods so `GlobalAdminController` never depends on `RAGService` directly
  PracticeQuestionService.cs Generates 3 follow-up practice questions from a prior chat exchange via one-shot LLM call; sanitises both inputs before prompt interpolation
  SchoolAdminService.cs  HTTP-compatible per-school admin operations — typed parameters + (bool, error) return tuples, no console I/O; `SchoolId` (int FK) is passed per-call by the controller, resolved from the caller's JWT identity via `ApplicationUser.SchoolId`
  TeacherDashboardService.cs Teacher-facing analytics backing `TeacherDashboardController` — class/subject/student-count listing, per-class topic "struggles" over N days, most-active students per class
  TempFileManager.cs     Tracks temp HTML files for cleanup on process exit (static utility)
  VisualisationService.cs CLI-only — writes an HTML string to a temp file and opens it in the OS default browser; must not be wired to any HTTP endpoint (see Security Requirements)
Models/             EF Core entity definitions and enums — no business logic
  School.cs is the canonical school entity (Id, Name, CreatedAt). ApplicationUser, Class, and Subject each hold a SchoolId FK — never a plain string school name.
  ChatSession.cs groups ChatMessage rows into a conversation (Title, SubjectId "folder", LastMessageAt). ChatMessage.SessionId is a required FK to ChatSession — every chat message belongs to exactly one session.
  ClassTeacher.cs / TeacherSubject.cs are join entities: ClassTeacher links a Class + Teacher + Subject (a teacher's subject assignment within a class); TeacherSubject links a teacher to a Subject they're qualified to teach.
  RefreshToken.cs, PasswordResetCode.cs, RateLimitEntry.cs back the auth/security flows described under Security Requirements below.
  UserRole.cs is the role enum: Student, Teacher, SchoolAdmin, Admin.
Data/               DbContext, migrations, IUserRepository interface and UserRepository implementation
  Migrations/       EF Core migration files — never hand-edit; use dotnet ef migrations add
Database/           Curriculum PDFs organised as Database/DataPdf/Grade{N}/{Subject}/
tessdata-main/      Vendored Tesseract `.traineddata` language files used by the PDFtoImage + Tesseract fallback OCR path
docker-compose.yml, Dockerfile.pix2text, pix2text_server.py  Local Qdrant/Postgres/Pix2Text container setup — `docker compose up -d pix2text` starts the math-OCR server
storage/            Qdrant local vector store data — never commit to git
snapshots/          Temporary HTML visualisation files — never commit to git
frontend/           Vue 3 + Vite + TypeScript SPA — dev server on :5173 (npm run dev), not a separate repo
  src/api/            Typed API client — no UI framework imports here
    client.ts             apiFetch<T>(): auth header attach, 401→refresh→retry-once with concurrent-request dedup, {error}/{errors} → ApiError normalisation
    tokenStorage.ts        localStorage wrapper for JWT/refresh token/user (XSS trade-off vs httpOnly cookies accepted for now — see Frontend Integration Notes)
    auth.ts                login/register/refresh/logout/forgot-password/verify-reset-code/reset-password — typed wrappers over apiFetch
    sse.ts                 parseSseStream(): generic data:-frame reader over a fetch ReadableStream, no knowledge of payload shape
    chat.ts                streamChatMessage(): SSE consumer for POST /api/chat/message; not yet wired to any UI
    types.ts                shared request/response/error types, including the ChatSseFrame discriminated union
  src/stores/auth.ts   Pinia store: {user, token, isAuthenticated, role}; login()/logout(); hydrates from tokenStorage on creation. Deliberately does NOT wrap forgotPassword/verifyResetCode/resetPassword — those don't mutate session state (no token/user change), so `ForgotPasswordForm.vue` calls `authApi` directly instead
  src/router/index.ts  routes (/login, /register, /forgot-password, /app) + beforeEach guard; /login, /register, and /forgot-password are the public routes, no per-role guards yet
  src/styles/          tokens.css (design tokens, see Frontend Integration Notes) + base.css (resets, font wiring)
  src/components/shared/
    Field.vue      labeled input wrapper; optional `revealable` prop adds a text "Show"/"Hide" toggle (flips input type, no icon asset), optional `label-end` slot renders content to the right of the label (used for the Sign In screen's "Forgot?" link); `inheritAttrs: false` + `v-bind="$attrs"` on the inner `<input>` so callers can pass through native attrs (e.g. `maxlength`, `inputmode`) that would otherwise land on the wrapping `<label>`
    SelectField.vue  same label/input visual language as Field.vue but renders a `<select>` with a plain-glyph caret — used for the Register screen's Grade dropdown; options are passed in by the caller, not hardcoded
    SchoolyMark.vue  inline-SVG logo mark (not a static asset — needs to read `var(--green-br)`/`var(--white)` tokens and render two variants), `size`/`variant` (`solid` | `ghost`) props
    AuthShell.vue    shared split-panel chrome (green branding panel + form panel, collapsing to single-column below 860px) wrapping all three auth screens; owns the page-level full-viewport layout (`.auth-page`/`.auth-card` fill the browser window edge-to-edge, no floating/centered card) so `LoginView.vue`/`RegisterView.vue`/`ForgotPasswordView.vue` stay one-line wrappers with no `<style>` of their own
  src/components/login/SignInForm.vue    no role selector (see Frontend Integration Notes); revealable password field with a "Forgot?" link (`label-end` slot) to /forgot-password; links to /register
  src/components/register/RoleTabs.vue, RegisterForm.vue   role toggle is functional here (unlike the deleted login-screen version) since RegisterRequest.role is required; RegisterForm.vue also has a client-side-only "agree to Terms" checkbox gating the submit button (never sent in the RegisterRequest payload — no backend field for it) and a Grade `SelectField` next to Full name (student role only); links back to /login
  src/components/forgot-password/  ForgotPasswordForm.vue owns a 3-step `step` ref ('email'|'code'|'password') and calls `authApi.forgotPassword/verifyResetCode/resetPassword` directly — one stateful component + route rather than three, since the steps share state (email, verified code) that shouldn't leak into the URL. EmailStep.vue/CodeStep.vue/NewPasswordStep.vue are dumb presentational subcomponents, each emitting `submit` with its collected value; NewPasswordStep does a client-side password-match check before ever calling the API
  src/views/  LoginView.vue, RegisterView.vue, ForgotPasswordView.vue, PlaceholderHomeView.vue — the only real screens so far
```

## Coding Conventions

### Naming
- Classes, interfaces, methods, properties: `PascalCase`
- Interfaces: `I` prefix — `IChatService`, `IUserRepository`
- Private fields: `_camelCase` with underscore prefix
- URL segments: `kebab-case` — `/api/auth/forgot-password`
- Database columns: `snake_case`, configured in `AppDbContext.OnModelCreating`

### Async Style
- All I/O (database, LLM, HTTP, file system) is `async Task<T>` / `async Task`
- Never use `.Result` or `.Wait()` — deadlock risk in ASP.NET Core's sync context
- Singletons that need DB access use `IDbContextFactory<AppDbContext>` — never inject `AppDbContext` directly into a singleton

### Error Handling
- Services return result tuples or error lists — they do not throw exceptions for expected failures
- Controllers own HTTP status codes; services own validation logic
- LLM and OCR calls use silent fallback (catch, log, return empty/unknown) — a failed AI call must never crash the HTTP request
- Startup validation is fail-fast: missing or placeholder config values throw before the app starts

### Response Format
- Single error: `{ "error": "message" }`
- Multiple errors: `{ "errors": ["...", "..."] }`
- User object: always `{ id, username, email, fullName, role, grade }` — no full ApplicationUser serialisation

## Library Choices

### Explicitly Prohibited
- `.Result` / `.Wait()` on async calls — deadlock risk
- Raw ADO.NET (SqlConnection, SqlCommand) anywhere except `RateLimiter.cs` where PostgreSQL `ON CONFLICT` atomicity is required
- Dapper or any ORM other than EF Core
- Swagger / OpenAPI packages — not in scope for this project

### Allowed but Scoped
- Raw `SqlQuery<T>` and `ExecuteSql` are used **only** in `RateLimiter.cs` for atomic upserts that EF Core's LINQ cannot express; keep them there

## Layer Rules

- **No SQL in controllers** — controllers call services and translate results to HTTP responses
- **No user DB access outside `IUserRepository`** — all `UserManager<ApplicationUser>` usage lives in `UserRepository.cs`
- **No auth logic in controllers** — `AuthService.cs` owns login, registration, token lifecycle; `AuthController.cs` only wires requests to the service and returns status codes
- **Rate limiting checks must come before auth service calls** — check `RateLimiter` before attempting any DB lookup in login/register/reset flows
- **No chat or LLM calls in controllers** — controllers stream or return results from `RAGService` / `IChatService`
- **Global admin HTTP endpoints require `Admin` role** — `GlobalAdminController` is backed by `AdminUserService` and restricted to the `Admin` JWT claim; `SchoolAdmin` users receive 403
- **SchoolAdmin endpoints must verify school membership** — always confirm the target user (student or teacher being operated on) belongs to the caller's school by comparing `SchoolId` integers; never trust a userId route parameter without a school-ownership check; never accept a school name string from the client — look up `SchoolId` from the authenticated user's record
- **School identity is always an int FK** — `ApplicationUser.SchoolId`, `Class.SchoolId`, `Subject.SchoolId` are all `int?`/`int` FKs to the `Schools` table; request DTOs that target a school use `int SchoolId`, not `string School`

## Security Requirements

- JWT secret must be ≥ 32 characters; enforced at startup before the app accepts connections
- Sensitive string comparisons (teacher registration code) must use `CryptographicOperations.FixedTimeEquals` — never `string ==`
- Password reset uses a DB-stored 6-digit code (10-min expiry) sent via email through `IEmailService` / `SmtpEmailService`; the code is never returned in the HTTP response. The `forgot-password` endpoint returns `404` when the email is not registered (enumeration protection is intentionally removed for this endpoint)
- Reset code verification and password reset share a per-email brute-force counter (max 5 attempts); issuing a new code clears the counter
- Rate limiting must remain database-backed — do not replace with `IMemoryCache` or `IDistributedCache`
- Rate limiting covers: login (per-account + per-IP), registration (per-email), password reset request (per-email, 5-min cooldown), reset code attempts (per-email, max 5), token refresh and logout (per-IP via `IsGeneralApiThrottled`)
- CORS allowed origins come from `Cors:AllowedOrigins` config — never hardcode `*` or specific URLs in code; allowed headers are restricted to `Content-Type` and `Authorization` only
- Refresh token exchange **and** revocation must both use `IsolationLevel.RepeatableRead` — do not lower this isolation level
- `appsettings.Local.json` contains real secrets for local dev — never commit it; production uses environment variables
- All user-supplied strings that reach LLM prompts must be passed through `InputSanitizer.SanitizeUserInput()` before interpolation — enforced in `RAGService.Ask()`, `ExamService.GenerateExamAsync()`, `ChatLogService.cs`, `ChatSessionService.cs`, and `PracticeQuestionService.cs`
- `RAGService` must be registered as **Scoped**, never Singleton — `_currentGrade` and `_temporaryChunks` are per-user instance state; in web endpoints, grade must come from the authenticated user's JWT claims, never from request parameters
- `VisualisationService` is CLI-only — it calls `Process.Start()` and holds static state; it must not be wired to any HTTP endpoint
- In production, `app.UseForwardedHeaders()` must run first in the pipeline and `KnownProxies` must list your specific proxy IPs — the current config trusts all proxies (safe for local dev, not for production)

## Frontend Integration Notes

- `Properties/launchSettings.json` pins local dev to `http://localhost:5080` (`dotnet run` reads this). It's HTTP-only on purpose: `Program.cs` calls `app.UseHttpsRedirection()` unconditionally, but that middleware silently no-ops (logs a warning, passes the request through) when Kestrel has no bound HTTPS URL — so local dev stays plain HTTP with no redirect and no dev-cert trust step needed. Don't add an HTTPS `applicationUrl` to the local profile without also updating frontend/CORS expectations, since doing so activates the redirect.
- Swagger/OpenAPI is intentionally absent (see Explicitly Prohibited above) — there is no generated API contract. The frontend contract lives in `PRD.md`'s Behavioral Specification (endpoint-by-endpoint request/response behavior) and, for the chat SSE stream specifically, in the frame-format comment above `ChatController.SendMessage`. Point frontend developers there instead of expecting generated docs.
- **Login has no role selector; registration does.** `LoginRequest` has no role field — the backend derives role from the authenticated user's JWT (`role` claim, PascalCase: `Student`/`Teacher`/`SchoolAdmin`/`Admin`) after credentials are verified, not from anything the client sends. Don't reintroduce a role toggle on the login form — a second auth-screens mockup (`~/Downloads/Schooly Auth Screens-selection.png`) shows Student/Teacher tabs on Sign In too, but that was deliberately **not** implemented, per this same rule. `RegisterView.vue`/`RegisterForm.vue` collect `POST /api/auth/register`'s required `role: "student"|"teacher"` field via `components/register/RoleTabs.vue`, plus grade (shown for students, via `SelectField.vue`) or a teacher registration code (shown for teachers, matched server-side against `TeacherRegistrationCode` config via `CryptographicOperations.FixedTimeEquals`). All three auth screens (login/register/forgot-password) share `components/shared/Field.vue` and the `components/shared/AuthShell.vue` split-panel chrome.
- **Auth screen copy is English**, matching the app's target UI language (see Language Policy below) and the auth-screens mockup literally. `PlaceholderHomeView.vue` and any other remaining Bulgarian UI strings are pre-existing/not yet migrated, not an intentional bilingual design — don't treat them as a model to copy for new screens.
- **Design tokens** for `frontend/src/styles/tokens.css` were extracted (by regex, since the source is a minified/bundled single-file React export) from a hand-made mockup at `~/Downloads/Schooly UI Mockups (standalone).html`. Palette: cream/paper base (`--paper: #FBF6EC`, `--cream: #F3E7CF`), green accents (`--green: #5E8A63`, `--green-br: #6FA873`), ink text (`--ink: #2B2D2B`); fonts `Fredoka` (headings) + `Nunito` (body) via `@fontsource`. The mockup's component names (`DashboardA/B`, `ChatA/B`, `Viewer3D`, `RetrievedChunk`/`SourceCard`, `LeafBuddy` mascot, mobile variants) indicate the intended screen inventory for later frontend steps — open the mockup file directly in a browser to reference exact layouts, since it couldn't be statically parsed with full fidelity. Note: the mockup contains the string `"student / parent / teacher"`, suggesting a parent role that does **not** exist in the backend's `UserRole` enum — don't build UI that assumes one exists without a corresponding backend change. No dedicated logo/mascot asset file exists anywhere in the repo — `SchoolyMark.vue` recreates the mockup's circular mark as inline SVG rather than importing an image.
- **Token storage is `localStorage`** (`frontend/src/api/tokenStorage.ts`), a known accepted trade-off: it's XSS-exposed, but httpOnly cookies would require backend changes (cookie issuance, CSRF handling) out of scope for the current frontend step.
- **Forgot Password is a fully wired end-to-end flow**, not a placeholder — `/forgot-password` → `ForgotPasswordForm.vue` drives the existing `POST /api/auth/forgot-password|verify-reset-code|reset-password` endpoints through their real request/response contract (404 on unknown email, 429 on rate limits, 400 on invalid code/password), ending in a redirect to `/login` on success. Requires a working SMTP config (see Local Environment Prerequisites in `README.md`) to actually receive the emailed code when testing locally.

## Language Policy

- **App UI target language is English.** This is the direction going forward, not a one-off exception — the redesigned auth screens (`LoginView.vue`/`RegisterView.vue`/`ForgotPasswordView.vue`) are in English for this reason. `PlaceholderHomeView.vue` and any other remaining Bulgarian UI strings predate this decision and haven't been migrated yet; don't copy their language when building new screens.
- **The AI tutor must respond in whatever language the student used, not a fixed language.** This applies to chat answers, mock exams, and practice questions — the AI's output to the student should mirror the student's input language (English, Bulgarian, or otherwise), independent of the UI's English copy. Enforced via explicit system-prompt instructions in `RAGService.cs` (`Ask`/`AskStreamAsync`), `ExamService.cs`, and `PracticeQuestionService.cs`; `ChatSessionService.cs`'s `GenerateTitleAsync` implemented this pattern first (`"...in the same language as the question."`) and is the reference wording. When adding a new LLM-calling prompt that produces user-facing text, include an equivalent instruction rather than assuming the model will infer it.
- **Exception: internal classification labels stay Bulgarian.** `ChatLogService.cs`'s subject/topic classifier (used to tag chat messages for folder/metadata purposes) always produces its `subject`/`topic` output in Bulgarian — these are canonical Bulgarian curriculum subject names for internal categorization, not the AI's answer to the student, so this policy doesn't apply to them.
- **Curriculum source material stays Bulgarian regardless.** This policy governs response language, not the language of the textbook content retrieved from Qdrant — `RAGService.cs`'s existing "...may be in a different language — translate as needed" instruction already handles translating source excerpts into the answer.

## Definition of Done

A feature is complete when all of the following are true:

1. Service method returns a typed result — no `object` returns, no unhandled exceptions for expected failure cases
2. Controller endpoint exists with the correct HTTP method, route, auth requirements, and status codes
3. Rate limiting and `[Authorize]` guards are applied where the feature requires them
4. A manual end-to-end test passes against a running local instance (login → use the feature → confirm result)
5. No new `.Result` / `.Wait()` calls or raw SQL outside `RateLimiter.cs` were introduced
