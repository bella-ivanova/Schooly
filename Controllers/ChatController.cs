using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAssistant.Data;
using StudyAssistant.Models;
using StudyAssistant.Services;

namespace StudyAssistant.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly RAGService              _rag;
    private readonly RateLimiter             _rateLimiter;
    private readonly ChatLogService          _chatLog;
    private readonly ChatSessionService      _chatSessions;
    private readonly IUserRepository         _users;
    private readonly LanguageDetectionService _languageDetector;

    // How long to wait with no new token/done frame before emitting a "still working on
    // it" status frame — long STEM reasoning calls (up to ~112s each, occasionally
    // several in a row on retry) would otherwise leave the client staring at total
    // silence for minutes with no indication anything is happening.
    private static readonly TimeSpan ThinkingHeartbeatInterval = TimeSpan.FromSeconds(4);
    private static readonly string[] ThinkingMessagesEn =
    {
        "Thinking through your question…",
        "Still working on it…",
        "Almost there…",
    };
    private static readonly string[] ThinkingMessagesBg =
    {
        "Обмислям въпроса ти…",
        "Все още работя по него…",
        "Почти готово…",
    };

    public ChatController(RAGService rag, RateLimiter rateLimiter, ChatLogService chatLog,
        ChatSessionService chatSessions, IUserRepository users, LanguageDetectionService languageDetector)
    {
        _rag              = rag;
        _rateLimiter      = rateLimiter;
        _chatLog          = chatLog;
        _chatSessions     = chatSessions;
        _users            = users;
        _languageDetector = languageDetector;
    }

    // POST /api/chat/message
    // Streams the LLM response token-by-token via Server-Sent Events.
    // Frames, in order:
    //   data: {"sessionId":N}\n\n                          — always first
    //   data: {"status":"..."}\n\n                          — zero or more, interleaved
    //                                                          anywhere before "done"; fires
    //                                                          only when >~4s pass with no
    //                                                          new token ready
    //   data: {"token":"..."}\n\n                           — zero or more
    //   data: {"done":true,"scene":<json or null>}\n\n
    //   data: {"title":"...","subject":"...","classId":N|null,"className":"..."|null}\n\n  — only on the session's first exchange
    [HttpPost("message")]
    [Authorize]
    public async Task SendMessage([FromBody] ChatMessageRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (_rateLimiter.IsGeneralApiThrottled(ip, out _))
        {
            HttpContext.Response.StatusCode = 429;
            HttpContext.Response.ContentType = "application/json";
            await HttpContext.Response.WriteAsync("{\"error\":\"Too many requests\"}");
            return;
        }

        var userId   = User.FindFirstValue("sub") ?? "";
        var gradeStr = User.FindFirstValue("grade") ?? "0";
        var grade    = int.TryParse(gradeStr, out var g) ? g : 0;

        ChatSession? session;
        if (req.SessionId is int sid)
        {
            session = await _chatSessions.GetOwnedAsync(sid, userId);
            if (session == null)
            {
                HttpContext.Response.StatusCode = 404;
                HttpContext.Response.ContentType = "application/json";
                await HttpContext.Response.WriteAsync("{\"error\":\"Session not found\"}");
                return;
            }
        }
        else
        {
            session = await _chatSessions.CreateAsync(userId);
        }

        var isFirstExchange = session.Title == null;

        _rag.SetGrade(grade);
        var priorTurns = await _chatSessions.GetRecentTurnsAsync(session.Id);
        _rag.SeedHistory(priorTurns);

        HttpContext.Response.StatusCode  = 200;
        HttpContext.Response.ContentType = "text/event-stream";
        HttpContext.Response.Headers.CacheControl = "no-cache";
        HttpContext.Response.Headers["X-Accel-Buffering"] = "no";

        var sessionPayload = JsonSerializer.Serialize(new { sessionId = session.Id });
        await HttpContext.Response.WriteAsync($"data: {sessionPayload}\n\n");
        await HttpContext.Response.Body.FlushAsync();

        var fullResponse = new StringBuilder();
        var streamFilter = new StereoStreamFilter();

        var languageName = _languageDetector.DetectLanguageName(req.Message);
        var thinkingMessages = languageName == "English" ? ThinkingMessagesEn : ThinkingMessagesBg;
        var heartbeatIndex = 0;

        // Manually pump the enumerator (this is exactly what `await foreach` desugars to)
        // so each MoveNextAsync() can be raced against a heartbeat timer without RAGService
        // ever knowing this layer exists. MoveNextAsync() returns a ValueTask<bool>, unsafe
        // to await twice, so it's converted to a Task<bool> exactly once per iteration and
        // reused for both the race and the final result read.
        var enumerator = _rag.AskStreamAsync(req.Message).GetAsyncEnumerator();
        try
        {
            while (true)
            {
                var moveNextTask = enumerator.MoveNextAsync().AsTask();

                while (await Task.WhenAny(moveNextTask, Task.Delay(ThinkingHeartbeatInterval)) != moveNextTask)
                {
                    var statusPayload = JsonSerializer.Serialize(
                        new { status = thinkingMessages[heartbeatIndex % thinkingMessages.Length] });
                    heartbeatIndex++;
                    await HttpContext.Response.WriteAsync($"data: {statusPayload}\n\n");
                    await HttpContext.Response.Body.FlushAsync();
                }

                if (!await moveNextTask) break;

                var token = enumerator.Current;
                fullResponse.Append(token);
                var visible = streamFilter.Feed(token);
                if (visible.Length > 0)
                {
                    var payload = JsonSerializer.Serialize(new { token = visible });
                    await HttpContext.Response.WriteAsync($"data: {payload}\n\n");
                    await HttpContext.Response.Body.FlushAsync();
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        var trailing = streamFilter.Flush();
        if (trailing.Length > 0)
        {
            var payload = JsonSerializer.Serialize(new { token = trailing });
            await HttpContext.Response.WriteAsync($"data: {payload}\n\n");
            await HttpContext.Response.Body.FlushAsync();
        }

        var scene = StereometryService.ExtractSceneJson(fullResponse.ToString());
        var donePayload = JsonSerializer.Serialize(new { done = true, scene });
        await HttpContext.Response.WriteAsync($"data: {donePayload}\n\n");
        await HttpContext.Response.Body.FlushAsync();

        var answer = StereometryService.StripSceneBlock(fullResponse.ToString());
        var (subject, topic) = await _chatLog.DetectSubjectTopicAsync(req.Message);

        var schoolId = await _chatSessions.ResolveSchoolIdAsync(userId);

        await _chatLog.SaveMessageAsync(userId, session.Id, "user",      req.Message, subject, topic, schoolId);
        await _chatLog.SaveMessageAsync(userId, session.Id, "assistant", answer,      subject, topic, schoolId);

        if (isFirstExchange)
        {
            var title = await _chatSessions.GenerateTitleAsync(req.Message, answer);
            var (_, _, classId, className) = await _chatSessions.SetFolderAndTitleOnceAsync(
                session.Id, userId, subject, schoolId, title ?? "New chat");

            var metaPayload = JsonSerializer.Serialize(new { title = title ?? "New chat", subject, classId, className });
            await HttpContext.Response.WriteAsync($"data: {metaPayload}\n\n");
            await HttpContext.Response.Body.FlushAsync();
        }
    }

    // GET /api/chat/sessions?subject=Math
    // Lists the caller's chat sessions, most recent first. Omit "subject" (or "All")
    // to list every session regardless of folder.
    [HttpGet("sessions")]
    [Authorize]
    public async Task<IActionResult> ListSessions([FromQuery] string? subject = null)
    {
        var userId = User.FindFirstValue("sub") ?? "";
        var sessions = await _chatSessions.ListAsync(userId, subject);

        var response = sessions.Select(s => new
        {
            id            = s.Id,
            title         = s.Title,
            subject       = s.Subject?.Name,
            classId       = s.ClassId,
            className     = s.Class?.Name,
            createdAt     = s.CreatedAt,
            lastMessageAt = s.LastMessageAt
        });
        return Ok(response);
    }

    // GET /api/chat/sessions/{id}/messages
    [HttpGet("sessions/{id:int}/messages")]
    [Authorize]
    public async Task<IActionResult> GetSessionMessages(int id)
    {
        var userId = User.FindFirstValue("sub") ?? "";
        var session = await _chatSessions.GetOwnedAsync(id, userId);
        if (session == null)
            return NotFound(new { error = "Session not found" });

        var messages = await _chatSessions.GetTranscriptAsync(id);
        var response = messages.Select(m => new
        {
            role      = m.Role,
            content   = m.Content,
            subject   = m.Subject?.Name,
            topic     = m.Topic,
            timestamp = m.Timestamp
        });
        return Ok(response);
    }

    // DELETE /api/chat/sessions/{id}
    [HttpDelete("sessions/{id:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteSession(int id)
    {
        var userId = User.FindFirstValue("sub") ?? "";
        var deleted = await _chatSessions.DeleteAsync(id, userId);
        if (!deleted) return NotFound(new { error = "Session not found" });
        return Ok();
    }

    // POST /api/chat/scene-html
    // Renders a <STEREO> scene JSON (as extracted from a "done" SSE frame) into the
    // standalone Three.js HTML page, for the frontend to display via <iframe srcdoc>.
    [HttpPost("scene-html")]
    [Authorize]
    public IActionResult GetSceneHtml([FromBody] SceneHtmlRequest body)
    {
        var html = StereometryHtmlBuilder.Build(body.Scene);
        return Ok(new { html });
    }

    // POST /api/chat/upload
    // Ingests a PDF into the session-scoped temporary vector store.
    // Subsequent /api/chat/message calls in the same session include this content.
    [HttpPost("upload")]
    [Authorize]
    [RequestSizeLimit(52_428_800)] // 50 MB
    public async Task<IActionResult> UploadPdf(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only PDF files are accepted." });

        var gradeStr = User.FindFirstValue("grade") ?? "0";
        var grade    = int.TryParse(gradeStr, out var g) ? g : 0;
        _rag.SetGrade(grade);

        var tempPath = Path.GetTempFileName() + ".pdf";
        try
        {
            await using (var fs = System.IO.File.Create(tempPath))
                await file.CopyToAsync(fs);

            var chunkCount = await _rag.AddTemporaryPDFAsync(tempPath);

            if (chunkCount == 0)
                return BadRequest(new { error = "Could not extract text from the uploaded PDF. The file may be image-only or corrupted." });

            return Ok(new { chunks = chunkCount });
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }
}

public record ChatMessageRequest([Required] string Message, int? SessionId = null);
public record SceneHtmlRequest([Required] string Scene);
