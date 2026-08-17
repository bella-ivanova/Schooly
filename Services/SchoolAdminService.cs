using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public record ClassSummaryDto(int Id, string Name, string? HomeroomTeacherUsername, int StudentCount);
public record UserSummaryDto(string Id, string Username, string FullName, string Role, int? Grade, IReadOnlyList<string> ClassNames);
public record SubjectSummaryDto(int Id, string Name);
public record SchoolSummaryDto(int Id, string Name, DateTime CreatedAt, int StudentCount, int TeacherCount);
public record SchoolTeacherCodeDto(string Code, DateTime CreatedAt);

public class SchoolAdminService
{
    private readonly AppDbContext _db;
    private readonly IUserRepository _users;

    public SchoolAdminService(AppDbContext db, IUserRepository users)
    {
        _db    = db;
        _users = users;
    }

    public async Task<(bool Success, string? Error)> AddClassAsync(int schoolId, string name, string? homeroomTeacherId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Class name is required.");

        if (homeroomTeacherId != null)
        {
            var teacher = await _users.GetByIdAsync(homeroomTeacherId);
            if (teacher == null)
                return (false, "Teacher not found.");
            if (teacher.SchoolId != schoolId)
                return (false, "Teacher does not belong to this school.");
        }

        _db.Classes.Add(new Class { Name = name, SchoolId = schoolId, HomeroomTeacherId = homeroomTeacherId });
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
            .Where(c => c.SchoolId == schoolId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return classes
            .Select(c => new ClassSummaryDto(c.Id, c.Name, c.HomeroomTeacher?.UserName, c.ClassStudents.Count))
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

    public async Task<(bool Success, string? Error)> AssignStudentAsync(int schoolId, int classId, string userId)
    {
        var student = await _users.GetByIdAsync(userId);
        if (student == null)
            return (false, "User not found.");
        if (student.Role != UserRole.Student)
            return (false, "User is not a student.");
        if (student.SchoolId != null && student.SchoolId != schoolId)
            return (false, "Student belongs to a different school.");

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == classId && c.SchoolId == schoolId);
        if (cls == null)
            return (false, "Class not found.");

        var alreadyMember = await _db.ClassStudents
            .AnyAsync(cs => cs.ClassId == classId && cs.StudentId == userId);
        if (alreadyMember)
            return (false, "Student is already in this class.");

        _db.ClassStudents.Add(new ClassStudent { ClassId = classId, StudentId = userId });
        if (student.SchoolId == null)
            student.SchoolId = schoolId;
        await _users.UpdateAsync(student);
        await _db.SaveChangesAsync();
        return (true, null);
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

        return users
            .Select(u => new UserSummaryDto(
                u.Id,
                u.UserName ?? "",
                u.FullName,
                u.Role.ToString(),
                u.Grade,
                grouped.TryGetValue(u.Id, out var names) ? names : Array.Empty<string>()))
            .ToList();
    }

    public async Task<IReadOnlyList<SubjectSummaryDto>> ListSubjectsAsync(int schoolId)
    {
        var subjects = await _db.Subjects
            .Where(s => s.SchoolId == schoolId)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return subjects
            .Select(s => new SubjectSummaryDto(s.Id, s.Name))
            .ToList();
    }
}
