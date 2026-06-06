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
    .AddJsonFile("appsettings.Local.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection()
    .AddDbContext<AppDbContext>(o =>
        o.UseNpgsql(config.GetConnectionString("DefaultConnection")))
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
var chatLogService   = new ChatLogService(db, chat);
var adminUserService        = new AdminUserService(db, repo);
var practiceService         = new PracticeQuestionService(chat);
var examService             = new ExamService(rag, chat);
var teacherDashboardService = new TeacherDashboardService(db);
var rateLimiter             = new RateLimiter();

// ── Login / register menu ───────────────────────────────────────────────
ApplicationUser? currentUser = null;

while (currentUser == null)
{
    Console.WriteLine("\n=== Schooly ===");
    Console.WriteLine("[1] Влез в профила си");
    Console.WriteLine("[2] Създай профил");
    Console.WriteLine("[3] Нулирай парола");
    Console.WriteLine("[4] Изход");
    Console.Write("\nИзбор: ");
    var choice = Console.ReadLine()?.Trim();

    if (choice == "4") break;

    if (choice == "1")
    {
        Console.Write("Потребителско име или имейл: ");
        var usernameOrEmail = Console.ReadLine()?.Trim() ?? "";

        if (rateLimiter.IsLoginLocked(usernameOrEmail, out var loginWait))
        {
            Console.WriteLine($"Твърде много грешни опити. Опитай след {RateLimiter.FormatRemaining(loginWait)}.");
            continue;
        }

        Console.Write("Парола: ");
        var password = ReadPassword();

        var loginError = await authService.LoginAsync(usernameOrEmail, password);
        if (loginError != null)
        {
            rateLimiter.RecordLoginFailure(usernameOrEmail);
            var left = rateLimiter.RemainingAttemptsBeforeDelay(usernameOrEmail);
            Console.WriteLine(left > 0
                ? $"Грешни данни. Остават {left} опита без забавяне."
                : $"Грешни данни. Следващият опит ще изисква изчакване.");
            continue;
        }

        rateLimiter.RecordLoginSuccess(usernameOrEmail);
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

        if (rateLimiter.IsRegistrationThrottled(email, out var regWait))
        {
            Console.WriteLine($"Регистрацията е опитана наскоро. Опитай след {RateLimiter.FormatRemaining(regWait)}.");
            continue;
        }
        Console.Write("Парола: ");
        var password = ReadPassword();

        Console.WriteLine("[1] Ученик   [2] Учител");
        Console.Write("Роля: ");
        var roleChoice = Console.ReadLine()?.Trim();
        UserRole role;
        if (roleChoice == "2")
        {
            Console.Write("Код за регистрация на учител: ");
            var enteredCode = ReadPassword();
            var expectedCode = config["TeacherRegistrationCode"] ?? "";
            if (string.IsNullOrEmpty(expectedCode) || enteredCode != expectedCode)
            {
                Console.WriteLine("Невалиден код. Регистрацията е отказана.");
                continue;
            }
            role = UserRole.Teacher;
        }
        else
        {
            role = UserRole.Student;
        }

        int? grade = null;
        if (role == UserRole.Student)
        {
            Console.Write("Клас (1-12): ");
            if (int.TryParse(Console.ReadLine()?.Trim(), out var g) && g >= 1 && g <= 12)
                grade = g;
        }

        string? school = null;
        if (role == UserRole.Teacher)
        {
            Console.Write("Училище: ");
            school = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(school)) school = null;
        }

        var (regUser, regErrors) = await authService.RegisterAsync(username, email, password, role, fullName, grade, school: school);
        if (regUser == null)
        {
            Console.WriteLine($"Грешка: {string.Join(", ", regErrors)}");
            continue;
        }

        rateLimiter.RecordRegistration(email);
        // Auto-login after registration
        await authService.LoginAsync(username, password);
        currentUser = regUser;
    }
    else if (choice == "3")
    {
        Console.Write("Имейл: ");
        var resetEmail = Console.ReadLine()?.Trim() ?? "";

        if (rateLimiter.IsPasswordResetThrottled(resetEmail, out var resetWait))
        {
            Console.WriteLine($"Заявката е изпратена наскоро. Опитай след {RateLimiter.FormatRemaining(resetWait)}.");
            continue;
        }

        var (token, tokenError) = await authService.RequestPasswordResetAsync(resetEmail);

        rateLimiter.RecordPasswordResetRequest(resetEmail);

        Console.WriteLine("Ако акаунт с този имейл съществува, токен е генериран.");
        if (token == null) continue;

        Console.WriteLine($"\nТокен за нулиране: {token}");
        Console.Write("Въведи токена: ");
        var inputToken = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Нова парола: ");
        var newPassword = ReadPassword();

        var (success, resetErrors) = await authService.ResetPasswordAsync(resetEmail, inputToken, newPassword);
        if (!success)
        {
            Console.WriteLine($"Грешка: {string.Join(", ", resetErrors)}");
            continue;
        }

        Console.WriteLine("Паролата е нулирана успешно. Влез с новата си парола.");
    }
}

if (currentUser == null) return;

// ── Route by role ───────────────────────────────────────────────────────
if (currentUser.Role == UserRole.Admin)
    await RunAdminMode(chat, rag, adminUserService);
else if (currentUser.Role == UserRole.SchoolAdmin)
    await RunSchoolAdminMode(currentUser, chat, rag, db, repo);
else if (currentUser.Role == UserRole.Teacher)
    await RunTeacherMode(currentUser, chat, rag, db, teacherDashboardService);
else
    await RunStudentMode(currentUser, chat, rag, chatLogService, practiceService, examService);

// ══════════════════════════════════════════════════════════════
// ADMIN MODE
// ══════════════════════════════════════════════════════════════
async Task RunAdminMode(IChatService chatService, RAGService ragService, AdminUserService adminSvc)
{
    chatService.SetSystemPrompt("You are a helpful AI assistant for school administrators. Answer questions clearly and concisely.");
    Console.WriteLine("\nAdmin режим. Команди:");
    Console.WriteLine("  /ingest <клас>           — зареди PDF за клас");
    Console.WriteLine("  /status <клас>           — файлове за клас");
    Console.WriteLine("  /delete <клас> <файл>    — изтрий файл от клас");
    Console.WriteLine("  /addclass                — добави клас");
    Console.WriteLine("  /listclasses             — покажи класовете");
    Console.WriteLine("  /assignstudent           — добави ученик в клас");
    Console.WriteLine("  /assignteachertoclass    — назначи учител на клас по предмет");
    Console.WriteLine("  /listusers               — покажи потребителите");
    Console.WriteLine("  /deleteclass             — изтрий клас");
    Console.WriteLine("  /createsubject           — добави предмет");
    Console.WriteLine("  /deletesubject           — изтрий предмет");
    Console.WriteLine("  /makeschooladmin         — направи потребител училищен администратор");
    Console.WriteLine("  /exit                    — изход");
    Console.WriteLine("  <въпрос>                 — разговор с AI\n");

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
        else if (input.StartsWith("/status "))
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
        else if (input == "/assignstudent")        { await adminSvc.AssignStudentAsync(); }
        else if (input == "/assignteachertoclass") { await adminSvc.AssignTeacherToClassAsync(); }
        else if (input == "/listusers")            { await adminSvc.ListUsersAsync(); }
        else if (input == "/deleteclass")          { await adminSvc.DeleteClassAsync(); }
        else if (input == "/createsubject")        { await adminSvc.CreateSubjectAsync(); }
        else if (input == "/deletesubject")        { await adminSvc.DeleteSubjectAsync(); }
        else if (input == "/makeschooladmin")      { await adminSvc.MakeSchoolAdminAsync(); }
        else if (input.StartsWith('/'))
        {
            Console.WriteLine("Непозната команда. Въведи /exit за изход.");
        }
        else
        {
            Console.Write("\nAI: ");
            try
            {
                if (StereometryDetector.IsStereometryQuestion(input))
                {
                    var aiResp = await ragService.Ask(input, capture: true, instructionSuffix: StereometryService.Instruction);
                    Console.WriteLine();
                    if (aiResp != null)
                    {
                        var sceneJson = StereometryService.ExtractSceneJson(aiResp);
                        if (sceneJson != null)
                        {
                            Console.WriteLine("[Отваря 3D визуализация в браузъра…]");
                            VisualisationService.ShowHtml(StereometryHtmlBuilder.Build(sceneJson));
                        }
                    }
                }
                else
                {
                    await ragService.Ask(input, capture: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Грешка] {ex.Message}");
            }
            Console.WriteLine();
        }
    }
}

// ══════════════════════════════════════════════════════════════
// STUDENT MODE
// ══════════════════════════════════════════════════════════════
async Task RunStudentMode(ApplicationUser user, IChatService chatService, RAGService ragService,
    ChatLogService logService, PracticeQuestionService practiceService, ExamService examService)
{
    if (user.Grade.HasValue)
        ragService.SetGrade(user.Grade.Value);

    var history = await logService.GetHistoryAsync(user.Id, limit: 1);
    Console.WriteLine(history.Count > 0
        ? $"\nДобре дошъл обратно, {user.FullName}!"
        : $"\nДобре дошъл в Schooly, {user.FullName}!");

    var weakSpots = await logService.GetWeakSpotsAsync(user.Id, days: 7, minCount: 2);
    if (weakSpots.Count > 0)
    {
        Console.WriteLine("\n🔁 Тази седмица си питал най-много за:");
        foreach (var (topic, subject, count) in weakSpots.Take(3))
            Console.WriteLine($"  • {topic} ({subject}) — {count} пъти");
        Console.WriteLine();
    }

    chatService.SetSystemPrompt(
        "You are a helpful school tutor. Explain concepts clearly and step by step.");

    Console.WriteLine("\nКоманди: /clear  /load <pdf>  /visualise  /exam  /exit\n");

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

        if (input == "/exam")
        {
            Console.Write("Въведи тема за изпита: ");
            var topic = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(topic)) continue;
            Console.WriteLine("\nГенерирам изпит…");
            var exam = await examService.GenerateExamAsync(topic, user.Grade ?? 10);
            Console.WriteLine("\n" + exam);
            Console.WriteLine();
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
            var capturedSchool   = user.School;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope  = services.CreateScope();
                    var scopedLog    = new ChatLogService(
                        scope.ServiceProvider.GetRequiredService<AppDbContext>(), chatService);
                    var (subject, topic) = await scopedLog.DetectSubjectTopicAsync(capturedInput);
                    await scopedLog.SaveMessageAsync(capturedUserId, "user",      capturedInput,    subject, topic, capturedSchool);
                    await scopedLog.SaveMessageAsync(capturedUserId, "assistant", capturedResponse, subject, topic, capturedSchool);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n[Грешка при запис на съобщението] {ex.Message}");
                }
            });

            var questions = await practiceService.GenerateAsync(input, aiResponse);
            if (questions.Count > 0)
            {
                Console.WriteLine("\n📝 Практически въпроси:");
                for (int i = 0; i < questions.Count; i++)
                    Console.WriteLine($"  {i + 1}. {questions[i]}");
                Console.WriteLine();
            }
        }
    }
}

// ══════════════════════════════════════════════════════════════
// SCHOOL ADMIN MODE
// ══════════════════════════════════════════════════════════════
async Task RunSchoolAdminMode(ApplicationUser user, IChatService chatService, RAGService ragService, AppDbContext database, IUserRepository repository)
{
    if (string.IsNullOrWhiteSpace(user.School))
    {
        Console.WriteLine("\n[Грешка] Акаунтът няма зададено училище. Свържи се с администратора.");
        return;
    }

    var svc = new SchoolAdminService(database, repository, user.School);
    chatService.SetSystemPrompt("You are a helpful AI assistant for school administrators. Answer questions clearly and concisely.");

    Console.WriteLine($"\nУчилищен администратор — {user.School}. Команди:");
    Console.WriteLine("  /addclass              — добави клас");
    Console.WriteLine("  /listclasses           — покажи класовете");
    Console.WriteLine("  /assignteacher         — назначи класен ръководител");
    Console.WriteLine("  /assignstudent         — добави ученик в клас");
    Console.WriteLine("  /removestudent         — премахни ученик от клас");
    Console.WriteLine("  /assignteachertoclass  — назначи учител на клас по предмет");
    Console.WriteLine("  /listsubjects          — покажи предметите с техните ID-та");
    Console.WriteLine("  /assignsubject         — назначи предмет на учител");
    Console.WriteLine("  /removesubject         — премахни предмет от учител");
    Console.WriteLine("  /listusers             — покажи потребителите в училището");
    Console.WriteLine("  /exit                  — изход");
    Console.WriteLine("  <въпрос>               — разговор с AI\n");

    while (true)
    {
        Console.Write($"SchoolAdmin [{user.School}]> ");
        var input = Console.ReadLine()?.Trim() ?? "";

        if (input == "/exit")          break;
        else if (input == "/addclass")       await svc.AddClassAsync();
        else if (input == "/listclasses")    await svc.ListClassesAsync();
        else if (input == "/assignteacher")  await svc.AssignTeacherAsync();
        else if (input == "/assignstudent")  await svc.AssignStudentAsync();
        else if (input == "/removestudent")        await svc.RemoveStudentAsync();
        else if (input == "/assignteachertoclass") await svc.AssignTeacherToClassAsync();
        else if (input == "/listsubjects")         await svc.ListSubjectsAsync();
        else if (input == "/assignsubject")        await svc.AssignSubjectToTeacherAsync();
        else if (input == "/removesubject")        await svc.RemoveSubjectFromTeacherAsync();
        else if (input == "/listusers")            await svc.ListUsersAsync();
        else if (input.StartsWith('/'))
            Console.WriteLine("Непозната команда. Въведи /exit за изход.");
        else
        {
            Console.Write("\nAI: ");
            try
            {
                if (StereometryDetector.IsStereometryQuestion(input))
                {
                    var aiResp = await ragService.Ask(input, capture: true, instructionSuffix: StereometryService.Instruction);
                    Console.WriteLine();
                    if (aiResp != null)
                    {
                        var sceneJson = StereometryService.ExtractSceneJson(aiResp);
                        if (sceneJson != null)
                        {
                            Console.WriteLine("[Отваря 3D визуализация в браузъра…]");
                            VisualisationService.ShowHtml(StereometryHtmlBuilder.Build(sceneJson));
                        }
                    }
                }
                else
                {
                    await ragService.Ask(input, capture: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Грешка] {ex.Message}");
            }
            Console.WriteLine();
        }
    }
}

// ══════════════════════════════════════════════════════════════
// TEACHER MODE
// ══════════════════════════════════════════════════════════════
async Task RunTeacherMode(ApplicationUser user, IChatService chatService, RAGService ragService, AppDbContext database, TeacherDashboardService dashboardService)
{
    chatService.SetSystemPrompt("You are a helpful AI assistant for school teachers. Answer questions clearly and concisely.");
    Console.WriteLine($"\nДобре дошъл, {user.FullName}! ({user.School})");
    Console.WriteLine("Команди:");
    Console.WriteLine("  /classes         — покажи моите класове и предмети");
    Console.WriteLine("  /struggles       — топ теми, с които учениците имат затруднения");
    Console.WriteLine("  /active          — най-активни ученици по клас");
    Console.WriteLine("  /exit            — изход");
    Console.WriteLine("  <въпрос>         — разговор с AI\n");

    while (true)
    {
        Console.Write("Учител> ");
        var input = Console.ReadLine()?.Trim() ?? "";
        if (input == "/exit") break;

        else if (input == "/classes")
        {
            var myClasses = await dashboardService.GetMyClassesAsync(user.Id);
            if (myClasses.Count == 0)
            {
                Console.WriteLine("Нямаш назначени класове.");
                continue;
            }

            foreach (var (cls, subjects, studentCount) in myClasses)
            {
                Console.WriteLine($"\n  Клас {cls.Name} ({cls.School}) — {studentCount} ученика");
                foreach (var s in subjects)
                    Console.WriteLine($"    • {s.Name}");
            }
        }

        else if (input == "/struggles")
        {
            var myClasses = await dashboardService.GetMyClassesAsync(user.Id);
            if (myClasses.Count == 0)
            {
                Console.WriteLine("Нямаш назначени класове.");
                continue;
            }

            Console.WriteLine("\nТвоите класове:");
            for (int i = 0; i < myClasses.Count; i++)
                Console.WriteLine($"  [{i + 1}] {myClasses[i].Class.Name}");

            Console.Write("Избери клас (номер): ");
            var pick = Console.ReadLine()?.Trim() ?? "";
            if (!int.TryParse(pick, out var idx) || idx < 1 || idx > myClasses.Count)
            {
                Console.WriteLine("Невалиден избор.");
                continue;
            }

            var selected  = myClasses[idx - 1];
            var struggles = await dashboardService.GetStrugglesByClassAsync(user.Id, selected.Class.Id);

            if (struggles.Count == 0 || struggles.All(s => s.TopTopics.Count == 0))
            {
                Console.WriteLine($"Няма достатъчно данни за клас {selected.Class.Name}.");
                continue;
            }

            foreach (var (cls, subject, topics) in struggles)
            {
                if (topics.Count == 0) continue;
                Console.WriteLine($"\n  Клас {cls.Name} — {subject.Name}:");
                for (int i = 0; i < topics.Count; i++)
                    Console.WriteLine($"    {i + 1}. {topics[i].Topic} ({topics[i].Count} въпроса)");
            }
        }

        else if (input == "/active")
        {
            var activeData = await dashboardService.GetActiveStudentsByClassAsync(user.Id);
            if (activeData.Count == 0)
            {
                Console.WriteLine("Нямаш назначени класове.");
                continue;
            }

            foreach (var (cls, students) in activeData)
            {
                Console.WriteLine($"\n  Клас {cls.Name}:");
                if (students.Count == 0)
                {
                    Console.WriteLine("    Няма активност.");
                    continue;
                }

                for (int i = 0; i < students.Count; i++)
                    Console.WriteLine($"    {i + 1}. {students[i].Student.FullName} ({students[i].Student.UserName}) — {students[i].QuestionCount} въпроса");
            }
        }

        else if (input.StartsWith('/'))
            Console.WriteLine("Непозната команда.");
        else
        {
            Console.Write("\nAI: ");
            try
            {
                if (StereometryDetector.IsStereometryQuestion(input))
                {
                    var aiResp = await ragService.Ask(input, capture: true, instructionSuffix: StereometryService.Instruction);
                    Console.WriteLine();
                    if (aiResp != null)
                    {
                        var sceneJson = StereometryService.ExtractSceneJson(aiResp);
                        if (sceneJson != null)
                        {
                            Console.WriteLine("[Отваря 3D визуализация в браузъра…]");
                            VisualisationService.ShowHtml(StereometryHtmlBuilder.Build(sceneJson));
                        }
                    }
                }
                else
                {
                    await ragService.Ask(input, capture: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Грешка] {ex.Message}");
            }
            Console.WriteLine();
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
