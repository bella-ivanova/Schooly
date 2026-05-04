namespace StudyAssistant.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    public string Role { get; set; } = "";        // "user" or "assistant"
    public string Content { get; set; } = "";
    public string Subject { get; set; } = "";     // e.g. "Математика"
    public string Topic { get; set; } = "";       // e.g. "Стереометрия"
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
