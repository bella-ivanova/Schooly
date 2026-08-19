using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public class ChatSessionService
{
    private readonly AppDbContext _db;
    private readonly IChatService _chat;
    private readonly IUserRepository _users;
    private readonly SubjectResolutionService _subjects;

    public ChatSessionService(AppDbContext db, IChatService chat, IUserRepository users, SubjectResolutionService subjects)
    {
        _db       = db;
        _chat     = chat;
        _users    = users;
        _subjects = subjects;
    }

    // Creates a brand-new, untitled, unfiled session for a "new chat".
    public async Task<ChatSession> CreateAsync(string userId)
    {
        var session = new ChatSession
        {
            UserId        = userId,
            CreatedAt     = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
        };
        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    // Ownership-checked fetch. Returns null if the session doesn't exist OR belongs
    // to a different user — callers map null to 404 either way, without leaking which.
    public async Task<ChatSession?> GetOwnedAsync(int sessionId, string userId) =>
        await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

    // Ownership-checked delete. Messages cascade at the DB level (ChatMessage's FK to
    // ChatSession is configured OnDelete Cascade in AppDbContext) — no manual cleanup needed.
    public async Task<bool> DeleteAsync(int sessionId, string userId)
    {
        var session = await GetOwnedAsync(sessionId, userId);
        if (session == null) return false;

        _db.ChatSessions.Remove(session);
        await _db.SaveChangesAsync();
        return true;
    }

    // Prior turns for multi-turn seeding, oldest first, capped so replayed prompt
    // size stays bounded.
    public async Task<List<(string Role, string Content)>> GetRecentTurnsAsync(int sessionId, int maxTurns = 20)
    {
        var recent = await _db.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.Timestamp)
            .Take(maxTurns)
            .OrderBy(m => m.Timestamp)
            .Select(m => new { m.Role, m.Content })
            .ToListAsync();

        return recent.Select(m => (m.Role, m.Content)).ToList();
    }

    // Folder-filterable session listing for the current user. subjectName null/blank -> "All".
    public async Task<List<ChatSession>> ListAsync(string userId, string? subjectName = null)
    {
        var query = _db.ChatSessions.Include(s => s.Subject).Include(s => s.Class).Where(s => s.UserId == userId);
        if (!string.IsNullOrWhiteSpace(subjectName))
            query = query.Where(s => s.Subject != null && s.Subject.Name == subjectName);

        return await query.OrderByDescending(s => s.LastMessageAt).ToListAsync();
    }

    // Full transcript. Caller must have already ownership-checked via GetOwnedAsync.
    public async Task<List<ChatMessage>> GetTranscriptAsync(int sessionId) =>
        await _db.ChatMessages.Include(m => m.Subject)
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

    // Get-or-creates the Subject for detectedSubject, then locks the session onto it
    // (and, if the student belongs to a class tagged with that same subject in the
    // same school, locks the session onto that class too). Callers only invoke this
    // on a session's first exchange, so the folder never drifts mid-conversation.
    public async Task<(int? SubjectId, string? SubjectName, int? ClassId, string? ClassName)> SetFolderAndTitleOnceAsync(
        int sessionId, string userId, string detectedSubject, int? schoolId, string? title)
    {
        int? subjectId = await _subjects.GetOrCreateSubjectIdAsync(detectedSubject, schoolId);

        int? classId = null;
        if (subjectId != null && schoolId != null)
        {
            classId = await _db.ClassStudents
                .Where(cs => cs.StudentId == userId)
                .Join(_db.Classes, cs => cs.ClassId, c => c.Id, (cs, c) => c)
                .Where(c => c.SchoolId == schoolId && c.SubjectId == subjectId)
                .OrderBy(c => c.Id)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();
        }

        string? subjectName = subjectId != null
            ? await _db.Subjects.Where(s => s.Id == subjectId).Select(s => s.Name).FirstOrDefaultAsync()
            : null;
        string? className = classId != null
            ? await _db.Classes.Where(c => c.Id == classId).Select(c => c.Name).FirstOrDefaultAsync()
            : null;

        await _db.ChatSessions.Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.SubjectId, subjectId)
                .SetProperty(x => x.ClassId, classId)
                .SetProperty(x => x.Title, title));

        return (subjectId, subjectName, classId, className);
    }

    // Detaches a removed class member's sessions from that class, re-filing them
    // under the class's current subject folder so history stays organized without
    // being attributed to a class they're no longer part of.
    public async Task DetachClassAsync(string userId, int classId, int? subjectId)
    {
        await _db.ChatSessions
            .Where(s => s.UserId == userId && s.ClassId == classId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ClassId, (int?)null)
                .SetProperty(x => x.SubjectId, subjectId));
    }

    // Looks up the caller's SchoolId, since it isn't present in the JWT and this
    // feature spans every role (not just students, who have it via ApplicationUser).
    public async Task<int?> ResolveSchoolIdAsync(string userId)
    {
        var user = await _users.GetByIdAsync(userId);
        return user?.SchoolId;
    }

    public async Task<string?> GenerateTitleAsync(string firstUserMessage, string firstAssistantMessage)
    {
        firstUserMessage      = InputSanitizer.SanitizeUserInput(firstUserMessage, maxLength: 2000);
        firstAssistantMessage = InputSanitizer.SanitizeUserInput(firstAssistantMessage, maxLength: 2000);

        var userPrompt =
            $"A student asked: <question>{firstUserMessage}</question>\n" +
            $"The tutor answered: <answer>{firstAssistantMessage}</answer>\n" +
            "Generate a short chat title summarizing the topic, in the same language as the question.\n" +
            "Reply ONLY with the title text, no quotes, no markdown, no punctuation at the end, max 6 words.";

        try
        {
            var response = await _chat.OneShotAsync(
                "You are a concise assistant that titles chat conversations for a school app.",
                userPrompt);

            var title = response.Trim().Trim('"', '\'');
            if (title.StartsWith("```"))
            {
                var nl   = title.IndexOf('\n');
                var last = title.LastIndexOf("```");
                if (nl >= 0 && last > nl)
                    title = title[(nl + 1)..last].Trim();
            }
            if (title.Length > 150)
                title = title[..150];

            return string.IsNullOrWhiteSpace(title) ? null : title;
        }
        catch
        {
            return null;
        }
    }
}
