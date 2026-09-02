using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public record ClassJoinCodeDto(string Code, DateTime CreatedAt);
public record TeacherRosterStudentDto(string Id, string Username, string FullName, int? Grade);
public record TeacherStudentDto(string Id, string Username, string FullName, int? Grade, List<string> ClassNames);
public record TeacherStudentWeakSpotDto(string Topic, string Subject, int Count);
public record TeacherStudentStatsDto(int QuestionCount30d, DateTime? LastActiveAt, int SavedExamCount, List<TeacherStudentWeakSpotDto> WeakSpots);

public class TeacherDashboardService
{
    private readonly AppDbContext _db;
    private readonly ChatLogService _chatLog;

    public TeacherDashboardService(AppDbContext db, ChatLogService chatLog)
    {
        _db = db;
        _chatLog = chatLog;
    }

    public async Task<List<(Class Class, List<Subject> Subjects, int StudentCount)>> GetMyClassesAsync(string teacherId)
    {
        var viaSubjects = await _db.ClassTeachers
            .Include(ct => ct.Class!)
                .ThenInclude(c => c.School)
            .Include(ct => ct.Class!)
                .ThenInclude(c => c.ClassStudents)
            .Include(ct => ct.Subject!)
                .ThenInclude(s => s.School)
            .Where(ct => ct.TeacherId == teacherId)
            .ToListAsync();

        var grouped = viaSubjects
            .GroupBy(ct => ct.ClassId)
            .ToDictionary(g => g.Key, g => (
                Class:        g.First().Class!,
                Subjects:     g.Select(ct => ct.Subject!).ToList(),
                StudentCount: g.First().Class!.ClassStudents.Count
            ));

        // Blank classes assigned to this teacher as homeroom but with no subject
        // assignment yet wouldn't otherwise appear here — surface them too so the
        // teacher can open them and generate a class join code.
        var homeroomOnly = await _db.Classes
            .Include(c => c.School)
            .Include(c => c.ClassStudents)
            .Where(c => c.HomeroomTeacherId == teacherId && !grouped.Keys.Contains(c.Id))
            .ToListAsync();

        var result = grouped.Values.ToList();
        result.AddRange(homeroomOnly.Select(c => (
            Class: c,
            Subjects: new List<Subject>(),
            StudentCount: c.ClassStudents.Count
        )));
        return result;
    }

    private async Task<bool> IsTeacherAuthorizedForClassAsync(string teacherId, int classId)
    {
        var isHomeroom = await _db.Classes
            .AnyAsync(c => c.Id == classId && c.HomeroomTeacherId == teacherId);
        if (isHomeroom) return true;

        return await _db.ClassTeachers
            .AnyAsync(ct => ct.ClassId == classId && ct.TeacherId == teacherId);
    }

    public async Task<ClassJoinCodeDto?> GetClassJoinCodeAsync(string teacherId, int classId)
    {
        if (!await IsTeacherAuthorizedForClassAsync(teacherId, classId))
            return null;

        return await _db.ClassJoinCodes
            .Where(c => c.ClassId == classId && c.IsActive)
            .Select(c => new ClassJoinCodeDto(c.Code, c.CreatedAt))
            .FirstOrDefaultAsync();
    }

    public async Task<(bool Success, string? Error, ClassJoinCodeDto? Code)> RegenerateClassJoinCodeAsync(string teacherId, int classId)
    {
        if (!await IsTeacherAuthorizedForClassAsync(teacherId, classId))
            return (false, "Class not found or you are not assigned to it.", null);

        await _db.ClassJoinCodes
            .Where(c => c.ClassId == classId && c.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsActive, false)
                .SetProperty(c => c.RevokedAt, DateTime.UtcNow));

        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        var entity = new ClassJoinCode
        {
            ClassId   = classId,
            Code      = code,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.ClassJoinCodes.Add(entity);
        await _db.SaveChangesAsync();

        return (true, null, new ClassJoinCodeDto(entity.Code, entity.CreatedAt));
    }

    public async Task<List<TeacherRosterStudentDto>?> GetClassRosterAsync(string teacherId, int classId)
    {
        if (!await IsTeacherAuthorizedForClassAsync(teacherId, classId))
            return null;

        return await _db.ClassStudents
            .Where(cs => cs.ClassId == classId)
            .Include(cs => cs.Student)
            .Select(cs => new TeacherRosterStudentDto(cs.Student!.Id, cs.Student.UserName ?? "", cs.Student.FullName, cs.Student.Grade))
            .ToListAsync();
    }

    // All class ids this teacher has access to, whether via a ClassTeacher subject
    // assignment or as a class's homeroom teacher — same union GetMyClassesAsync uses,
    // shared here so the cross-class student views can't miss homeroom-only classes.
    private async Task<List<int>> GetAuthorizedClassIdsAsync(string teacherId)
    {
        var viaSubjects = await _db.ClassTeachers
            .Where(ct => ct.TeacherId == teacherId)
            .Select(ct => ct.ClassId)
            .Distinct()
            .ToListAsync();

        var homeroomIds = await _db.Classes
            .Where(c => c.HomeroomTeacherId == teacherId)
            .Select(c => c.Id)
            .ToListAsync();

        return viaSubjects.Union(homeroomIds).ToList();
    }

    public async Task<List<TeacherStudentDto>> GetAllStudentsAsync(string teacherId)
    {
        var classIds = await GetAuthorizedClassIdsAsync(teacherId);
        if (classIds.Count == 0)
            return new List<TeacherStudentDto>();

        var rows = await _db.ClassStudents
            .Where(cs => classIds.Contains(cs.ClassId))
            .Include(cs => cs.Student)
            .Include(cs => cs.Class)
            .ToListAsync();

        return rows
            .GroupBy(cs => cs.StudentId)
            .Select(g => new TeacherStudentDto(
                g.Key,
                g.First().Student!.UserName ?? "",
                g.First().Student!.FullName,
                g.First().Student!.Grade,
                g.Select(cs => cs.Class!.Name).Distinct().ToList()))
            .ToList();
    }

    public async Task<TeacherStudentDto?> GetStudentDetailAsync(string teacherId, string studentId)
    {
        var classIds = await GetAuthorizedClassIdsAsync(teacherId);
        if (classIds.Count == 0)
            return null;

        var rows = await _db.ClassStudents
            .Where(cs => classIds.Contains(cs.ClassId) && cs.StudentId == studentId)
            .Include(cs => cs.Student)
            .Include(cs => cs.Class)
            .ToListAsync();

        if (rows.Count == 0)
            return null;

        return new TeacherStudentDto(
            studentId,
            rows[0].Student!.UserName ?? "",
            rows[0].Student!.FullName,
            rows[0].Student!.Grade,
            rows.Select(cs => cs.Class!.Name).Distinct().ToList());
    }

    public async Task<TeacherStudentStatsDto?> GetStudentStatsAsync(string teacherId, string studentId, int days = 30)
    {
        var classIds = await GetAuthorizedClassIdsAsync(teacherId);
        if (classIds.Count == 0)
            return null;

        var isAuthorized = await _db.ClassStudents
            .AnyAsync(cs => classIds.Contains(cs.ClassId) && cs.StudentId == studentId);
        if (!isAuthorized)
            return null;

        var since = DateTime.UtcNow.AddDays(-days);

        var questionCount = await _db.ChatMessages
            .CountAsync(m => m.UserId == studentId && m.Role == "user" && m.Timestamp >= since);

        var lastActiveAt = await _db.ChatSessions
            .Where(s => s.UserId == studentId)
            .OrderByDescending(s => s.LastMessageAt)
            .Select(s => (DateTime?)s.LastMessageAt)
            .FirstOrDefaultAsync();

        var savedExamCount = await _db.SavedExams.CountAsync(e => e.UserId == studentId);

        var weakSpots = await _chatLog.GetWeakSpotsAsync(studentId, days, minCount: 2);

        return new TeacherStudentStatsDto(
            questionCount,
            lastActiveAt,
            savedExamCount,
            weakSpots.Select(w => new TeacherStudentWeakSpotDto(w.Topic, w.Subject, w.Count)).ToList());
    }

    public async Task<List<(Class Class, Subject Subject, List<(string Topic, int Count)> TopTopics)>>
        GetStrugglesByClassAsync(string teacherId, int classId, int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);

        if (!await IsTeacherAuthorizedForClassAsync(teacherId, classId))
            return new List<(Class, Subject, List<(string, int)>)>();

        var cls = await _db.Classes
            .Include(c => c.School)
            .Include(c => c.Subject)
            .FirstOrDefaultAsync(c => c.Id == classId);
        if (cls == null)
            return new List<(Class, Subject, List<(string, int)>)>();

        var classTeachers = await _db.ClassTeachers
            .Include(ct => ct.Subject)
            .Where(ct => ct.TeacherId == teacherId && ct.ClassId == classId)
            .ToListAsync();

        // Subject id -> Subject entity, deduplicated: ClassTeacher-assigned subjects
        // plus the class's own tagged subject, which may have no ClassTeacher row yet
        // (e.g. a homeroom teacher whose class was just tagged by a SchoolAdmin).
        var subjectsById = classTeachers
            .Where(ct => ct.Subject != null)
            .ToDictionary(ct => ct.SubjectId, ct => ct.Subject!);
        if (cls.SubjectId != null && cls.Subject != null && !subjectsById.ContainsKey(cls.SubjectId.Value))
            subjectsById[cls.SubjectId.Value] = cls.Subject;

        if (subjectsById.Count == 0)
            return new List<(Class, Subject, List<(string, int)>)>();

        var subjectIds = subjectsById.Keys.ToHashSet();

        var studentIds = await _db.ClassStudents
            .Where(cs => cs.ClassId == classId)
            .Select(cs => cs.StudentId)
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

        return subjectsById
            .Select(kv => (
                Class:     cls,
                Subject:   kv.Value,
                TopTopics: messages
                    .Where(m => m.SubjectId == kv.Key)
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
                .Include(c => c.School)
                .Include(c => c.ClassStudents)
                    .ThenInclude(cs => cs.Student)
                .FirstOrDefaultAsync(c => c.Id == cid);

            if (cls is null) continue;

            var studentIds = cls.ClassStudents.Select(cs => cs.StudentId).ToList();

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

            var studentDict = cls.ClassStudents.ToDictionary(cs => cs.StudentId, cs => cs.Student!);
            var topStudents = counts
                .Where(c => studentDict.ContainsKey(c.UserId))
                .Select(c => (Student: studentDict[c.UserId], QuestionCount: c.Count))
                .ToList();

            result.Add((cls, topStudents));
        }

        return result;
    }
}
