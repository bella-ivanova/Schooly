using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace StudyAssistant.Services;

public class EmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public string Model => _model;

    public EmbeddingService(string model = "nomic-embed-text")
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _model = model;
    }

    public async Task<List<float[]>> GetEmbeddingsAsync(List<string> inputs, int batchSize = 10)
    {
        var results = new List<float[]>();

        for (int i = 0; i < inputs.Count; i += batchSize)
        {
            var batch = inputs
                .GetRange(i, Math.Min(batchSize, inputs.Count - i))
                .Select(SanitizeText)
                .ToList();

            var requestBody = new { model = _model, input = batch };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync("http://localhost:11434/api/embed", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Ollama said: {errorBody}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            foreach (var item in doc.RootElement.GetProperty("embeddings").EnumerateArray())
            {
                results.Add(item.EnumerateArray().Select(x => x.GetSingle()).ToArray());
            }
        }

        return results;
    }

    // Strip control characters and hard-cap length to stay within model context
    private static string SanitizeText(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c == '\0') continue;              // null byte
            if (c < 0x20 && c != '\t') continue; // control chars except tab
            sb.Append(c);
        }
        var result = sb.ToString().Trim();
        // Hard cap: even if chunker produces something oversized, never exceed 500 chars
        return result.Length > 500 ? result[..500] : result;
    }
}
