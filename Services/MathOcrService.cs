using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;

namespace StudyAssistant.Services;

public class MathOcrService
{
    private readonly HttpClient _httpClient;
    private const string ServerUrl = "http://127.0.0.1:8503/pix2text";

    public MathOcrService()
    {
        // Pix2Text can be slow on large pages — no timeout
        _httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    // Sends a PNG page image to the Pix2Text server and gets back text with
    // LaTeX math mixed in (e.g. "The formula is $\frac{a}{b}$ which means...").
    public async Task<string> ReadPageAsync(byte[] imageBytes)
    {
        using var form = new MultipartFormDataContent();

        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        // "image" and "file_type" match the field names in pix2text's serve.py
        form.Add(imageContent, "image", "page.png");
        form.Add(new StringContent("text_formula"), "file_type");

        var response = await _httpClient.PostAsync(ServerUrl, form);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"Pix2Text server error {(int)response.StatusCode}: {body}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("results").GetString() ?? "";
    }

    // Starts the pix2text_server.py script as a background process if the
    // server isn't already running on port 8503.
    // Returns the Process so the caller can stop it on exit, or null if it
    // was already running.
    public static async Task<Process?> EnsureStartedAsync(int port = 8503)
    {
        if (await IsPortOpenAsync(port))
        {
            Console.WriteLine("Pix2Text server already running.");
            return null;
        }

        Console.Write("Starting Pix2Text server (first run downloads models — may take a minute)...");

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "pix2text_server.py");

        // Also look next to the .csproj (useful during dotnet run)
        if (!File.Exists(scriptPath))
            scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "pix2text_server.py");

        if (!File.Exists(scriptPath))
        {
            Console.WriteLine(" script not found.");
            Console.WriteLine($"Expected pix2text_server.py at: {scriptPath}");
            return null;
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = "python3",
                Arguments              = $"\"{scriptPath}\"",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            }
        };

        try { process.Start(); }
        catch
        {
            Console.WriteLine(" python3 not found.");
            return null;
        }

        // Poll until the server is ready (up to 90 s — model loading is slow)
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            if (await IsPortOpenAsync(port))
            {
                Console.WriteLine(" ready.");
                return process;
            }
            await Task.Delay(500);
        }

        Console.WriteLine(" timed out.");
        process.Kill();
        return null;
    }

    private static async Task<bool> IsPortOpenAsync(int port)
    {
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port);
            return true;
        }
        catch { return false; }
    }
}
