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

    private async Task<(bool Ok, int? SchoolId, IActionResult? Reject)> ResolveSchoolAsync()
    {
        var role = User.FindFirstValue("role");
        if (role != "SchoolAdmin")
            return (false, null, StatusCode(403, new { error = "Access restricted to school administrators." }));

        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub")
                    ?? "";

        var caller = await _users.GetByIdAsync(callerId);
        if (caller == null || caller.SchoolId == null)
            return (false, null, StatusCode(403, new { error = "School administrator is not assigned to a school." }));

        return (true, caller.SchoolId, null);
    }

    // GET /api/admin/school
    [HttpGet("school")]
    public async Task<IActionResult> GetSchool()
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var name = await _admin.GetSchoolNameAsync(schoolId!.Value);
        return Ok(new { id = schoolId.Value, name });
    }

    // GET /api/admin/teacher-code
    [HttpGet("teacher-code")]
    public async Task<IActionResult> GetTeacherCode()
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var code = await _admin.GetTeacherCodeAsync(schoolId!.Value);
        return Ok(code);
    }

    // POST /api/admin/teacher-code/regenerate
    [HttpPost("teacher-code/regenerate")]
    public async Task<IActionResult> RegenerateTeacherCode()
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error, code) = await _admin.RegenerateTeacherCodeAsync(schoolId!.Value);
        if (!success) return BadRequest(new { error });

        return Ok(code);
    }

    // GET /api/admin/classes
    [HttpGet("classes")]
    public async Task<IActionResult> GetClasses()
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var classes = await _admin.ListClassesAsync(schoolId!.Value);
        return Ok(classes);
    }

    // POST /api/admin/classes
    [HttpPost("classes")]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassRequest body)
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.AddClassAsync(schoolId!.Value, body.Name, body.SubjectId, body.HomeroomTeacherId, body.Grade);
        if (!success) return BadRequest(new { error });

        return StatusCode(201);
    }

    // POST /api/admin/subjects
    [HttpPost("subjects")]
    public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectRequest body)
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.CreateSubjectAsync(schoolId!.Value, body.Name);
        if (!success) return BadRequest(new { error });

        return StatusCode(201);
    }

    // PUT /api/admin/classes/{classId}/homeroom
    [HttpPut("classes/{classId:int}/homeroom")]
    public async Task<IActionResult> SetHomeroomTeacher(int classId, [FromBody] SetHomeroomRequest body)
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.AssignTeacherAsync(schoolId!.Value, classId, body.TeacherId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // DELETE /api/admin/classes/{classId}/students/{userId}
    [HttpDelete("classes/{classId:int}/students/{userId}")]
    public async Task<IActionResult> RemoveStudent(int classId, string userId)
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.RemoveStudentAsync(schoolId!.Value, classId, userId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // POST /api/admin/classes/{classId}/teachers
    [HttpPost("classes/{classId:int}/teachers")]
    public async Task<IActionResult> AssignTeacherToClass(int classId, [FromBody] AssignTeacherToClassRequest body)
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.AssignTeacherToClassAsync(schoolId!.Value, classId, body.TeacherId, body.SubjectName);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // GET /api/admin/classes/{classId}
    [HttpGet("classes/{classId:int}")]
    public async Task<IActionResult> GetClassDetail(int classId)
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var detail = await _admin.GetClassDetailAsync(schoolId!.Value, classId);
        if (detail == null) return NotFound(new { error = "Class not found." });

        return Ok(detail);
    }

    // PUT /api/admin/classes/{classId}
    [HttpPut("classes/{classId:int}")]
    public async Task<IActionResult> UpdateClass(int classId, [FromBody] UpdateClassRequest body)
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.UpdateClassAsync(schoolId!.Value, classId, body.Name, body.SubjectId, body.Grade);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // DELETE /api/admin/classes/{classId}/teachers/{teacherId}/subjects/{subjectId}
    [HttpDelete("classes/{classId:int}/teachers/{teacherId}/subjects/{subjectId:int}")]
    public async Task<IActionResult> RemoveTeacherFromClass(int classId, string teacherId, int subjectId)
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.RemoveTeacherFromClassAsync(schoolId!.Value, classId, teacherId, subjectId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // POST /api/admin/teachers/{teacherId}/subjects/{subjectId}
    [HttpPost("teachers/{teacherId}/subjects/{subjectId:int}")]
    public async Task<IActionResult> AssignSubjectToTeacher(string teacherId, int subjectId)
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.AssignSubjectToTeacherAsync(schoolId!.Value, teacherId, subjectId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // DELETE /api/admin/teachers/{teacherId}/subjects/{subjectId}
    [HttpDelete("teachers/{teacherId}/subjects/{subjectId:int}")]
    public async Task<IActionResult> RemoveSubjectFromTeacher(string teacherId, int subjectId)
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.RemoveSubjectFromTeacherAsync(schoolId!.Value, teacherId, subjectId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // GET /api/admin/teachers/{teacherId}/subjects
    [HttpGet("teachers/{teacherId}/subjects")]
    public async Task<IActionResult> GetTeacherSubjects(string teacherId)
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error, subjects) = await _admin.GetTeacherSubjectsAsync(schoolId!.Value, teacherId);
        if (!success) return BadRequest(new { error });

        return Ok(subjects);
    }

    // GET /api/admin/subjects
    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects()
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var subjects = await _admin.ListSubjectsAsync(schoolId!.Value);
        return Ok(subjects);
    }

    // DELETE /api/admin/subjects/{subjectId}
    [HttpDelete("subjects/{subjectId:int}")]
    public async Task<IActionResult> DeleteSubject(int subjectId)
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var (success, error) = await _admin.DeleteSubjectAsync(schoolId!.Value, subjectId);
        if (!success) return BadRequest(new { error });

        return Ok();
    }

    // GET /api/admin/users
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var (ok, schoolId, reject) = await ResolveSchoolAsync();
        if (!ok) return reject!;

        var users = await _admin.ListUsersAsync(schoolId!.Value);
        return Ok(users);
    }
}

public record CreateClassRequest(string Name, int SubjectId, string? HomeroomTeacherId, int? Grade);
public record CreateSubjectRequest(string Name);
public record SetHomeroomRequest(string TeacherId);
public record AssignTeacherToClassRequest(string TeacherId, string SubjectName);
public record UpdateClassRequest(string Name, int SubjectId, int? Grade);
