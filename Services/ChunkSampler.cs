using System.Text.Json;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace StudyAssistant.Services;

// CLI-only, READ-ONLY tool for browsing the production "studyassist" collection so a
// human can pick real chunks to build a retrieval regression fixture. Never calls
// UpsertAsync/DeleteAsync/DeleteCollectionAsync against this (or any) collection —
// Scroll/Retrieve only. Same CLI-only rule as RetrievalDiagnostics/VisualisationService.
public static class ChunkSampler
{
    // Must match QdrantService's private CollectionName constant.
    public const string ProductionCollectionName = "studyassist";

    public static async Task SampleAsync(string host, int port, int grade, string? sourceFile, int limit)
    {
        var client = new QdrantClient(host, port);

        var filter = new Filter();
        filter.Must.Add(Conditions.Match("grade", (long)grade));
        if (!string.IsNullOrEmpty(sourceFile))
            filter.Must.Add(Conditions.MatchKeyword("sourceFile", sourceFile));

        var response = await client.ScrollAsync(
            ProductionCollectionName,
            filter:          filter,
            limit:           (uint)limit,
            payloadSelector: true);

        if (response.Result.Count == 0)
        {
            Console.WriteLine("No chunks found for that grade/sourceFile filter.");
            return;
        }

        Console.WriteLine($"{"PointId",-38} {"Page",-5} {"Subject",-12} {"Source",-30} Text");
        Console.WriteLine(new string('-', 140));

        foreach (var point in response.Result)
        {
            var text = point.Payload["text"].StringValue.Replace("\n", " ").Replace("\r", " ").Trim();
            var preview = text.Length > 200 ? text[..200] + "..." : text;
            var page = point.Payload.TryGetValue("page", out var p) ? (int)p.IntegerValue : 0;
            var subject = point.Payload.TryGetValue("subject", out var s) ? s.StringValue : "";
            var source = point.Payload.TryGetValue("sourceFile", out var sf) ? sf.StringValue : "";

            Console.WriteLine($"{point.Id.Uuid,-38} {page,-5} {Truncate(subject, 12),-12} {Truncate(source, 30),-30} {preview}");
        }

        Console.WriteLine();
        Console.WriteLine($"{response.Result.Count} chunk(s) shown (limit {limit}). Use 'show-chunk <pointId>' for full text.");
    }

    public static async Task ShowChunkAsync(string host, int port, string pointId)
    {
        var client = new QdrantClient(host, port);

        if (!Guid.TryParse(pointId, out var guid))
        {
            Console.WriteLine($"'{pointId}' is not a valid point id (expected a GUID, as printed by sample-chunks).");
            return;
        }

        var points = await client.RetrieveAsync(ProductionCollectionName, guid, withPayload: true);
        if (points.Count == 0)
        {
            Console.WriteLine($"No chunk found with id '{pointId}' in '{ProductionCollectionName}'.");
            return;
        }

        var point = points[0];
        Console.WriteLine($"PointId:    {point.Id.Uuid}");
        Console.WriteLine($"SourceFile: {(point.Payload.TryGetValue("sourceFile", out var sf) ? sf.StringValue : "")}");
        Console.WriteLine($"Page:       {(point.Payload.TryGetValue("page", out var p) ? p.IntegerValue.ToString() : "")}");
        Console.WriteLine($"Subject:    {(point.Payload.TryGetValue("subject", out var s) ? s.StringValue : "")}");
        Console.WriteLine($"Grade:      {(point.Payload.TryGetValue("grade", out var g) ? g.IntegerValue.ToString() : "")}");
        Console.WriteLine();
        Console.WriteLine(point.Payload["text"].StringValue);
    }

    // Shared resolver reused by RetrievalComparisonRunner: scrolls the given collection
    // (production when resolving fixture source chunks, or the diagnostic's own temp
    // collection when re-correlating search results) filtered by sourceFile+grade, and
    // returns the first point on the given page whose text contains matchText. Returns
    // null if nothing matched — callers should treat that as "fixture may be stale vs.
    // current ingestion" rather than silently mis-scoring.
    public static async Task<(string PointId, string Text, string Subject)?> ResolveChunkAsync(
        QdrantClient client, string collectionName, int grade, string sourceFile, int page, string matchText)
    {
        var filter = new Filter();
        filter.Must.Add(Conditions.Match("grade", (long)grade));
        filter.Must.Add(Conditions.MatchKeyword("sourceFile", sourceFile));

        PointId? offset = null;
        const uint batchSize = 100;

        while (true)
        {
            var response = await client.ScrollAsync(
                collectionName,
                filter:          filter,
                limit:           batchSize,
                offset:          offset,
                payloadSelector: true);

            foreach (var point in response.Result)
            {
                var pointPage = point.Payload.TryGetValue("page", out var p) ? (int)p.IntegerValue : -1;
                if (pointPage != page) continue;

                var text = point.Payload["text"].StringValue;
                if (text.Contains(matchText, StringComparison.OrdinalIgnoreCase))
                {
                    var subject = point.Payload.TryGetValue("subject", out var s) ? s.StringValue : "";
                    return (point.Id.Uuid, text, subject);
                }
            }

            if (response.Result.Count < batchSize || response.NextPageOffset is null) break;
            offset = response.NextPageOffset;
        }

        return null;
    }

    // Read-only full scan of every chunk for a grade — used by RetrievalComparisonRunner's
    // --full-corpus mode to re-embed the ENTIRE corpus (not just the fixture's own union of
    // expected chunks) into a temp collection, removing the pool-size confound of testing
    // against a tiny decoy set. Uses an exact grade match, unlike QdrantService.SearchAsync's
    // "grade <= N" range filter — equivalent today since only Grade10/Math exists on disk,
    // but would silently diverge from what production actually competes against if other
    // grades are ever ingested.
    public static async Task<List<(string PointId, string Text, string Subject, string SourceFile, int Page)>> GetAllChunksForGradeAsync(
        QdrantClient client, string collectionName, int grade)
    {
        var results = new List<(string PointId, string Text, string Subject, string SourceFile, int Page)>();

        var filter = new Filter();
        filter.Must.Add(Conditions.Match("grade", (long)grade));

        PointId? offset = null;
        const uint batchSize = 200;

        while (true)
        {
            var response = await client.ScrollAsync(
                collectionName,
                filter:          filter,
                limit:           batchSize,
                offset:          offset,
                payloadSelector: true);

            foreach (var point in response.Result)
            {
                var text = point.Payload["text"].StringValue;
                var subject = point.Payload.TryGetValue("subject", out var s) ? s.StringValue : "";
                var sourceFile = point.Payload.TryGetValue("sourceFile", out var sf) ? sf.StringValue : "";
                var page = point.Payload.TryGetValue("page", out var p) ? (int)p.IntegerValue : 0;
                results.Add((point.Id.Uuid, text, subject, sourceFile, page));
            }

            if (response.Result.Count < batchSize || response.NextPageOffset is null) break;
            offset = response.NextPageOffset;
        }

        return results;
    }

    // READ-ONLY validation pass over a fixture file (see RetrievalFixtureModels.cs):
    // resolves every expectedChunk against production and reports OK/MISSING, so typos
    // or stale matchText snippets are caught before building/running the comparison.
    public static async Task ValidateFixtureAsync(string host, int port, string fixturePath)
    {
        var json = await File.ReadAllTextAsync(fixturePath);
        var fixture = JsonSerializer.Deserialize<RetrievalFixture>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (fixture == null)
        {
            Console.WriteLine($"Could not parse fixture at '{fixturePath}'.");
            return;
        }

        var client = new QdrantClient(host, port);
        int ok = 0, missing = 0;

        foreach (var q in fixture.Questions)
        {
            foreach (var expected in q.ExpectedChunks)
            {
                var resolved = await ResolveChunkAsync(client, ProductionCollectionName, fixture.Grade, expected.SourceFile, expected.Page, expected.MatchText);
                if (resolved == null)
                {
                    missing++;
                    Console.WriteLine($"MISSING  [{q.Id}] {expected.SourceFile} p.{expected.Page} matchText=\"{expected.MatchText}\"");
                }
                else
                {
                    ok++;
                    Console.WriteLine($"OK       [{q.Id}] {expected.SourceFile} p.{expected.Page} -> {resolved.Value.PointId}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{ok} resolved, {missing} missing, across {fixture.Questions.Count} questions.");
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}
