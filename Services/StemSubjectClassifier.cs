using System.Text.Json;

namespace StudyAssistant.Services;

// Pre-answer router deciding whether a chat question should go through the STEM
// structured-handoff pipeline (Qwen reasoning -> BgGPT narration) or the existing
// single-stage pipeline. Runs via the existing IChatService (BgGPT) rather than the
// reasoning model: BgGPT is already resident for every request today, so classifying
// through it costs one extra fast JSON round-trip on an already-warm model, instead of
// forcing a Qwen load/swap on every single chat message regardless of subject.
public class StemSubjectClassifier
{
    private readonly IChatService _chat;
    private readonly ILogger<StemSubjectClassifier> _logger;

    public StemSubjectClassifier(IChatService chat, ILogger<StemSubjectClassifier> logger)
    {
        _chat   = chat;
        _logger = logger;
    }

    // Low temperature for this call only: this is a categorical judgment that should be
    // stable on identical input, not fluent prose. _chat is the shared, Scoped IChatService
    // (BgGPT) instance RAGService also narrates the final answer with later in the same
    // request, so the original temperature is restored in `finally` rather than left low.
    private const double ClassificationTemperature = 0.1;

    public async Task<StemSubject> ClassifyAsync(string question)
    {
        var originalTemperature = _chat.Temperature;
        try
        {
            question = InputSanitizer.SanitizeUserInput(question, maxLength: 2000);
            _chat.Temperature = ClassificationTemperature;

            var response = await _chat.OneShotAsync(
                "You are a subject classifier for a school tutoring chat app. Classify the student's " +
                "question (it may be written in Bulgarian, English, or another language) into exactly " +
                "one category. Reply ONLY with a JSON object and nothing else, using these exact " +
                "lowercase English values: {\"subject\": \"math\" | \"physics\" | \"chemistry\" | \"other\"}.\n" +
                "Use \"math\" for arithmetic, algebra, geometry, trigonometry, or calculus problems.\n" +
                "Use \"physics\" for mechanics, electricity, magnetism, optics, thermodynamics, or waves questions.\n" +
                "Use \"chemistry\" for chemical reactions, formulas, elements, compounds, or stoichiometry questions.\n" +
                "Use \"other\" for everything else (history, literature, biology, geography, languages, general chat).\n" +
                "A physics or chemistry word problem often looks like plain arithmetic on the surface — " +
                "classify by the subject matter (a physical quantity like speed, force, energy, or mass; " +
                "or a chemical entity like an element, compound, mole, or chemical formula), not by how " +
                "simple the calculation is. If the question involves any calculation or scientific concept " +
                "at all, classify it by the closest matching subject even if you're unsure which exact one " +
                "— only use \"other\" when the question clearly has no mathematical or scientific content.",
                question);

            var json = response.Trim();
            if (json.StartsWith("```"))
            {
                var nl   = json.IndexOf('\n');
                var last = json.LastIndexOf("```");
                if (nl >= 0 && last > nl)
                    json = json[(nl + 1)..last].Trim();
            }

            using var doc = JsonDocument.Parse(json);
            var subject = doc.RootElement.GetProperty("subject").GetString() ?? "other";

            return subject.Trim().ToLowerInvariant() switch
            {
                "math"      => StemSubject.Math,
                "physics"   => StemSubject.Physics,
                "chemistry" => StemSubject.Chemistry,
                _           => StemSubject.None
            };
        }
        catch (Exception ex)
        {
            // Fail open to the existing, proven single-stage pipeline — a classifier
            // failure must never block chat.
            _logger.LogWarning(ex, "STEM subject classification failed for question {Question}", question);
            return StemSubject.None;
        }
        finally
        {
            _chat.Temperature = originalTemperature;
        }
    }
}
