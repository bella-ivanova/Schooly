using System.Text.Json;

namespace StudyAssistant.Services;

// CLI-only. Runs a fixture of sample math/physics/chemistry (+ one non-STEM negative
// control) questions through the STEM structured-handoff pipeline end to end, logging the
// JSON Qwen produced (or its prose fallback) alongside BgGPT's final Bulgarian narration
// for manual verification, and tracking the malformed-JSON retry rate across the run. See
// TestData/StemFixtures/sample-questions.json for the default question set. Never wired to
// any HTTP endpoint — same CLI-only convention as RetrievalComparisonRunner/ChunkSampler.
public static class StemPipelineTestRunner
{
    public static async Task RunAsync(RAGService rag, StemSubjectClassifier classifier, string fixturePath)
    {
        var json = await File.ReadAllTextAsync(fixturePath);
        var fixture = JsonSerializer.Deserialize<StemTestFixture>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (fixture == null || fixture.Questions.Count == 0)
        {
            Console.WriteLine($"Could not parse fixture at '{fixturePath}', or it has no questions.");
            return;
        }

        Console.WriteLine($"Loaded fixture '{fixturePath}': {fixture.Questions.Count} questions.");
        Console.WriteLine();

        int routedToStem = 0, structuredOk = 0, fallbackUsed = 0, totalFailures = 0;
        int retrySum = 0, questionsWithRetries = 0;
        int subjectMismatches = 0;

        foreach (var q in fixture.Questions)
        {
            Console.WriteLine($"=== [{q.Id}] {q.Question} (grade {q.Grade}, expected: {q.ExpectedSubject}) ===");
            rag.SetGrade(q.Grade);

            var subject = await classifier.ClassifyAsync(q.Question);
            var expectedMatches = string.Equals(subject.ToString(), q.ExpectedSubject, StringComparison.OrdinalIgnoreCase);
            if (!expectedMatches) subjectMismatches++;
            Console.WriteLine($"Classifier: {subject} {(expectedMatches ? "(matches expected)" : "(MISMATCH vs expected)")}");

            if (subject == StemSubject.None)
            {
                Console.WriteLine("Skipped — not routed through the STEM pipeline.");
                Console.WriteLine();
                continue;
            }

            routedToStem++;
            var (reasoning, narration) = await rag.AskStemDiagnosticAsync(q.Question);

            retrySum += reasoning.RetryCount;
            if (reasoning.RetryCount > 0) questionsWithRetries++;
            Console.WriteLine($"Structured JSON retries used: {reasoning.RetryCount}");

            if (reasoning.Structured != null)
            {
                structuredOk++;
                var reserialized = JsonSerializer.Serialize(reasoning.Structured, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine("Stage 1 (Qwen, structured JSON):");
                Console.WriteLine(reserialized);
            }
            else if (reasoning.FallbackProse != null)
            {
                fallbackUsed++;
                Console.WriteLine("Stage 1 (Qwen, FALLBACK PROSE — structured JSON was exhausted):");
                Console.WriteLine(reasoning.FallbackProse);
            }
            else
            {
                totalFailures++;
                Console.WriteLine("Stage 1 FAILED ENTIRELY — pipeline degraded to the generic single-stage answer path.");
            }

            Console.WriteLine("Stage 2 (BgGPT, final Bulgarian narration):");
            Console.WriteLine(narration);
            Console.WriteLine();
        }

        Console.WriteLine("=== Summary ===");
        Console.WriteLine($"Questions: {fixture.Questions.Count} total, {routedToStem} routed through the STEM pipeline.");
        Console.WriteLine($"Classifier mismatches vs expected subject: {subjectMismatches}/{fixture.Questions.Count}");
        if (routedToStem > 0)
        {
            Console.WriteLine($"Structured JSON succeeded: {structuredOk}/{routedToStem}");
            Console.WriteLine($"Fallback prose used (structured JSON exhausted): {fallbackUsed}/{routedToStem}");
            Console.WriteLine($"Total pipeline failures (degraded to generic path): {totalFailures}/{routedToStem}");
            var avgRetries = retrySum / (double)routedToStem;
            Console.WriteLine($"Malformed-JSON retry rate: {questionsWithRetries}/{routedToStem} questions needed at least one retry (avg {avgRetries:F2} retries/question).");
        }
    }
}
