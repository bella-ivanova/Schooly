using System.ComponentModel.DataAnnotations;

namespace StudyAssistant.Models;

public class SavedStereoModel
{
    public int Id { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = "";

    [MaxLength(2000)]
    public string Question { get; set; } = "";

    public string SceneJson { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}
