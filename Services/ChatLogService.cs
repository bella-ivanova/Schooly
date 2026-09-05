using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public class ChatLogService
{
    private readonly AppDbContext _db;
    private readonly IChatService _chat;
    private readonly SubjectResolutionService _subjects;
    private readonly ILogger<ChatLogService> _logger;

    // Low temperature for this call only: this is a categorical subject/topic judgment
    // that should be stable on similar input, not fluent prose — same fix applied to
    // StemSubjectClassifier.ClassifyAsync (see CLAUDE.md's "STEM Structured-Handoff
    // Pipeline" section) after the default 0.7 was shown to flip an identical question's
    // classification between runs. _chat is the shared, Scoped IChatService (BgGPT)
    // instance also used to stream the actual answer earlier in the same request
    // (ChatController calls this only after AskStreamAsync has finished), so the original
    // temperature is restored in `finally` rather than left low.
    private const double ClassificationTemperature = 0.1;

    public ChatLogService(AppDbContext db, IChatService chat, SubjectResolutionService subjects, ILogger<ChatLogService> logger)
    {
        _db       = db;
        _chat     = chat;
        _subjects = subjects;
        _logger   = logger;
    }

    public async Task SaveMessageAsync(string userId, int sessionId, string role, string content,
        string detectedSubject = "Unknown", string topic = "Unknown", int? schoolId = null)
    {
        int? subjectId = await _subjects.GetOrCreateSubjectIdAsync(detectedSubject, schoolId);

        _db.ChatMessages.Add(new ChatMessage
        {
            UserId    = userId,
            SessionId = sessionId,
            Role      = role,
            Content   = content,
            SubjectId = subjectId,
            Topic     = topic,
            Timestamp = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _db.ChatSessions.Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastMessageAt, DateTime.UtcNow));
    }

    public async Task<(string subject, string topic)> DetectSubjectTopicAsync(string question)
    {
        var originalTemperature = _chat.Temperature;
        try
        {
            question = InputSanitizer.SanitizeUserInput(question, maxLength: 2000);
            _chat.Temperature = ClassificationTemperature;

            var response = await _chat.OneShotAsync(
                "You are a concise classifier. The user message is a student question. " +
                "Reply ONLY with a JSON object and nothing else: " +
                "{\"subject\": \"<Bulgarian school curriculum subject name>\", \"topic\": \"<short topic label in Bulgarian>\"}",
                question);

            var json = response.Trim();
            if (json.StartsWith("```"))
            {
                var nl   = json.IndexOf('\n');
                var last = json.LastIndexOf("```");
                if (nl >= 0 && last > nl)
                    json = json[(nl + 1)..last].Trim();
            }

            using var doc = JsonDocument.Parse(json);
            var subject   = doc.RootElement.GetProperty("subject").GetString() ?? "Unknown";
            var topic     = doc.RootElement.GetProperty("topic").GetString()   ?? "Unknown";
            return (subject, topic);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat subject/topic classification failed for question {Question}", question);
            return ("Unknown", "Unknown");
        }
        finally
        {
            _chat.Temperature = originalTemperature;
        }
    }

    public async Task<List<(string Topic, string Subject, int Count)>> GetWeakSpotsAsync(
        string userId, int days = 7, int minCount = 2)
    {
        var since = DateTime.UtcNow.AddDays(-days);

        var messages = await _db.ChatMessages
            .Include(m => m.Subject)
            .Where(m => m.UserId == userId && m.Role == "user" && m.Timestamp >= since && m.Topic != "Unknown")
            .ToListAsync();

        return messages
            .GroupBy(m => m.Topic)
            .Where(g => g.Count() >= minCount)
            .OrderByDescending(g => g.Count())
            .Select(g => (
                Topic:   g.Key,
                Subject: g.OrderByDescending(m => m.Timestamp).First().Subject?.Name ?? "Unknown",
                Count:   g.Count()
            ))
            .ToList();
    }

    public async Task<List<ChatMessage>> GetHistoryAsync(string userId, int limit = 50)
    {
        return await _db.ChatMessages
            .Include(m => m.Subject)
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }
}
