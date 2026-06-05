namespace StudyAssistant.Services;

public class ExamService
{
    private readonly RAGService _rag;
    private readonly IChatService _chat;

    public ExamService(RAGService rag, IChatService chat)
    {
        _rag  = rag;
        _chat = chat;
    }

    public async Task<string> GenerateExamAsync(string topic, int grade)
    {
        var chunks = await _rag.GetChunksAsync(topic);

        if (string.IsNullOrWhiteSpace(chunks))
            return $"Няма намерен материал за темата '{topic}'. Опитай с друга тема.";

        var userPrompt =
            $"Using ONLY the following textbook material for Grade {grade}, generate a mock exam on the topic '{topic}'.\n" +
            "Include:\n" +
            "- 3 multiple choice questions (with 4 options each, mark the correct one with *)\n" +
            "- 2 short answer questions\n" +
            "- 1 problem-solving question with full working space\n" +
            "Format clearly with numbered sections. Write in Bulgarian.\n" +
            "--- Textbook Material ---\n" +
            chunks +
            "\n--- End of Material ---";

        return await _chat.OneShotAsync(
            "You are a school exam generator. Generate clear, well-structured tests.",
            userPrompt);
    }
}
