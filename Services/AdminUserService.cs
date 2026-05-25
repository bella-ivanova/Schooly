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

    public async Task AddClassAsync()
    {
        Console.Write("Име на клас (напр. 10А): ");
        var name = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Училище: ");
        var school = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Потребителско име на учителя: ");
        var teacherUsername = Console.ReadLine()?.Trim() ?? "";

        var teacher = await _users.GetByUsernameAsync(teacherUsername);
        if (teacher == null)
        {
            Console.WriteLine($"Потребителят '{teacherUsername}' не е намерен.");
            return;
        }

        _db.Classes.Add(new Class { Name = name, School = school, TeacherId = teacher.Id });
        await _db.SaveChangesAsync();
        Console.WriteLine($"Клас '{name}' е създаден.");
    }

    public async Task ListClassesAsync()
    {
        var classes = await _db.Classes
            .Include(c => c.Teacher)
            .Include(c => c.Students)
            .OrderBy(c => c.Name)
            .ToListAsync();

        if (classes.Count == 0)
        {
            Console.WriteLine("Няма регистрирани класове.");
            return;
        }

        foreach (var c in classes)
            Console.WriteLine($"  {c.Name} — {c.School} | Учител: {c.Teacher?.UserName ?? "?"} | Ученици: {c.Students.Count}");
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

        student.ClassId = cls.Id;
        student.School  = cls.School;
        await _users.UpdateAsync(student);
        Console.WriteLine($"Ученик '{studentUsername}' е добавен в клас '{className}'.");
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

        user.Role   = UserRole.SchoolAdmin;
        user.School = school;
        await _users.UpdateAsync(user);
        Console.WriteLine($"'{username}' е вече училищен администратор на '{school}'.");
    }

    public async Task CreateSubjectAsync()
    {
        Console.Write("Название на предмета: ");
        var name = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Училище: ");
        var school = Console.ReadLine()?.Trim() ?? "";

        var exists = await _db.Subjects.AnyAsync(s => s.Name == name && s.School == school);
        if (exists)
        {
            Console.WriteLine($"Предмет '{name}' вече съществува в '{school}'.");
            return;
        }

        _db.Subjects.Add(new Subject { Name = name, School = school });
        await _db.SaveChangesAsync();
        Console.WriteLine($"Предмет '{name}' е създаден в '{school}'.");
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

        _db.Subjects.Remove(subject);
        await _db.SaveChangesAsync();
        Console.WriteLine($"Предмет '{subject.Name}' е изтрит.");
    }

    public async Task DeleteClassAsync()
    {
        Console.Write("Клас за изтриване: ");
        var className = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Училище: ");
        var school = Console.ReadLine()?.Trim() ?? "";

        var cls = await _db.Classes
            .Include(c => c.Students)
            .FirstOrDefaultAsync(c => c.Name == className && c.School == school);

        if (cls == null)
        {
            Console.WriteLine($"Клас '{className}' не е намерен.");
            return;
        }

        foreach (var s in cls.Students)
            s.ClassId = null;

        _db.Classes.Remove(cls);
        await _db.SaveChangesAsync();
        Console.WriteLine($"Клас '{className}' е изтрит. Учениците са освободени.");
    }
}
