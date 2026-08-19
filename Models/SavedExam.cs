using System.ComponentModel.DataAnnotations;

namespace StudyAssistant.Models;

public class SavedExam
{
    public int Id { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = "";

    [MaxLength(300)]
    public string Topic { get; set; } = "";

    public string Content { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}
