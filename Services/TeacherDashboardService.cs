using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public class TeacherDashboardService
{
    private readonly AppDbContext _db;

    public TeacherDashboardService(AppDbContext db) { _db = db; }

    public async Task<List<(Class Class, List<Subject> Subjects, int StudentCount)>> GetMyClassesAsync(string teacherId)
    {
        var rows = await _db.ClassTeachers
            .Include(ct => ct.Class!)
                .ThenInclude(c => c.Students)
            .Include(ct => ct.Subject)
            .Where(ct => ct.TeacherId == teacherId)
            .ToListAsync();

        return rows
            .GroupBy(ct => ct.ClassId)
            .Select(g => (
                Class:        g.First().Class!,
                Subjects:     g.Select(ct => ct.Subject!).ToList(),
                StudentCount: g.First().Class!.Students.Count
            ))
            .ToList();
    }

    public async Task<List<(Class Class, Subject Subject, List<(string Topic, int Count)> TopTopics)>>
        GetStrugglesByClassAsync(string teacherId, int classId, int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);

        var classTeachers = await _db.ClassTeachers
            .Include(ct => ct.Class)
            .Include(ct => ct.Subject)
            .Where(ct => ct.TeacherId == teacherId && ct.ClassId == classId)
            .ToListAsync();

        if (classTeachers.Count == 0)
            return new List<(Class, Subject, List<(string, int)>)>();

        var cls = classTeachers.First().Class!;
        var subjectIds = classTeachers.Select(ct => ct.SubjectId).ToHashSet();

        var studentIds = await _db.Set<ApplicationUser>()
            .Where(u => u.ClassId == classId)
            .Select(u => u.Id)
            .ToListAsync();

        var messages = studentIds.Count == 0
            ? new List<ChatMessage>()
            : await _db.ChatMessages
                .Where(m => studentIds.Contains(m.UserId)
                         && m.Timestamp >= since
                         && m.Topic != "Unknown"
                         && m.SubjectId != null
                         && subjectIds.Contains(m.SubjectId!.Value)
                         && m.Role == "user")
                .ToListAsync();

        return classTeachers
            .Select(ct => (
                Class:     cls,
                Subject:   ct.Subject!,
                TopTopics: messages
                    .Where(m => m.SubjectId == ct.SubjectId)
                    .GroupBy(m => m.Topic)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => (Topic: g.Key, Count: g.Count()))
                    .ToList()
            ))
            .ToList();
    }

    public async Task<List<(Class Class, List<(ApplicationUser Student, int QuestionCount)> TopStudents)>>
        GetActiveStudentsByClassAsync(string teacherId, int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);

        var classIds = await _db.ClassTeachers
            .Where(ct => ct.TeacherId == teacherId)
            .Select(ct => ct.ClassId)
            .Distinct()
            .ToListAsync();

        var result = new List<(Class, List<(ApplicationUser, int)>)>();

        foreach (var cid in classIds)
        {
            var cls = await _db.Classes
                .Include(c => c.Students)
                .FirstAsync(c => c.Id == cid);

            var studentIds = cls.Students.Select(s => s.Id).ToList();

            if (studentIds.Count == 0)
            {
                result.Add((cls, new List<(ApplicationUser, int)>()));
                continue;
            }

            var counts = await _db.ChatMessages
                .Where(m => studentIds.Contains(m.UserId)
                         && m.Timestamp >= since
                         && m.Role == "user")
                .GroupBy(m => m.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            var studentDict = cls.Students.ToDictionary(s => s.Id);
            var topStudents = counts
                .Where(c => studentDict.ContainsKey(c.UserId))
                .Select(c => (Student: studentDict[c.UserId], QuestionCount: c.Count))
                .ToList();

            result.Add((cls, topStudents));
        }

        return result;
    }
}
