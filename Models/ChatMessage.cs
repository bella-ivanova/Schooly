namespace StudyAssistant.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    public string Role { get; set; } = "";        // "user" or "assistant"
    public string Content { get; set; } = "";
    public int? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public string Topic { get; set; } = "";       // e.g. "Стереометрия"
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
