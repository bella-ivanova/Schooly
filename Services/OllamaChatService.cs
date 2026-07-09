using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace StudyAssistant.Services;

public class OllamaChatService : IChatService
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

    public void SeedHistory(IEnumerable<(string Role, string Content)> turns)
    {
        _messages.Clear();
        foreach (var (role, content) in turns)
            _messages.Add(new Message
            {
                Role    = role == "assistant" ? ChatRole.Assistant : ChatRole.User,
                Content = content
            });
    }

    // Sends a single message with a completely fresh history so the call never
    // bleeds into (or is contaminated by) the ongoing conversation state.
    public async Task<string> OneShotAsync(string systemPrompt, string userMessage)
    {
        var tempMessages = new List<Message>
        {
            new Message { Role = ChatRole.System, Content = systemPrompt },
            new Message { Role = ChatRole.User,   Content = userMessage  }
        };

        var request = new ChatRequest
        {
            Model    = _model,
            Messages = tempMessages,
            Stream   = false,
            Options  = new RequestOptions { Temperature = (float)Temperature }
        };

        var sb = new System.Text.StringBuilder();
        await foreach (var token in _ollama.ChatAsync(request))
            sb.Append(token?.Message?.Content);

        return sb.ToString();
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
    public async Task StreamMessageAsync(string newUserMessage, string? apiMessage = null, string? systemPromptOverride = null)
    {
        _messages.Add(new Message { Role = ChatRole.User, Content = newUserMessage });

        // Build the list of messages to send: swap the last user message for the
        // enriched RAG prompt when one is provided.
        IEnumerable<Message> apiMessages = apiMessage != null
            ? [.._messages.Take(_messages.Count - 1),
               new Message { Role = ChatRole.User, Content = apiMessage }]
            : _messages;

        // If a per-request system prompt is supplied, prepend it and strip any
        // existing system message so the caller controls the role boundary.
        if (systemPromptOverride != null)
            apiMessages = [new Message { Role = ChatRole.System, Content = systemPromptOverride },
                           ..apiMessages.Where(m => m.Role != ChatRole.System)];

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

    // Like StreamMessageAsync but:
    //   • Suppresses the <GEOM>…</GEOM> block from the console so the student
    //     never sees raw JSON while still capturing it for VisualisationService.
    //   • Returns the full unfiltered response (including the GEOM block) so the
    //     caller can extract and render the JSON.
    //
    // Uses a small look-ahead buffer (length of the opening tag) to detect the
    // tag even when it arrives split across multiple tokens.
    public async Task<string> StreamMessageFilteredAsync(string newUserMessage, string? apiMessage = null, string? systemPromptOverride = null)
    {
        _messages.Add(new Message { Role = ChatRole.User, Content = newUserMessage });

        IEnumerable<Message> apiMessages = apiMessage != null
            ? [.._messages.Take(_messages.Count - 1),
               new Message { Role = ChatRole.User, Content = apiMessage }]
            : _messages;

        if (systemPromptOverride != null)
            apiMessages = [new Message { Role = ChatRole.System, Content = systemPromptOverride },
                           ..apiMessages.Where(m => m.Role != ChatRole.System)];

        var request = new ChatRequest
        {
            Model    = _model,
            Messages = apiMessages,
            Stream   = true,
            Options  = new RequestOptions { Temperature = (float)Temperature }
        };

        var fullResponse    = new System.Text.StringBuilder();
        var displayBuffer   = new System.Text.StringBuilder();
        bool geomStarted    = false;
        const string openTag = "<STEREO>";

        try
        {
            await foreach (var token in _ollama.ChatAsync(request))
            {
                if (token == null) continue;
                var chunk = token.Message?.Content;
                if (string.IsNullOrEmpty(chunk)) continue;

                fullResponse.Append(chunk);

                if (!geomStarted)
                {
                    displayBuffer.Append(chunk);
                    var buf = displayBuffer.ToString();
                    var idx = buf.IndexOf(openTag, StringComparison.Ordinal);

                    if (idx >= 0)
                    {
                        // Print everything before the opening tag, then go silent.
                        if (idx > 0) Console.Write(buf[..idx]);
                        geomStarted = true;
                    }
                    else
                    {
                        // Safe to flush everything except the last (tag.Length - 1) chars,
                        // which might be the start of a tag split across tokens.
                        var safeLen = buf.Length - (openTag.Length - 1);
                        if (safeLen > 0)
                        {
                            Console.Write(buf[..safeLen]);
                            displayBuffer.Remove(0, safeLen);
                        }
                    }
                }
                // Once the GEOM tag has started, accumulate silently.
            }

            // If <GEOM> never appeared, flush whatever is left in the buffer.
            if (!geomStarted && displayBuffer.Length > 0)
                Console.Write(displayBuffer.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[Chat error] {ex.Message}");
            Console.WriteLine($"Is model '{_model}' pulled? Run: ollama pull {_model}");
            _messages.Add(new Message { Role = ChatRole.Assistant, Content = fullResponse.ToString() });
            return fullResponse.ToString();
        }

        if (fullResponse.Length == 0)
            Console.WriteLine($"[No response from model '{_model}'. Is it pulled? Run: ollama pull {_model}]");

        _messages.Add(new Message { Role = ChatRole.Assistant, Content = fullResponse.ToString() });
        return fullResponse.ToString();
    }

    // Yields tokens as they arrive so HTTP endpoints can stream via SSE.
    // Does not write to Console — callers decide how to surface the output.
    public async IAsyncEnumerable<string> StreamTokensAsync(
        string newUserMessage, string? apiMessage = null, string? systemPromptOverride = null)
    {
        _messages.Add(new Message { Role = ChatRole.User, Content = newUserMessage });

        IEnumerable<Message> apiMessages = apiMessage != null
            ? [.._messages.Take(_messages.Count - 1),
               new Message { Role = ChatRole.User, Content = apiMessage }]
            : _messages;

        if (systemPromptOverride != null)
            apiMessages = [new Message { Role = ChatRole.System, Content = systemPromptOverride },
                           ..apiMessages.Where(m => m.Role != ChatRole.System)];

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
                    fullResponse.Append(chunk);
                    yield return chunk;
                }
            }
        }
        finally
        {
            // Always persist to history, even if the stream was interrupted
            _messages.Add(new Message { Role = ChatRole.Assistant, Content = fullResponse.ToString() });
        }
    }
}
