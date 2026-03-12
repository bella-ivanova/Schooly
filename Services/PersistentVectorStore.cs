using System.Text.Json;

namespace StudyAssistant.Services;

public class PersistentVectorStore
{
    private readonly List<StoredChunk> _store = new();
    private readonly HashSet<string> _ingestedFiles = new();
    private readonly string _filePath;

    public int ChunkCount => _store.Count;
    public IReadOnlyCollection<string> IngestedFiles => _ingestedFiles;

    // #4 — embedding model saved in metadata so mismatches can be detected
    public string EmbeddingModel { get; private set; } = "";

    public PersistentVectorStore(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    public void SetEmbeddingModel(string model) => EmbeddingModel = model;

    public void Add(string text, float[] embedding, string subject = "", string sourceFile = "")
    {
        _store.Add(new StoredChunk
        {
            Text = text,
            Embedding = embedding,
            Subject = subject,
            SourceFile = sourceFile
        });
    }

    public bool IsFileIngested(string fileName) => _ingestedFiles.Contains(fileName);

    public void AddIngestedFile(string fileName) => _ingestedFiles.Add(fileName);

    // Removes all chunks belonging to fileKey and removes it from the ingested list.
    // Returns the number of chunks removed.
    public int DeleteFile(string fileKey)
    {
        var before = _store.Count;
        _store.RemoveAll(c => c.SourceFile == fileKey);
        _ingestedFiles.Remove(fileKey);
        return before - _store.Count;
    }

    public List<(string Text, string Subject)> Query(float[] queryEmbedding, int topK = 5, float minScore = 0.1f)
    {
        return _store
            .Select(x => new
            {
                x.Text,
                x.Subject,
                Score = CosineSimilarity(queryEmbedding, x.Embedding)
            })
            .Where(x => x.Score >= minScore)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => (x.Text, x.Subject))
            .ToList();
    }

    public void Save()
    {
        var data = new StoreData
        {
            EmbeddingModel = EmbeddingModel,
            IngestedFiles = _ingestedFiles.ToList(),
            Chunks = _store
        };
        File.WriteAllText(_filePath, JsonSerializer.Serialize(data));
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
            return;

        var json = File.ReadAllText(_filePath);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            // Old format: plain array of chunks — migrate on next save
            var chunks = JsonSerializer.Deserialize<List<StoredChunk>>(json);
            if (chunks != null) _store.AddRange(chunks);
        }
        else
        {
            var data = JsonSerializer.Deserialize<StoreData>(json);
            if (data != null)
            {
                EmbeddingModel = data.EmbeddingModel;
                _store.AddRange(data.Chunks);
                foreach (var f in data.IngestedFiles)
                    _ingestedFiles.Add(f);
            }
        }
    }

    // #5 — throw on dimension mismatch instead of silently returning 0
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new InvalidOperationException(
                $"Embedding dimension mismatch: query has {a.Length} dims, stored vector has {b.Length} dims. " +
                "Re-ingest using the same embedding model.");

        float dot = 0, normA = 0, normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
            return 0;

        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    private class StoreData
    {
        public string EmbeddingModel { get; set; } = "";
        public List<string> IngestedFiles { get; set; } = new();
        public List<StoredChunk> Chunks { get; set; } = new();
    }
}

public class StoredChunk
{
    public string Text { get; set; } = "";
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public string Subject { get; set; } = "";
    public string SourceFile { get; set; } = "";
}
