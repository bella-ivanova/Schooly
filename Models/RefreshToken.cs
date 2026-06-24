using System.ComponentModel.DataAnnotations;

namespace StudyAssistant.Models;

public class RefreshToken
{
    public int Id { get; set; }

    [MaxLength(128)]
    public string Token { get; set; } = "";

    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; }
}
