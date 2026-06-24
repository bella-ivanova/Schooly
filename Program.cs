using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudyAssistant.Data;
using StudyAssistant.Models;
using StudyAssistant.Services;

var builder = WebApplication.CreateBuilder(args);

// Fail fast if secrets are missing or still hold placeholder values.
var jwtSecret      = builder.Configuration["Jwt:Secret"] ?? "";
var connStr        = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
var teacherRegCode = builder.Configuration["TeacherRegistrationCode"] ?? "";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
string[] placeholders = ["REPLACE_WITH_JWT_SECRET", "REPLACE_WITH_DB_PASSWORD",
                         "REPLACE_WITH_CONNECTION_STRING", "REPLACE_WITH_TEACHER_CODE",
                         "CHANGE_ME_BEFORE_DEPLOY", "REPLACE_WITH_FRONTEND_ORIGIN"];

if (string.IsNullOrEmpty(jwtSecret) || placeholders.Any(p => jwtSecret.Equals(p, StringComparison.Ordinal)))
    throw new InvalidOperationException("Jwt:Secret is not set. Supply it via the Jwt__Secret environment variable.");
if (jwtSecret.Length < 32)
    throw new InvalidOperationException("Jwt:Secret must be at least 32 characters.");
if (string.IsNullOrEmpty(connStr) || placeholders.Any(connStr.Contains))
    throw new InvalidOperationException("Connection string is not configured. Supply it via ConnectionStrings__DefaultConnection.");
if (string.IsNullOrEmpty(teacherRegCode) || placeholders.Any(p => teacherRegCode.Equals(p, StringComparison.Ordinal)))
    throw new InvalidOperationException("TeacherRegistrationCode is not set or is still the default placeholder.");
if (allowedOrigins.Length == 0 || allowedOrigins.Any(o => placeholders.Contains(o)))
    throw new InvalidOperationException("Cors:AllowedOrigins is not configured. Add your frontend origin(s) via Cors__AllowedOrigins__0.");

// ── Services ──────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .WithMethods("GET", "POST", "PUT", "DELETE")));

builder.Services.AddControllers();

builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(connStr));

builder.Services
    .AddDbContext<AppDbContext>(o => o.UseNpgsql(connStr))
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
    .AddScoped<AuthService>()
    .AddSingleton<RateLimiter>();

// ── JWT Authentication ────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
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

// ── App pipeline ──────────────────────────────────────────────────────────
var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
