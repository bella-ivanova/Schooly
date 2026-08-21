using System.Globalization;
using System.Text.RegularExpressions;
using LanguageDetection;

namespace StudyAssistant.Services;

// Wraps the offline n-gram language identifier so prompt-building code can tell the LLM
// explicitly which language to answer in, instead of relying on the model to infer it
// from a "respond in the same language" instruction (unreliable for short inputs / minicpm-v).
public class LanguageDetectionService
{
    private static readonly Regex CyrillicChar = new(@"\p{IsCyrillic}", RegexOptions.Compiled);

    private readonly LanguageDetector _detector = new();

    public LanguageDetectionService() => _detector.AddAllLanguages();

    // Returns an English display name (e.g. "Bulgarian") or null if detection is
    // unavailable/unreliable. Callers must treat null as "don't know" and fall back
    // to the generic "respond in the student's language" instruction.
    //
    // requireStrongSignal: the n-gram model is only reliable on real sentences — bare
    // topic phrases with no function words ("Algebra", "Quadratic Equations") are
    // frequently misdetected with high confidence (e.g. "Algebra" -> Afrikaans). Cyrillic
    // script is unambiguous at any length (this app's only Cyrillic curriculum language is
    // Bulgarian), so that check always applies; for everything else, pass true when the
    // input may be a bare topic (ExamService) rather than a full question, which requires
    // at least a few words before trusting the non-Cyrillic result.
    public string? DetectLanguageName(string text, bool requireStrongSignal = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (CyrillicChar.IsMatch(text)) return "Bulgarian";

        if (requireStrongSignal && text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 4)
            return null;

        try
        {
            var code = _detector.Detect(text);
            if (string.IsNullOrEmpty(code)) return null;
            return CultureInfo.GetCultureInfo(code).EnglishName;
        }
        catch
        {
            return null;
        }
    }
}
