using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAssistant.Services;

namespace StudyAssistant.Controllers;

[ApiController]
[Route("api/student")]
[Authorize]
public class StudentController : ControllerBase
{
    private readonly PracticeQuestionService _practiceQuestions;
    private readonly ChatLogService _chatLog;
    private readonly ExamService _examService;
    private readonly AuthService _auth;
    private readonly RateLimiter _rateLimiter;

    public StudentController(
        PracticeQuestionService practiceQuestions, ChatLogService chatLog, ExamService examService,
        AuthService auth, RateLimiter rateLimiter)
    {
        _practiceQuestions = practiceQuestions;
        _chatLog           = chatLog;
        _examService       = examService;
        _auth              = auth;
        _rateLimiter       = rateLimiter;
    }

    private IActionResult? RequireStudentRole()
    {
        var role = User.FindFirstValue("role");
        if (role != "Student")
            return StatusCode(403, new { error = "Access restricted to students." });
        return null;
    }

    // POST /api/student/practice-questions
    [HttpPost("practice-questions")]
    public async Task<IActionResult> GetPracticeQuestions([FromBody] PracticeQuestionsRequest body)
    {
        var reject = RequireStudentRole();
        if (reject != null) return reject;

        var questions = await _practiceQuestions.GenerateAsync(body.OriginalQuestion, body.AiResponse);
        return Ok(new { questions });
    }

    // GET /api/student/weak-spots
    [HttpGet("weak-spots")]
    public async Task<IActionResult> GetWeakSpots([FromQuery] int days = 7)
    {
        var reject = RequireStudentRole();
        if (reject != null) return reject;

        if (days < 1) days = 1;
        if (days > 365) days = 365;

        var userId = User.FindFirstValue("sub") ?? "";
        var results = await _chatLog.GetWeakSpotsAsync(userId, days);

        var response = results.Select(r => new { topic = r.Topic, subject = r.Subject, count = r.Count });
        return Ok(response);
    }

    // GET /api/student/history
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int limit = 50)
    {
        var reject = RequireStudentRole();
        if (reject != null) return reject;

        if (limit < 1) limit = 1;
        if (limit > 200) limit = 200;

        var userId = User.FindFirstValue("sub") ?? "";
        var messages = await _chatLog.GetHistoryAsync(userId, limit);

        var response = messages.Select(m => new
        {
            role      = m.Role,
            content   = m.Content,
            subject   = m.Subject?.Name,
            topic     = m.Topic,
            timestamp = m.Timestamp
        });

        return Ok(response);
    }

    // POST /api/student/exam
    [HttpPost("exam")]
    public async Task<IActionResult> GenerateExam([FromBody] GenerateExamRequest body)
    {
        var reject = RequireStudentRole();
        if (reject != null) return reject;

        var gradeStr = User.FindFirstValue("grade") ?? "0";
        var grade    = int.TryParse(gradeStr, out var g) ? g : 0;
        var userId   = User.FindFirstValue("sub") ?? "";

        var (id, exam) = await _examService.GenerateAndSaveAsync(body.Topic, grade, userId);
        return Ok(new { id, exam });
    }

    // GET /api/student/exams
    [HttpGet("exams")]
    public async Task<IActionResult> ListExams()
    {
        var reject = RequireStudentRole();
        if (reject != null) return reject;

        var userId = User.FindFirstValue("sub") ?? "";
        var exams  = await _examService.ListSavedAsync(userId);

        var response = exams.Select(e => new { id = e.Id, topic = e.Topic, createdAt = e.CreatedAt });
        return Ok(response);
    }

    // GET /api/student/exams/{id}
    [HttpGet("exams/{id:int}")]
    public async Task<IActionResult> GetExam(int id)
    {
        var reject = RequireStudentRole();
        if (reject != null) return reject;

        var userId = User.FindFirstValue("sub") ?? "";
        var exam   = await _examService.GetSavedAsync(userId, id);
        if (exam == null) return NotFound(new { error = "Exam not found" });

        return Ok(new { id = exam.Id, topic = exam.Topic, content = exam.Content, createdAt = exam.CreatedAt });
    }

    // GET /api/student/classes
    [HttpGet("classes")]
    public async Task<IActionResult> GetClasses()
    {
        var reject = RequireStudentRole();
        if (reject != null) return reject;

        var userId = User.FindFirstValue("sub") ?? "";
        var (schoolId, schoolName, classes) = await _auth.GetStudentClassesAsync(userId);

        return Ok(new
        {
            schoolId,
            schoolName,
            classes = classes.Select(c => new { classId = c.ClassId, className = c.ClassName })
        });
    }

    // POST /api/student/classes
    [HttpPost("classes")]
    public async Task<IActionResult> JoinClass([FromBody] JoinClassRequest body)
    {
        var reject = RequireStudentRole();
        if (reject != null) return reject;

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (_rateLimiter.IsGeneralApiThrottled(ip, out _))
            return StatusCode(429, new { error = "Too many requests. Please wait before trying again." });

        var userId = User.FindFirstValue("sub") ?? "";
        var (success, error, classId, schoolId) = await _auth.JoinClassAsync(userId, body.Code);
        if (!success) return BadRequest(new { error });

        var (resolvedSchoolId, schoolName, classes) = await _auth.GetStudentClassesAsync(userId);
        return Ok(new
        {
            schoolId = resolvedSchoolId,
            schoolName,
            classes = classes.Select(c => new { classId = c.ClassId, className = c.ClassName })
        });
    }
}

public record PracticeQuestionsRequest([Required] string OriginalQuestion, [Required] string AiResponse);
public record GenerateExamRequest([Required] string Topic);
public record JoinClassRequest([Required, MaxLength(100)] string Code);
