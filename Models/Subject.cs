using System.ComponentModel.DataAnnotations;

namespace StudyAssistant.Models;

public class Subject
{
    public int Id { get; set; }
    [MaxLength(100)]
    public string Name { get; set; } = "";
    public int SchoolId { get; set; }
    public School? School { get; set; }
    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
}
