using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public class AdminUserService
{
    private readonly AppDbContext _db;
    private readonly IUserRepository _users;

    public AdminUserService(AppDbContext db, IUserRepository users)
    {
        _db    = db;
        _users = users;
    }

    // ── School ────────────────────────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> CreateSchoolAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "School name is required.");

        var alreadyExists = await _db.Classes.AnyAsync(c => c.School == name)
                         || await _db.Set<ApplicationUser>().AnyAsync(u => u.School == name);
        if (alreadyExists)
            return (false, $"A school named '{name}' already exists.");

        return (true, null);
    }

    public async Task CreateSchoolAsync()
    {
        Console.Write("Название на училището: ");
        var name = Console.ReadLine()?.Trim() ?? "";
        var (success, error) = await CreateSchoolAsync(name);
        Console.WriteLine(success ? $"Училище '{name}' е регистрирано." : error);
    }

    // ── Classes ───────────────────────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> AddClassAsync(
        string school, string name, string? homeroomTeacherId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Class name is required.");
        if (string.IsNullOrWhiteSpace(school))
            return (false, "School name is required.");

        if (homeroomTeacherId != null)
        {
            var teacher = await _users.GetByIdAsync(homeroomTeacherId);
            if (teacher == null)
                return (false, "Teacher not found.");
        }

        _db.Classes.Add(new Class { Name = name, School = school, HomeroomTeacherId = homeroomTeacherId });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task AddClassAsync()
    {
        Console.Write("Име на клас (напр. 10А): ");
        var name = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Училище: ");
        var school = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Потребителско име на класния ръководител (Enter за пропускане): ");
        var teacherUsername = Console.ReadLine()?.Trim() ?? "";

        string? homeroomTeacherId = null;
        if (!string.IsNullOrWhiteSpace(teacherUsername))
        {
            var teacher = await _users.GetByUsernameAsync(teacherUsername);
            if (teacher == null)
            {
                Console.WriteLine($"Потребителят '{teacherUsername}' не е намерен.");
                return;
            }
            homeroomTeacherId = teacher.Id;
        }

        var (success, error) = await AddClassAsync(school, name, homeroomTeacherId);
        Console.WriteLine(success ? $"Клас '{name}' е създаден." : error);
    }

    public async Task<IReadOnlyList<ClassSummaryDto>> ListClassesAsync(string? school = null)
    {
        var query = _db.Classes
            .Include(c => c.HomeroomTeacher)
            .Include(c => c.Students)
            .AsQueryable();

        if (school != null)
            query = query.Where(c => c.School == school);

        var classes = await query.OrderBy(c => c.Name).ToListAsync();

        return classes
            .Select(c => new ClassSummaryDto(c.Id, c.Name, c.HomeroomTeacher?.UserName, c.Students.Count))
            .ToList();
    }

    public async Task ListClassesAsync()
    {
        var classes = await _db.Classes
            .Include(c => c.HomeroomTeacher)
            .Include(c => c.Students)
            .OrderBy(c => c.Name)
            .ToListAsync();

        if (classes.Count == 0)
        {
            Console.WriteLine("Няма регистрирани класове.");
            return;
        }

        foreach (var c in classes)
            Console.WriteLine($"  {c.Name} — {c.School} | Класен: {c.HomeroomTeacher?.UserName ?? "—"} | Ученици: {c.Students.Count}");
    }

    public async Task<(bool Success, string? Error)> DeleteClassAsync(int classId)
    {
        var cls = await _db.Classes
            .Include(c => c.Students)
            .FirstOrDefaultAsync(c => c.Id == classId);

        if (cls == null)
            return (false, "Class not found.");

        foreach (var s in cls.Students)
            s.ClassId = null;

        _db.Classes.Remove(cls);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task DeleteClassAsync()
    {
        Console.Write("Клас за изтриване: ");
        var className = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Училище: ");
        var school = Console.ReadLine()?.Trim() ?? "";

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Name == className && c.School == school);
        if (cls == null)
        {
            Console.WriteLine($"Клас '{className}' не е намерен.");
            return;
        }

        var (success, error) = await DeleteClassAsync(cls.Id);
        Console.WriteLine(success ? $"Клас '{className}' е изтрит. Учениците са освободени." : error);
    }

    // ── Students ──────────────────────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> AssignStudentToClassAsync(string studentId, int classId)
    {
        var student = await _users.GetByIdAsync(studentId);
        if (student == null)
            return (false, "User not found.");
        if (student.Role != UserRole.Student)
            return (false, "User is not a student.");

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == classId);
        if (cls == null)
            return (false, "Class not found.");

        student.ClassId = cls.Id;
        student.School  = cls.School;
        await _users.UpdateAsync(student);
        return (true, null);
    }

    public async Task AssignStudentAsync()
    {
        Console.Write("Потребителско име на ученика: ");
        var studentUsername = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Клас (напр. 10А): ");
        var className = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Училище: ");
        var school = Console.ReadLine()?.Trim() ?? "";

        var student = await _users.GetByUsernameAsync(studentUsername);
        if (student == null)
        {
            Console.WriteLine($"Потребителят '{studentUsername}' не е намерен.");
            return;
        }

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Name == className && c.School == school);
        if (cls == null)
        {
            Console.WriteLine($"Клас '{className}' не е намерен.");
            return;
        }

        var (success, error) = await AssignStudentToClassAsync(student.Id, cls.Id);
        Console.WriteLine(success ? $"Ученик '{studentUsername}' е добавен в клас '{className}'." : error);
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<UserSummaryDto>> ListUsersAsync(string? school = null)
    {
        var query = _db.Set<ApplicationUser>()
            .Include(u => u.Class)
            .AsQueryable();

        if (school != null)
            query = query.Where(u => u.School == school);

        var users = await query
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

    public async Task ListUsersAsync()
    {
        var users = await _db.Set<ApplicationUser>()
            .Include(u => u.Class)
            .OrderBy(u => u.Role)
            .ThenBy(u => u.UserName)
            .ToListAsync();

        if (users.Count == 0)
        {
            Console.WriteLine("Няма регистрирани потребители.");
            return;
        }

        foreach (var u in users)
        {
            var gradeTag  = u.Grade.HasValue ? $"  Клас {u.Grade}" : "";
            var classTag  = u.Class != null ? $" ({u.Class.Name})" : " (Без клас)";
            var schoolTag = u.School != null ? $" | {u.School}" : "";
            Console.WriteLine($"  [{u.Role}] {u.UserName} — {u.FullName}{gradeTag}{classTag}{schoolTag}");
        }
    }

    public async Task<(bool Success, string? Error)> MakeSchoolAdminAsync(string userId, string school)
    {
        if (string.IsNullOrWhiteSpace(school))
            return (false, "School name is required.");

        var user = await _users.GetByIdAsync(userId);
        if (user == null)
            return (false, "User not found.");

        user.Role   = UserRole.SchoolAdmin;
        user.School = school;
        await _users.UpdateAsync(user);
        return (true, null);
    }

    public async Task MakeSchoolAdminAsync()
    {
        Console.Write("Потребителско име: ");
        var username = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Училище: ");
        var school = Console.ReadLine()?.Trim() ?? "";

        var user = await _users.GetByUsernameAsync(username);
        if (user == null)
        {
            Console.WriteLine($"Потребителят '{username}' не е намерен.");
            return;
        }

        var (success, error) = await MakeSchoolAdminAsync(user.Id, school);
        Console.WriteLine(success ? $"'{username}' е вече училищен администратор на '{school}'." : error);
    }

    // ── Teachers ──────────────────────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> AssignTeacherToClassAsync(
        string school, int classId, string teacherId, string subjectName)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return (false, "Subject name is required.");

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == classId && c.School == school);
        if (cls == null)
            return (false, "Class not found.");

        var teacher = await _users.GetByIdAsync(teacherId);
        if (teacher == null)
            return (false, "Teacher not found.");

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

    public async Task AssignTeacherToClassAsync()
    {
        Console.Write("Клас (напр. 10А): ");
        var className = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Училище: ");
        var school = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Потребителско име на учителя: ");
        var teacherUsername = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Предмет: ");
        var subjectName = Console.ReadLine()?.Trim() ?? "";

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Name == className && c.School == school);
        if (cls == null)
        {
            Console.WriteLine($"Клас '{className}' не е намерен.");
            return;
        }

        var teacher = await _users.GetByUsernameAsync(teacherUsername);
        if (teacher == null)
        {
            Console.WriteLine($"Потребителят '{teacherUsername}' не е намерен.");
            return;
        }

        var (success, error) = await AssignTeacherToClassAsync(school, cls.Id, teacher.Id, subjectName);
        Console.WriteLine(success
            ? $"Учител '{teacherUsername}' е назначен на '{subjectName}' в клас '{className}'."
            : error);
    }

    // ── Subjects ──────────────────────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> CreateSubjectAsync(string school, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Subject name is required.");
        if (string.IsNullOrWhiteSpace(school))
            return (false, "School name is required.");

        var exists = await _db.Subjects.AnyAsync(s => s.Name == name && s.School == school);
        if (exists)
            return (false, $"Subject '{name}' already exists in '{school}'.");

        _db.Subjects.Add(new Subject { Name = name, School = school });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task CreateSubjectAsync()
    {
        Console.Write("Название на предмета: ");
        var name = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Училище: ");
        var school = Console.ReadLine()?.Trim() ?? "";

        var (success, error) = await CreateSubjectAsync(school, name);
        Console.WriteLine(success ? $"Предмет '{name}' е създаден в '{school}'." : error);
    }

    public async Task<(bool Success, string? Error)> DeleteSubjectAsync(int subjectId)
    {
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId);
        if (subject == null)
            return (false, $"Subject with ID {subjectId} not found.");

        _db.Subjects.Remove(subject);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task DeleteSubjectAsync()
    {
        Console.Write("ID на предмета: ");
        if (!int.TryParse(Console.ReadLine()?.Trim(), out var subjectId))
        {
            Console.WriteLine("Невалидно ID.");
            return;
        }
        Console.Write("Училище: ");
        var school = Console.ReadLine()?.Trim() ?? "";

        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId && s.School == school);
        if (subject == null)
        {
            Console.WriteLine($"Предмет с ID {subjectId} не е намерен в '{school}'.");
            return;
        }

        var (success, error) = await DeleteSubjectAsync(subject.Id);
        Console.WriteLine(success ? $"Предмет '{subject.Name}' е изтрит." : error);
    }
}
