using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAssistant.Services;

namespace StudyAssistant.Controllers;

[ApiController]
[Route("api/teacher")]
[Authorize]
public class TeacherDashboardController : ControllerBase
{
    private readonly TeacherDashboardService _dashboard;

    public TeacherDashboardController(TeacherDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    // GET /api/teacher/classes
    [HttpGet("classes")]
    public async Task<IActionResult> GetMyClasses()
    {
        var role = User.FindFirstValue("role");
        if (role != "Teacher" && role != "SchoolAdmin")
            return StatusCode(403, new { error = "Access restricted to teachers and school administrators." });

        var teacherId = User.FindFirstValue("sub") ?? "";

        var results = await _dashboard.GetMyClassesAsync(teacherId);

        var response = results.Select(r => new
        {
            @class = new
            {
                id     = r.Class.Id,
                name   = r.Class.Name,
                school = r.Class.School
            },
            subjects = r.Subjects.Select(s => new
            {
                id     = s.Id,
                name   = s.Name,
                school = s.School
            }).ToList(),
            studentCount = r.StudentCount
        }).ToList();

        return Ok(response);
    }

    // GET /api/teacher/classes/{classId}/struggles?days=30
    [HttpGet("classes/{classId:int}/struggles")]
    public async Task<IActionResult> GetStrugglesByClass(int classId, [FromQuery] int days = 30)
    {
        var role = User.FindFirstValue("role");
        if (role != "Teacher" && role != "SchoolAdmin")
            return StatusCode(403, new { error = "Access restricted to teachers and school administrators." });

        if (days < 1)   days = 1;
        if (days > 365) days = 365;

        var teacherId = User.FindFirstValue("sub") ?? "";

        var results = await _dashboard.GetStrugglesByClassAsync(teacherId, classId, days);

        if (results.Count == 0)
            return NotFound(new { error = "Class not found or you are not assigned to it." });

        var response = results.Select(r => new
        {
            @class    = new { id = r.Class.Id, name = r.Class.Name, school = r.Class.School },
            subject   = new { id = r.Subject.Id, name = r.Subject.Name },
            topTopics = r.TopTopics.Select(t => new { topic = t.Topic, count = t.Count }).ToList()
        }).ToList();

        return Ok(response);
    }

    // GET /api/teacher/activity?days=30
    [HttpGet("activity")]
    public async Task<IActionResult> GetActiveStudentsByClass([FromQuery] int days = 30)
    {
        var role = User.FindFirstValue("role");
        if (role != "Teacher" && role != "SchoolAdmin")
            return StatusCode(403, new { error = "Access restricted to teachers and school administrators." });

        if (days < 1)   days = 1;
        if (days > 365) days = 365;

        var teacherId = User.FindFirstValue("sub") ?? "";

        var results = await _dashboard.GetActiveStudentsByClassAsync(teacherId, days);

        var response = results.Select(r => new
        {
            @class      = new { id = r.Class.Id, name = r.Class.Name, school = r.Class.School },
            topStudents = r.TopStudents.Select(s => new
            {
                student = new
                {
                    id       = s.Student.Id,
                    username = s.Student.UserName,
                    fullName = s.Student.FullName,
                    grade    = s.Student.Grade
                },
                questionCount = s.QuestionCount
            }).ToList()
        }).ToList();

        return Ok(response);
    }
}
