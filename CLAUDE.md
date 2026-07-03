# CLAUDE.md — StudyAssist Project Context

## Technology Stack

| Technology | Version | Why This Choice |
|---|---|---|
| ASP.NET Core | 10.0 | Target platform; .NET 10 for performance improvements and minimal API features |
| PostgreSQL + EF Core + Npgsql | 10.* | Relational data with migrations; PostgreSQL's `ON CONFLICT DO UPDATE` is required for atomic rate-limit upserts that EF Core cannot express |
| ASP.NET Identity | 10.* | PBKDF2 password hashing, password validation rules, and direct password change without reinventing them |
| MailKit | 4.* | SMTP email delivery for password reset codes |
| JWT Bearer (HS256) | 10.* / 8.* | Stateless auth with revocable refresh tokens; HS256 is sufficient for a single-server trust boundary |
| OllamaSharp | 5.4.24 | Local LLM inference — keeps student data on-premises; has native streaming support |
| ZhipuAI (HTTP) | — | Remote LLM fallback via OpenAI-compatible API (`/v4/chat/completions`) when Ollama is unavailable |
| Qdrant | 1.17.0 | Vector DB for RAG; supports payload-based metadata filtering so grade-N students only receive grade ≤ N curriculum material |
| nomic-embed-text (Ollama) | — | 768-dim embeddings; dimension is hardcoded in `EmbeddingService.cs` and must match the Qdrant collection |
| UglyToad.PdfPig | 1.7.0-custom-5 | Fast plain-text extraction from machine-readable PDFs (primary OCR path) |
| PDFtoImage + Tesseract | 5.2.0 | Fallback OCR for image-heavy or scanned PDFs |
| Pix2Text (Docker, :8503) | — | Math OCR producing LaTeX output — required because Bulgarian math textbooks contain dense formula notation |
| SkiaSharp | 3.119.2 | Image manipulation in the OCR pipeline; macOS native assets are included because dev happens on Mac |

## Directory Structure

```
Controllers/        HTTP endpoints only — no business logic, no DB access
  AuthController.cs      /api/auth — login, register, logout, token refresh, password reset
  ChatController.cs      /api/chat/message (SSE streaming) and /api/chat/upload (session PDF ingest)
Services/           All business logic and external integrations
  AuthService.cs         User/token lifecycle (registration, login, JWT, refresh tokens, password reset)
  IEmailService.cs       Email abstraction interface (SmtpEmailService implements it via MailKit)
  SmtpEmailService.cs    SMTP email delivery — sends 6-digit password reset codes
  RateLimiter.cs         Brute-force protection — singleton, database-backed, survives restarts
  IChatService.cs        LLM abstraction interface (OllamaChatService and ZhipuAIChatService implement it)
  RAGService.cs          Retrieval + LLM orchestration (embeds query → searches Qdrant → calls LLM); `AskStreamAsync` is the HTTP-facing entry point that yields tokens, `Ask` is the CLI entry point
  EmbeddingService.cs    Text → 768-dim vector via Ollama nomic-embed-text
  QdrantService.cs       Vector DB CRUD (upsert, search with grade filter, delete by file)
  PDFLoader.cs           PDF → text via triple-method fallback (static utility)
  ChatLogService.cs      Chat message persistence + subject/topic tagging via LLM classification
  StereometryService.cs  3D geometry JSON schema definition + <STEREO> block extraction from LLM output
  ExamService.cs         Mock exam generation from curriculum material via RAG
  AdminUserService.cs    CLI-only admin operations (class, subject, teacher, user management)
  TempFileManager.cs     Tracks temp HTML files for cleanup on process exit (static utility)
Models/             EF Core entity definitions and enums — no business logic
Data/               DbContext, migrations, IUserRepository interface and UserRepository implementation
  Migrations/       EF Core migration files — never hand-edit; use dotnet ef migrations add
Database/           Curriculum PDFs organised as Database/DataPdf/Grade{N}/{Subject}/
storage/            Qdrant local vector store data — never commit to git
snapshots/          Temporary HTML visualisation files — never commit to git
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
- **Admin operations are CLI-only** — `AdminUserService` is never registered as an HTTP controller

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
- All user-supplied strings that reach LLM prompts must be passed through `InputSanitizer.SanitizeUserInput()` before interpolation — enforced in `RAGService.Ask()` and `ExamService.GenerateExamAsync()`
- `RAGService` must be registered as **Scoped**, never Singleton — `_currentGrade` and `_temporaryChunks` are per-user instance state; in web endpoints, grade must come from the authenticated user's JWT claims, never from request parameters
- `VisualisationService` is CLI-only — it calls `Process.Start()` and holds static state; it must not be wired to any HTTP endpoint
- In production, `app.UseForwardedHeaders()` must run first in the pipeline and `KnownProxies` must list your specific proxy IPs — the current config trusts all proxies (safe for local dev, not for production)

## Definition of Done

A feature is complete when all of the following are true:

1. Service method returns a typed result — no `object` returns, no unhandled exceptions for expected failure cases
2. Controller endpoint exists with the correct HTTP method, route, auth requirements, and status codes
3. Rate limiting and `[Authorize]` guards are applied where the feature requires them
4. A manual end-to-end test passes against a running local instance (login → use the feature → confirm result)
5. No new `.Result` / `.Wait()` calls or raw SQL outside `RateLimiter.cs` were introduced
