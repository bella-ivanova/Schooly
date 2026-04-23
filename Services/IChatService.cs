namespace StudyAssistant.Services;

public interface IChatService
{
    double Temperature { get; set; }
    void SetSystemPrompt(string prompt);
    Task StreamMessageAsync(string newUserMessage, string? apiMessage = null);
    Task<string> StreamMessageFilteredAsync(string newUserMessage, string? apiMessage = null);
}
