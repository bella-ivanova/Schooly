using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace StudyAssistant.Services;

/// <summary>
/// Calls ZhipuAI's OpenAI-compatible API directly, bypassing Ollama.
/// Supports streaming SSE responses and the same GEOM-filtering logic
/// used by OllamaChatService for the 3D visualiser feature.
/// </summary>
public class ZhipuAIChatService : IChatService
{
    private const string BaseUrl = "https://open.bigmodel.cn/api/paas/v4/chat/completions";

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly List<ChatMsg> _messages = [];

    public double Temperature { get; set; } = 0.7;

    public ZhipuAIChatService(string model, string apiKey)
    {
        // Strip Ollama's ":cloud" tag — ZhipuAI only recognises the base model name.
        _model = model.Split(':')[0];
        _httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public void SetSystemPrompt(string prompt)
    {
        _messages.Clear();
        _messages.Add(new ChatMsg("system", prompt));
    }

    public async Task<string> OneShotAsync(string systemPrompt, string userMessage)
    {
        var body = JsonSerializer.Serialize(new
        {
            model       = _model,
            messages    = new[] {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userMessage  }
            },
            stream      = false,
            temperature = Temperature,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        try
        {
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"\n[Chat error] {(int)response.StatusCode}: {err}");
                return "";
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[Chat error] {ex.Message}");
            return "";
        }
    }

    public async Task StreamMessageAsync(string newUserMessage, string? apiMessage = null)
    {
        _messages.Add(new ChatMsg("user", newUserMessage));
        var fullResponse = await SendStreamAsync(apiMessage ?? newUserMessage, print: true);
        _messages.Add(new ChatMsg("assistant", fullResponse));
    }

    public async Task<string> StreamMessageFilteredAsync(string newUserMessage, string? apiMessage = null)
    {
        _messages.Add(new ChatMsg("user", newUserMessage));
        var fullResponse = await SendStreamAsync(apiMessage ?? newUserMessage, print: true, filterGeom: true);
        _messages.Add(new ChatMsg("assistant", fullResponse));
        return fullResponse;
    }

    // ── Core streaming implementation ─────────────────────────────────────────
    private async Task<string> SendStreamAsync(string userContent, bool print, bool filterGeom = false)
    {
        // Build the messages list: replace last user turn with userContent
        // (same pattern as OllamaChatService — userContent may be a RAG-enriched prompt)
        var apiMessages = _messages
            .Take(_messages.Count - 1)
            .Append(new ChatMsg("user", userContent))
            .Select(m => new { role = m.Role, content = m.Content });

        var body = JsonSerializer.Serialize(new
        {
            model       = _model,
            messages    = apiMessages,
            stream      = true,
            temperature = Temperature,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        var fullResponse  = new StringBuilder();
        var displayBuffer = new StringBuilder();
        bool geomStarted  = false;
        const string openTag = "<STEREO>";

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"\n[Chat error] {(int)response.StatusCode}: {err}");
                return "";
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;
                var data = line[6..];
                if (data == "[DONE]") break;

                string? chunk = null;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var delta = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("delta");

                    if (delta.TryGetProperty("content", out var contentEl))
                        chunk = contentEl.GetString();
                }
                catch { continue; }

                if (string.IsNullOrEmpty(chunk)) continue;
                fullResponse.Append(chunk);

                if (!print) continue;

                if (!filterGeom)
                {
                    Console.Write(chunk);
                    continue;
                }

                // Filtering mode: suppress the <GEOM>…</GEOM> block from console output
                if (!geomStarted)
                {
                    displayBuffer.Append(chunk);
                    var buf = displayBuffer.ToString();
                    var idx = buf.IndexOf(openTag, StringComparison.Ordinal);

                    if (idx >= 0)
                    {
                        if (idx > 0) Console.Write(buf[..idx]);
                        geomStarted = true;
                    }
                    else
                    {
                        var safeLen = buf.Length - (openTag.Length - 1);
                        if (safeLen > 0)
                        {
                            Console.Write(buf[..safeLen]);
                            displayBuffer.Remove(0, safeLen);
                        }
                    }
                }
            }

            if (filterGeom && !geomStarted && displayBuffer.Length > 0)
                Console.Write(displayBuffer.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[Chat error] {ex.Message}");
        }

        return fullResponse.ToString();
    }

    private record ChatMsg(string Role, string Content);
}
