using System.Text;
using OllamaSharp;
using OllamaSharp.Models;

namespace StudyAssistant.Services;

public class EmbeddingService
{
    // nomic-embed-text and nomic-embed-text-v2-moe are both trained to expect a task-
    // instruction prefix on every embedded string — the query side and the document/index
    // side are embedded differently, so a query must never be embedded with the document
    // prefix or vice versa. Verified against nomic-ai/nomic-embed-text-v2-moe's model card
    // that v2-moe uses this same convention as v1.5, rather than assumed.
    public const string DocumentPrefix = "search_document: ";
    public const string QueryPrefix    = "search_query: ";

    private readonly OllamaApiClient _ollama;
    private readonly string _model;

    public string Model => _model;

    public EmbeddingService(string model = "nomic-embed-text-v2-moe")
    {
        _model  = model;
        _ollama = new OllamaApiClient("http://localhost:11434");
    }

    // Prefix-aware wrappers for production call sites — GetEmbeddingsAsync itself stays
    // prefix-agnostic below since the CLI diagnostics (RetrievalDiagnostics,
    // RetrievalComparisonRunner) need to apply prefixes selectively to compare configs.
    public Task<List<float[]>> GetDocumentEmbeddingsAsync(List<string> texts, int batchSize = 10) =>
        GetEmbeddingsAsync(texts.Select(t => DocumentPrefix + t).ToList(), batchSize);

    public Task<List<float[]>> GetQueryEmbeddingsAsync(List<string> queries, int batchSize = 10) =>
        GetEmbeddingsAsync(queries.Select(q => QueryPrefix + q).ToList(), batchSize);

    // Returns one float[] embedding per input string.
    // Processes in batches of 10 to avoid overloading the model.
    public async Task<List<float[]>> GetEmbeddingsAsync(List<string> inputs, int batchSize = 10)
    {
        var results = new List<float[]>();

        for (int i = 0; i < inputs.Count; i += batchSize)
        {
            var batch = inputs
                .GetRange(i, Math.Min(batchSize, inputs.Count - i))
                .Select(SanitizeText)
                .ToList();

            var response = await _ollama.EmbedAsync(new EmbedRequest
            {
                Model = _model,
                Input = batch
            });

            if (response?.Embeddings == null || response.Embeddings.Count == 0)
                throw new Exception(
                    $"Embedding model '{_model}' returned no embeddings. " +
                    $"Is it pulled? Run: ollama pull {_model}");

            foreach (var embedding in response.Embeddings)
                results.Add(embedding.ToArray());
        }

        return results;
    }

    // Removes control characters and hard-caps length so the model never
    // receives malformed or oversized text.
    private static string SanitizeText(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c == '\0') continue;               // null byte
            if (c < 0x20 && c != '\t') continue;  // control chars except tab
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }
}
