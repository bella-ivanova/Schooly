using System.Text;
using System.Text.Json;

namespace StudyAssistant.Services;

public class OCRService
{
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
    stream = false
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
