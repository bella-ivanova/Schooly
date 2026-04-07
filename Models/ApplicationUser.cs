using Microsoft.AspNetCore.Identity;

namespace StudyAssistant.Models;

public enum UserRole
{
    Student,
    Teacher
}

public class ApplicationUser : IdentityUser
{
    public UserRole Role { get; set; } = UserRole.Student;

    // Null for teachers; 1–12 for students.
    public int? Grade { get; set; }

    // Class letter within a grade (e.g. "A", "B", "C"). Null for teachers.
    public string? Class { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
