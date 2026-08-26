using Qdrant.Client;
using Qdrant.Client.Grpc;

// Alias to avoid conflict with System.Range
using QRange = Qdrant.Client.Grpc.Range;

namespace StudyAssistant.Services;

public class QdrantService
{
    private readonly QdrantClient _client;

    // The name of the collection inside Qdrant — like a table in a database.
    private const string CollectionName = "studyassist";

    public QdrantService(string host = "localhost", int port = 6334)
    {
        _client = new QdrantClient(host, port);
    }

    // Creates the collection if it doesn't already exist.
    // vectorSize must match the embedding model output (nomic-embed-text = 768 dimensions).
    public async Task EnsureCollectionAsync(uint vectorSize = 768)
    {
        var collections = await _client.ListCollectionsAsync();
        bool exists = collections.Any(c => c == CollectionName);

        if (!exists)
        {
            await _client.CreateCollectionAsync(CollectionName,
                new VectorParams { Size = vectorSize, Distance = Distance.Cosine });
            Console.WriteLine($"Created Qdrant collection '{CollectionName}'.");
        }
    }

    // Stores a batch of chunks with their embeddings into Qdrant.
    public async Task UpsertChunksAsync(List<ChunkData> chunks)
    {
        if (chunks.Count == 0) return;

        var points = new List<PointStruct>();

        foreach (var chunk in chunks)
        {
            // Dense = the modern way to set a plain float vector
            var dense = new DenseVector();
            dense.Data.AddRange(chunk.Embedding);

            var point = new PointStruct
            {
                Id      = new PointId { Uuid = Guid.NewGuid().ToString() },
                Vectors = new Vectors { Vector = new Vector { Dense = dense } },
            };

            point.Payload["text"]       = chunk.Text;
            point.Payload["subject"]    = chunk.Subject;
            point.Payload["grade"]      = chunk.Grade;
            point.Payload["sourceFile"] = chunk.SourceFile;
            point.Payload["page"]       = chunk.PageNumber;

            points.Add(point);
        }

        await _client.UpsertAsync(CollectionName, points);
    }

    // Searches for the most similar chunks to a query embedding.
    // Optionally filters to only search chunks from grades 1 up to gradeFilter.
    public async Task<List<SearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK = 10,
        float minScore = 0.1f,
        int? gradeFilter = null)
    {
        Filter? filter = null;

        if (gradeFilter.HasValue)
        {
            // Build a filter: grade >= 1 AND grade <= N
            var gradeRange = new Filter();
            gradeRange.Must.Add(Conditions.Range("grade", new QRange { Gte = 1, Lte = gradeFilter.Value }));
            filter = gradeRange;
        }

        var results = await _client.SearchAsync(
            CollectionName,
            queryEmbedding,
            filter:          filter,
            limit:           (ulong)topK,
            scoreThreshold:  minScore,
            payloadSelector: true   // true = include all payload fields in the results
        );

        return results.Select(r => new SearchResult
        {
            Text       = r.Payload["text"].StringValue,
            Subject    = r.Payload["subject"].StringValue,
            Grade      = (int)r.Payload["grade"].IntegerValue,
            SourceFile = r.Payload["sourceFile"].StringValue,
            // Guarded: points upserted before this field was added won't have it yet.
            Page       = r.Payload.TryGetValue("page", out var page) ? (int)page.IntegerValue : 0,
            Score      = r.Score
        }).ToList();
    }

    // Deletes all chunks that came from a specific file in a specific grade.
    public async Task DeleteFileAsync(int grade, string sourceFile)
    {
        var filter = new Filter();
        filter.Must.Add(Conditions.Match("grade",      (long)grade));
        filter.Must.Add(Conditions.MatchKeyword("sourceFile", sourceFile));
        await _client.DeleteAsync(CollectionName, filter);
    }

    // Returns all unique source file names stored for a given grade.
    public async Task<List<string>> GetIngestedFilesAsync(int grade)
    {
        var files     = new HashSet<string>();
        PointId?   offset    = null;
        const uint batchSize = 100;

        while (true)
        {
            var filter = new Filter();
            filter.Must.Add(Conditions.Match("grade", (long)grade));

            var response = await _client.ScrollAsync(
                CollectionName,
                filter:          filter,
                limit:           batchSize,
                offset:          offset,
                payloadSelector: true
            );

            foreach (var point in response.Result)
                files.Add(point.Payload["sourceFile"].StringValue);

            if (response.Result.Count < batchSize || response.NextPageOffset is null) break;
            offset = response.NextPageOffset;
        }

        return files.ToList();
    }
}

// Data passed in when storing a chunk
public class ChunkData
{
    public string  Text       { get; set; } = "";
    public float[] Embedding  { get; set; } = [];
    public string  Subject    { get; set; } = "";
    public int     Grade      { get; set; }
    public string  SourceFile { get; set; } = "";
    public int     PageNumber { get; set; }
}

// Data returned from a search
public class SearchResult
{
    public string Text       { get; set; } = "";
    public string Subject    { get; set; } = "";
    public int    Grade      { get; set; }
    public string SourceFile { get; set; } = "";
    public int    Page       { get; set; }
    public float  Score      { get; set; }
}
