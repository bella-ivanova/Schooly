using System.Text;
using System.Text.Json;

namespace StudyAssistant.Services;

public class OCRService
{
    // A thinking-capable vision model (e.g. qwen3.5:9b) otherwise burns its default ~4096-token
    // context window on its internal reasoning trace before ever emitting a Content token, the
    // same empty-Content failure already root-caused for the STEM pipeline's OneShotReasoningAsync
    // (see StemAnswerPipelineService.cs) -- confirmed directly against this model: the identical
    // request without `think: false` + explicit options returned empty Content while its Thinking
    // field showed it had correctly read the image. think:false is a no-op for non-thinking models
    // like minicpm-v, so this is safe regardless of which model Llm:OllamaVisionModel points at.
    private const int NumPredict = 4096;
    private const int NumCtx = 8192;

    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OCRService(HttpClient httpClient, string model)
    {
        _httpClient = httpClient;
        _model = model;
    }

    public async Task<string> ReadPageAsync(byte[] imageBytes)
    {
        var base64Image = Convert.ToBase64String(imageBytes);

        var requestBody = new
    {
    model = _model,
    messages = new[]
    {
        new
        {
            role = "user",
            content = "Extract all text exactly as written, including all math formulas and symbols.",
            images = new[] { base64Image }
        }
    },
    stream = false,
    think = false,
    options = new { num_predict = NumPredict, num_ctx = NumCtx }
    };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(
            "http://localhost:11434/api/chat",
            content
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"OCR API Error: {response.StatusCode} - {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
}
