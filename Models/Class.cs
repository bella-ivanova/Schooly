using System.ComponentModel.DataAnnotations;

namespace StudyAssistant.Models;

public class Class
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; } = "";
    public int SchoolId { get; set; }
    public School? School { get; set; }

    // Grade level (1–12, Bulgarian school system) this class belongs to. Optional —
    // nullable for classes created before this column existed and not enforced as
    // required at creation, unlike SubjectId below.
    public int? Grade { get; set; }

    // The class's subject tag, set at creation time. Nullable at the DB level so
    // classes created before this column existed stay untagged, but SchoolAdmin/
    // GlobalAdmin class-creation endpoints enforce it as required going forward.
    public int? SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public string? HomeroomTeacherId { get; set; }
    public ApplicationUser? HomeroomTeacher { get; set; }
    public ICollection<ClassStudent> ClassStudents { get; set; } = new List<ClassStudent>();
    public ICollection<ClassTeacher> ClassTeachers { get; set; } = new List<ClassTeacher>();
}
