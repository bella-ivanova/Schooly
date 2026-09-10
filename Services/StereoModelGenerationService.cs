using System.Text.Json;

namespace StudyAssistant.Services;

// Generates only the <STEREO> scene JSON for a stereometry question, independent of any
// solved answer. Extracted from RAGService.GenerateStereoBlockAsync so the same generation
// logic can be reused both by RAGService's STEM chat path (run concurrently with Qwen's
// reasoning stage) and by StereoModelService's standalone "3D model only" feature.
//
// Uses the reasoning model (Qwen, OllamaChatService injected by concrete type — see
// Program.cs DI wiring), not BgGPT, via OneShotReasoningAsync(think: true) — same call shape
// StemAnswerPipelineService.TryFallbackProseAsync already uses. Fixed 2026-09-10 after this
// method (originally BgGPT via IChatService.OneShotAsync) reproducibly failed a live pyramid
// lateral-edge question 6/6 times, even after adding GeometryDisambiguationNote to the prompt
// (below): BgGPT wasn't failing on lateral-edge-vs-slant-height confusion specifically, it was
// anchoring on and copying numeric values straight out of StereometryService.Instruction's own
// worked examples (verbatim on one run; a "1.732"/"69.4°" mash-up of two unrelated examples on
// four more; a literal reproduction of the instruction's own "WRONG EXAMPLE" corner-origin
// snippet on the sixth) rather than deriving fresh coordinates from the question — the same
// class of multi-step-reasoning unreliability that motivated building the whole Qwen-based STEM
// pipeline in the first place (see CLAUDE.md's STEM Structured-Handoff Pipeline section), which
// prompt wording alone could not fix. Qwen already independently reasons through this exact
// problem type correctly (see the 2026-09-05 pyramid-lateral-edge fix note), so it's used here
// too rather than inventing new correctness logic.
//
// The system prompt deliberately lets the model briefly work the problem before the <STEREO>
// block (ExtractSceneJson discards everything outside the tags anyway) rather than instructing
// it to "produce ONLY the scene, no other text" — tested directly against Ollama first: the
// "ONLY the scene" framing left the model with nowhere to derive coordinates from. Qwen's
// think:true reasoning channel now does this "work through the problem first" step natively
// (jsonFormat stays false so the model can still free-write the <STEREO>-tagged block in its
// visible Content, same as the fallback-prose call shape), rather than relying on the model to
// volunteer prose reasoning before the tag as the BgGPT version had to.
//
// ReasoningNumPredict/ReasoningNumCtx reuse StemAnswerPipelineService's exact values (not just
// the same rationale) — this call now shares that service's think:true-plus-large-RAG-context
// failure mode (see the STEM pipeline's Fixed 2026-09-04/05 note: NumCtx left unset silently
// caps at ~4096, leaving too little room for a thinking trace once a large RAG context is
// included, producing empty Content), so it needs the same headroom.
//
// Retries on invalid JSON: direct testing against Ollama showed the *same* prompt/config
// produced a valid, question-specific JSON scene on one call and a bogus XML-tag-styled
// block (would fail to parse in the viewer) on another at the same temperature — inherent
// sampling variance, not something a single attempt can be trusted to avoid.
public class StereoModelGenerationService
{
    private const int SceneGenerationMaxRetries = 2; // up to 2 retries on invalid JSON (3 attempts total)

    // Matches StemAnswerPipelineService.ReasoningNumPredict/ReasoningNumCtx — see class comment.
    private const int ReasoningNumPredict = 8192;
    private const int ReasoningNumCtx     = 16384;

    private readonly OllamaChatService _chat; // Qwen — concrete type, see Program.cs DI wiring
    private readonly ILogger<StereoModelGenerationService> _logger;

    public StereoModelGenerationService(OllamaChatService chat, ILogger<StereoModelGenerationService> logger)
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
            "the 3D scene." + StemAnswerPipelineService.GeometryDisambiguationNote +
            StemAnswerPipelineService.EfficientReasoningNote + "\n" +
            StereometryService.Instruction;

        var userMessage =
            "--- Textbook Material ---\n" +
            context +
            "\n--- End of Material ---\n\n" +
            "<student_question>\n" + question + "\n</student_question>";

        for (int attempt = 0; attempt <= SceneGenerationMaxRetries; attempt++)
        {
            try
            {
                var (raw, thinking, doneReason, evalCount) = await _chat.OneShotReasoningAsync(
                    systemPrompt, userMessage, think: true, jsonFormat: false,
                    numPredict: ReasoningNumPredict, numCtx: ReasoningNumCtx);
                var sceneJson = StereometryService.ExtractSceneJson(raw);

                if (sceneJson != null)
                {
                    JsonDocument.Parse(sceneJson).Dispose(); // throws JsonException if malformed
                    return sceneJson;
                }

                _logger.LogWarning(
                    "STEREO scene generation attempt {Attempt}/{Max} produced no <STEREO> block for {Question}. " +
                    "Content length: {Len}, thinking length: {ThinkLen}, doneReason: {DoneReason}, evalCount: {EvalCount}",
                    attempt + 1, SceneGenerationMaxRetries + 1, question,
                    raw.Length, thinking?.Length ?? 0, doneReason ?? "(none)", evalCount);
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
