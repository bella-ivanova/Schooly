using System.Text.Json;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace StudyAssistant.Services;

// CLI-only. Evaluates a retrieval regression fixture (see RetrievalFixtureModels.cs)
// against 3 embedding configs: current production, prefixed (same model), and
// prefixed + nomic-embed-text-v2-moe. Never writes/deletes against the production
// "studyassist" collection (config 1 is a pure read via QdrantService.SearchAsync;
// configs 2/3 only ever Scroll it via ChunkSampler.ResolveChunkAsync to copy chunk
// text out). Configs 2/3 create disposable temp collections, always torn down in a
// finally block before returning.
//
// Both nomic-embed-text v1.5 and nomic-embed-text-v2-moe use the same
// "search_query: "/"search_document: " task-prefix convention — confirmed against
// nomic-ai/nomic-embed-text-v2-moe's model card before hardcoding these constants,
// rather than assuming v2-moe (a different, multilingual MoE architecture) follows
// v1.5's convention unverified.
public static class RetrievalComparisonRunner
{
    private const string TempCollectionPrefixed     = "studyassist_diag_temp_prefixed";
    private const string TempCollectionV2Moe        = "studyassist_diag_temp_v2moe";
    private const string TempCollectionPrefixedFull = "studyassist_diag_temp_prefixed_full";
    private const string TempCollectionV2MoeFull    = "studyassist_diag_temp_v2moe_full";
    private const string V2MoeModel                 = "nomic-embed-text-v2-moe";
    private const int    TopNForHit                 = 3;
    private const int    WideTopK                   = 50;
    private const int    SmallPoolBatchSize         = 10;
    private const int    FullCorpusBatchSize        = 50;

    public static async Task RunAsync(
        string fixturePath, string embedModel, string qdrantHost, int qdrantPort,
        bool fullCorpus = false, bool currentPrefix = false)
    {
        var json = await File.ReadAllTextAsync(fixturePath);
        var fixture = JsonSerializer.Deserialize<RetrievalFixture>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (fixture == null)
        {
            Console.WriteLine($"Could not parse fixture at '{fixturePath}'.");
            return;
        }

        Console.WriteLine($"Loaded fixture '{fixturePath}': {fixture.Questions.Count} questions, grade {fixture.Grade}.");
        Console.WriteLine("Prefix convention verified against nomic-ai/nomic-embed-text-v2-moe's model card: both v1.5 and");
        Console.WriteLine("v2-moe document \"search_query: \"/\"search_document: \" as their task-instruction prefix — the same");
        Console.WriteLine("convention is used for both prefixed configs below.");
        if (fullCorpus)
            Console.WriteLine("Mode: --full-corpus — configs 2/3 re-embed the ENTIRE grade's corpus, not just the fixture's own chunks.");
        if (currentPrefix)
            Console.WriteLine("Mode: --current-prefix — Config 1 embeds the query WITH the prefix (use after production has been migrated to always prefix).");
        Console.WriteLine();

        var prodEmbedding = new EmbeddingService(embedModel);
        var prodQdrant    = new QdrantService(qdrantHost, qdrantPort);

        Console.WriteLine($"=== Config 1: current (production collection, production model, {(currentPrefix ? "prefixed" : "no prefix")}) ===");
        var currentResults = await EvalCurrentAsync(fixture, prodEmbedding, prodQdrant, currentPrefix);

        List<(string PointId, string Text, string Subject, string SourceFile, int Page)>? fullCorpusChunks = null;
        if (fullCorpus)
        {
            Console.WriteLine();
            Console.WriteLine($"Scanning full corpus for grade {fixture.Grade} (read-only Scroll against '{ChunkSampler.ProductionCollectionName}')...");
            var scanClient = new QdrantClient(qdrantHost, qdrantPort);
            fullCorpusChunks = await ChunkSampler.GetAllChunksForGradeAsync(scanClient, ChunkSampler.ProductionCollectionName, fixture.Grade);
            Console.WriteLine($"Found {fullCorpusChunks.Count} chunks — this exact set will be embedded into both temp collections below.");
        }

        var prefixedCollectionName = fullCorpus ? TempCollectionPrefixedFull : TempCollectionPrefixed;
        var v2moeCollectionName    = fullCorpus ? TempCollectionV2MoeFull    : TempCollectionV2Moe;
        var batchSize = fullCorpus ? FullCorpusBatchSize : SmallPoolBatchSize;

        Console.WriteLine();
        Console.WriteLine($"=== Config 2: prefixed ({embedModel}, temp collection) ===");
        var prefixedResults = await EvalPrefixedTempCollectionAsync(fixture, embedModel, prefixedCollectionName, qdrantHost, qdrantPort, batchSize, fullCorpusChunks);

        Console.WriteLine();
        Console.WriteLine($"=== Config 3: prefixed + {V2MoeModel} (temp collection) ===");
        var v2moeResults = await EvalPrefixedTempCollectionAsync(fixture, V2MoeModel, v2moeCollectionName, qdrantHost, qdrantPort, batchSize, fullCorpusChunks);

        Console.WriteLine();
        PrintPerQuestionTable(fixture, currentResults, prefixedResults, v2moeResults);
        PrintAggregateSummary(fixture, currentResults, prefixedResults, v2moeResults, currentPrefix);

        Console.WriteLine();
        Console.WriteLine($"Production collection '{ChunkSampler.ProductionCollectionName}' was only read (Scroll/Search) this run — never written to.");
        Console.WriteLine($"Temp collections created and deleted this run: {prefixedCollectionName}, {v2moeCollectionName}.");
        if (fullCorpus)
            Console.WriteLine("NOTE: full-corpus hit rates are expected to be lower than a small-decoy-pool run — that's the pool-size confound being removed, not a regression. Compare against the current-config baseline, not against a prior small-pool headline number.");
    }

    private readonly record struct ChunkEval(int? Rank, float? Score)
    {
        public bool Top3 => Rank.HasValue && Rank.Value <= TopNForHit;
    }

    // Config 1: production collection, production model. Pure read via the unmodified
    // QdrantService.SearchAsync — wide topK so a chunk's true rank/score is captured even
    // when it falls outside top-3. usePrefix defaults to false (today's actual production
    // behavior); pass true post-migration to validate the real, now-always-prefixed
    // production collection end-to-end with zero new tooling.
    private static async Task<Dictionary<string, List<ChunkEval>>> EvalCurrentAsync(
        RetrievalFixture fixture, EmbeddingService embed, QdrantService qdrant, bool usePrefix = false)
    {
        var byQuestion = new Dictionary<string, List<ChunkEval>>();

        foreach (var q in fixture.Questions)
        {
            var textToEmbed = usePrefix ? EmbeddingService.QueryPrefix + q.Question : q.Question;
            var embeddings = await embed.GetEmbeddingsAsync(new List<string> { textToEmbed });
            var results = embeddings.Count == 0
                ? []
                : await qdrant.SearchAsync(embeddings[0], topK: WideTopK, minScore: 0f, gradeFilter: fixture.Grade);

            var flat = results.Select(r => new RankedResult(null, r.SourceFile, r.Page, r.Text, r.Score)).ToList();
            byQuestion[q.Id] = q.ExpectedChunks.Select(expected => FindRank(flat, expected, expectedPointId: null)).ToList();
        }

        return byQuestion;
    }

    // Configs 2 & 3: builds a disposable temp collection (never QdrantService, never the
    // production collection name), re-embeds either the fixture's unique chunks (small
    // decoy pool, default) or the ENTIRE grade's corpus (fullCorpusChunks, --full-corpus
    // mode) with the document prefix, upserts them, searches with the query prefix, then
    // always tears the temp collection down before returning.
    //
    // In full-corpus mode, each temp point reuses its ORIGINAL production point id (safe
    // — this runs before any reingest) so FindRank can match by exact id instead of the
    // fuzzy (sourceFile, page, matchText-contains) check. This matters at full-corpus scale:
    // PDFLoader.ChunkPages routinely produces multiple overlapping chunks per page, so the
    // fuzzy match alone could latch onto the wrong sibling chunk once all of them are present.
    //
    // KNOWN LIMITATION: some pages in this corpus contain several byte-identical duplicate
    // chunks (confirmed: e.g. 12 exact copies of one worked example on Irational p.10) —
    // real content repetition or an upstream ingestion artifact, not something this tool
    // changes. Exact-id matching can only credit a hit when that one arbitrarily-resolved
    // duplicate's id specifically ranks top-K, even though any of its identical twins
    // ranking top-K would serve the same answer in production. This makes --full-corpus
    // mode a conservative (slightly pessimistic) estimate wherever duplicates exist — a
    // direct compare-retrieval run against the real collection (fuzzy match, no id lock)
    // isn't subject to this and is the more representative number when the two disagree.
    private static async Task<Dictionary<string, List<ChunkEval>>> EvalPrefixedTempCollectionAsync(
        RetrievalFixture fixture, string modelName, string tempCollectionName, string host, int port,
        int batchSize = SmallPoolBatchSize,
        List<(string PointId, string Text, string Subject, string SourceFile, int Page)>? fullCorpusChunks = null)
    {
        if (tempCollectionName == ChunkSampler.ProductionCollectionName)
            throw new InvalidOperationException("Refusing to use the production collection name for a temp collection.");

        var client = new QdrantClient(host, port);
        var embed  = new EmbeddingService(modelName);
        var byQuestion = new Dictionary<string, List<ChunkEval>>();

        try
        {
            if (await client.CollectionExistsAsync(tempCollectionName))
                await client.DeleteCollectionAsync(tempCollectionName);

            var dim = await ProbeDimensionAsync(embed);
            await client.CreateCollectionAsync(tempCollectionName, new VectorParams { Size = (uint)dim, Distance = Distance.Cosine });
            Console.WriteLine($"Model: {modelName} (dim={dim}) — temp collection '{tempCollectionName}' created.");

            // (PointId, Text, Subject, SourceFile, Page) — PointId is the ORIGINAL production
            // id in full-corpus mode, or null in small-pool mode (where a fresh Guid is minted
            // per upserted point instead, since there's no single "the" source id to reuse).
            List<(string? PointId, string Text, string Subject, string SourceFile, int Page)> sourceChunks;

            if (fullCorpusChunks != null)
            {
                sourceChunks = fullCorpusChunks.Select(c => ((string?)c.PointId, c.Text, c.Subject, c.SourceFile, c.Page)).ToList();
            }
            else
            {
                // Union of all expected chunks across the fixture, resolved against
                // production read-only (Scroll only) so each unique chunk is re-embedded once.
                var uniqueChunks = new Dictionary<(string SourceFile, int Page, string MatchText), (string Text, string Subject)>();
                foreach (var expected in fixture.Questions.SelectMany(q => q.ExpectedChunks))
                {
                    var key = (expected.SourceFile, expected.Page, expected.MatchText);
                    if (uniqueChunks.ContainsKey(key)) continue;

                    var resolved = await ChunkSampler.ResolveChunkAsync(
                        client, ChunkSampler.ProductionCollectionName, fixture.Grade, expected.SourceFile, expected.Page, expected.MatchText);

                    if (resolved == null)
                    {
                        Console.WriteLine($"  WARNING: could not resolve {expected.SourceFile} p.{expected.Page} matchText=\"{expected.MatchText}\" — fixture may be stale vs. current ingestion.");
                        continue;
                    }

                    uniqueChunks[key] = (resolved.Value.Text, resolved.Value.Subject);
                }

                sourceChunks = uniqueChunks.Select(kv => ((string?)null, kv.Value.Text, kv.Value.Subject, kv.Key.SourceFile, kv.Key.Page)).ToList();
            }

            var documentTexts = sourceChunks.Select(c => EmbeddingService.DocumentPrefix + c.Text).ToList();
            var documentEmbeddings = await EmbedWithProgressAsync(embed, documentTexts, batchSize, "Indexing");

            var points = new List<PointStruct>();
            for (int i = 0; i < sourceChunks.Count; i++)
            {
                var chunk = sourceChunks[i];
                var dense = new DenseVector();
                dense.Data.AddRange(documentEmbeddings[i]);

                var point = new PointStruct
                {
                    Id      = new PointId { Uuid = chunk.PointId ?? Guid.NewGuid().ToString() },
                    Vectors = new Vectors { Vector = new Vector { Dense = dense } },
                };
                point.Payload["text"]       = chunk.Text;
                point.Payload["subject"]    = chunk.Subject;
                point.Payload["grade"]      = fixture.Grade;
                point.Payload["sourceFile"] = chunk.SourceFile;
                point.Payload["page"]       = chunk.Page;
                points.Add(point);
            }
            await client.UpsertAsync(tempCollectionName, points);
            Console.WriteLine($"  Upserted {points.Count} chunk(s), embedded with \"{EmbeddingService.DocumentPrefix.Trim()}\" prefix.");

            foreach (var q in fixture.Questions)
            {
                var queryEmbeddings = await embed.GetEmbeddingsAsync(new List<string> { EmbeddingService.QueryPrefix + q.Question });
                var searchResults = await client.SearchAsync(
                    tempCollectionName,
                    queryEmbeddings[0],
                    limit:           (ulong)WideTopK,
                    scoreThreshold:  0f,
                    payloadSelector: true);

                var flat = searchResults.Select(r => new RankedResult(
                    r.Id.Uuid,
                    r.Payload["sourceFile"].StringValue,
                    (int)r.Payload["page"].IntegerValue,
                    r.Payload["text"].StringValue,
                    r.Score)).ToList();

                byQuestion[q.Id] = q.ExpectedChunks.Select(expected =>
                {
                    var expectedPointId = fullCorpusChunks != null ? ResolveChunkIdFromList(sourceChunks, expected) : null;
                    return FindRank(flat, expected, expectedPointId);
                }).ToList();
            }
        }
        finally
        {
            if (await client.CollectionExistsAsync(tempCollectionName))
                await client.DeleteCollectionAsync(tempCollectionName);
        }

        return byQuestion;
    }

    private readonly record struct RankedResult(string? PointId, string SourceFile, int Page, string Text, float Score);

    // Finds the expected chunk's rank/score within a page's-worth of already-ranked search
    // results. When expectedPointId is known (full-corpus mode), matches by exact id — the
    // only reliable option once a page can legitimately hold several overlapping chunks.
    // Otherwise falls back to the fuzzy (sourceFile, page, matchText-contains) check used
    // everywhere else (Config 1, which must survive a reingest and can never rely on a
    // stable id; and small-pool mode, where each temp point gets a fresh, unrelated Guid).
    private static ChunkEval FindRank(List<RankedResult> rankedResults, ExpectedChunk expected, string? expectedPointId)
    {
        for (int i = 0; i < rankedResults.Count; i++)
        {
            var r = rankedResults[i];
            var isMatch = expectedPointId != null
                ? r.PointId == expectedPointId
                : r.SourceFile == expected.SourceFile && r.Page == expected.Page &&
                  r.Text.Contains(expected.MatchText, StringComparison.OrdinalIgnoreCase);

            if (isMatch) return new ChunkEval(i + 1, r.Score);
        }
        return new ChunkEval(null, null);
    }

    // In-memory equivalent of ChunkSampler.ResolveChunkAsync, scanning an already-fetched
    // chunk list (the same one used to populate the full-corpus temp collection) instead of
    // making another Qdrant round-trip.
    private static string? ResolveChunkIdFromList(
        List<(string? PointId, string Text, string Subject, string SourceFile, int Page)> chunks, ExpectedChunk expected)
    {
        foreach (var c in chunks)
        {
            if (c.SourceFile == expected.SourceFile && c.Page == expected.Page &&
                c.Text.Contains(expected.MatchText, StringComparison.OrdinalIgnoreCase))
                return c.PointId;
        }
        return null;
    }

    // Manually chunks the embed calls (rather than relying on GetEmbeddingsAsync's internal
    // batching alone) so a multi-minute full-corpus run can print periodic progress instead
    // of looking hung — mirrors RAGService's existing per-file ingestion progress line.
    private static async Task<List<float[]>> EmbedWithProgressAsync(EmbeddingService embed, List<string> texts, int batchSize, string label)
    {
        var results = new List<float[]>();
        for (int i = 0; i < texts.Count; i += batchSize)
        {
            var batch = texts.GetRange(i, Math.Min(batchSize, texts.Count - i));
            var embeddings = await embed.GetEmbeddingsAsync(batch, batchSize: batch.Count);
            results.AddRange(embeddings);
            Console.WriteLine($"  {label}: embedded {results.Count}/{texts.Count}");
        }
        return results;
    }

    // Embedding dimensionality is a fixed architectural property of a model, independent
    // of input text, so probing with one throwaway string is reliable; GetEmbeddingsAsync
    // already throws a clear "is it pulled?" error if the model isn't available locally.
    private static async Task<int> ProbeDimensionAsync(EmbeddingService embed)
    {
        var probe = await embed.GetEmbeddingsAsync(new List<string> { "probe" });
        return probe[0].Length;
    }

    private static void PrintPerQuestionTable(
        RetrievalFixture fixture,
        Dictionary<string, List<ChunkEval>> current,
        Dictionary<string, List<ChunkEval>> prefixed,
        Dictionary<string, List<ChunkEval>> v2moe)
    {
        foreach (var q in fixture.Questions)
        {
            Console.WriteLine($"{q.Id} [{q.Type}] \"{q.Question}\"");
            for (int i = 0; i < q.ExpectedChunks.Count; i++)
            {
                var expected = q.ExpectedChunks[i];
                Console.WriteLine($"  Expected ({expected.SourceFile} p.{expected.Page}):");
                PrintConfigLine("Current             ", current[q.Id][i]);
                PrintConfigLine("Prefixed            ", prefixed[q.Id][i]);
                PrintConfigLine("Prefixed+v2-moe     ", v2moe[q.Id][i]);
            }

            var currentPass = EvaluatePass(q, current[q.Id]);
            var prefixedPass = EvaluatePass(q, prefixed[q.Id]);
            var v2moePass = EvaluatePass(q, v2moe[q.Id]);
            Console.WriteLine($"  Question pass (requireAll={q.RequireAll}): current={(currentPass ? "YES" : "no")}  prefixed={(prefixedPass ? "YES" : "no")}  prefixed+v2moe={(v2moePass ? "YES" : "no")}");
            Console.WriteLine();
        }
    }

    private static void PrintConfigLine(string label, ChunkEval e)
    {
        var rank = e.Rank?.ToString() ?? "not found";
        var score = e.Score.HasValue ? e.Score.Value.ToString("F4") : "-";
        Console.WriteLine($"    {label}: rank={rank,-10} score={score,-8} top3={(e.Top3 ? "YES" : "no")}");
    }

    private static bool EvaluatePass(FixtureQuestion q, List<ChunkEval> evals) =>
        q.RequireAll ? evals.All(e => e.Top3) : evals.Any(e => e.Top3);

    private static void PrintAggregateSummary(
        RetrievalFixture fixture,
        Dictionary<string, List<ChunkEval>> current,
        Dictionary<string, List<ChunkEval>> prefixed,
        Dictionary<string, List<ChunkEval>> v2moe,
        bool currentPrefix)
    {
        Console.WriteLine($"=== Aggregate ({fixture.Questions.Count} questions) ===");
        PrintAggregateRow($"Current (prod, {(currentPrefix ? "prefixed" : "no prefix")})".PadRight(35), fixture, current);
        PrintAggregateRow("Prefixed (same model)             ", fixture, prefixed);
        PrintAggregateRow($"Prefixed + {V2MoeModel}", fixture, v2moe);
    }

    private static void PrintAggregateRow(string label, RetrievalFixture fixture, Dictionary<string, List<ChunkEval>> results)
    {
        var passCount = fixture.Questions.Count(q => EvaluatePass(q, results[q.Id]));
        var allEvals = fixture.Questions.SelectMany(q => results[q.Id]).ToList();
        var avgScore = allEvals.Count == 0 ? 0f : allEvals.Average(e => e.Score ?? 0f);
        var notFoundCount = allEvals.Count(e => !e.Rank.HasValue);

        Console.WriteLine(
            $"{label}  questions passing (all expected chunks in top-{TopNForHit}): {passCount}/{fixture.Questions.Count}   " +
            $"avg score of expected chunks: {avgScore:F4}   ({notFoundCount} not found in top-{WideTopK})");
    }
}
