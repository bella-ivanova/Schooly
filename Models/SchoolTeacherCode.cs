using System.ComponentModel.DataAnnotations;

namespace StudyAssistant.Models;

public class SchoolTeacherCode
{
    public int Id { get; set; }

    public int SchoolId { get; set; }

    [MaxLength(24)]
    public string Code { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    public School School { get; set; } = null!;
}
