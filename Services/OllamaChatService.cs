using System.Text;
using System.Text.Json;

public class OllamaChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly List<ChatMessage> _messages;

    public double Temperature { get; set; } = 0.7;

    public OllamaChatService(string model)
    {
        _httpClient = new HttpClient();
        _model = model;
        _messages = new List<ChatMessage>();
    }

    // ✅ System Prompt Support
    public void SetSystemPrompt(string prompt)
    {
        _messages.Clear();
        _messages.Add(new ChatMessage
        {
            Role = "system",
            Content = prompt
        });
    }

    // ✅ Conversation Memory (Auto stored)
    public async Task<string> SendMessageAsync(string userMessage)
    {
        _messages.Add(new ChatMessage
        {
            Role = "user",
            Content = userMessage
        });

        var requestBody = new
        {
            model = _model,
            messages = _messages,
            options = new
            {
                temperature = Temperature
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

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        var assistantReply = doc.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        _messages.Add(new ChatMessage
        {
            Role = "assistant",
            Content = assistantReply ?? ""
        });

        return assistantReply ?? "";
    }

    // historyMessage is stored in conversation history.
    // apiMessage (if provided) is what actually gets sent to the model — used by RAG to inject context.
    public async Task StreamMessageAsync(string historyMessage, string? apiMessage = null)
    {
    _messages.Add(new ChatMessage
    {
        Role = "user",
        Content = historyMessage
    });

    // Build the message list for the API: replace the last user message with apiMessage if provided
    List<ChatMessage> apiMessages;
    if (apiMessage != null)
    {
        apiMessages = [.._messages.Take(_messages.Count - 1),
            new ChatMessage { Role = "user", Content = apiMessage }];
    }
    else
    {
        apiMessages = _messages;
    }

    var requestBody = new
    {
        model = _model,
        messages = apiMessages,
        options = new
        {
            temperature = Temperature
        },
        stream = true
    };

    var request = new HttpRequestMessage(
        HttpMethod.Post,
        "http://localhost:11434/api/chat")
    {
        Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json")
    };

    var response = await _httpClient.SendAsync(
        request,
        HttpCompletionOption.ResponseHeadersRead);

    using var stream = await response.Content.ReadAsStreamAsync();
    using var reader = new StreamReader(stream);

    StringBuilder fullResponse = new StringBuilder();

    string? line;
    while ((line = await reader.ReadLineAsync()) != null)
    {

        if (string.IsNullOrWhiteSpace(line))
            continue;

        using var doc = JsonDocument.Parse(line);

        if (doc.RootElement.TryGetProperty("message", out var msg))
        {
            var chunk = msg.GetProperty("content").GetString();
            Console.Write(chunk);
            fullResponse.Append(chunk);
        }
    }

    _messages.Add(new ChatMessage
    {
        Role = "assistant",
        Content = fullResponse.ToString()
    });
    }
}


