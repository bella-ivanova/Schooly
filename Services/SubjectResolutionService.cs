using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

// Shared get-or-create lookup for the AI-classified subject string -> Subject row,
// used by both ChatSessionService (session folder) and ChatLogService (per-message
// tagging) so a chat is never silently left unfoldered just because no admin has
// pre-created a Subject with that exact name yet.
public class SubjectResolutionService
{
    private readonly AppDbContext _db;

    public SubjectResolutionService(AppDbContext db)
    {
        _db = db;
    }

    // schoolId null resolves/creates the personal/global fallback bucket for
    // students with no school. Returns null only for the "Unknown" sentinel.
    public async Task<int?> GetOrCreateSubjectIdAsync(string detectedSubject, int? schoolId)
    {
        if (string.IsNullOrWhiteSpace(detectedSubject) || detectedSubject == "Unknown")
            return null;

        var existing = await _db.Subjects
            .FirstOrDefaultAsync(s => s.Name == detectedSubject && s.SchoolId == schoolId);
        if (existing != null)
            return existing.Id;

        var subject = new Subject { Name = detectedSubject, SchoolId = schoolId };
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();
        return subject.Id;
    }
}
