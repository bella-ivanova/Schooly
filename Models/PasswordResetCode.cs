using System.ComponentModel.DataAnnotations;

namespace StudyAssistant.Models;

public class PasswordResetCode
{
    public int Id { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = "";

    [MaxLength(6)]
    public string Code { get; set; } = "";

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsUsed { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
