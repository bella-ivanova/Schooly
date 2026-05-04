using Microsoft.AspNetCore.Identity;

namespace StudyAssistant.Models;

public enum UserRole
{
    Student,
    Teacher,
    Admin,
}

public class ApplicationUser : IdentityUser
{
    public UserRole Role { get; set; } = UserRole.Student;

    public string FullName { get; set; } = "";

    // Grade within the Bulgarian school system (1–12). Null for teachers/admins.
    public int? Grade { get; set; }

    // Class letter within a grade (e.g. "А", "Б"). Null for teachers/admins.
    public string? ClassLetter { get; set; }

    // FK to the structured Class entity (set by a teacher/admin via AssignStudent).
    public int? ClassId { get; set; }
    public Class? Class { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
