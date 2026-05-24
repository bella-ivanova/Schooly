using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public class SchoolAdminService
{
    private readonly AppDbContext _db;
    private readonly IUserRepository _users;
    private readonly string _school;

    public SchoolAdminService(AppDbContext db, IUserRepository users, string school)
    {
        _db     = db;
        _users  = users;
        _school = school;
    }

    public async Task AddClassAsync()
    {
        Console.Write("Име на клас (напр. 10А): ");
        var name = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Потребителско име на учителя: ");
        var teacherUsername = Console.ReadLine()?.Trim() ?? "";

        var teacher = await _users.GetByUsernameAsync(teacherUsername);
        if (teacher == null)
        {
            Console.WriteLine($"Потребителят '{teacherUsername}' не е намерен.");
            return;
        }

        if (teacher.School != _school)
        {
            Console.WriteLine($"Учителят не принадлежи към училище '{_school}'.");
            return;
        }

        _db.Classes.Add(new Class { Name = name, School = _school, TeacherId = teacher.Id });
        await _db.SaveChangesAsync();
        Console.WriteLine($"Клас '{name}' е създаден в '{_school}'.");
    }

    public async Task ListClassesAsync()
    {
        var classes = await _db.Classes
            .Include(c => c.Teacher)
            .Include(c => c.Students)
            .Where(c => c.School == _school)
            .OrderBy(c => c.Name)
            .ToListAsync();

        if (classes.Count == 0)
        {
            Console.WriteLine($"Няма класове в '{_school}'.");
            return;
        }

        foreach (var c in classes)
            Console.WriteLine($"  {c.Name} | Учител: {c.Teacher?.UserName ?? "?"} | Ученици: {c.Students.Count}");
    }

    public async Task AssignTeacherAsync()
    {
        Console.Write("Клас (напр. 10А): ");
        var className = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Потребителско име на учителя: ");
        var teacherUsername = Console.ReadLine()?.Trim() ?? "";

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Name == className && c.School == _school);
        if (cls == null)
        {
            Console.WriteLine($"Клас '{className}' не е намерен в '{_school}'.");
            return;
        }

        var teacher = await _users.GetByUsernameAsync(teacherUsername);
        if (teacher == null)
        {
            Console.WriteLine($"Потребителят '{teacherUsername}' не е намерен.");
            return;
        }

        if (teacher.School != _school)
        {
            Console.WriteLine($"Учителят не принадлежи към училище '{_school}'.");
            return;
        }

        cls.TeacherId = teacher.Id;
        await _db.SaveChangesAsync();
        Console.WriteLine($"Учител '{teacherUsername}' е назначен на клас '{className}'.");
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

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Name == className && c.School == _school);
        if (cls == null)
        {
            Console.WriteLine($"Клас '{className}' не е намерен в '{_school}'.");
            return;
        }

        student.ClassId = cls.Id;
        student.School  = _school;
        await _users.UpdateAsync(student);
        Console.WriteLine($"Ученик '{studentUsername}' е добавен в клас '{className}'.");
    }

    public async Task RemoveStudentAsync()
    {
        Console.Write("Потребителско име на ученика: ");
        var studentUsername = Console.ReadLine()?.Trim() ?? "";

        var student = await _users.GetByUsernameAsync(studentUsername);
        if (student == null)
        {
            Console.WriteLine($"Потребителят '{studentUsername}' не е намерен.");
            return;
        }

        if (student.School != _school)
        {
            Console.WriteLine("Ученикът не принадлежи към твоето училище.");
            return;
        }

        student.ClassId = null;
        await _users.UpdateAsync(student);
        Console.WriteLine($"Ученик '{studentUsername}' е премахнат от класа си.");
    }

    public async Task ListUsersAsync()
    {
        var users = await _db.Set<ApplicationUser>()
            .Include(u => u.Class)
            .Where(u => u.School == _school)
            .OrderBy(u => u.Role)
            .ThenBy(u => u.UserName)
            .ToListAsync();

        if (users.Count == 0)
        {
            Console.WriteLine($"Няма потребители в '{_school}'.");
            return;
        }

        foreach (var u in users)
        {
            var gradeTag = u.Grade.HasValue ? $"  Клас {u.Grade}" : "";
            var classTag = u.Class != null ? $" ({u.Class.Name})" : " (Без клас)";
            Console.WriteLine($"  [{u.Role}] {u.UserName} — {u.FullName}{gradeTag}{classTag}");
        }
    }
}
