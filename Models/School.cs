using System.ComponentModel.DataAnnotations;

namespace StudyAssistant.Models;

public class School
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
