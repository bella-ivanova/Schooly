using System.ComponentModel.DataAnnotations;

namespace StudyAssistant.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    [MaxLength(20)]
    public string Role { get; set; } = "";        // "user" or "assistant"
    [MaxLength(20000)]
    public string Content { get; set; } = "";
    public int? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    [MaxLength(100)]
    public string Topic { get; set; } = "";       // e.g. "Стереометрия"
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
