using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudyAssistant.Data;
using StudyAssistant.Models;
using StudyAssistant.Services;

Console.WriteLine("=== Schooly ===\n");

// ── Dependency injection ────────────────────────────────────────────────
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

// Delete temp visualisation HTML files on exit.
AppDomain.CurrentDomain.ProcessExit += (_, _) => TempFileManager.CleanupAll();

// ── Infrastructure (hardcoded defaults) ────────────────────────────────
var model      = "glm-5:cloud";
var embedModel = "nomic-embed-text";
var ocrModel   = "minicpm-v";

var ocr     = new OCRService(ocrModel);
Console.WriteLine($"OCR enabled with model: {ocrModel}");
var mathOcr = new MathOcrService();

var zhipuKey = Environment.GetEnvironmentVariable("ZHIPUAI_API_KEY") ?? "";
IChatService chat = model.StartsWith("glm-", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(zhipuKey)
    ? new ZhipuAIChatService(model, zhipuKey)
    : new OllamaChatService(model);

var embedding = new EmbeddingService(embedModel);
var qdrant    = new QdrantService();
var rag       = new RAGService(chat, embedding, qdrant, ocr, mathOcr);

// ── Auth + log services ─────────────────────────────────────────────────
var authService      = services.GetRequiredService<AuthService>();
var repo             = services.GetRequiredService<IUserRepository>();
var db               = services.GetRequiredService<AppDbContext>();
var classChat        = new OllamaChatService("llama3.2");
var chatLogService   = new ChatLogService(db, classChat);
var adminUserService = new AdminUserService(db, repo);

// ── Login / register menu ───────────────────────────────────────────────
ApplicationUser? currentUser = null;

while (currentUser == null)
{
    Console.WriteLine("\n=== Schooly ===");
    Console.WriteLine("[1] Влез в профила си");
    Console.WriteLine("[2] Създай профил");
    Console.WriteLine("[3] Изход");
    Console.Write("\nИзбор: ");
    var choice = Console.ReadLine()?.Trim();

    if (choice == "3") break;

    if (choice == "1")
    {
        Console.Write("Потребителско име или имейл: ");
        var usernameOrEmail = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Парола: ");
        var password = ReadPassword();

        var (_, loginError) = await authService.LoginAsync(usernameOrEmail, password);
        if (loginError != null)
        {
            Console.WriteLine("Грешни данни. Опитай пак.");
            continue;
        }

        currentUser = usernameOrEmail.Contains('@')
            ? await repo.GetByEmailAsync(usernameOrEmail)
            : await repo.GetByUsernameAsync(usernameOrEmail);
    }
    else if (choice == "2")
    {
        Console.Write("Пълно име: ");
        var fullName = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Потребителско име: ");
        var username = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Имейл: ");
        var email = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Парола: ");
        var password = ReadPassword();

        Console.WriteLine("[1] Ученик   [2] Учител");
        Console.Write("Роля: ");
        var roleChoice = Console.ReadLine()?.Trim();
        var role = roleChoice == "2" ? UserRole.Teacher : UserRole.Student;

        int? grade = null;
        if (role == UserRole.Student)
        {
            Console.Write("Клас (1-12): ");
            if (int.TryParse(Console.ReadLine()?.Trim(), out var g) && g >= 1 && g <= 12)
                grade = g;
        }

        var (regUser, regErrors) = await authService.RegisterAsync(username, email, password, role, fullName, grade);
        if (regUser == null)
        {
            Console.WriteLine($"Грешка: {string.Join(", ", regErrors)}");
            continue;
        }

        // Auto-login after registration
        await authService.LoginAsync(username, password);
        currentUser = regUser;
    }
}

if (currentUser == null) return;

// ── Route by role ───────────────────────────────────────────────────────
if (currentUser.Role == UserRole.Admin || currentUser.UserName == "admin")
    await RunAdminMode(rag, adminUserService);
else if (currentUser.Role == UserRole.Teacher)
    Console.WriteLine("\nУчителското табло идва скоро!");
else
    await RunStudentMode(currentUser, chat, rag, chatLogService);

// ══════════════════════════════════════════════════════════════
// ADMIN MODE
// ══════════════════════════════════════════════════════════════
async Task RunAdminMode(RAGService ragService, AdminUserService adminSvc)
{
    Console.WriteLine("\nAdmin режим. Команди:");
    Console.WriteLine("  /ingest <клас>           — зареди PDF за клас");
    Console.WriteLine("  /status <клас>           — файлове за клас");
    Console.WriteLine("  /delete <клас> <файл>    — изтрий файл от клас");
    Console.WriteLine("  /addclass                — добави клас");
    Console.WriteLine("  /listclasses             — покажи класовете");
    Console.WriteLine("  /assignstudent           — добави ученик в клас");
    Console.WriteLine("  /listusers               — покажи потребителите");
    Console.WriteLine("  /deleteclass             — изтрий клас");
    Console.WriteLine("  /exit                    — изход\n");

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
                Console.WriteLine("Употреба: /ingest <клас>");
                continue;
            }
            try { await ragService.IngestGradePDFsAsync(grade); }
            catch (Exception ex) when (IsQdrantDown(ex))
            { Console.WriteLine("[Qdrant не работи. Стартирай с: docker-compose up -d qdrant]"); }
        }
        else if (input.StartsWith("/status"))
        {
            var parts = input.Split(' ', 2);
            if (parts.Length < 2 || !int.TryParse(parts[1].Trim(), out var grade))
            {
                Console.WriteLine("Употреба: /status <клас>");
                continue;
            }
            try
            {
                var files = await ragService.GetIngestedFilesAsync(grade);
                if (files.Count == 0) { Console.WriteLine($"Няма файлове за клас {grade}."); continue; }
                Console.WriteLine($"Клас {grade}:");
                foreach (var f in files.OrderBy(x => x)) Console.WriteLine($"  {f}");
            }
            catch (Exception ex) when (IsQdrantDown(ex))
            { Console.WriteLine("[Qdrant не работи. Стартирай с: docker-compose up -d qdrant]"); }
        }
        else if (input.StartsWith("/delete "))
        {
            var parts = input.Split(' ', 3);
            if (parts.Length < 3 || !int.TryParse(parts[1].Trim(), out var grade))
            {
                Console.WriteLine("Употреба: /delete <клас> <файл>");
                continue;
            }
            try { await ragService.DeleteGradeFileAsync(grade, parts[2].Trim()); }
            catch (Exception ex) when (IsQdrantDown(ex))
            { Console.WriteLine("[Qdrant не работи. Стартирай с: docker-compose up -d qdrant]"); }
        }
        else if (input == "/addclass")        { await adminSvc.AddClassAsync(); }
        else if (input == "/listclasses")     { await adminSvc.ListClassesAsync(); }
        else if (input == "/assignstudent")   { await adminSvc.AssignStudentAsync(); }
        else if (input == "/listusers")       { await adminSvc.ListUsersAsync(); }
        else if (input == "/deleteclass")     { await adminSvc.DeleteClassAsync(); }
        else
        {
            Console.WriteLine("Непозната команда. Въведи /exit за изход.");
        }
    }
}

// ══════════════════════════════════════════════════════════════
// STUDENT MODE
// ══════════════════════════════════════════════════════════════
async Task RunStudentMode(ApplicationUser user, IChatService chatService, RAGService ragService, ChatLogService logService)
{
    if (user.Grade.HasValue)
        ragService.SetGrade(user.Grade.Value);

    var history = await logService.GetHistoryAsync(user.Id, limit: 1);
    Console.WriteLine(history.Count > 0
        ? $"\nДобре дошъл обратно, {user.FullName}!"
        : $"\nДобре дошъл в Schooly, {user.FullName}!");

    chatService.SetSystemPrompt(
        "You are a helpful school tutor. Explain concepts clearly and step by step.");

    Console.WriteLine("\nКоманди: /clear  /load <pdf>  /visualise  /exit\n");

    while (true)
    {
        Console.Write("\nТи: ");
        var input = Console.ReadLine()?.Trim() ?? "";

        if (input == "/exit") break;

        if (input == "/clear")
        {
            ragService.ClearTemporaryChunks();
            chatService.SetSystemPrompt(
                "You are a helpful school tutor. Explain concepts clearly and step by step.");
            Console.WriteLine("Разговорът е изчистен.");
            continue;
        }

        if (input.StartsWith("/load "))
        {
            var path = input[6..].Trim();
            if (!File.Exists(path)) { Console.WriteLine($"Файлът не е намерен: {path}"); continue; }
            await ragService.AddTemporaryPDFAsync(path);
            continue;
        }

        if (input == "/visualise")
        {
            VisualisationService.ShowLast();
            continue;
        }

        if (string.IsNullOrWhiteSpace(input)) continue;

        Console.Write("\nAI: ");
        string? aiResponse = null;
        try
        {
            if (StereometryDetector.IsStereometryQuestion(input))
            {
                aiResponse = await ragService.Ask(
                    input,
                    capture: true,
                    instructionSuffix: StereometryService.Instruction
                );

                Console.WriteLine();

                if (aiResponse != null)
                {
                    var sceneJson = StereometryService.ExtractSceneJson(aiResponse);
                    if (sceneJson != null)
                    {
                        Console.WriteLine("[Отваря 3D визуализация в браузъра…]");
                        VisualisationService.ShowHtml(StereometryHtmlBuilder.Build(sceneJson));
                    }
                }
            }
            else
            {
                aiResponse = await ragService.Ask(input, capture: true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[Грешка] {ex.Message}");
        }

        Console.WriteLine();

        // Fire-and-forget: detect subject/topic and persist both messages
        if (aiResponse != null)
        {
            var capturedInput    = input;
            var capturedResponse = aiResponse;
            var capturedUserId   = user.Id;
            _ = Task.Run(async () =>
            {
                var (subject, topic) = await logService.DetectSubjectTopicAsync(capturedInput);
                await logService.SaveMessageAsync(capturedUserId, "user",      capturedInput,    subject, topic);
                await logService.SaveMessageAsync(capturedUserId, "assistant", capturedResponse, subject, topic);
            });
        }
    }
}

// ── Helpers ─────────────────────────────────────────────────────────────
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
        { if (pwd.Length > 0) pwd.Remove(pwd.Length - 1, 1); }
        else
            pwd.Append(key.KeyChar);
    }
    Console.WriteLine();
    return pwd.ToString();
}
