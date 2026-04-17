using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudyAssistant.Data;
using StudyAssistant.Models;
using StudyAssistant.Services;

Console.WriteLine("=== Study Assistant ===\n");

// ── Dependency injection (auth stack) ──────────────────────────
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var services = new ServiceCollection()
    .AddDbContext<AppDbContext>(o =>
        o.UseNpgsql(config.GetConnectionString("DefaultConnection")))
    .AddIdentityCore<ApplicationUser>(o =>
    {
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequireUppercase       = false;
        o.Password.RequiredLength         = 8;
    })
    .AddEntityFrameworkStores<AppDbContext>().Services
    .AddScoped<IUserRepository, UserRepository>()
    .AddSingleton<IConfiguration>(config)
    .AddScoped<AuthService>()
    .BuildServiceProvider();

var authService = services.GetRequiredService<AuthService>();

// Delete any temp visualisation HTML files when the process exits.
AppDomain.CurrentDomain.ProcessExit += (_, _) => TempFileManager.CleanupAll();

// ── Model setup ────────────────────────────────────────────────
Console.Write("Enter model name (default: glm-5:cloud): ");
var modelInput = Console.ReadLine();
var model = string.IsNullOrWhiteSpace(modelInput) ? "glm-5:cloud" : modelInput;

Console.Write("Enter embedding model (default: nomic-embed-text): ");
var embedInput = Console.ReadLine();
var embedModel = string.IsNullOrWhiteSpace(embedInput) ? "nomic-embed-text" : embedInput;

Console.Write("Enter OCR vision model (default: minicpm-v, press Enter to skip OCR): ");
var ocrInput = Console.ReadLine()?.Trim();
OCRService? ocr = null;
if (!string.IsNullOrWhiteSpace(ocrInput) || ocrInput == "")
{
    var ocrModel = string.IsNullOrWhiteSpace(ocrInput) ? "minicpm-v" : ocrInput;
    ocr = new OCRService(ocrModel);
    Console.WriteLine($"OCR enabled with model: {ocrModel}");
}

Console.Write("Enable Pix2Text math OCR? Needed for LaTeX formulas in textbooks (y/n, default n): ");
var mathOcrInput = Console.ReadLine()?.Trim().ToLower();
MathOcrService? mathOcr = null;
if (mathOcrInput == "y")
{
    mathOcr = new MathOcrService();
}

var chat      = new OllamaChatService(model);
var embedding = new EmbeddingService(embedModel);
var qdrant    = new QdrantService();
var rag       = new RAGService(chat, embedding, qdrant, ocr, mathOcr);

// ── Mode selection ─────────────────────────────────────────────
Console.WriteLine("\nSelect mode:");
Console.WriteLine("  1. Student");
Console.WriteLine("  2. Teacher");
Console.Write("\nChoice: ");
var modeInput = Console.ReadLine()?.Trim();

if (modeInput == "/bleblablebliblobli")
{
    await RunAdminMode(rag);
}
else if (modeInput == "2")
{
    var principal = await RunLoginAsync(authService, UserRole.Teacher);
    if (principal != null)
    {
        var repo = services.GetRequiredService<IUserRepository>();
        await RunTeacherMode(principal, repo, rag);
    }
}
else
{
    await RunStudentMode(chat, rag);
}

// ══════════════════════════════════════════════════════════════
// ADMIN MODE
// ══════════════════════════════════════════════════════════════
async Task RunAdminMode(RAGService ragService)
{
    const string AdminPassword = "xg-sk3";

    Console.Write("\nEnter admin password: ");
    var pwd = ReadPassword();

    if (pwd != AdminPassword)
    {
        Console.WriteLine("\nIncorrect password. Exiting.");
        return;
    }

    Console.WriteLine("\nAdmin mode. Commands:");
    Console.WriteLine("  /ingest <grade>          — ingest PDFs for a grade");
    Console.WriteLine("  /status <grade>          — list files ingested for a grade");
    Console.WriteLine("  /delete <grade> <file>   — delete a file from a grade");
    Console.WriteLine("  /exit                    — quit\n");

    while (true)
    {
        Console.Write("Admin> ");
        var input = Console.ReadLine()?.Trim() ?? "";

        if (input == "/exit") break;

        if (input.StartsWith("/ingest "))
        {
            var parts = input.Split(' ', 2);
            if (parts.Length < 2 || !int.TryParse(parts[1].Trim(), out var grade) || grade < 1)
            {
                Console.WriteLine("Usage: /ingest <grade number>");
                continue;
            }
            try { await ragService.IngestGradePDFsAsync(grade); }
            catch (Exception ex) when (IsQdrantDown(ex))
            {
                Console.WriteLine("[Qdrant is not running. Start it with: docker-compose up -d qdrant]");
            }
        }
        else if (input.StartsWith("/status"))
        {
            var parts = input.Split(' ', 2);
            if (parts.Length < 2 || !int.TryParse(parts[1].Trim(), out var grade))
            {
                Console.WriteLine("Usage: /status <grade number>");
                continue;
            }
            try
            {
                var files = await ragService.GetIngestedFilesAsync(grade);
                if (files.Count == 0)
                {
                    Console.WriteLine($"No files ingested for Grade {grade}.");
                    continue;
                }
                Console.WriteLine($"Grade {grade} ingested files:");
                foreach (var f in files.OrderBy(x => x))
                    Console.WriteLine($"  {f}");
            }
            catch (Exception ex) when (IsQdrantDown(ex))
            {
                Console.WriteLine("[Qdrant is not running. Start it with: docker-compose up -d qdrant]");
            }
        }
        else if (input.StartsWith("/delete "))
        {
            var parts = input.Split(' ', 3);
            if (parts.Length < 3 || !int.TryParse(parts[1].Trim(), out var grade))
            {
                Console.WriteLine("Usage: /delete <grade> <subject/filename.pdf>");
                continue;
            }
            var fileKey = parts[2].Trim();
            try { await ragService.DeleteGradeFileAsync(grade, fileKey); }
            catch (Exception ex) when (IsQdrantDown(ex))
            {
                Console.WriteLine("[Qdrant is not running. Start it with: docker-compose up -d qdrant]");
            }
        }
        else
        {
            Console.WriteLine("Unknown command. Type /exit to quit.");
        }
    }
}

// ══════════════════════════════════════════════════════════════
// STUDENT MODE
// ══════════════════════════════════════════════════════════════
async Task RunStudentMode(OllamaChatService chatService, RAGService ragService)
{
    Console.Write("\nEnter your grade (1-12), or press Enter to skip: ");
    var gradeInput = Console.ReadLine()?.Trim();
    if (!string.IsNullOrWhiteSpace(gradeInput) && int.TryParse(gradeInput, out var grade) && grade >= 1 && grade <= 12)
        ragService.SetGrade(grade);

    chatService.SetSystemPrompt(
        "You are a helpful school tutor. Explain concepts clearly and step by step.");

    Console.WriteLine("\nCommands: /clear  /load <pdf path>  /visualise  /exit\n");

    while (true)
    {
        Console.Write("\nYou: ");
        var input = Console.ReadLine()?.Trim() ?? "";

        if (input == "/exit") break;

        if (input == "/clear")
        {
            ragService.ClearTemporaryChunks();
            chatService.SetSystemPrompt(
                "You are a helpful school tutor. Explain concepts clearly and step by step.");
            Console.WriteLine("Conversation cleared.");
            continue;
        }

        if (input.StartsWith("/load "))
        {
            var path = input[6..].Trim();
            if (!File.Exists(path))
            {
                Console.WriteLine($"File not found: {path}");
                continue;
            }
            await ragService.AddTemporaryPDFAsync(path);
            continue;
        }

        // Re-open the most recently generated 3D visualisation.
        if (input == "/visualise")
        {
            VisualisationService.ShowLast();
            continue;
        }

        if (string.IsNullOrWhiteSpace(input)) continue;

        Console.Write("\nAI: ");
        try
        {
            if (StereometryDetector.IsStereometryQuestion(input))
            {
                // For stereometry questions:
                //   • StreamMessageFilteredAsync streams the answer to the console
                //     but silently swallows the trailing <GEOM>…</GEOM> block.
                //   • The full response (including the GEOM JSON) is returned so
                //     we can extract it and open the browser visualiser.
                var fullResponse = await ragService.Ask(
                    input,
                    capture: true,
                    instructionSuffix: VisualisationService.GeomInstruction
                );

                Console.WriteLine();

                if (fullResponse != null)
                {
                    var geomJson = VisualisationService.ExtractGeomJson(fullResponse);
                    if (geomJson != null)
                    {
                        Console.WriteLine("[Opening 3D visualisation in browser…]");
                        VisualisationService.ShowVisualisation(geomJson);
                    }
                }
            }
            else
            {
                await ragService.Ask(input);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[Error] {ex.Message}");
        }
        Console.WriteLine();
    }
}

// ══════════════════════════════════════════════════════════════
// TEACHER MODE
// ══════════════════════════════════════════════════════════════
async Task RunTeacherMode(ClaimsPrincipal principal, IUserRepository repo, RAGService ragService)
{
    var name = principal.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName) ?? "Teacher";
    Console.WriteLine($"\nWelcome, {name}.");
    Console.WriteLine("Commands:");
    Console.WriteLine("  /students                  — list all students");
    Console.WriteLine("  /students <grade>          — list students in a grade");
    Console.WriteLine("  /students <grade> <class>  — list students in a grade and class");
    Console.WriteLine("  /exit                      — quit\n");

    while (true)
    {
        Console.Write("Teacher> ");
        var input = Console.ReadLine()?.Trim() ?? "";

        if (input == "/exit") break;

        if (input == "/students" || input.StartsWith("/students "))
        {
            var parts = input.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

            int? gradeFilter = null;
            string? classFilter = null;

            if (parts.Length >= 2 && int.TryParse(parts[1], out var g) && g >= 1 && g <= 12)
                gradeFilter = g;

            if (parts.Length >= 3 && gradeFilter.HasValue)
                classFilter = parts[2].Trim().ToUpperInvariant();

            var allStudents = await repo.GetByRoleAsync(UserRole.Student);

            var students = allStudents
                .Where(u => !gradeFilter.HasValue || u.Grade == gradeFilter)
                .Where(u => classFilter == null || string.Equals(u.Class, classFilter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(u => u.Grade)
                .ThenBy(u => u.Class)
                .ThenBy(u => u.UserName)
                .ToList();

            if (students.Count == 0)
            {
                var noResultMsg = (gradeFilter, classFilter) switch
                {
                    (null, _)              => "No students registered.",
                    ({ } gr, null)         => $"No students in Grade {gr}.",
                    ({ } gr, { } cl)       => $"No students in Grade {gr} Class {cl}."
                };
                Console.WriteLine(noResultMsg);
                continue;
            }

            var header = (gradeFilter, classFilter) switch
            {
                (null, _)              => "All students:",
                ({ } gr, null)         => $"Grade {gr} students:",
                ({ } gr, { } cl)       => $"Grade {gr} Class {cl} students:"
            };
            Console.WriteLine(header);

            foreach (var s in students)
            {
                var gradeTag  = s.Grade.HasValue ? $"Grade {s.Grade}" : "no grade";
                var classTag  = !string.IsNullOrWhiteSpace(s.Class) ? $" Class {s.Class}" : "";
                Console.WriteLine($"  [{gradeTag}{classTag}] {s.UserName}  ({s.Email})");
            }
        }
        else
        {
            Console.WriteLine("Unknown command. Type /exit to quit.");
        }
    }
}

// ── Login helper ───────────────────────────────────────────────
// Prompts for credentials, validates the JWT, and checks the expected role.
// Returns the validated ClaimsPrincipal on success, or null on failure.
async Task<ClaimsPrincipal?> RunLoginAsync(AuthService auth, UserRole expectedRole)
{
    Console.WriteLine($"\n── {expectedRole} Login ──");
    Console.Write("Username or email: ");
    var usernameOrEmail = Console.ReadLine()?.Trim() ?? "";

    Console.Write("Password: ");
    var password = ReadPassword();

    var (token, error) = await auth.LoginAsync(usernameOrEmail, password);
    if (error != null)
    {
        Console.WriteLine($"Login failed: {error}");
        return null;
    }

    var principal = auth.ValidateToken(token!);
    if (principal == null)
    {
        Console.WriteLine("Login failed: token could not be verified.");
        return null;
    }

    var roleClaim = principal.FindFirstValue("role");
    if (!Enum.TryParse<UserRole>(roleClaim, out var role) || role != expectedRole)
    {
        Console.WriteLine($"Access denied: this login is not a {expectedRole} account.");
        return null;
    }

    return principal;
}

// ── Helpers ────────────────────────────────────────────────────
static bool IsQdrantDown(Exception ex) =>
    ex.Message.Contains("Connection refused") || ex.Message.Contains("Unavailable");

static string ReadPassword()
{
    var pwd = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace)
        {
            if (pwd.Length > 0) pwd.Remove(pwd.Length - 1, 1);
        }
        else
        {
            pwd.Append(key.KeyChar);
        }
    }
    Console.WriteLine();
    return pwd.ToString();
}
