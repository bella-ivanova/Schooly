using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace StudyAssistant.Models;

public class ApplicationUser : IdentityUser
{
    public UserRole Role { get; set; } = UserRole.Student;

    [MaxLength(150)]
    public string FullName { get; set; } = "";

    // Grade within the Bulgarian school system (1–12). Null for teachers/admins.
    public int? Grade { get; set; }

    // Class letter within a grade (e.g. "А", "Б"). Null for teachers/admins.
    [MaxLength(5)]
    public string? ClassLetter { get; set; }

    // FK to the structured Class entity (set by a teacher/admin via AssignStudent).
    public int? ClassId { get; set; }
    public Class? Class { get; set; }

    // School the user belongs to. Set at registration for Teachers/SchoolAdmins;
    // auto-set when a student is assigned to a class.
    public int? SchoolId { get; set; }
    public School? School { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
}
