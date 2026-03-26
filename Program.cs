using System.Diagnostics;
using StudyAssistant.Services;

Console.WriteLine("=== Study Assistant ===\n");

// ── Start Qdrant in the background ─────────────────────────────
var qdrantProcess = await QdrantService.EnsureStartedAsync();

// Stop Qdrant when the app exits (only if we started it)
Console.CancelKeyPress += (_, _) => qdrantProcess?.Kill();
AppDomain.CurrentDomain.ProcessExit += (_, _) => qdrantProcess?.Kill();

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
Process? pix2textProcess = null;
if (mathOcrInput == "y")
{
    mathOcr = new MathOcrService();
    pix2textProcess = await MathOcrService.EnsureStartedAsync();
    Console.CancelKeyPress += (_, _) => pix2textProcess?.Kill();
    AppDomain.CurrentDomain.ProcessExit += (_, _) => pix2textProcess?.Kill();
}

var chat      = new OllamaChatService(model);
var embedding = new EmbeddingService(embedModel);
var qdrant    = new QdrantService();
var rag       = new RAGService(chat, embedding, qdrant, ocr, mathOcr);

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
            await ragService.IngestGradePDFsAsync(grade);
        }
        else if (input.StartsWith("/status"))
        {
            var parts = input.Split(' ', 2);
            if (parts.Length < 2 || !int.TryParse(parts[1].Trim(), out var grade))
            {
                Console.WriteLine("Usage: /status <grade number>");
                continue;
            }
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
        else if (input.StartsWith("/delete "))
        {
            var parts = input.Split(' ', 3);
            if (parts.Length < 3 || !int.TryParse(parts[1].Trim(), out var grade))
            {
                Console.WriteLine("Usage: /delete <grade> <subject/filename.pdf>");
                continue;
            }
            var fileKey = parts[2].Trim();
            await ragService.DeleteGradeFileAsync(grade, fileKey);
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
        try
        {
            await ragService.Ask(input);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[Error] {ex.Message}");
        }
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
