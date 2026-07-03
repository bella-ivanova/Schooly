using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAssistant.Data;
using StudyAssistant.Services;

namespace StudyAssistant.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
public class SchoolAdminController : ControllerBase
{
    private readonly SchoolAdminService _admin;
    private readonly IUserRepository _users;

    public SchoolAdminController(SchoolAdminService admin, IUserRepository users)
    {
        _admin = admin;
        _users = users;
    }

    private async Task<(bool Ok, string? School, IActionResult? Reject)> ResolveSchoolAsync()
    {
        var role = User.FindFirstValue("role");
        if (role != "SchoolAdmin")
            return (false, null, StatusCode(403, new { error = "Access restricted to school administrators." }));

        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub")
                    ?? "";

        var caller = await _users.GetByIdAsync(callerId);
        if (caller == null || string.IsNullOrEmpty(caller.School))
            return (false, null, StatusCode(403, new { error = "School administrator is not assigned to a school." }));

        return (true, caller.School, null);
    }

    // GET /api/admin/classes
    [HttpGet("classes")]
    public async Task<IActionResult> GetClasses()
    {
        var (ok, school, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var classes = await _admin.ListClassesAsync(school!);
        return Ok(classes);
    }

    // POST /api/admin/classes
    [HttpPost("classes")]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassRequest body)
    {
        var (ok, school, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.AddClassAsync(school!, body.Name, body.HomeroomTeacherId);
        if (!success) return BadRequest(new { error });

        return StatusCode(201);
    }

    // PUT /api/admin/classes/{classId}/homeroom
    [HttpPut("classes/{classId:int}/homeroom")]
    public async Task<IActionResult> SetHomeroomTeacher(int classId, [FromBody] SetHomeroomRequest body)
    {
        var (ok, school, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.AssignTeacherAsync(school!, classId, body.TeacherId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // POST /api/admin/classes/{classId}/students
    [HttpPost("classes/{classId:int}/students")]
    public async Task<IActionResult> AssignStudent(int classId, [FromBody] AssignStudentRequest body)
    {
        var (ok, school, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.AssignStudentAsync(school!, classId, body.UserId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // DELETE /api/admin/classes/{classId}/students/{userId}
    [HttpDelete("classes/{classId:int}/students/{userId}")]
    public async Task<IActionResult> RemoveStudent(int classId, string userId)
    {
        var (ok, school, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.RemoveStudentAsync(school!, userId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // POST /api/admin/classes/{classId}/teachers
    [HttpPost("classes/{classId:int}/teachers")]
    public async Task<IActionResult> AssignTeacherToClass(int classId, [FromBody] AssignTeacherToClassRequest body)
    {
        var (ok, school, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.AssignTeacherToClassAsync(school!, classId, body.TeacherId, body.SubjectName);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // POST /api/admin/teachers/{teacherId}/subjects/{subjectId}
    [HttpPost("teachers/{teacherId}/subjects/{subjectId:int}")]
    public async Task<IActionResult> AssignSubjectToTeacher(string teacherId, int subjectId)
    {
        var (ok, school, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.AssignSubjectToTeacherAsync(school!, teacherId, subjectId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // DELETE /api/admin/teachers/{teacherId}/subjects/{subjectId}
    [HttpDelete("teachers/{teacherId}/subjects/{subjectId:int}")]
    public async Task<IActionResult> RemoveSubjectFromTeacher(string teacherId, int subjectId)
    {
        var (ok, school, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.RemoveSubjectFromTeacherAsync(school!, teacherId, subjectId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // GET /api/admin/subjects
    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects()
    {
        var (ok, school, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var subjects = await _admin.ListSubjectsAsync(school!);
        return Ok(subjects);
    }

    // GET /api/admin/users
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var (ok, school, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var users = await _admin.ListUsersAsync(school!);
        return Ok(users);
    }
}

public record CreateClassRequest(string Name, string? HomeroomTeacherId);
public record SetHomeroomRequest(string TeacherId);
public record AssignStudentRequest(string UserId);
public record AssignTeacherToClassRequest(string TeacherId, string SubjectName);
