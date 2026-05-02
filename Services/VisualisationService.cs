using System.Diagnostics;

namespace StudyAssistant.Services;

public static class VisualisationService
{
    private static string? _lastHtmlPath;

    // Writes an HTML string to a temp file and opens it in the default browser.
    public static void ShowHtml(string html)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"schooly_vis_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.html"
        );

        File.WriteAllText(path, html);
        TempFileManager.RegisterTempFile(path);
        _lastHtmlPath = path;

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    // Re-opens the most recently generated visualisation.
    public static void ShowLast()
    {
        if (_lastHtmlPath != null && File.Exists(_lastHtmlPath))
            Process.Start(new ProcessStartInfo(_lastHtmlPath) { UseShellExecute = true });
        else
            Console.WriteLine("[No visualisation available yet.]");
    }
}
