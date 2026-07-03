using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public class AdminUserService
{
    private readonly AppDbContext _db;
    private readonly IUserRepository _users;
    private readonly RAGService _rag;

    public AdminUserService(AppDbContext db, IUserRepository users, RAGService rag)
    {
        _db    = db;
        _users = users;
        _rag   = rag;
    }

    // ── Curriculum Files (Global, RAG-backed) ───────────────────────────────────

    public async Task<List<string>> ListCurriculumFilesAsync(int grade) =>
        await _rag.GetIngestedFilesAsync(grade);

    public async Task<(bool Success, string? Error, int ChunkCount)> UploadCurriculumFileAsync(
        int grade, string subject, string fileName, Stream content, bool overwrite) =>
        await _rag.IngestUploadedFileAsync(grade, subject, fileName, content, overwrite);

    public async Task<bool> DeleteCurriculumFileAsync(int grade, string fileKey) =>
        await _rag.DeleteGradeFileAsync(grade, fileKey);

    // ── School ────────────────────────────────────────────────────────────────

    private async Task<bool> SchoolExistsAsync(int schoolId) =>
        await _db.Schools.AnyAsync(s => s.Id == schoolId);

    private async Task<School?> FindSchoolByNameAsync(string name) =>
        await _db.Schools.FirstOrDefaultAsync(s => s.Name == name);

    public async Task<(bool Success, string? Error)> CreateSchoolAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "School name is required.");

        if (await _db.Schools.AnyAsync(s => s.Name == name))
            return (false, $"A school named '{name}' already exists.");

        _db.Schools.Add(new School { Name = name });
        await _db.SaveChangesAsync();
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
        int schoolId, string name, string? homeroomTeacherId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Class name is required.");
        if (!await SchoolExistsAsync(schoolId))
            return (false, "School not found.");

        if (homeroomTeacherId != null)
        {
            var teacher = await _users.GetByIdAsync(homeroomTeacherId);
            if (teacher == null)
                return (false, "Teacher not found.");
        }

        _db.Classes.Add(new Class { Name = name, SchoolId = schoolId, HomeroomTeacherId = homeroomTeacherId });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task AddClassAsync()
    {
        Console.Write("Име на клас (напр. 10А): ");
        var name = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Училище: ");
        var schoolName = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Потребителско име на класния ръководител (Enter за пропускане): ");
        var teacherUsername = Console.ReadLine()?.Trim() ?? "";

        var school = await FindSchoolByNameAsync(schoolName);
        if (school == null) { Console.WriteLine($"Училище '{schoolName}' не е намерено."); return; }

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

        var (success, error) = await AddClassAsync(school.Id, name, homeroomTeacherId);
        Console.WriteLine(success ? $"Клас '{name}' е създаден." : error);
    }

    public async Task<IReadOnlyList<ClassSummaryDto>> ListClassesAsync(int? schoolId = null)
    {
        var query = _db.Classes
            .Include(c => c.HomeroomTeacher)
            .Include(c => c.Students)
            .AsQueryable();

        if (schoolId != null)
            query = query.Where(c => c.SchoolId == schoolId);

        var classes = await query.OrderBy(c => c.Name).ToListAsync();

        return classes
            .Select(c => new ClassSummaryDto(c.Id, c.Name, c.HomeroomTeacher?.UserName, c.Students.Count))
            .ToList();
    }

    public async Task ListClassesAsync()
    {
        var classes = await _db.Classes
            .Include(c => c.School)
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
            Console.WriteLine($"  {c.Name} — {c.School?.Name} | Класен: {c.HomeroomTeacher?.UserName ?? "—"} | Ученици: {c.Students.Count}");
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
        var schoolName = Console.ReadLine()?.Trim() ?? "";

        var school = await FindSchoolByNameAsync(schoolName);
        if (school == null) { Console.WriteLine($"Училище '{schoolName}' не е намерено."); return; }

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Name == className && c.SchoolId == school.Id);
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

        student.ClassId  = cls.Id;
        student.SchoolId = cls.SchoolId;
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
        var schoolName = Console.ReadLine()?.Trim() ?? "";

        var student = await _users.GetByUsernameAsync(studentUsername);
        if (student == null)
        {
            Console.WriteLine($"Потребителят '{studentUsername}' не е намерен.");
            return;
        }

        var school = await FindSchoolByNameAsync(schoolName);
        if (school == null) { Console.WriteLine($"Училище '{schoolName}' не е намерено."); return; }

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Name == className && c.SchoolId == school.Id);
        if (cls == null)
        {
            Console.WriteLine($"Клас '{className}' не е намерен.");
            return;
        }

        var (success, error) = await AssignStudentToClassAsync(student.Id, cls.Id);
        Console.WriteLine(success ? $"Ученик '{studentUsername}' е добавен в клас '{className}'." : error);
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<UserSummaryDto>> ListUsersAsync(int? schoolId = null)
    {
        var query = _db.Set<ApplicationUser>()
            .Include(u => u.Class)
            .AsQueryable();

        if (schoolId != null)
            query = query.Where(u => u.SchoolId == schoolId);

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
            .Include(u => u.School)
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
            var schoolTag = u.SchoolId != null ? $" | {u.School?.Name}" : "";
            Console.WriteLine($"  [{u.Role}] {u.UserName} — {u.FullName}{gradeTag}{classTag}{schoolTag}");
        }
    }

    public async Task<(bool Success, string? Error)> MakeSchoolAdminAsync(string userId, int schoolId)
    {
        if (!await SchoolExistsAsync(schoolId))
            return (false, "School not found.");

        var user = await _users.GetByIdAsync(userId);
        if (user == null)
            return (false, "User not found.");

        user.Role     = UserRole.SchoolAdmin;
        user.SchoolId = schoolId;
        await _users.UpdateAsync(user);
        return (true, null);
    }

    public async Task MakeSchoolAdminAsync()
    {
        Console.Write("Потребителско име: ");
        var username = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Училище: ");
        var schoolName = Console.ReadLine()?.Trim() ?? "";

        var user = await _users.GetByUsernameAsync(username);
        if (user == null)
        {
            Console.WriteLine($"Потребителят '{username}' не е намерен.");
            return;
        }

        var school = await FindSchoolByNameAsync(schoolName);
        if (school == null) { Console.WriteLine($"Училище '{schoolName}' не е намерено."); return; }

        var (success, error) = await MakeSchoolAdminAsync(user.Id, school.Id);
        Console.WriteLine(success ? $"'{username}' е вече училищен администратор на '{schoolName}'." : error);
    }

    // ── Teachers ──────────────────────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> AssignTeacherToClassAsync(
        int schoolId, int classId, string teacherId, string subjectName)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return (false, "Subject name is required.");
        if (!await SchoolExistsAsync(schoolId))
            return (false, "School not found.");

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == classId && c.SchoolId == schoolId);
        if (cls == null)
            return (false, "Class not found.");

        var teacher = await _users.GetByIdAsync(teacherId);
        if (teacher == null)
            return (false, "Teacher not found.");

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

    public async Task AssignTeacherToClassAsync()
    {
        Console.Write("Клас (напр. 10А): ");
        var className = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Училище: ");
        var schoolName = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Потребителско име на учителя: ");
        var teacherUsername = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Предмет: ");
        var subjectName = Console.ReadLine()?.Trim() ?? "";

        var school = await FindSchoolByNameAsync(schoolName);
        if (school == null) { Console.WriteLine($"Училище '{schoolName}' не е намерено."); return; }

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Name == className && c.SchoolId == school.Id);
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

        var (success, error) = await AssignTeacherToClassAsync(school.Id, cls.Id, teacher.Id, subjectName);
        Console.WriteLine(success
            ? $"Учител '{teacherUsername}' е назначен на '{subjectName}' в клас '{className}'."
            : error);
    }

    // ── Subjects ──────────────────────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> CreateSubjectAsync(int schoolId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Subject name is required.");
        if (!await SchoolExistsAsync(schoolId))
            return (false, "School not found.");

        var exists = await _db.Subjects.AnyAsync(s => s.Name == name && s.SchoolId == schoolId);
        if (exists)
            return (false, $"Subject '{name}' already exists in this school.");

        _db.Subjects.Add(new Subject { Name = name, SchoolId = schoolId });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task CreateSubjectAsync()
    {
        Console.Write("Название на предмета: ");
        var name = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Училище: ");
        var schoolName = Console.ReadLine()?.Trim() ?? "";

        var school = await FindSchoolByNameAsync(schoolName);
        if (school == null) { Console.WriteLine($"Училище '{schoolName}' не е намерено."); return; }

        var (success, error) = await CreateSubjectAsync(school.Id, name);
        Console.WriteLine(success ? $"Предмет '{name}' е създаден в '{schoolName}'." : error);
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
        var schoolName = Console.ReadLine()?.Trim() ?? "";

        var school = await FindSchoolByNameAsync(schoolName);
        if (school == null) { Console.WriteLine($"Училище '{schoolName}' не е намерено."); return; }

        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId && s.SchoolId == school.Id);
        if (subject == null)
        {
            Console.WriteLine($"Предмет с ID {subjectId} не е намерен в '{schoolName}'.");
            return;
        }

        var (success, error) = await DeleteSubjectAsync(subject.Id);
        Console.WriteLine(success ? $"Предмет '{subject.Name}' е изтрит." : error);
    }
}
