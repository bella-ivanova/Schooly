using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public class StereoModelService
{
    private readonly StereoModelGenerationService _generator;
    private readonly AppDbContext _db;

    public StereoModelService(StereoModelGenerationService generator, AppDbContext db)
    {
        _generator = generator;
        _db = db;
    }

    public async Task<(int? Id, string? Scene, string? Error)> GenerateAndSaveAsync(string question, string userId)
    {
        var sanitized = InputSanitizer.SanitizeUserInput(question, maxLength: 2000);

        if (string.IsNullOrWhiteSpace(sanitized))
            return (null, null, "Please enter a question.");

        if (!StereometryDetector.IsStereometryQuestion(sanitized))
            return (null, null, "This doesn't look like a 3D geometry question. Describe a solid shape (pyramid, prism, cone, cylinder…) and its measurements.");

        var scene = await _generator.GenerateSceneAsync(sanitized);
        if (scene == null)
            return (null, null, "Could not generate a 3D model for this question. Try rephrasing it with clearer measurements.");

        var saved = new SavedStereoModel
        {
            UserId    = userId,
            Question  = sanitized,
            SceneJson = scene,
            CreatedAt = DateTime.UtcNow,
        };
        _db.SavedStereoModels.Add(saved);
        await _db.SaveChangesAsync();

        return (saved.Id, scene, null);
    }

    public async Task<List<SavedStereoModel>> ListSavedAsync(string userId) =>
        await _db.SavedStereoModels
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

    public async Task<SavedStereoModel?> GetSavedAsync(string userId, int id) =>
        await _db.SavedStereoModels.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
}
