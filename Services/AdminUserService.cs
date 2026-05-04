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

        var student = await _users.GetByUsernameAsync(studentUsername);
        if (student == null)
        {
            Console.WriteLine($"Потребителят '{studentUsername}' не е намерен.");
            return;
        }

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Name == className);
        if (cls == null)
        {
            Console.WriteLine($"Клас '{className}' не е намерен.");
            return;
        }

        student.ClassId = cls.Id;
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
            var gradeTag = u.Grade.HasValue ? $"  Клас {u.Grade}" : "";
            var classTag = u.Class != null ? $" ({u.Class.Name})" : " (Без клас)";
            Console.WriteLine($"  [{u.Role}] {u.UserName} — {u.FullName}{gradeTag}{classTag}");
        }
    }

    public async Task DeleteClassAsync()
    {
        Console.Write("Клас за изтриване: ");
        var className = Console.ReadLine()?.Trim() ?? "";

        var cls = await _db.Classes
            .Include(c => c.Students)
            .FirstOrDefaultAsync(c => c.Name == className);

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
