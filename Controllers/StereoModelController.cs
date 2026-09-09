using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAssistant.Services;

namespace StudyAssistant.Controllers;

[ApiController]
[Route("api/stereo-models")]
[Authorize]
public class StereoModelController : ControllerBase
{
    private readonly StereoModelService _stereoModels;

    public StereoModelController(StereoModelService stereoModels)
    {
        _stereoModels = stereoModels;
    }

    private IActionResult? RequireStudentOrTeacherRole()
    {
        var role = User.FindFirstValue("role");
        if (role != "Student" && role != "Teacher")
            return StatusCode(403, new { error = "Access restricted to students and teachers." });
        return null;
    }

    // POST /api/stereo-models
    // Generates a 3D scene for a stereometry question without solving/narrating it, and
    // persists it to the caller's history.
    [HttpPost]
    public async Task<IActionResult> Generate([FromBody] GenerateStereoModelRequest body)
    {
        var reject = RequireStudentOrTeacherRole();
        if (reject != null) return reject;

        var userId = User.FindFirstValue("sub") ?? "";
        var (id, scene, error) = await _stereoModels.GenerateAndSaveAsync(body.Question, userId);
        if (error != null) return BadRequest(new { error });

        return Ok(new { id, scene });
    }

    // GET /api/stereo-models
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var reject = RequireStudentOrTeacherRole();
        if (reject != null) return reject;

        var userId = User.FindFirstValue("sub") ?? "";
        var models = await _stereoModels.ListSavedAsync(userId);

        var response = models.Select(m => new { id = m.Id, question = m.Question, createdAt = m.CreatedAt });
        return Ok(response);
    }

    // GET /api/stereo-models/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var reject = RequireStudentOrTeacherRole();
        if (reject != null) return reject;

        var userId = User.FindFirstValue("sub") ?? "";
        var model  = await _stereoModels.GetSavedAsync(userId, id);
        if (model == null) return NotFound(new { error = "3D model not found" });

        return Ok(new { id = model.Id, question = model.Question, scene = model.SceneJson, createdAt = model.CreatedAt });
    }
}

public record GenerateStereoModelRequest([Required] string Question);
