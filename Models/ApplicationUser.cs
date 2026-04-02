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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
