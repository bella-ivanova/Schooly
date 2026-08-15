using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudyAssistant.Data;
using StudyAssistant.Models;
using StudyAssistant.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Fail fast if secrets are missing or still hold placeholder values.
var jwtSecret      = builder.Configuration["Jwt:Secret"] ?? "";
var connStr        = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var smtpHost       = builder.Configuration["Smtp:Host"]     ?? "";
var smtpUsername   = builder.Configuration["Smtp:Username"] ?? "";
var smtpPassword   = builder.Configuration["Smtp:Password"] ?? "";
var smtpFrom       = builder.Configuration["Smtp:From"]     ?? "";
string[] placeholders = ["REPLACE_WITH_JWT_SECRET", "REPLACE_WITH_DB_PASSWORD",
                         "REPLACE_WITH_CONNECTION_STRING",
                         "CHANGE_ME_BEFORE_DEPLOY", "REPLACE_WITH_FRONTEND_ORIGIN",
                         "REPLACE_WITH_SMTP_HOST", "REPLACE_WITH_SMTP_USERNAME",
                         "REPLACE_WITH_SMTP_PASSWORD", "REPLACE_WITH_FROM_EMAIL"];

if (string.IsNullOrEmpty(jwtSecret) || placeholders.Any(p => jwtSecret.Equals(p, StringComparison.Ordinal)))
    throw new InvalidOperationException("Jwt:Secret is not set. Supply it via the Jwt__Secret environment variable.");
if (jwtSecret.Length < 32)
    throw new InvalidOperationException("Jwt:Secret must be at least 32 characters.");
if (string.IsNullOrEmpty(connStr) || placeholders.Any(connStr.Contains))
    throw new InvalidOperationException("Connection string is not configured. Supply it via ConnectionStrings__DefaultConnection.");
if (allowedOrigins.Length == 0 || allowedOrigins.Any(o => placeholders.Contains(o)))
    throw new InvalidOperationException("Cors:AllowedOrigins is not configured. Add your frontend origin(s) via Cors__AllowedOrigins__0.");
if (string.IsNullOrEmpty(smtpHost) || placeholders.Contains(smtpHost))
    throw new InvalidOperationException("Smtp:Host is not configured. Supply it via the Smtp__Host environment variable.");
if (string.IsNullOrEmpty(smtpUsername) || placeholders.Contains(smtpUsername))
    throw new InvalidOperationException("Smtp:Username is not configured. Supply it via the Smtp__Username environment variable.");
if (string.IsNullOrEmpty(smtpPassword) || placeholders.Contains(smtpPassword))
    throw new InvalidOperationException("Smtp:Password is not configured. Supply it via the Smtp__Password environment variable.");
if (string.IsNullOrEmpty(smtpFrom) || placeholders.Contains(smtpFrom))
    throw new InvalidOperationException("Smtp:From is not configured. Supply it via the Smtp__From environment variable.");

// Trust X-Forwarded-For / X-Forwarded-Proto from the immediate upstream proxy only.
// In production, restrict KnownProxies to the specific proxy IP(s) in front of this server.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// ── Services ──────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .WithHeaders("Content-Type", "Authorization")
              .WithMethods("GET", "POST", "PUT", "DELETE")));

builder.Services.AddControllers();

builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(connStr));

builder.Services
    .AddIdentityCore<ApplicationUser>(o =>
    {
        o.Password.RequireNonAlphanumeric = true;
        o.Password.RequireUppercase       = true;
        o.Password.RequireDigit           = true;
        o.Password.RequiredLength         = 10;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders().Services
    .AddDataProtection().Services
    .AddScoped<IUserRepository, UserRepository>()
    .AddScoped<IEmailService, SmtpEmailService>()
    .AddScoped<AuthService>()
    .AddSingleton<RateLimiter>();

// ── JWT Authentication ────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // Without this, the default JwtSecurityTokenHandler remaps short claim names
        // ("sub", "role") to long ClaimTypes URIs on the inbound principal, so every
        // User.FindFirstValue("sub")/("role") call across the app silently returns null.
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"]   ?? "StudyAssistant",
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"] ?? "StudyAssistantUsers",
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Named HttpClients for services that call long-running local inference servers.
// Timeout.InfiniteTimeSpan is intentional — OCR/math-OCR can take minutes per page.
builder.Services.AddHttpClient("ocr",    c => c.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddHttpClient("mathocr", c => c.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddHttpClient("zhipuai", c => c.Timeout = Timeout.InfiniteTimeSpan);

// ── LLM / RAG services ────────────────────────────────────────────────────
var ollamaModel  = builder.Configuration["Llm:OllamaModel"]      ?? "minicpm-v";
var embedModel   = builder.Configuration["Llm:OllamaEmbedModel"]  ?? "nomic-embed-text";
var visionModel  = builder.Configuration["Llm:OllamaVisionModel"] ?? "minicpm-v";
var qdrantHost   = builder.Configuration["Qdrant:Host"]           ?? "localhost";
var qdrantPort   = int.TryParse(builder.Configuration["Qdrant:Port"], out var qp) ? qp : 6334;

// IChatService is Scoped so each request gets a fresh conversation history.
builder.Services.AddScoped<IChatService>(_ => new OllamaChatService(ollamaModel));
builder.Services.AddScoped<EmbeddingService>(_ => new EmbeddingService(embedModel));
builder.Services.AddScoped<QdrantService>(_ => new QdrantService(qdrantHost, qdrantPort));
builder.Services.AddScoped<OCRService>(sp =>
    new OCRService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("ocr"), visionModel));
builder.Services.AddScoped<MathOcrService>(sp =>
    new MathOcrService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("mathocr")));
// RAGService must be Scoped — _currentGrade and _temporaryChunks are per-request state.
builder.Services.AddScoped<RAGService>();
builder.Services.AddScoped<ChatLogService>();
builder.Services.AddScoped<ChatSessionService>();
builder.Services.AddScoped<TeacherDashboardService>();
builder.Services.AddScoped<SchoolAdminService>();
builder.Services.AddScoped<AdminUserService>();
builder.Services.AddScoped<PracticeQuestionService>();
builder.Services.AddScoped<ExamService>();

// ── App pipeline ──────────────────────────────────────────────────────────
var app = builder.Build();

app.UseForwardedHeaders();
app.UseHsts();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
