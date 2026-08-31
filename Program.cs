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
var ollamaModel  = builder.Configuration["Llm:OllamaModel"]      ?? "todorov/bggpt";
var embedModel   = builder.Configuration["Llm:OllamaEmbedModel"]  ?? "nomic-embed-text-v2-moe";
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
builder.Services.AddScoped<SubjectResolutionService>();
builder.Services.AddScoped<ChatLogService>();
builder.Services.AddScoped<ChatSessionService>();
builder.Services.AddScoped<TeacherDashboardService>();
builder.Services.AddScoped<SchoolAdminService>();
builder.Services.AddScoped<AdminUserService>();
builder.Services.AddScoped<PracticeQuestionService>();
builder.Services.AddScoped<ExamService>();
// Stateless after AddAllLanguages() loads its embedded n-gram profiles — safe to share.
builder.Services.AddSingleton<LanguageDetectionService>();

// ── App pipeline ──────────────────────────────────────────────────────────
// Args-gated diagnostic entry point — never reachable via HTTP, same CLI-only
// pattern as VisualisationService. Usage:
//   dotnet run -- diagnose-retrieval "<question>" <grade> [--prefix]
if (args.Length > 0 && args[0] == "diagnose-retrieval")
{
    if (args.Length < 3 || !int.TryParse(args[2], out var diagGrade))
    {
        Console.WriteLine("Usage: dotnet run -- diagnose-retrieval \"<question>\" <grade> [--prefix]");
        return;
    }

    bool diagUsePrefix = args.Length > 3 && args[3] == "--prefix";
    var diagEmbedding = new EmbeddingService(embedModel);
    var diagQdrant    = new QdrantService(qdrantHost, qdrantPort);
    await RetrievalDiagnostics.RunAsync(args[1], diagGrade, diagEmbedding, diagQdrant, diagUsePrefix);
    return;
}

// Args-gated, READ-ONLY diagnostic tool for browsing the production "studyassist"
// collection so real chunks/pages can be picked to build a retrieval regression
// fixture. Never wired to HTTP, never writes/deletes against the collection. Usage:
//   dotnet run -- sample-chunks <grade> [<sourceFile>] [<limit>]
if (args.Length > 0 && args[0] == "sample-chunks")
{
    if (args.Length < 2 || !int.TryParse(args[1], out var sampleGrade))
    {
        Console.WriteLine("Usage: dotnet run -- sample-chunks <grade> [<sourceFile>] [<limit>]");
        return;
    }

    string? sampleFile = args.Length > 2 ? args[2] : null;
    int sampleLimit = args.Length > 3 && int.TryParse(args[3], out var sl) ? sl : 15;
    await ChunkSampler.SampleAsync(qdrantHost, qdrantPort, sampleGrade, sampleFile, sampleLimit);
    return;
}

// Companion to sample-chunks — prints one chunk's full untruncated text by point id,
// for copying an exact matchText snippet into a fixture. READ-ONLY. Usage:
//   dotnet run -- show-chunk <pointId>
if (args.Length > 0 && args[0] == "show-chunk")
{
    if (args.Length < 2)
    {
        Console.WriteLine("Usage: dotnet run -- show-chunk <pointId>");
        return;
    }

    await ChunkSampler.ShowChunkAsync(qdrantHost, qdrantPort, args[1]);
    return;
}

// Args-gated, READ-ONLY validator: resolves every fixture expectedChunk against the
// production collection and reports OK/MISSING, catching typos or stale matchText
// snippets before running the full comparison. Usage:
//   dotnet run -- validate-fixture <fixturePath>
if (args.Length > 0 && args[0] == "validate-fixture")
{
    if (args.Length < 2)
    {
        Console.WriteLine("Usage: dotnet run -- validate-fixture <fixturePath>");
        return;
    }

    await ChunkSampler.ValidateFixtureAsync(qdrantHost, qdrantPort, args[1]);
    return;
}

// Args-gated diagnostic entry point comparing retrieval quality across 3 embedding
// configs (current / prefixed / prefixed+v2-moe) against a hand-authored fixture.
// Only ever reads the production "studyassist" collection; uses disposable temp
// collections for the other two configs, always torn down before returning. Usage:
//   dotnet run -- compare-retrieval <fixturePath> [--full-corpus] [--current-prefix]
// --full-corpus: configs 2/3 re-embed the ENTIRE grade's corpus instead of just the
//   fixture's own chunks, removing the pool-size confound of a small decoy set.
// --current-prefix: Config 1 embeds the query WITH the prefix — use after production
//   has been migrated to always prefix, to validate the real collection end-to-end.
if (args.Length > 0 && args[0] == "compare-retrieval")
{
    if (args.Length < 2)
    {
        Console.WriteLine("Usage: dotnet run -- compare-retrieval <fixturePath> [--full-corpus] [--current-prefix]");
        return;
    }

    bool compareFullCorpus = args.Contains("--full-corpus");
    bool compareCurrentPrefix = args.Contains("--current-prefix");
    await RetrievalComparisonRunner.RunAsync(args[1], embedModel, qdrantHost, qdrantPort, compareFullCorpus, compareCurrentPrefix);
    return;
}

var app = builder.Build();

// Args-gated diagnostic entry point — never reachable via HTTP, same CLI-only
// pattern as diagnose-retrieval/VisualisationService. Usage:
//   dotnet run -- reingest-grade <grade>
// Deletes each already-ingested file's chunks for the grade, then re-ingests the
// whole grade folder fresh (picks up ingestion pipeline changes, e.g. the OCR fix).
if (args.Length > 0 && args[0] == "reingest-grade")
{
    if (args.Length < 2 || !int.TryParse(args[1], out var reingestGrade))
    {
        Console.WriteLine("Usage: dotnet run -- reingest-grade <grade>");
        return;
    }

    using var scope = app.Services.CreateScope();
    var ragService = scope.ServiceProvider.GetRequiredService<RAGService>();

    var existingFiles = await ragService.GetIngestedFilesAsync(reingestGrade);
    foreach (var fileKey in existingFiles)
        await ragService.DeleteGradeFileAsync(reingestGrade, fileKey);

    await ragService.IngestGradePDFsAsync(reingestGrade);
    return;
}

app.UseForwardedHeaders();
app.UseHsts();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
