namespace StudyAssistant.Services;

public interface IChatService
{
    double Temperature { get; set; }
    void SetSystemPrompt(string prompt);
    Task<string> OneShotAsync(string systemPrompt, string userMessage);
    Task StreamMessageAsync(string newUserMessage, string? apiMessage = null, string? systemPromptOverride = null);
    Task<string> StreamMessageFilteredAsync(string newUserMessage, string? apiMessage = null, string? systemPromptOverride = null);
}
