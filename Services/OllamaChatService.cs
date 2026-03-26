using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace StudyAssistant.Services;

public class OllamaChatService
{
    private readonly OllamaApiClient _ollama;
    private readonly string _model;
    private readonly List<Message> _messages = new();

    public double Temperature { get; set; } = 0.7;

    public OllamaChatService(string model)
    {
        _model = model;
        _ollama = new OllamaApiClient("http://localhost:11434");
    }

    // Clears history and sets a system prompt.
    // Every new conversation starts here so the AI knows its role.
    public void SetSystemPrompt(string prompt)
    {
        _messages.Clear();
        _messages.Add(new Message { Role = ChatRole.System, Content = prompt });
    }

    // Sends a message and waits for the full reply before returning.
    // Used internally; streaming is preferred for interactive chat.
    public async Task<string> SendMessageAsync(string userMessage)
    {
        _messages.Add(new Message { Role = ChatRole.User, Content = userMessage });

        var request = new ChatRequest
        {
            Model   = _model,
            Messages = _messages,
            Stream  = false,
            Options = new RequestOptions { Temperature = (float)Temperature }
        };

        var fullResponse = new System.Text.StringBuilder();
        await foreach (var token in _ollama.ChatAsync(request))
            fullResponse.Append(token?.Message?.Content);

        var reply = fullResponse.ToString();
        _messages.Add(new Message { Role = ChatRole.Assistant, Content = reply });
        return reply;
    }

    // Streams the response token by token so the user sees words appear in real time.
    //
    // newUserMessage  — stored in conversation history so the AI remembers the exchange.
    // apiMessage      — what actually gets sent to the model (used by RAG to inject
    //                   textbook context around the question without polluting history).
    public async Task StreamMessageAsync(string newUserMessage, string? apiMessage = null)
    {
        _messages.Add(new Message { Role = ChatRole.User, Content = newUserMessage });

        // Build the list of messages to send: swap the last user message for the
        // enriched RAG prompt when one is provided.
        IEnumerable<Message> apiMessages = apiMessage != null
            ? [.._messages.Take(_messages.Count - 1),
               new Message { Role = ChatRole.User, Content = apiMessage }]
            : _messages;

        var request = new ChatRequest
        {
            Model    = _model,
            Messages = apiMessages,
            Stream   = true,
            Options  = new RequestOptions { Temperature = (float)Temperature }
        };

        var fullResponse = new System.Text.StringBuilder();

        try
        {
            await foreach (var token in _ollama.ChatAsync(request))
            {
                if (token == null) continue;
                var chunk = token.Message?.Content;
                if (!string.IsNullOrEmpty(chunk))
                {
                    Console.Write(chunk);
                    fullResponse.Append(chunk);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[Chat error] {ex.Message}");
            Console.WriteLine($"Is model '{_model}' pulled? Run: ollama pull {_model}");
            return;
        }

        if (fullResponse.Length == 0)
            Console.WriteLine($"[No response from model '{_model}'. Is it pulled? Run: ollama pull {_model}]");

        _messages.Add(new Message { Role = ChatRole.Assistant, Content = fullResponse.ToString() });
    }
}
