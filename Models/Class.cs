using System.ComponentModel.DataAnnotations;

namespace StudyAssistant.Models;

public class Class
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; } = "";
    [MaxLength(200)]
    public string School { get; set; } = "";
    public string? HomeroomTeacherId { get; set; }
    public ApplicationUser? HomeroomTeacher { get; set; }
    public ICollection<ApplicationUser> Students { get; set; } = new List<ApplicationUser>();
    public ICollection<ClassTeacher> ClassTeachers { get; set; } = new List<ClassTeacher>();
}
