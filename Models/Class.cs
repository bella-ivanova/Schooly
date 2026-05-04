namespace StudyAssistant.Models;

public class Class
{
    public int Id { get; set; }
    public string Name { get; set; } = "";       // e.g. "10А"
    public string School { get; set; } = "";     // e.g. "ППМГ Пловдив"
    public string TeacherId { get; set; } = "";  // FK → ApplicationUser.Id
    public ApplicationUser? Teacher { get; set; }
    public ICollection<ApplicationUser> Students { get; set; } = new List<ApplicationUser>();
}
