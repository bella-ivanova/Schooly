using System.ComponentModel.DataAnnotations;

namespace StudyAssistant.Models;

public class Subject
{
    public int Id { get; set; }
    [MaxLength(100)]
    public string Name { get; set; } = "";
    [MaxLength(200)]
    public string School { get; set; } = "";
    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
}
