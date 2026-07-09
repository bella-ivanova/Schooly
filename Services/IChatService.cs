namespace StudyAssistant.Services;

public interface IChatService
{
    double Temperature { get; set; }
    void SetSystemPrompt(string prompt);
    Task<string> OneShotAsync(string systemPrompt, string userMessage);
    Task StreamMessageAsync(string newUserMessage, string? apiMessage = null, string? systemPromptOverride = null);
    Task<string> StreamMessageFilteredAsync(string newUserMessage, string? apiMessage = null, string? systemPromptOverride = null);
    IAsyncEnumerable<string> StreamTokensAsync(string newUserMessage, string? apiMessage = null, string? systemPromptOverride = null);

    // Replaces in-memory history with prior turns (oldest first) so the next
    // Stream*/OneShot call has real conversation context. Role is "user" or
    // "assistant"; does not itself call the model.
    void SeedHistory(IEnumerable<(string Role, string Content)> turns);
}
