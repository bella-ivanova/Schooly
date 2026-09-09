using System.Text.Json;

namespace StudyAssistant.Services;

// Generates only the <STEREO> scene JSON for a stereometry question, independent of any
// solved answer. Extracted from RAGService.GenerateStereoBlockAsync so the same generation
// logic can be reused both by RAGService's STEM chat path (run concurrently with Qwen's
// reasoning stage) and by StereoModelService's standalone "3D model only" feature.
//
// The system prompt deliberately lets the model briefly work the problem in prose before
// the <STEREO> block (ExtractSceneJson discards everything outside the tags anyway) rather
// than instructing it to "produce ONLY the scene, no other text" — tested directly against
// Ollama first: the "ONLY the scene" framing left the model with nowhere to derive
// coordinates from, and it answered by copying the instruction's own worked example verbatim
// into the tags instead of computing one for the actual question; letting it reason first
// produced a correct, question-specific scene instead. numCtx is set explicitly (matching
// StemAnswerPipelineService.ReasoningNumCtx) for the same reason it mattered there — this
// call can include a potentially-large RAG context, and NumCtx left unset defaults to
// Ollama's much smaller ~4096 window (see the STEM pipeline's Fixed 2026-09-04/05 note).
//
// Retries on invalid JSON: direct testing against Ollama showed the *same* prompt/config
// produced a valid, question-specific JSON scene on one call and a bogus XML-tag-styled
// block (would fail to parse in the viewer) on another at the same temperature — inherent
// sampling variance, not something a single attempt can be trusted to avoid.
public class StereoModelGenerationService
{
    private const int SceneGenerationMaxRetries = 2; // up to 2 retries on invalid JSON (3 attempts total)

    private readonly IChatService _chat;
    private readonly ILogger<StereoModelGenerationService> _logger;

    public StereoModelGenerationService(IChatService chat, ILogger<StereoModelGenerationService> logger)
    {
        _chat = chat;
        _logger = logger;
    }

    public async Task<string?> GenerateSceneAsync(string question, string context = "")
    {
        var systemPrompt =
            "You are a school tutor helping visualize 3D geometry for a student's " +
            "solid-geometry question. Briefly work through the problem, identifying the " +
            "shape and key measurements, then follow the instructions below to describe " +
            "the 3D scene.\n" + StereometryService.Instruction;

        var userMessage =
            "--- Textbook Material ---\n" +
            context +
            "\n--- End of Material ---\n\n" +
            "<student_question>\n" + question + "\n</student_question>";

        for (int attempt = 0; attempt <= SceneGenerationMaxRetries; attempt++)
        {
            try
            {
                var raw = await _chat.OneShotAsync(systemPrompt, userMessage, numPredict: 1536, numCtx: 16384);
                var sceneJson = StereometryService.ExtractSceneJson(raw);

                if (sceneJson != null)
                {
                    JsonDocument.Parse(sceneJson).Dispose(); // throws JsonException if malformed
                    return sceneJson;
                }

                _logger.LogWarning(
                    "STEREO scene generation attempt {Attempt}/{Max} produced no <STEREO> block for {Question}",
                    attempt + 1, SceneGenerationMaxRetries + 1, question);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "STEREO scene generation attempt {Attempt}/{Max} produced malformed JSON for {Question}",
                    attempt + 1, SceneGenerationMaxRetries + 1, question);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "STEREO scene generation failed for question {Question}", question);
                return null;
            }
        }

        _logger.LogWarning("STEREO scene generation exhausted all attempts for {Question}", question);
        return null;
    }
}
