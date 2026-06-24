using System.Text.Json;

namespace StudyAssistant.Services;

public class MathOcrService
{
    private readonly HttpClient _httpClient;
    private const string ServerUrl = "http://127.0.0.1:8503/pix2text";

    public MathOcrService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Sends a PNG page image to the Pix2Text server and gets back text with
    // LaTeX math mixed in (e.g. "The formula is $\frac{a}{b}$ which means...").
    public async Task<string> ReadPageAsync(byte[] imageBytes)
    {
        using var form = new MultipartFormDataContent();

        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        // "image" and "file_type" match the field names in pix2text's serve.py
        form.Add(imageContent, "image", "page.png");
        form.Add(new StringContent("text_formula"), "file_type");

        var response = await _httpClient.PostAsync(ServerUrl, form);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"Pix2Text server error {(int)response.StatusCode}: {body}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("results").GetString() ?? "";
    }

}
