namespace StudyAssistant.Models;

public class RateLimitEntry
{
    public string Key { get; set; } = "";
    public string Type { get; set; } = "";
    public int FailureCount { get; set; }
    public DateTime? NextAttemptAfter { get; set; }
    public DateTime? LastRequestAt { get; set; }
}
