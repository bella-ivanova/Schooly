using System.Text.Json;

namespace StudyAssistant.Services;

public class PracticeQuestionService
{
    private readonly IChatService _chat;

    public PracticeQuestionService(IChatService chat)
    {
        _chat = chat;
    }

    public async Task<List<string>> GenerateAsync(string originalQuestion, string aiResponse)
    {
        var userPrompt =
            $"A student asked: <question>{originalQuestion}</question>\n" +
            $"The tutor answered: <answer>{aiResponse}</answer>\n" +
            "Generate exactly 3 short practice questions on the same topic.\n" +
            "Reply ONLY with a JSON array of 3 strings, no explanation, no markdown:\n" +
            "[\"question 1\", \"question 2\", \"question 3\"]";

        try
        {
            var response = await _chat.OneShotAsync(
                "You are a school tutor generating practice questions.",
                userPrompt);

            var json = response.Trim();
            if (json.StartsWith("```"))
            {
                var nl   = json.IndexOf('\n');
                var last = json.LastIndexOf("```");
                if (nl >= 0 && last > nl)
                    json = json[(nl + 1)..last].Trim();
            }

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }
}
