using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public class ChatLogService
{
    private readonly AppDbContext _db;
    private readonly OllamaChatService _chat;

    public ChatLogService(AppDbContext db, OllamaChatService chat)
    {
        _db   = db;
        _chat = chat;
    }

    public async Task SaveMessageAsync(string userId, string role, string content,
        string subject = "Unknown", string topic = "Unknown")
    {
        _db.ChatMessages.Add(new ChatMessage
        {
            UserId    = userId,
            Role      = role,
            Content   = content,
            Subject   = subject,
            Topic     = topic,
            Timestamp = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    public async Task<(string subject, string topic)> DetectSubjectTopicAsync(string question)
    {
        // Fresh history for each call so detection never bleeds into the main conversation.
        _chat.SetSystemPrompt("You are a concise classifier. Reply ONLY with valid JSON, no explanation.");

        var prompt =
            $"Given this student question: '{question}', reply with ONLY a JSON object with no extra text: " +
            "{\"subject\": \"<Bulgarian school curriculum subject name>\", \"topic\": \"<short topic label in Bulgarian>\"}";

        try
        {
            var response = await _chat.SendMessageAsync(prompt);

            // Strip markdown code fences if the model wraps the JSON
            var json = response.Trim();
            if (json.StartsWith("```"))
            {
                var nl   = json.IndexOf('\n');
                var last = json.LastIndexOf("```");
                if (nl >= 0 && last > nl)
                    json = json[(nl + 1)..last].Trim();
            }

            using var doc    = JsonDocument.Parse(json);
            var subject      = doc.RootElement.GetProperty("subject").GetString() ?? "Unknown";
            var topic        = doc.RootElement.GetProperty("topic").GetString()   ?? "Unknown";
            return (subject, topic);
        }
        catch
        {
            return ("Unknown", "Unknown");
        }
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
