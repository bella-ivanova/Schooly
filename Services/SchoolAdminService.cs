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

            if (teacher.School != _school)
            {
                Console.WriteLine($"Учителят не принадлежи към училище '{_school}'.");
                return;
            }

            homeroomTeacherId = teacher.Id;
        }

        _db.Classes.Add(new Class { Name = name, School = _school, HomeroomTeacherId = homeroomTeacherId });
        await _db.SaveChangesAsync();
        Console.WriteLine($"Клас '{name}' е създаден в '{_school}'.");
    }

    public async Task ListClassesAsync()
    {
        var classes = await _db.Classes
            .Include(c => c.HomeroomTeacher)
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
            Console.WriteLine($"  {c.Name} | Класен: {c.HomeroomTeacher?.UserName ?? "—"} | Ученици: {c.Students.Count}");
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

        cls.HomeroomTeacherId = teacher.Id;
        await _db.SaveChangesAsync();
        Console.WriteLine($"Учител '{teacherUsername}' е назначен за класен ръководител на '{className}'.");
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

    public async Task AssignTeacherToClassAsync()
    {
        Console.Write("Клас (напр. 10А): ");
        var className = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Потребителско име на учителя: ");
        var teacherUsername = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Предмет: ");
        var subjectName = Console.ReadLine()?.Trim() ?? "";

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

        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Name == subjectName && s.School == _school);
        if (subject == null)
        {
            subject = new Subject { Name = subjectName, School = _school };
            _db.Subjects.Add(subject);
            await _db.SaveChangesAsync();
        }

        var existing = await _db.ClassTeachers.FirstOrDefaultAsync(
            ct => ct.ClassId == cls.Id && ct.TeacherId == teacher.Id && ct.SubjectId == subject.Id);
        if (existing != null)
        {
            Console.WriteLine($"Учителят вече преподава '{subjectName}' в клас '{className}'.");
            return;
        }

        _db.ClassTeachers.Add(new ClassTeacher { ClassId = cls.Id, TeacherId = teacher.Id, SubjectId = subject.Id });
        await _db.SaveChangesAsync();
        Console.WriteLine($"Учител '{teacherUsername}' е назначен на '{subjectName}' в клас '{className}'.");
    }

    public async Task AssignSubjectToTeacherAsync()
    {
        Console.Write("Потребителско име на учителя: ");
        var teacherUsername = Console.ReadLine()?.Trim() ?? "";
        Console.Write("ID на предмета: ");
        if (!int.TryParse(Console.ReadLine()?.Trim(), out var subjectId))
        {
            Console.WriteLine("Невалидно ID.");
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

        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId && s.School == _school);
        if (subject == null)
        {
            Console.WriteLine($"Предмет с ID {subjectId} не е намерен в '{_school}'.");
            return;
        }

        var alreadyAssigned = await _db.TeacherSubjects
            .AnyAsync(ts => ts.TeacherId == teacher.Id && ts.SubjectId == subjectId);
        if (alreadyAssigned)
        {
            Console.WriteLine($"Предметът вече е назначен на '{teacherUsername}'.");
            return;
        }

        _db.TeacherSubjects.Add(new TeacherSubject { TeacherId = teacher.Id, SubjectId = subjectId });
        await _db.SaveChangesAsync();
        Console.WriteLine($"Предмет '{subject.Name}' е назначен на '{teacherUsername}'.");
    }

    public async Task RemoveSubjectFromTeacherAsync()
    {
        Console.Write("Потребителско име на учителя: ");
        var teacherUsername = Console.ReadLine()?.Trim() ?? "";
        Console.Write("ID на предмета: ");
        if (!int.TryParse(Console.ReadLine()?.Trim(), out var subjectId))
        {
            Console.WriteLine("Невалидно ID.");
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

        var row = await _db.TeacherSubjects
            .FirstOrDefaultAsync(ts => ts.TeacherId == teacher.Id && ts.SubjectId == subjectId);
        if (row == null)
        {
            Console.WriteLine("Връзката не е намерена.");
            return;
        }

        _db.TeacherSubjects.Remove(row);
        await _db.SaveChangesAsync();
        Console.WriteLine($"Предметът е премахнат от '{teacherUsername}'.");
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

    public async Task ListSubjectsAsync()
    {
        var subjects = await _db.Subjects
            .Where(s => s.School == _school)
            .OrderBy(s => s.Name)
            .ToListAsync();

        if (subjects.Count == 0)
        {
            Console.WriteLine($"Няма предмети в '{_school}'.");
            return;
        }

        Console.WriteLine($"Предмети в '{_school}':");
        foreach (var s in subjects)
            Console.WriteLine($"  [{s.Id}] {s.Name}");
    }
}
