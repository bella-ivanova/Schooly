using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAssistant.Services;

namespace StudyAssistant.Controllers;

[ApiController]
[Route("api/global-admin")]
[Authorize]
public class GlobalAdminController : ControllerBase
{
    private readonly AdminUserService _admin;

    public GlobalAdminController(AdminUserService admin)
    {
        _admin = admin;
    }

    private IActionResult? RequireAdminRole()
    {
        var role = User.FindFirstValue("role");
        if (role != "Admin")
            return StatusCode(403, new { error = "Access restricted to global administrators." });
        return null;
    }

    // POST /api/global-admin/schools
    [HttpPost("schools")]
    public async Task<IActionResult> CreateSchool([FromBody] GlobalCreateSchoolRequest body)
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var (success, error) = await _admin.CreateSchoolAsync(body.Name);
        if (!success) return BadRequest(new { error });

        return StatusCode(201);
    }

    // GET /api/global-admin/schools
    [HttpGet("schools")]
    public async Task<IActionResult> GetSchools()
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var schools = await _admin.ListSchoolsAsync();
        return Ok(schools);
    }

    // GET /api/global-admin/users
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var users = await _admin.ListUsersAsync(schoolId: null);
        return Ok(users);
    }

    // GET /api/global-admin/classes
    [HttpGet("classes")]
    public async Task<IActionResult> GetClasses()
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var classes = await _admin.ListClassesAsync(schoolId: null);
        return Ok(classes);
    }

    // POST /api/global-admin/classes
    [HttpPost("classes")]
    public async Task<IActionResult> CreateClass([FromBody] GlobalCreateClassRequest body)
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var (success, error) = await _admin.AddClassAsync(body.SchoolId, body.Name, body.SubjectId, body.HomeroomTeacherId);
        if (!success) return BadRequest(new { error });

        return StatusCode(201);
    }

    // DELETE /api/global-admin/classes/{classId}
    [HttpDelete("classes/{classId:int}")]
    public async Task<IActionResult> DeleteClass(int classId)
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var (success, error) = await _admin.DeleteClassAsync(classId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // POST /api/global-admin/subjects
    [HttpPost("subjects")]
    public async Task<IActionResult> CreateSubject([FromBody] GlobalCreateSubjectRequest body)
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var (success, error) = await _admin.CreateSubjectAsync(body.SchoolId, body.Name);
        if (!success) return BadRequest(new { error });

        return StatusCode(201);
    }

    // DELETE /api/global-admin/subjects/{subjectId}
    [HttpDelete("subjects/{subjectId:int}")]
    public async Task<IActionResult> DeleteSubject(int subjectId)
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var (success, error) = await _admin.DeleteSubjectAsync(subjectId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // GET /api/global-admin/subjects
    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects()
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var subjects = await _admin.ListSubjectsAsync(schoolId: null);
        return Ok(subjects);
    }

    // POST /api/global-admin/classes/{classId}/students
    [HttpPost("classes/{classId:int}/students")]
    public async Task<IActionResult> AssignStudent(int classId, [FromBody] GlobalAssignStudentRequest body)
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var (success, error) = await _admin.AssignStudentToClassAsync(body.UserId, classId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // POST /api/global-admin/classes/{classId}/teachers
    [HttpPost("classes/{classId:int}/teachers")]
    public async Task<IActionResult> AssignTeacher(int classId, [FromBody] GlobalAssignTeacherRequest body)
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var (success, error) = await _admin.AssignTeacherToClassAsync(body.SchoolId, classId, body.TeacherId, body.SubjectName);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // PUT /api/global-admin/users/{userId}/role
    [HttpPut("users/{userId}/role")]
    public async Task<IActionResult> SetUserRole(string userId, [FromBody] GlobalSetRoleRequest body)
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var (success, error) = await _admin.MakeSchoolAdminAsync(userId, body.SchoolId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // GET /api/global-admin/curriculum/grades/{grade}/files
    [HttpGet("curriculum/grades/{grade:int}/files")]
    public async Task<IActionResult> GetCurriculumFiles(int grade)
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var files = await _admin.ListCurriculumFilesAsync(grade);
        return Ok(files);
    }

    // POST /api/global-admin/curriculum/grades/{grade}/files
    [HttpPost("curriculum/grades/{grade:int}/files")]
    [RequestSizeLimit(52_428_800)] // 50 MB, matches ChatController.UploadPdf
    public async Task<IActionResult> UploadCurriculumFile(int grade, [FromForm] string? subject, IFormFile file)
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only PDF files are accepted." });

        await using var stream = file.OpenReadStream();
        var (success, error, chunkCount) = await _admin.UploadCurriculumFileAsync(
            grade, subject ?? "", file.FileName, stream, overwrite: false);

        if (!success) return Conflict(new { error });
        return StatusCode(201, new { chunks = chunkCount });
    }

    // PUT /api/global-admin/curriculum/grades/{grade}/files/{*fileKey}
    [HttpPut("curriculum/grades/{grade:int}/files/{*fileKey}")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> ReplaceCurriculumFile(int grade, string fileKey, IFormFile file)
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        var subject  = Path.GetDirectoryName(fileKey)?.Replace('\\', '/') ?? "";
        var fileName = Path.GetFileName(fileKey);

        await using var stream = file.OpenReadStream();
        var (success, error, chunkCount) = await _admin.UploadCurriculumFileAsync(
            grade, subject, fileName, stream, overwrite: true);

        if (!success) return BadRequest(new { error });
        return Ok(new { chunks = chunkCount });
    }

    // DELETE /api/global-admin/curriculum/grades/{grade}/files/{*fileKey}
    [HttpDelete("curriculum/grades/{grade:int}/files/{*fileKey}")]
    public async Task<IActionResult> DeleteCurriculumFile(int grade, string fileKey)
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var deleted = await _admin.DeleteCurriculumFileAsync(grade, fileKey);
        if (!deleted)
            return NotFound(new { error = $"File '{fileKey}' not found in grade {grade}." });

        return Ok();
    }
}

public record GlobalCreateSchoolRequest(string Name);
public record GlobalCreateClassRequest(int SchoolId, string Name, int SubjectId, string? HomeroomTeacherId);
public record GlobalCreateSubjectRequest(int SchoolId, string Name);
public record GlobalAssignStudentRequest(string UserId);
public record GlobalAssignTeacherRequest(int SchoolId, string TeacherId, string SubjectName);
public record GlobalSetRoleRequest(int SchoolId);
