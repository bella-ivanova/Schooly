using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public record ClassSummaryDto(int Id, string Name, int? Grade, int? SubjectId, string? SubjectName, string? HomeroomTeacherUsername, int StudentCount, int SchoolId, string SchoolName);
public record UserSummaryDto(string Id, string Username, string FullName, string Role, int? Grade, IReadOnlyList<string> ClassNames, int? SchoolId, string? SchoolName, IReadOnlyList<string> Subjects);
public record SubjectSummaryDto(int Id, string Name, int SchoolId, string SchoolName);
public record SchoolSummaryDto(int Id, string Name, DateTime CreatedAt, int StudentCount, int TeacherCount);
public record SchoolTeacherCodeDto(string Code, DateTime CreatedAt);
public record ClassTeacherAssignmentDto(string TeacherId, string TeacherUsername, int SubjectId, string SubjectName);
public record ClassRosterStudentDto(string Id, string Username, string FullName);
public record ClassDetailDto(
    int Id, string Name, int? Grade, int? SubjectId, string? SubjectName, string? HomeroomTeacherUsername,
    IReadOnlyList<ClassRosterStudentDto> Students,
    IReadOnlyList<ClassTeacherAssignmentDto> TeacherAssignments);
public record TeacherSubjectDto(int SubjectId, string SubjectName);

public class SchoolAdminService
{
    private readonly AppDbContext _db;
    private readonly IUserRepository _users;
    private readonly ChatSessionService _chatSessions;

    public SchoolAdminService(AppDbContext db, IUserRepository users, ChatSessionService chatSessions)
    {
        _db           = db;
        _users        = users;
        _chatSessions = chatSessions;
    }

    public async Task<(bool Success, string? Error)> AddClassAsync(int schoolId, string name, int subjectId, string? homeroomTeacherId, int? grade = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Class name is required.");
        if (grade is < 1 or > 12)
            return (false, "Grade must be between 1 and 12.");

        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId && s.SchoolId == schoolId);
        if (subject == null)
            return (false, "Subject not found in this school.");

        if (homeroomTeacherId != null)
        {
            var teacher = await _users.GetByIdAsync(homeroomTeacherId);
            if (teacher == null)
                return (false, "Teacher not found.");
            if (teacher.SchoolId != schoolId)
                return (false, "Teacher does not belong to this school.");
        }

        _db.Classes.Add(new Class { Name = name, SchoolId = schoolId, SubjectId = subjectId, HomeroomTeacherId = homeroomTeacherId, Grade = grade });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CreateSubjectAsync(int schoolId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Subject name is required.");

        var exists = await _db.Subjects.AnyAsync(s => s.Name == name && s.SchoolId == schoolId);
        if (exists)
            return (false, $"Subject '{name}' already exists in this school.");

        _db.Subjects.Add(new Subject { Name = name, SchoolId = schoolId });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<string?> GetSchoolNameAsync(int schoolId) =>
        await _db.Schools.Where(s => s.Id == schoolId).Select(s => s.Name).FirstOrDefaultAsync();

    public async Task<SchoolTeacherCodeDto?> GetTeacherCodeAsync(int schoolId)
    {
        return await _db.SchoolTeacherCodes
            .Where(c => c.SchoolId == schoolId && c.IsActive)
            .Select(c => new SchoolTeacherCodeDto(c.Code, c.CreatedAt))
            .FirstOrDefaultAsync();
    }

    public async Task<(bool Success, string? Error, SchoolTeacherCodeDto? Code)> RegenerateTeacherCodeAsync(int schoolId)
    {
        await _db.SchoolTeacherCodes
            .Where(c => c.SchoolId == schoolId && c.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsActive, false)
                .SetProperty(c => c.RevokedAt, DateTime.UtcNow));

        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        var entity = new SchoolTeacherCode
        {
            SchoolId  = schoolId,
            Code      = code,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.SchoolTeacherCodes.Add(entity);
        await _db.SaveChangesAsync();

        return (true, null, new SchoolTeacherCodeDto(entity.Code, entity.CreatedAt));
    }

    public async Task<IReadOnlyList<ClassSummaryDto>> ListClassesAsync(int schoolId)
    {
        var classes = await _db.Classes
            .Include(c => c.HomeroomTeacher)
            .Include(c => c.ClassStudents)
            .Include(c => c.Subject)
            .Where(c => c.SchoolId == schoolId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var schoolName = await GetSchoolNameAsync(schoolId) ?? "";

        return classes
            .Select(c => new ClassSummaryDto(c.Id, c.Name, c.Grade, c.SubjectId, c.Subject?.Name, c.HomeroomTeacher?.UserName, c.ClassStudents.Count, schoolId, schoolName))
            .ToList();
    }

    public async Task<(bool Success, string? Error)> AssignTeacherAsync(int schoolId, int classId, string teacherId)
    {
        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == classId && c.SchoolId == schoolId);
        if (cls == null)
            return (false, "Class not found.");

        var teacher = await _users.GetByIdAsync(teacherId);
        if (teacher == null)
            return (false, "Teacher not found.");
        if (teacher.SchoolId != schoolId)
            return (false, "Teacher does not belong to this school.");

        cls.HomeroomTeacherId = teacher.Id;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateClassAsync(int schoolId, int classId, string name, int subjectId, int? grade = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Class name is required.");
        if (grade is < 1 or > 12)
            return (false, "Grade must be between 1 and 12.");

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == classId && c.SchoolId == schoolId);
        if (cls == null)
            return (false, "Class not found.");

        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId && s.SchoolId == schoolId);
        if (subject == null)
            return (false, "Subject not found in this school.");

        cls.Name = name;
        cls.SubjectId = subjectId;
        cls.Grade = grade;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<ClassDetailDto?> GetClassDetailAsync(int schoolId, int classId)
    {
        var cls = await _db.Classes
            .Include(c => c.HomeroomTeacher)
            .Include(c => c.Subject)
            .Include(c => c.ClassStudents).ThenInclude(cs => cs.Student)
            .Include(c => c.ClassTeachers).ThenInclude(ct => ct.Teacher)
            .Include(c => c.ClassTeachers).ThenInclude(ct => ct.Subject)
            .FirstOrDefaultAsync(c => c.Id == classId && c.SchoolId == schoolId);

        if (cls == null)
            return null;

        var students = cls.ClassStudents
            .Select(cs => new ClassRosterStudentDto(cs.Student!.Id, cs.Student.UserName ?? "", cs.Student.FullName))
            .ToList();

        var teacherAssignments = cls.ClassTeachers
            .Select(ct => new ClassTeacherAssignmentDto(ct.TeacherId, ct.Teacher!.UserName ?? "", ct.SubjectId, ct.Subject!.Name))
            .ToList();

        return new ClassDetailDto(cls.Id, cls.Name, cls.Grade, cls.SubjectId, cls.Subject?.Name, cls.HomeroomTeacher?.UserName, students, teacherAssignments);
    }

    public async Task<(bool Success, string? Error)> RemoveStudentAsync(int schoolId, int classId, string userId)
    {
        var student = await _users.GetByIdAsync(userId);
        if (student == null)
            return (false, "User not found.");
        if (student.SchoolId != schoolId)
            return (false, "Student does not belong to this school.");

        var membership = await _db.ClassStudents
            .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == userId);
        if (membership == null)
            return (false, "Student is not in this class.");

        _db.ClassStudents.Remove(membership);
        await _db.SaveChangesAsync();

        var classSubjectId = await _db.Classes.Where(c => c.Id == classId).Select(c => (int?)c.SubjectId).FirstOrDefaultAsync();
        await _chatSessions.DetachClassAsync(userId, classId, classSubjectId);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AssignTeacherToClassAsync(int schoolId, int classId, string teacherId, string subjectName)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return (false, "Subject name is required.");

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == classId && c.SchoolId == schoolId);
        if (cls == null)
            return (false, "Class not found.");

        var teacher = await _users.GetByIdAsync(teacherId);
        if (teacher == null)
            return (false, "Teacher not found.");
        if (teacher.SchoolId != schoolId)
            return (false, "Teacher does not belong to this school.");

        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Name == subjectName && s.SchoolId == schoolId);
        if (subject == null)
        {
            subject = new Subject { Name = subjectName, SchoolId = schoolId };
            _db.Subjects.Add(subject);
            await _db.SaveChangesAsync();
        }

        var existing = await _db.ClassTeachers.FirstOrDefaultAsync(
            ct => ct.ClassId == cls.Id && ct.TeacherId == teacher.Id && ct.SubjectId == subject.Id);
        if (existing != null)
            return (false, "Teacher is already assigned to this subject in that class.");

        _db.ClassTeachers.Add(new ClassTeacher { ClassId = cls.Id, TeacherId = teacher.Id, SubjectId = subject.Id });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemoveTeacherFromClassAsync(int schoolId, int classId, string teacherId, int subjectId)
    {
        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == classId && c.SchoolId == schoolId);
        if (cls == null)
            return (false, "Class not found.");

        var row = await _db.ClassTeachers
            .FirstOrDefaultAsync(ct => ct.ClassId == classId && ct.TeacherId == teacherId && ct.SubjectId == subjectId);
        if (row == null)
            return (false, "Assignment not found.");

        _db.ClassTeachers.Remove(row);

        var hasOtherAssignments = await _db.ClassTeachers
            .AnyAsync(ct => ct.ClassId == classId && ct.TeacherId == teacherId && ct.SubjectId != subjectId);
        if (!hasOtherAssignments && cls.HomeroomTeacherId == teacherId)
            cls.HomeroomTeacherId = null;

        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AssignSubjectToTeacherAsync(int schoolId, string teacherId, int subjectId)
    {
        var teacher = await _users.GetByIdAsync(teacherId);
        if (teacher == null)
            return (false, "Teacher not found.");
        if (teacher.SchoolId != schoolId)
            return (false, "Teacher does not belong to this school.");

        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId && s.SchoolId == schoolId);
        if (subject == null)
            return (false, "Subject not found.");

        var alreadyAssigned = await _db.TeacherSubjects
            .AnyAsync(ts => ts.TeacherId == teacher.Id && ts.SubjectId == subjectId);
        if (alreadyAssigned)
            return (false, "Subject is already assigned to this teacher.");

        _db.TeacherSubjects.Add(new TeacherSubject { TeacherId = teacher.Id, SubjectId = subjectId });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemoveSubjectFromTeacherAsync(int schoolId, string teacherId, int subjectId)
    {
        var teacher = await _users.GetByIdAsync(teacherId);
        if (teacher == null)
            return (false, "Teacher not found.");
        if (teacher.SchoolId != schoolId)
            return (false, "Teacher does not belong to this school.");

        var row = await _db.TeacherSubjects
            .FirstOrDefaultAsync(ts => ts.TeacherId == teacher.Id && ts.SubjectId == subjectId);
        if (row == null)
            return (false, "Assignment not found.");

        _db.TeacherSubjects.Remove(row);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error, IReadOnlyList<TeacherSubjectDto>? Subjects)> GetTeacherSubjectsAsync(int schoolId, string teacherId)
    {
        var teacher = await _users.GetByIdAsync(teacherId);
        if (teacher == null)
            return (false, "Teacher not found.", null);
        if (teacher.SchoolId != schoolId)
            return (false, "Teacher does not belong to this school.", null);

        var subjects = await _db.TeacherSubjects
            .Where(ts => ts.TeacherId == teacherId)
            .Include(ts => ts.Subject)
            .OrderBy(ts => ts.Subject!.Name)
            .Select(ts => new TeacherSubjectDto(ts.SubjectId, ts.Subject!.Name))
            .ToListAsync();

        return (true, null, subjects);
    }

    public async Task<IReadOnlyList<UserSummaryDto>> ListUsersAsync(int schoolId)
    {
        var users = await _db.Set<ApplicationUser>()
            .Where(u => u.SchoolId == schoolId)
            .OrderBy(u => u.Role)
            .ThenBy(u => u.UserName)
            .ToListAsync();

        var userIds = users.Select(u => u.Id).ToList();
        var classNamesByStudent = await _db.ClassStudents
            .Where(cs => userIds.Contains(cs.StudentId))
            .Include(cs => cs.Class)
            .ToListAsync();
        var grouped = classNamesByStudent
            .GroupBy(cs => cs.StudentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(cs => cs.Class!.Name).ToList());

        var classTeachers = await _db.ClassTeachers
            .Where(ct => userIds.Contains(ct.TeacherId))
            .Include(ct => ct.Subject)
            .ToListAsync();
        var subjectsByTeacher = classTeachers
            .GroupBy(ct => ct.TeacherId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(ct => ct.Subject!.Name).Distinct().ToList());

        var schoolName = await GetSchoolNameAsync(schoolId) ?? "";

        return users
            .Select(u => new UserSummaryDto(
                u.Id,
                u.UserName ?? "",
                u.FullName,
                u.Role.ToString(),
                u.Grade,
                grouped.TryGetValue(u.Id, out var names) ? names : Array.Empty<string>(),
                schoolId,
                schoolName,
                subjectsByTeacher.TryGetValue(u.Id, out var subjects) ? subjects : Array.Empty<string>()))
            .ToList();
    }

    public async Task<IReadOnlyList<SubjectSummaryDto>> ListSubjectsAsync(int schoolId)
    {
        var subjects = await _db.Subjects
            .Where(s => s.SchoolId == schoolId)
            .OrderBy(s => s.Name)
            .ToListAsync();

        var schoolName = await GetSchoolNameAsync(schoolId) ?? "";

        return subjects
            .Select(s => new SubjectSummaryDto(s.Id, s.Name, schoolId, schoolName))
            .ToList();
    }

    public async Task<(bool Success, string? Error)> DeleteSubjectAsync(int schoolId, int subjectId)
    {
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId && s.SchoolId == schoolId);
        if (subject == null)
            return (false, "Subject not found in this school.");

        var usedByClass = await _db.Classes.AnyAsync(c => c.SubjectId == subjectId);
        if (usedByClass)
            return (false, "Subject is still assigned to at least one class and cannot be deleted.");

        var usedByTeacher = await _db.TeacherSubjects.AnyAsync(ts => ts.SubjectId == subjectId);
        if (usedByTeacher)
            return (false, "Subject is still assigned to at least one teacher and cannot be deleted.");

        _db.Subjects.Remove(subject);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}
