using System.Text.Json;

namespace StudyAssistant.Services;

public class MathOcrService
{
    private readonly HttpClient _httpClient;
    private const string ServerUrl = "http://127.0.0.1:8503/pix2text";
    private const string FormulasServerUrl = "http://127.0.0.1:8503/pix2text/formulas";

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

        // "image" and "file_type" match the field names in pix2text's serve.py.
        // resized_shape must be sent explicitly: the server's default (768, an int)
        // fails its own "str" Form() type validation when the field is omitted.
        form.Add(imageContent, "image", "page.png");
        form.Add(new StringContent("text_formula"), "file_type");
        form.Add(new StringContent("768"), "resized_shape");

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

    // Sends a PNG page image to the Pix2Text server's formula-only endpoint and
    // gets back a list of LaTeX strings for the formula regions on that page —
    // never touches the general-text (CnOcr) recognition path.
    public async Task<List<string>> DetectFormulasAsync(byte[] imageBytes)
    {
        using var form = new MultipartFormDataContent();

        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        form.Add(imageContent, "image", "page.png");
        form.Add(new StringContent("768"), "resized_shape");

        var response = await _httpClient.PostAsync(FormulasServerUrl, form);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"Pix2Text formulas endpoint error {(int)response.StatusCode}: {body}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("formulas")
            .EnumerateArray()
            .Select(e => e.GetString() ?? "")
            .Where(s => s.Length > 0)
            .ToList();
    }
}
