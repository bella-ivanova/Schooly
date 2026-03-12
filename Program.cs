using StudyAssistant.Services;

Console.WriteLine("=== Study Assistant ===\n");

// ── Model setup ────────────────────────────────────────────────
Console.Write("Enter model name (default: glm5): ");
var modelInput = Console.ReadLine();
var model = string.IsNullOrWhiteSpace(modelInput) ? "glm5" : modelInput;

Console.Write("Enter embedding model (default: nomic-embed-text): ");
var embedInput = Console.ReadLine();
var embedModel = string.IsNullOrWhiteSpace(embedInput) ? "nomic-embed-text" : embedInput;

var chat = new OllamaChatService(model);
var embedding = new EmbeddingService(embedModel);
var rag = new RAGService(chat, embedding);

// ── Mode selection ─────────────────────────────────────────────
Console.WriteLine("\nSelect mode:");
Console.WriteLine("  1. Student");
Console.WriteLine("  2. Admin");
Console.Write("\nChoice: ");
var modeInput = Console.ReadLine()?.Trim();

if (modeInput == "2")
    await RunAdminMode(rag);
else
    await RunStudentMode(chat, rag);

// ══════════════════════════════════════════════════════════════
// ADMIN MODE
// ══════════════════════════════════════════════════════════════
async Task RunAdminMode(RAGService ragService)
{
    const string AdminPassword = "admin123";

    Console.Write("\nEnter admin password: ");
    var pwd = ReadPassword();

    if (pwd != AdminPassword)
    {
        Console.WriteLine("\nIncorrect password. Exiting.");
        return;
    }

    Console.WriteLine("\nAdmin mode. Commands:");
    Console.WriteLine("  /ingest <grade>          — ingest PDFs for a grade");
    Console.WriteLine("  /status                  — show ingested grades");
    Console.WriteLine("  /list <grade>            — list files ingested for a grade");
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
            await ragService.IngestGradePDFsAsync(grade);
        }
        else if (input == "/status")
        {
            var jsonDir = Path.Combine("Database", "DataJson");
            if (!Directory.Exists(jsonDir))
            {
                Console.WriteLine("No grades ingested yet.");
                continue;
            }
            var files = Directory.GetFiles(jsonDir, "Grade*.json");
            if (files.Length == 0)
            {
                Console.WriteLine("No grades ingested yet.");
                continue;
            }
            foreach (var f in files.OrderBy(x => x))
            {
                var info = new FileInfo(f);
                Console.WriteLine($"  {Path.GetFileNameWithoutExtension(f)}  ({info.Length / 1024} KB)");
            }
        }
        else if (input.StartsWith("/list "))
        {
            var parts = input.Split(' ', 2);
            if (parts.Length < 2 || !int.TryParse(parts[1].Trim(), out var grade))
            {
                Console.WriteLine("Usage: /list <grade number>");
                continue;
            }
            ragService.SetGrade(grade);
            var ingestedFiles = ragService.GetIngestedFiles(grade);
            if (ingestedFiles == null || ingestedFiles.Count == 0)
            {
                Console.WriteLine($"No files ingested for Grade {grade}.");
                continue;
            }
            Console.WriteLine($"Grade {grade} ingested files:");
            foreach (var f in ingestedFiles.OrderBy(x => x))
                Console.WriteLine($"  {f}");
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
            ragService.SetGrade(grade);
            ragService.DeleteGradeFile(grade, fileKey);
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
    // Pick grade
    Console.Write("\nEnter your grade (1-12), or press Enter to skip: ");
    var gradeInput = Console.ReadLine()?.Trim();
    if (!string.IsNullOrWhiteSpace(gradeInput) && int.TryParse(gradeInput, out var grade) && grade >= 1 && grade <= 12)
        ragService.SetGrade(grade);

    chatService.SetSystemPrompt(
        "You are a helpful school tutor. Explain concepts clearly and step by step.");

    Console.WriteLine("\nCommands: /clear  /load <pdf path>  /exit\n");

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

        if (string.IsNullOrWhiteSpace(input)) continue;

        Console.Write("\nAI: ");
        await ragService.Ask(input);
        Console.WriteLine();
    }
}

// ── Helpers ────────────────────────────────────────────────────
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
