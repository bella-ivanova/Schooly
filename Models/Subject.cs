using System.ComponentModel.DataAnnotations;

namespace StudyAssistant.Models;

public class Subject
{
    public int Id { get; set; }
    [MaxLength(100)]
    public string Name { get; set; } = "";

    // Null for a personal/global subject not tied to any school — the auto-created
    // fallback bucket for students with no SchoolId. School-scoped Subjects (the
    // normal case) always have this set and are never null.
    public int? SchoolId { get; set; }
    public School? School { get; set; }
    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
}
