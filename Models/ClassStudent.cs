namespace StudyAssistant.Models;

public class ClassStudent
{
    public int ClassId { get; set; }
    public Class? Class { get; set; }
    public string StudentId { get; set; } = "";
    public ApplicationUser? Student { get; set; }
}
