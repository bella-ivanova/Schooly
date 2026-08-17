namespace StudyAssistant.Models;

public class ChatSession
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }

    // Null until GenerateTitleAsync succeeds after the session's first exchange;
    // also doubles as the "has title-gen run yet" flag.
    public string? Title { get; set; }

    // Folder grouping. Set once from the first exchange's detected subject and
    // never overwritten afterward, so the folder doesn't jump mid-conversation.
    public int? SubjectId { get; set; }
    public Subject? Subject { get; set; }

    // The student's class whose SubjectId tag matches the resolved SubjectId above,
    // if any. Set once alongside SubjectId on the first exchange, same locking rule.
    public int? ClassId { get; set; }
    public Class? Class { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Denormalized recency column, bumped every time a message is saved into this
    // session, so session lists sort without a MAX(Timestamp) aggregate/join.
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
