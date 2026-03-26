using System.Diagnostics;
using System.Net.Sockets;
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

    // Starts Qdrant in the background if it isn't already running.
    // Returns the Process so the caller can stop it when the app exits.
    // Returns null if Qdrant was already running (we didn't start it, so we shouldn't stop it).
    public static async Task<Process?> EnsureStartedAsync(int port = 6334)
    {
        if (await IsPortOpenAsync(port))
        {
            Console.WriteLine("Qdrant already running.");
            return null;
        }

        Console.Write("Starting Qdrant...");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = "qdrant",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            }
        };

        try
        {
            process.Start();
        }
        catch
        {
            Console.WriteLine(" not found.");
            Console.WriteLine("Qdrant binary is missing. Download it from https://github.com/qdrant/qdrant/releases");
            Console.WriteLine("Then place the binary somewhere on your PATH (e.g. /usr/local/bin/qdrant).");
            return null;
        }

        // Poll until Qdrant is ready to accept connections (up to 15 seconds)
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (await IsPortOpenAsync(port))
            {
                Console.WriteLine(" ready.");
                return process;
            }
            await Task.Delay(300);
        }

        Console.WriteLine(" timed out.");
        process.Kill();
        return null;
    }

    // Quick TCP probe — just checks whether port 6334 is accepting connections.
    private static async Task<bool> IsPortOpenAsync(int port)
    {
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("localhost", port);
            return true;
        }
        catch
        {
            return false;
        }
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
}

// Data returned from a search
public class SearchResult
{
    public string Text       { get; set; } = "";
    public string Subject    { get; set; } = "";
    public int    Grade      { get; set; }
    public string SourceFile { get; set; } = "";
    public float  Score      { get; set; }
}
