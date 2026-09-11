namespace StudyAssistant.Services;

public class StemAnswer
{
    public string QuestionType { get; set; } = ""; // "numeric" or "conceptual"
    public string? Answer { get; set; }
    public string? Unit { get; set; }
    public string? Formula { get; set; }
    public List<string> Steps { get; set; } = new();
    public string? Explanation { get; set; } // used when QuestionType == "conceptual"
}

public enum StemSubject { None, Math, Physics, Chemistry }

// Result of Stage 1 (Qwen). Exactly one of Structured/FallbackProse is non-null on partial
// success; both null means the whole reasoning stage failed and the caller should degrade
// to the generic single-stage pipeline.
public record StemReasoningResult(StemAnswer? Structured, string? FallbackProse, int RetryCount);

// Switch between the two-stage Qwen(JSON)->BgGPT(narration) pipeline and a direct mode where
// Qwen streams the full final answer itself. Backed by config key Llm:StemDirectAnswer,
// read once in Program.cs and registered as a singleton so RAGService can branch on it.
public record StemPipelineOptions(bool DirectAnswer);
