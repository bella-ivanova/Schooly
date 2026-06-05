using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public class ChatLogService
{
    private readonly AppDbContext _db;
    private readonly IChatService _chat;

    public ChatLogService(AppDbContext db, IChatService chat)
    {
        _db   = db;
        _chat = chat;
    }

    public async Task SaveMessageAsync(string userId, string role, string content,
        string detectedSubject = "Unknown", string topic = "Unknown", string? school = null)
    {
        int? subjectId = null;
        if (school != null && detectedSubject != "Unknown")
        {
            var sub = await _db.Subjects
                .FirstOrDefaultAsync(s => s.Name == detectedSubject && s.School == school);
            subjectId = sub?.Id;
        }

        _db.ChatMessages.Add(new ChatMessage
        {
            UserId    = userId,
            Role      = role,
            Content   = content,
            SubjectId = subjectId,
            Topic     = topic,
            Timestamp = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    public async Task<(string subject, string topic)> DetectSubjectTopicAsync(string question)
    {
        var prompt =
            $"Given this student question: '{question}', reply with ONLY a JSON object with no extra text: " +
            "{\"subject\": \"<Bulgarian school curriculum subject name>\", \"topic\": \"<short topic label in Bulgarian>\"}";

        try
        {
            var response = await _chat.OneShotAsync(
                "You are a concise classifier. Reply ONLY with valid JSON, no explanation.",
                prompt);

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
        catch
        {
            return ("Unknown", "Unknown");
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
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }
}
