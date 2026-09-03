using System.Text.Json;

namespace StudyAssistant.Services;

public class PracticeQuestionService
{
    private readonly IChatService _chat;
    private readonly LanguageDetectionService _languageDetector;
    private readonly ILogger<PracticeQuestionService> _logger;

    public PracticeQuestionService(IChatService chat, LanguageDetectionService languageDetector,
                                   ILogger<PracticeQuestionService> logger)
    {
        _chat = chat;
        _languageDetector = languageDetector;
        _logger = logger;
    }

    public async Task<List<string>> GenerateAsync(string originalQuestion, string aiResponse)
    {
        originalQuestion = InputSanitizer.SanitizeUserInput(originalQuestion, maxLength: 2000);
        aiResponse       = InputSanitizer.SanitizeUserInput(aiResponse, maxLength: 2000);

        var userPrompt =
            $"A student asked: <question>{originalQuestion}</question>\n" +
            $"The tutor answered: <answer>{aiResponse}</answer>\n" +
            "Generate exactly 3 short practice questions on the same topic.\n" +
            "Reply ONLY with a JSON array of 3 strings, no explanation, no markdown:\n" +
            "[\"question 1\", \"question 2\", \"question 3\"]";

        string? response = null;
        try
        {
            var languageName = _languageDetector.DetectLanguageName(originalQuestion);
            var languageInstruction = languageName is not null
                ? $"Generate the questions entirely in {languageName}."
                : "Generate the questions in the same language as the student's original question.";

            response = await _chat.OneShotAsync(
                "You are a school tutor generating practice questions. " + languageInstruction,
                userPrompt);

            var trimmed = response.Trim();
            var start = trimmed.IndexOf('[');
            var end   = trimmed.LastIndexOf(']');
            if (start < 0 || end <= start)
                throw new JsonException("No JSON array found in model response.");
            var json = trimmed[start..(end + 1)];

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to parse practice questions JSON for question {Question} (response length {ResponseLength})",
                originalQuestion, response?.Length ?? 0);
            return new List<string>();
        }
    }
}
