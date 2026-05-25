namespace StudyAssistant.Models;

public class TeacherSubject
{
    public string TeacherId { get; set; } = "";
    public ApplicationUser? Teacher { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
}
