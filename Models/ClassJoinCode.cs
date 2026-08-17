using System.ComponentModel.DataAnnotations;

namespace StudyAssistant.Models;

public class ClassJoinCode
{
    public int Id { get; set; }

    public int ClassId { get; set; }

    [MaxLength(24)]
    public string Code { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    public Class Class { get; set; } = null!;
}
