using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAssistant.Services;

namespace StudyAssistant.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly RAGService     _rag;
    private readonly RateLimiter    _rateLimiter;
    private readonly ChatLogService _chatLog;

    public ChatController(RAGService rag, RateLimiter rateLimiter, ChatLogService chatLog)
    {
        _rag         = rag;
        _rateLimiter = rateLimiter;
        _chatLog     = chatLog;
    }

    // POST /api/chat/message
    // Streams the LLM response token-by-token via Server-Sent Events.
    // Each event: data: {"token":"..."}\n\n
    // Final event: data: {"done":true,"scene":<json or null>}\n\n
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

        _rag.SetGrade(grade);

        HttpContext.Response.StatusCode  = 200;
        HttpContext.Response.ContentType = "text/event-stream";
        HttpContext.Response.Headers.CacheControl = "no-cache";
        HttpContext.Response.Headers["X-Accel-Buffering"] = "no";

        var fullResponse = new StringBuilder();

        await foreach (var token in _rag.AskStreamAsync(req.Message))
        {
            fullResponse.Append(token);
            var payload = JsonSerializer.Serialize(new { token });
            await HttpContext.Response.WriteAsync($"data: {payload}\n\n");
            await HttpContext.Response.Body.FlushAsync();
        }

        var scene = StereometryService.ExtractSceneJson(fullResponse.ToString());
        var donePayload = JsonSerializer.Serialize(new { done = true, scene });
        await HttpContext.Response.WriteAsync($"data: {donePayload}\n\n");
        await HttpContext.Response.Body.FlushAsync();

        var answer = fullResponse.ToString();
        var (subject, topic) = await _chatLog.DetectSubjectTopicAsync(req.Message);
        await _chatLog.SaveMessageAsync(userId, "user",      req.Message, subject, topic);
        await _chatLog.SaveMessageAsync(userId, "assistant", answer,      subject, topic);
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

public record ChatMessageRequest([Required] string Message);
