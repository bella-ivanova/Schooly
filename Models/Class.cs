using System.ComponentModel.DataAnnotations;

namespace StudyAssistant.Models;

public class Class
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; } = "";
    public int SchoolId { get; set; }
    public School? School { get; set; }
    public string? HomeroomTeacherId { get; set; }
    public ApplicationUser? HomeroomTeacher { get; set; }
    public ICollection<ClassStudent> ClassStudents { get; set; } = new List<ClassStudent>();
    public ICollection<ClassTeacher> ClassTeachers { get; set; } = new List<ClassTeacher>();
}
