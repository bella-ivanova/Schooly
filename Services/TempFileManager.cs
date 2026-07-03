using System.Collections.Concurrent;

namespace StudyAssistant.Services;

/// <summary>
/// Tracks temporary HTML visualisation files created during a session
/// so they can all be deleted when the process exits.
/// </summary>
public static class TempFileManager
{
    private static readonly ConcurrentBag<string> _paths = [];

    public static void RegisterTempFile(string path) => _paths.Add(path);

    /// <summary>
    /// Deletes all registered temp files. Safe to call from a ProcessExit handler.
    /// </summary>
    public static void CleanupAll()
    {
        foreach (var path in _paths)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort on exit */ }
        }
        _paths.Clear();
    }
}
