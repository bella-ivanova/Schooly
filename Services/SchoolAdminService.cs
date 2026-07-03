using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public record ClassSummaryDto(int Id, string Name, string? HomeroomTeacherUsername, int StudentCount);
public record UserSummaryDto(string Id, string Username, string FullName, string Role, int? Grade, string? ClassName);
public record SubjectSummaryDto(int Id, string Name);

public class SchoolAdminService
{
    private readonly AppDbContext _db;
    private readonly IUserRepository _users;

    public SchoolAdminService(AppDbContext db, IUserRepository users)
    {
        _db    = db;
        _users = users;
    }

    public async Task<(bool Success, string? Error)> AddClassAsync(string school, string name, string? homeroomTeacherId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Class name is required.");

        if (homeroomTeacherId != null)
        {
            var teacher = await _users.GetByIdAsync(homeroomTeacherId);
            if (teacher == null)
                return (false, "Teacher not found.");
            if (teacher.School != school)
                return (false, "Teacher does not belong to this school.");
        }

        _db.Classes.Add(new Class { Name = name, School = school, HomeroomTeacherId = homeroomTeacherId });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<IReadOnlyList<ClassSummaryDto>> ListClassesAsync(string school)
    {
        var classes = await _db.Classes
            .Include(c => c.HomeroomTeacher)
            .Include(c => c.Students)
            .Where(c => c.School == school)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return classes
            .Select(c => new ClassSummaryDto(c.Id, c.Name, c.HomeroomTeacher?.UserName, c.Students.Count))
            .ToList();
    }

    public async Task<(bool Success, string? Error)> AssignTeacherAsync(string school, int classId, string teacherId)
    {
        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == classId && c.School == school);
        if (cls == null)
            return (false, "Class not found.");

        var teacher = await _users.GetByIdAsync(teacherId);
        if (teacher == null)
            return (false, "Teacher not found.");
        if (teacher.School != school)
            return (false, "Teacher does not belong to this school.");

        cls.HomeroomTeacherId = teacher.Id;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AssignStudentAsync(string school, int classId, string userId)
    {
        var student = await _users.GetByIdAsync(userId);
        if (student == null)
            return (false, "User not found.");
        if (student.Role != UserRole.Student)
            return (false, "User is not a student.");
        if (!string.IsNullOrEmpty(student.School) && student.School != school)
            return (false, "Student belongs to a different school.");

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == classId && c.School == school);
        if (cls == null)
            return (false, "Class not found.");

        student.ClassId = cls.Id;
        student.School  = school;
        await _users.UpdateAsync(student);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemoveStudentAsync(string school, string userId)
    {
        var student = await _users.GetByIdAsync(userId);
        if (student == null)
            return (false, "User not found.");
        if (student.School != school)
            return (false, "Student does not belong to this school.");

        student.ClassId = null;
        await _users.UpdateAsync(student);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AssignTeacherToClassAsync(string school, int classId, string teacherId, string subjectName)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return (false, "Subject name is required.");

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == classId && c.School == school);
        if (cls == null)
            return (false, "Class not found.");

        var teacher = await _users.GetByIdAsync(teacherId);
        if (teacher == null)
            return (false, "Teacher not found.");
        if (teacher.School != school)
            return (false, "Teacher does not belong to this school.");

        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Name == subjectName && s.School == school);
        if (subject == null)
        {
            subject = new Subject { Name = subjectName, School = school };
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

    public async Task<(bool Success, string? Error)> AssignSubjectToTeacherAsync(string school, string teacherId, int subjectId)
    {
        var teacher = await _users.GetByIdAsync(teacherId);
        if (teacher == null)
            return (false, "Teacher not found.");
        if (teacher.School != school)
            return (false, "Teacher does not belong to this school.");

        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId && s.School == school);
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

    public async Task<(bool Success, string? Error)> RemoveSubjectFromTeacherAsync(string school, string teacherId, int subjectId)
    {
        var teacher = await _users.GetByIdAsync(teacherId);
        if (teacher == null)
            return (false, "Teacher not found.");
        if (teacher.School != school)
            return (false, "Teacher does not belong to this school.");

        var row = await _db.TeacherSubjects
            .FirstOrDefaultAsync(ts => ts.TeacherId == teacher.Id && ts.SubjectId == subjectId);
        if (row == null)
            return (false, "Assignment not found.");

        _db.TeacherSubjects.Remove(row);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<IReadOnlyList<UserSummaryDto>> ListUsersAsync(string school)
    {
        var users = await _db.Set<ApplicationUser>()
            .Include(u => u.Class)
            .Where(u => u.School == school)
            .OrderBy(u => u.Role)
            .ThenBy(u => u.UserName)
            .ToListAsync();

        return users
            .Select(u => new UserSummaryDto(
                u.Id,
                u.UserName ?? "",
                u.FullName,
                u.Role.ToString(),
                u.Grade,
                u.Class?.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<SubjectSummaryDto>> ListSubjectsAsync(string school)
    {
        var subjects = await _db.Subjects
            .Where(s => s.School == school)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return subjects
            .Select(s => new SubjectSummaryDto(s.Id, s.Name))
            .ToList();
    }
}
