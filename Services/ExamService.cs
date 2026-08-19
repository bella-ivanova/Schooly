using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public class ExamService
{
    private readonly RAGService _rag;
    private readonly IChatService _chat;
    private readonly AppDbContext _db;

    public ExamService(RAGService rag, IChatService chat, AppDbContext db)
    {
        _rag  = rag;
        _chat = chat;
        _db   = db;
    }

    public async Task<string> GenerateExamAsync(string topic, int grade)
    {
        topic = InputSanitizer.SanitizeUserInput(topic, maxLength: 300);
        _rag.SetGrade(grade);
        var chunks = await _rag.GetChunksAsync(topic);

        if (string.IsNullOrWhiteSpace(chunks))
            return $"Няма намерен материал за темата '{topic}'. Опитай с друга тема.";

        var systemPrompt =
            "You are a school exam generator. Use ONLY the provided textbook material.\n" +
            "Generate a mock exam that includes:\n" +
            "- 3 multiple choice questions (with 4 options each, mark the correct one with *)\n" +
            "- 2 short answer questions\n" +
            "- 1 problem-solving question with full working space\n" +
            "Format clearly with numbered sections. Respond in the same language as the topic given below.";

        var userPrompt =
            $"Grade: {grade}\n" +
            $"Topic: {topic}\n" +
            "--- Textbook Material ---\n" +
            chunks +
            "\n--- End of Material ---";

        return await _chat.OneShotAsync(systemPrompt, userPrompt);
    }

    public async Task<(int Id, string Exam)> GenerateAndSaveAsync(string topic, int grade, string userId)
    {
        var exam = await GenerateExamAsync(topic, grade);

        var saved = new SavedExam
        {
            UserId    = userId,
            Topic     = InputSanitizer.SanitizeUserInput(topic, maxLength: 300),
            Content   = exam,
            CreatedAt = DateTime.UtcNow,
        };
        _db.SavedExams.Add(saved);
        await _db.SaveChangesAsync();

        return (saved.Id, exam);
    }

    public async Task<List<SavedExam>> ListSavedAsync(string userId) =>
        await _db.SavedExams
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

    public async Task<SavedExam?> GetSavedAsync(string userId, int id) =>
        await _db.SavedExams.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
}
