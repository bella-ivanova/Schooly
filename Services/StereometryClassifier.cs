using System.Text.Json;

namespace StudyAssistant.Services;

// Decides whether a question is about 3D solid geometry, i.e. whether a <STEREO> scene should
// be generated for it. Replaces the old keyword list (StereometryDetector), which missed common
// terms like "кълбо" and fired on words plane-geometry questions share ("диагонал", "edge",
// "равнина"). Same shape as StemSubjectClassifier: one-shot BgGPT call on the already-warm
// model, temperature zeroed for this call only, Ollama's constrained-JSON mode, fail closed.
//
// Shares the Scoped IChatService with StemSubjectClassifier, and both temporarily change its
// Temperature — callers must never run the two concurrently (RAGService awaits the STEM
// classifier first).
public class StereometryClassifier
{
    private readonly IChatService _chat;
    private readonly ILogger<StereometryClassifier> _logger;

    public StereometryClassifier(IChatService chat, ILogger<StereometryClassifier> logger)
    {
        _chat   = chat;
        _logger = logger;
    }

    private const double ClassificationTemperature = 0.0;

    // Unlike StemSubjectClassifier, a valid "false" is not retried — most chat questions aren't
    // stereometry, so re-asking on "false" would add two LLM calls to nearly every message.
    // Only a failed attempt (malformed JSON, network error) is retried.
    private const int MaxRetries = 2;

    public async Task<bool> IsStereometryAsync(string question)
    {
        question = InputSanitizer.SanitizeUserInput(question, maxLength: 2000);

        var originalTemperature = _chat.Temperature;
        try
        {
            _chat.Temperature = ClassificationTemperature;

            for (var attempt = 0; attempt <= MaxRetries; attempt++)
            {
                var result = await ClassifyOnceAsync(question);
                if (result.HasValue) return result.Value;
            }

            _logger.LogWarning("Stereometry classification exhausted all attempts for {Question}; treating as not stereometry", question);
            return false;
        }
        finally
        {
            _chat.Temperature = originalTemperature;
        }
    }

    // null = the attempt failed and may be retried.
    private async Task<bool?> ClassifyOnceAsync(string question)
    {
        try
        {
            var response = await _chat.OneShotAsync(
                "You are a classifier for a school tutoring chat app. Decide whether the student's " +
                "question (it may be written in Bulgarian, English, or another language) is about 3D " +
                "solid geometry (stereometry). Reply ONLY with a JSON object and nothing else: " +
                "{\"stereometry\": true} or {\"stereometry\": false}.\n" +
                "Answer true for questions about three-dimensional solids or space: pyramids, prisms, " +
                "cubes, cuboids / rectangular boxes (паралелепипед), cylinders, cones, spheres and balls " +
                "(сфера, кълбо), tetrahedra, octahedra and other polyhedra, truncated solids, cross-sections " +
                "of solids, space diagonals, skew lines, dihedral angles, and lines or planes in space.\n" +
                "Answer false for plane (2D) geometry — triangles, quadrilaterals, circles, polygons, a " +
                "diagonal of a rectangle or square — and for all other maths, physics, chemistry, and " +
                "non-maths questions.",
                question,
                jsonFormat: true);

            var json = response.Trim();
            if (json.StartsWith("```"))
            {
                var nl   = json.IndexOf('\n');
                var last = json.LastIndexOf("```");
                if (nl >= 0 && last > nl)
                    json = json[(nl + 1)..last].Trim();
            }

            using var doc = JsonDocument.Parse(json);
            var value = doc.RootElement.GetProperty("stereometry");

            return value.ValueKind switch
            {
                JsonValueKind.True   => true,
                JsonValueKind.False  => false,
                JsonValueKind.String => value.GetString()?.Trim().ToLowerInvariant() == "true",
                _ => throw new JsonException($"Unexpected 'stereometry' value kind: {value.ValueKind}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stereometry classification attempt failed for question {Question}", question);
            return null;
        }
    }
}
