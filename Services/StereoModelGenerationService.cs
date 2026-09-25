using System.Text.Json;
using System.Text.Json.Nodes;

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

        // Fixed 2026-09-24: a cube cross-section question ("сечение през средите на шест ръба")
        // failed all 3 attempts with doneReason "length", evalCount 8192 and empty Content — Qwen
        // spent the whole NumPredict budget on its think:true trace (~20k chars) before emitting a
        // single Content token, and retrying the identical think:true call just failed the same way
        // (23-minute chat response, no scene). Raising NumPredict would only make an already
        // ~8-minute attempt longer and need a bigger NumCtx on a 16GB machine, so once thinking has
        // exhausted the budget, the remaining attempts run with think:false instead — the prompt
        // already asks the model to briefly work the problem before the <STEREO> block, so it
        // reasons in visible Content (discarded by ExtractSceneJson) rather than the hidden trace.
        // The switch triggers on any "length" stop, not only empty Content: a rerun of the same
        // question ran out mid-way through some visible prose (1903 chars, no <STEREO> block), which
        // is just as unrecoverable. A bare think:false retry then produced a wrong section (4 of the
        // 6 "hexagon" points on one cube face), so the tail of the truncated attempt's own working
        // is passed along as <earlier_analysis> — it usually ends where the coordinates were being
        // derived, so the fast attempt builds on it instead of re-solving from scratch.
        const int PriorAnalysisMaxChars = 8000;
        var useThinking = true;
        string? priorAnalysis = null;
        string? rejectionFeedback = null;

        for (int attempt = 0; attempt <= SceneGenerationMaxRetries; attempt++)
        {
            try
            {
                var attemptMessage = priorAnalysis == null
                    ? userMessage
                    : userMessage +
                      "\n\n<earlier_analysis>\n" + priorAnalysis + "\n</earlier_analysis>\n" +
                      "The above is your own earlier, unfinished working on this question. Reuse its " +
                      "correct conclusions instead of re-deriving them, then output the <STEREO> block. " +
                      "Still follow the COORDINATE SYSTEM rules exactly: base in the plane y = 0, " +
                      "centred at x = 0, z = 0. The earlier working may focus on one part of the " +
                      "figure (e.g. only the cross-section points) — the scene must still contain the " +
                      "whole solid: all of its vertices and edges, with that part drawn on top of it.";
                if (rejectionFeedback != null)
                    attemptMessage += "\n\nYour previous <STEREO> block for this question was rejected: " +
                                      rejectionFeedback + " Recompute the coordinates and output a corrected block.";

                var (raw, thinking, doneReason, evalCount) = await _chat.OneShotReasoningAsync(
                    systemPrompt, attemptMessage, think: useThinking, jsonFormat: false,
                    numPredict: ReasoningNumPredict, numCtx: ReasoningNumCtx);
                var sceneJson = StereometryService.ExtractSceneJson(raw);

                if (sceneJson != null)
                {
                    JsonDocument.Parse(sceneJson).Dispose(); // throws JsonException if malformed

                    sceneJson = RemoveDuplicateFaces(sceneJson);
                    var problem = FindGeometryProblem(sceneJson);
                    if (problem == null)
                        return RemoveCopiedExampleAngle(sceneJson, question);

                    _logger.LogWarning(
                        "STEREO scene generation attempt {Attempt}/{Max} was geometrically invalid for {Question}: {Problem}",
                        attempt + 1, SceneGenerationMaxRetries + 1, question, problem);
                    rejectionFeedback = problem;
                    continue;
                }

                _logger.LogWarning(
                    "STEREO scene generation attempt {Attempt}/{Max} (think: {Think}) produced no <STEREO> block for {Question}. " +
                    "Content length: {Len}, thinking length: {ThinkLen}, doneReason: {DoneReason}, evalCount: {EvalCount}",
                    attempt + 1, SceneGenerationMaxRetries + 1, useThinking, question,
                    raw.Length, thinking?.Length ?? 0, doneReason ?? "(none)", evalCount);

                if (useThinking && doneReason == "length")
                {
                    useThinking = false;
                    var working = (thinking ?? "") + "\n" + raw;
                    priorAnalysis = working.Length > PriorAnalysisMaxChars
                        ? working[^PriorAnalysisMaxChars..]
                        : working;
                }
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

    // Objective check run on every parsed scene, so a geometrically impossible one is retried
    // instead of shown. Added 2026-09-25 after a cube cross-section question returned a
    // "regular hexagon" with three of its six points on the same cube face — not planar, so not
    // a section at all. Checks only what is unambiguous for any solid: every face with 4+
    // points must be flat. Tolerance is relative to the scene's size so rounded coordinates
    // (1.732 for √3) never trip it. Returns a short description of the first problem, or null.
    private static string? FindGeometryProblem(string sceneJson)
    {
        if (JsonNode.Parse(sceneJson) is not JsonObject scene ||
            scene["vertices"] is not JsonObject vertices)
            return null;

        var points = new Dictionary<string, (double X, double Y, double Z)>();
        foreach (var (name, value) in vertices)
        {
            if (value is JsonArray { Count: >= 3 } p &&
                TryNumber(p[0], out var x) && TryNumber(p[1], out var y) && TryNumber(p[2], out var z))
                points[name] = (x, y, z);
        }
        if (points.Count == 0) return null;

        var extent = new[]
        {
            points.Values.Max(p => p.X) - points.Values.Min(p => p.X),
            points.Values.Max(p => p.Y) - points.Values.Min(p => p.Y),
            points.Values.Max(p => p.Z) - points.Values.Min(p => p.Z),
        }.Max();
        var tolerance = Math.Max(0.05, extent * 0.02);

        if (scene["faces"] is not JsonArray faces) return null;
        foreach (var face in faces)
        {
            if (face?["pts"] is not JsonArray pts || pts.Count < 4) continue;
            var facePoints = pts
                .Select(n => n?.ToString())
                .Where(n => n != null && points.ContainsKey(n))
                .Select(n => points[n!])
                .ToList();
            if (facePoints.Count < 4) continue;

            var label = face["label"]?.ToString() ?? string.Join("", pts.Select(n => n?.ToString()));
            var deviation = MaxDistanceFromPlane(facePoints);
            if (deviation == null)
                continue; // all points collinear — degenerate but harmless to render
            if (deviation > tolerance)
                return $"face \"{label}\" ({string.Join(", ", pts.Select(n => n?.ToString()))}) is not flat — " +
                       $"its points do not lie in one plane (off by up to {deviation:0.##}).";
        }

        // A section is sometimes drawn only as a closed loop of line/dashed helpers instead of
        // a face. Any such loop (every point on it touching exactly two segments of the loop,
        // 4+ points) must be flat too.
        if (scene["helpers"] is JsonArray helpers)
        {
            var adjacency = new Dictionary<string, HashSet<string>>();
            foreach (var h in helpers)
            {
                var kind = h?["kind"]?.ToString();
                if (kind != "line" && kind != "dashed") continue;
                var (from, to) = (h!["from"]?.ToString(), h["to"]?.ToString());
                if (from == null || to == null || from == to ||
                    !points.ContainsKey(from) || !points.ContainsKey(to)) continue;
                (adjacency.TryGetValue(from, out var f) ? f : adjacency[from] = new()).Add(to);
                (adjacency.TryGetValue(to, out var t) ? t : adjacency[to] = new()).Add(from);
            }

            var visited = new HashSet<string>();
            foreach (var start in adjacency.Keys)
            {
                if (!visited.Add(start)) continue;
                var component = new List<string> { start };
                var queue = new Queue<string>([start]);
                while (queue.Count > 0)
                    foreach (var next in adjacency[queue.Dequeue()])
                        if (visited.Add(next)) { component.Add(next); queue.Enqueue(next); }

                if (component.Count < 4 || component.Any(n => adjacency[n].Count != 2)) continue;

                var deviation = MaxDistanceFromPlane(component.Select(n => points[n]).ToList());
                if (deviation > tolerance)
                    return $"the closed outline {string.Join("-", component)} drawn with helper lines is not flat — " +
                           $"its points do not lie in one plane (off by up to {deviation:0.##}).";
            }
        }
        return null;
    }

    // Plane through the first three non-collinear points; returns the largest distance of any
    // point from it, or null if every point is collinear.
    private static double? MaxDistanceFromPlane(List<(double X, double Y, double Z)> pts)
    {
        var a = pts[0];
        for (int i = 1; i < pts.Count; i++)
        for (int j = i + 1; j < pts.Count; j++)
        {
            var (u, v) = (Sub(pts[i], a), Sub(pts[j], a));
            var n = (X: u.Y * v.Z - u.Z * v.Y, Y: u.Z * v.X - u.X * v.Z, Z: u.X * v.Y - u.Y * v.X);
            var len = Math.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
            if (len < 1e-9) continue;
            return pts.Max(p =>
            {
                var d = Sub(p, a);
                return Math.Abs(d.X * n.X + d.Y * n.Y + d.Z * n.Z) / len;
            });
        }
        return null;

        static (double X, double Y, double Z) Sub((double X, double Y, double Z) p, (double X, double Y, double Z) q) =>
            (p.X - q.X, p.Y - q.Y, p.Z - q.Z);
    }

    private static bool TryNumber(JsonNode? node, out double value)
    {
        value = 0;
        return node is JsonValue v && v.TryGetValue(out value);
    }

    // Drops faces listing the same set of points as an earlier face (seen in practice: a cube's
    // base re-listed as "дясна страна"). Cosmetic, so fixed silently rather than retried.
    // Fails open: returns the scene unchanged on any parse problem.
    private string RemoveDuplicateFaces(string sceneJson)
    {
        try
        {
            if (JsonNode.Parse(sceneJson) is not JsonObject scene ||
                scene["faces"] is not JsonArray faces)
                return sceneJson;

            var seen = new HashSet<string>();
            var duplicates = faces
                .Where(f => f?["pts"] is JsonArray pts &&
                            !seen.Add(string.Join("|", pts.Select(n => n?.ToString()).OrderBy(n => n))))
                .ToList();
            if (duplicates.Count == 0) return sceneJson;

            foreach (var face in duplicates) faces.Remove(face);
            return scene.ToJsonString(new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check scene faces for duplicates");
            return sceneJson;
        }
    }

    // Safety net for StereometryService.Instruction's worked-example angle leaking into the
    // legend: a hexagonal-pyramid height question (no angle asked) came back with the example's
    // exact "SAB ∩ основа ≈69.4°" entry (true face angle there: ≈40.9°), despite the prompt's
    // don't-copy wording. Drops any angle entry containing 69.4 unless the question itself uses
    // the example's own side 6/height 8 numbers. Fails open: returns the scene unchanged on any
    // parse problem.
    private string RemoveCopiedExampleAngle(string sceneJson, string question)
    {
        if (question.Contains('6') && question.Contains('8')) return sceneJson;

        try
        {
            if (JsonNode.Parse(sceneJson) is not JsonObject scene ||
                scene["angles"] is not JsonArray angles)
                return sceneJson;

            var copied = angles
                .Where(a => a?["value"]?.ToString().Contains("69.4") == true)
                .ToList();
            if (copied.Count == 0) return sceneJson;

            foreach (var entry in copied) angles.Remove(entry);
            _logger.LogInformation(
                "Removed {Count} angle entr(y/ies) copied from the prompt example for {Question}",
                copied.Count, question);

            return scene.ToJsonString(new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check scene angles for copied example values");
            return sceneJson;
        }
    }
}
