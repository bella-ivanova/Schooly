namespace StudyAssistant.Services;

public static class InputSanitizer
{
    // Strips null bytes and C0 control characters (except tab), enforces max length.
    // Preserves all printable characters including punctuation and Unicode text.
    public static string SanitizeUserInput(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var sb = new System.Text.StringBuilder(Math.Min(input.Length, maxLength));
        foreach (var c in input)
        {
            if (sb.Length == maxLength) break;
            if (c == '\0') continue;
            if (c < 0x20 && c != '\t' && c != '\n' && c != '\r') continue;
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }
}
