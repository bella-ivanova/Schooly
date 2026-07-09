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

    public ZhipuAIChatService(HttpClient httpClient, string model, string apiKey)
    {
        _model = model.Split(':')[0];
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public void SetSystemPrompt(string prompt)
    {
        _messages.Clear();
        _messages.Add(new ChatMsg("system", prompt));
    }

    public void SeedHistory(IEnumerable<(string Role, string Content)> turns)
    {
        _messages.Clear();
        foreach (var (role, content) in turns)
            _messages.Add(new ChatMsg(role == "assistant" ? "assistant" : "user", content));
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

    public async Task StreamMessageAsync(string newUserMessage, string? apiMessage = null, string? systemPromptOverride = null)
    {
        _messages.Add(new ChatMsg("user", newUserMessage));
        var fullResponse = await SendStreamAsync(apiMessage ?? newUserMessage, print: true, systemPromptOverride: systemPromptOverride);
        _messages.Add(new ChatMsg("assistant", fullResponse));
    }

    public async Task<string> StreamMessageFilteredAsync(string newUserMessage, string? apiMessage = null, string? systemPromptOverride = null)
    {
        _messages.Add(new ChatMsg("user", newUserMessage));
        var fullResponse = await SendStreamAsync(apiMessage ?? newUserMessage, print: true, filterGeom: true, systemPromptOverride: systemPromptOverride);
        _messages.Add(new ChatMsg("assistant", fullResponse));
        return fullResponse;
    }

    // ── Core streaming implementation ─────────────────────────────────────────
    private async Task<string> SendStreamAsync(string userContent, bool print, bool filterGeom = false, string? systemPromptOverride = null)
    {
        // Build the messages list: replace last user turn with userContent.
        // If a per-request system prompt is supplied, prepend it and strip any
        // existing system message so the caller controls the role boundary.
        IEnumerable<ChatMsg> baseMessages = _messages
            .Take(_messages.Count - 1)
            .Append(new ChatMsg("user", userContent));

        if (systemPromptOverride != null)
            baseMessages = new[] { new ChatMsg("system", systemPromptOverride) }
                .Concat(baseMessages.Where(m => m.Role != "system"));

        var apiMessages = baseMessages.Select(m => new { role = m.Role, content = m.Content });

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

    // Yields tokens as they arrive so HTTP endpoints can stream via SSE.
    // Does not write to Console — callers decide how to surface the output.
    public async IAsyncEnumerable<string> StreamTokensAsync(
        string newUserMessage, string? apiMessage = null, string? systemPromptOverride = null)
    {
        _messages.Add(new ChatMsg("user", newUserMessage));

        IEnumerable<ChatMsg> baseMessages = _messages
            .Take(_messages.Count - 1)
            .Append(new ChatMsg("user", apiMessage ?? newUserMessage));

        if (systemPromptOverride != null)
            baseMessages = new[] { new ChatMsg("system", systemPromptOverride) }
                .Concat(baseMessages.Where(m => m.Role != "system"));

        var body = JsonSerializer.Serialize(new
        {
            model       = _model,
            messages    = baseMessages.Select(m => new { role = m.Role, content = m.Content }),
            stream      = true,
            temperature = Temperature,
        });

        var fullResponse = new StringBuilder();

        await foreach (var chunk in YieldChunksAsync(body))
        {
            fullResponse.Append(chunk);
            yield return chunk;
        }

        _messages.Add(new ChatMsg("assistant", fullResponse.ToString()));
    }

    private async IAsyncEnumerable<string> YieldChunksAsync(string requestBody)
    {
        HttpResponseMessage? response = null;
        bool success = false;

        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };
            response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            success  = response.IsSuccessStatusCode;

            if (!success)
                Console.Error.WriteLine($"\n[YieldChunksAsync] {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n[YieldChunksAsync error] {ex.Message}");
        }

        if (!success || response is null)
        {
            response?.Dispose();
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") break;

            string? chunk = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
                if (delta.TryGetProperty("content", out var contentEl))
                    chunk = contentEl.GetString();
            }
            catch { continue; }

            if (!string.IsNullOrEmpty(chunk))
                yield return chunk;
        }

        response.Dispose();
    }

    private record ChatMsg(string Role, string Content);
}
