namespace StudyAssistant.Services;

// CLI-only diagnostic tool for inspecting raw Qdrant retrieval quality outside the
// current 0.1f minScore threshold. Must not be wired to any HTTP endpoint — same
// CLI-only rule as VisualisationService.
public static class RetrievalDiagnostics
{
    private const float CurrentMinScore = 0.1f;

    // Reproduces RAGService.GetContextAsync's current embedding behavior exactly:
    // the raw question text, no "search_query:"/"search_document:" prefix.
    public static async Task RunAsync(string question, int grade, EmbeddingService embeddingService, QdrantService qdrant)
    {
        Console.WriteLine($"Question: \"{question}\"");
        Console.WriteLine($"Grade filter: <= {grade}");
        Console.WriteLine($"Embedding model: {embeddingService.Model} (raw text, no prefix)");
        Console.WriteLine();

        var embeddings = await embeddingService.GetEmbeddingsAsync(new List<string> { question });
        if (embeddings.Count == 0)
        {
            Console.WriteLine("Embedding call returned no result — is the embedding model pulled?");
            return;
        }

        var results = await qdrant.SearchAsync(
            embeddings[0],
            topK:        20,
            minScore:    0f,
            gradeFilter: grade);

        if (results.Count == 0)
        {
            Console.WriteLine("No results at all (minScore 0) — collection may be empty or unreachable.");
            return;
        }

        Console.WriteLine($"{"Rank",-5} {"Score",-8} {"Pass?",-6} {"Subject",-12} {"Grade",-6} {"Source",-30} Text");
        Console.WriteLine(new string('-', 120));

        int rank = 1;
        foreach (var r in results)
        {
            bool passes = r.Score >= CurrentMinScore;
            var preview = r.Text.Replace("\n", " ").Replace("\r", " ").Trim();
            if (preview.Length > 150) preview = preview[..150] + "...";

            Console.WriteLine(
                $"{rank,-5} {r.Score,-8:F4} {(passes ? "YES" : "no"),-6} {Truncate(r.Subject, 12),-12} " +
                $"{r.Grade,-6} {Truncate(r.SourceFile, 30),-30} {preview}");
            rank++;
        }

        Console.WriteLine();
        var passCount = results.Count(r => r.Score >= CurrentMinScore);
        Console.WriteLine($"{passCount}/{results.Count} results would pass the current minScore {CurrentMinScore} threshold.");
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}
