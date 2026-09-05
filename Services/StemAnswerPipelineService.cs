using System.Text.Json;

namespace StudyAssistant.Services;

// Stage 1 of the STEM structured-handoff pipeline: calls the reasoning model (Qwen) to
// solve a math/physics/chemistry question, either as structured JSON (StemAnswer) or,
// if that fails, as a plain-prose fallback. Does not touch BgGPT or streaming — narration
// (Stage 2) stays in RAGService so the IAsyncEnumerable<string> token contract lives in
// exactly one place. This service only orchestrates Stage 1 and builds the Stage 2 prompts
// as static helpers shared by RAGService and the CLI test harness.
public class StemAnswerPipelineService
{
    private const int MaxJsonRetries = 2; // up to 2 retries on JsonException (3 attempts total)

    // NumPredict caps generation length; NumCtx caps the *total* window (prompt + generation)
    // and is the one that actually mattered here. Root-caused via OllamaChatService's
    // DoneReason/EvalCount diagnostics: repeated runs on the same math question returned an
    // IDENTICAL EvalCount every time despite different random content (temperature 0.2,
    // non-zero) — a natural stop would vary; an identical count pointed at a fixed ceiling
    // instead. Cause: NumCtx was never set, so Ollama fell back to its own default context
    // window (observed ~4096 total). A math question's real RAG context (up to 10 dense,
    // formula-heavy retrieved chunks) plus the system prompt can consume most of that window
    // on its own, leaving too little of it for a `think: true` trace plus JSON content —
    // physics/chemistry questions (no ingested curriculum yet, so ~empty RAG context) were
    // unaffected, which is why only math questions were failing. Both raised well past the
    // observed failure point; safe to be generous now that OllamaChatService's HttpClient
    // timeout is infinite, so a larger window just means a longer wait, not a client failure.
    private const int ReasoningNumPredict = 8192;
    private const int ReasoningNumCtx     = 16384;

    private readonly OllamaChatService _reasoningChat; // Qwen — concrete type, see Program.cs DI wiring
    private readonly ILogger<StemAnswerPipelineService> _logger;

    public StemAnswerPipelineService(OllamaChatService reasoningChat, ILogger<StemAnswerPipelineService> logger)
    {
        _reasoningChat = reasoningChat;
        _logger        = logger;
    }

    public async Task<StemReasoningResult> SolveAsync(string question, string context)
    {
        StemAnswer? structured = null;
        int retries = 0;

        try
        {
            (structured, retries) = await TryStructuredAsync(question, context);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Qwen structured call failed outright for question {Question}", question);
        }

        if (structured != null)
            return new StemReasoningResult(structured, null, retries);

        var prose = await TryFallbackProseAsync(question, context);
        return new StemReasoningResult(null, prose, retries);
    }

    private async Task<(StemAnswer? Answer, int RetriesUsed)> TryStructuredAsync(string question, string context)
    {
        var userMessage = BuildStage1UserMessage(question, context);

        for (int attempt = 0; attempt <= MaxJsonRetries; attempt++)
        {
            var (raw, thinking, doneReason, evalCount) = await _reasoningChat.OneShotReasoningAsync(
                Stage1SystemPrompt, userMessage, think: true, jsonFormat: true, numPredict: ReasoningNumPredict, numCtx: ReasoningNumCtx);

            try
            {
                return (ParseStemAnswer(raw), attempt);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "Qwen structured JSON parse failed (attempt {Attempt}/{Max}) for {Question}. " +
                    "Content length: {Len}, thinking length: {ThinkLen}, doneReason: {DoneReason}, evalCount: {EvalCount}",
                    attempt + 1, MaxJsonRetries + 1, question, raw.Length, thinking?.Length ?? 0, doneReason ?? "(none)", evalCount);
            }
        }

        _logger.LogWarning("Qwen structured JSON exhausted all attempts for {Question}; falling back to prose.", question);
        return (null, MaxJsonRetries + 1);
    }

    private static StemAnswer ParseStemAnswer(string raw)
    {
        var trimmed = raw.Trim();
        var start = trimmed.IndexOf('{');
        var end   = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new JsonException("No JSON object found in Qwen response.");

        var json = trimmed[start..(end + 1)];
        var answer = JsonSerializer.Deserialize<StemAnswer>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (answer is null || string.IsNullOrWhiteSpace(answer.QuestionType))
            throw new JsonException("Parsed StemAnswer is missing questionType.");

        return answer;
    }

    private async Task<string?> TryFallbackProseAsync(string question, string context)
    {
        try
        {
            var (raw, thinking, doneReason, evalCount) = await _reasoningChat.OneShotReasoningAsync(
                FallbackSystemPrompt, BuildStage1UserMessage(question, context),
                think: true, jsonFormat: false, numPredict: ReasoningNumPredict, numCtx: ReasoningNumCtx);

            if (string.IsNullOrWhiteSpace(raw))
                _logger.LogWarning(
                    "Qwen prose fallback returned empty content for {Question}. " +
                    "Thinking length: {ThinkLen}, doneReason: {DoneReason}, evalCount: {EvalCount}",
                    question, thinking?.Length ?? 0, doneReason ?? "(none)", evalCount);

            return string.IsNullOrWhiteSpace(raw) ? null : raw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Qwen prose fallback also failed for {Question}", question);
            return null;
        }
    }

    private static string BuildStage1UserMessage(string question, string context) =>
        "--- Textbook Material ---\n" +
        context +
        "\n--- End of Material ---\n\n" +
        "<student_question>\n" + question + "\n</student_question>";

    // ---- Stage 2 (BgGPT narration) prompt builders — static so RAGService and the CLI
    // test harness share exactly one source of truth for these prompts. ----

    public static string BuildStructuredNarrationSystemPrompt(string questionType, string languageInstruction)
    {
        var basePrompt =
            "You are a school tutor. You are given a JSON object with a fully solved answer to the " +
            "student's math/physics/chemistry question, produced by an internal reasoning module. Your " +
            "ONLY job is to narrate it fluently — you must NOT recompute, re-derive, re-round, alter, or " +
            "re-translate any number, unit, or formula in the JSON. Reproduce every number, unit, and " +
            "formula EXACTLY as given.\n";

        var branch = questionType.Trim().Equals("conceptual", StringComparison.OrdinalIgnoreCase)
            ? "This is a conceptual question. Use the Explanation field as the factual basis and rephrase " +
              "it into a natural, fluent explanation. You may rephrase for fluency but must preserve every " +
              "technical term and fact accurately — do not introduce new facts not present in the " +
              "Explanation field."
            : "This is a numeric problem. Present the formula (Formula field, verbatim), the step-by-step " +
              "solution (Steps, narrated fluently but keeping every number/operation exactly as given), and " +
              "the final answer with its unit (Answer + Unit fields, verbatim — never round or convert them).";

        return basePrompt + branch + "\n" + languageInstruction;
    }

    public static string BuildNarrationUserContent(StemAnswer answer, string question)
    {
        var json = JsonSerializer.Serialize(answer);
        return "--- Solved by internal reasoning module (values are FINAL — do not recompute) ---\n" +
               json +
               "\n--- End of solved data ---\n\n" +
               "<student_question>\n" + question + "\n</student_question>";
    }

    public static string BuildFallbackNarrationSystemPrompt(string languageInstruction) =>
        "You are a school tutor. Below is a draft solution to the student's math/physics/chemistry " +
        "question, written by an internal reasoning module (it may be in English or mixed notation). " +
        "Rewrite it as a natural, fluent answer for the student, preserving every number, unit, and " +
        "formula exactly as given — do not recompute or alter them, only translate/polish the " +
        "surrounding language and phrasing. " + languageInstruction;

    public static string BuildFallbackNarrationUserContent(string prose, string question) =>
        "--- Draft solution from internal reasoning module ---\n" +
        prose +
        "\n--- End of draft ---\n\n" +
        "<student_question>\n" + question + "\n</student_question>";

    private const string Stage1SystemPrompt =
        "You are a math/physics/chemistry problem-solving engine. You will receive textbook context " +
        "and a student's question. Solve the problem step by step using standard mathematical/ " +
        "scientific reasoning and notation. Respond with ONLY a single JSON object — no markdown code " +
        "fences, no prose before or after it — matching EXACTLY this shape:\n" +
        "{\n" +
        "  \"questionType\": \"numeric\" | \"conceptual\",\n" +
        "  \"answer\": string or null,\n" +
        "  \"unit\": string or null,\n" +
        "  \"formula\": string or null,\n" +
        "  \"steps\": [string, ...],\n" +
        "  \"explanation\": string or null\n" +
        "}\n" +
        "Use \"numeric\" when the question asks to calculate/solve for a value; use \"conceptual\" when it " +
        "asks to explain/define/describe a concept. Keep formulas in plain notation, not LaTeX. Base " +
        "your solution on the provided textbook context where relevant; if the context is insufficient, " +
        "still solve using standard curriculum methods. Do not write explanatory prose in Bulgarian or " +
        "any other language — this output is machine-parsed, not shown to the student.";

    private const string FallbackSystemPrompt =
        "You are a math/physics/chemistry tutor. Solve the student's problem step by step using the " +
        "provided textbook context and standard curriculum methods. Show the formula, the worked " +
        "steps, and the final answer with its unit. Write clearly in English; another module will " +
        "translate your answer.";
}
