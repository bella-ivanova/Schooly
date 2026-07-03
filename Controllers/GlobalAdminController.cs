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

    // GET /api/global-admin/users
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var users = await _admin.ListUsersAsync();
        return Ok(users);
    }

    // GET /api/global-admin/classes
    [HttpGet("classes")]
    public async Task<IActionResult> GetClasses()
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var classes = await _admin.ListClassesAsync();
        return Ok(classes);
    }

    // POST /api/global-admin/classes
    [HttpPost("classes")]
    public async Task<IActionResult> CreateClass([FromBody] GlobalCreateClassRequest body)
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var (success, error) = await _admin.AddClassAsync(body.School, body.Name, body.HomeroomTeacherId);
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

        var (success, error) = await _admin.CreateSubjectAsync(body.School, body.Name);
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

        var (success, error) = await _admin.AssignTeacherToClassAsync(body.School, classId, body.TeacherId, body.SubjectName);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // PUT /api/global-admin/users/{userId}/role
    [HttpPut("users/{userId}/role")]
    public async Task<IActionResult> SetUserRole(string userId, [FromBody] GlobalSetRoleRequest body)
    {
        var reject = RequireAdminRole();
        if (reject != null) return reject;

        var (success, error) = await _admin.MakeSchoolAdminAsync(userId, body.School);
        if (!success) return BadRequest(new { error });

        return Ok();
    }
}

public record GlobalCreateSchoolRequest(string Name);
public record GlobalCreateClassRequest(string School, string Name, string? HomeroomTeacherId);
public record GlobalCreateSubjectRequest(string School, string Name);
public record GlobalAssignStudentRequest(string UserId);
public record GlobalAssignTeacherRequest(string School, string TeacherId, string SubjectName);
public record GlobalSetRoleRequest(string School);
