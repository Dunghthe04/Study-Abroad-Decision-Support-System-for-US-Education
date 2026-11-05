using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Domain.Constants;
using System;
using System.Text.Json;

namespace StudyAbroad.Api.Controllers;

/// <summary>Proxies chat requests to the Python advisor service so the browser only talks to this API.</summary>
[ApiController]
[Authorize]
[Route("api/v1/advisor")]
public class AdvisorController(ChatService chat, ILogger<AdvisorController> logger) : ControllerBase
{
    [Authorize(Policy = AgeFeatures.ProcessStudentData)] // [Q11] dưới 16 cần phụ huynh đồng ý
    [HttpPost("chat")]
    public async Task<ActionResult<AdvisorChatResponse>> Chat(AdvisorChatRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });
        if (request.Messages.Count == 0)
            return ValidationProblem("Messages must not be empty.");
        if (request.StudyLevel is not null && !StudyLevels.IsValid(request.StudyLevel))
            return ValidationProblem($"Unknown study level '{request.StudyLevel}'.");

        try
        {
            return Ok(await chat.ChatAsync(userId.Value, request, ct));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Advisor service call failed");
            return Problem("Advisor service is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    /// Như Chat nhưng trả lời theo từng ý khi xong: mỗi dòng một JSON (NDJSON). Các dòng: progress (sắp trả lời N câu),
    /// partial (câu trả lời một ý), rồi final (đúng JSON của Chat, kèm sessionId, nút, widget) hoặc error.
    /// Câu hiện cuối cùng lấy từ dòng final, vì gợi ý trường và giải thích trường có thể thay câu trả lời ở bước cuối.
    /// </summary>
    [Authorize(Policy = AgeFeatures.ProcessStudentData)] // [Q11] dưới 16 cần phụ huynh đồng ý
    [HttpPost("chat/stream")]
    public async Task<IActionResult> ChatStream(
        AdvisorChatRequest request, [FromServices] IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> json, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });
        if (request.Messages.Count == 0)
            return ValidationProblem("Messages must not be empty.");
        if (request.StudyLevel is not null && !StudyLevels.IsValid(request.StudyLevel))
            return ValidationProblem($"Unknown study level '{request.StudyLevel}'.");

        Response.ContentType = "application/x-ndjson";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // nginx gửi từng dòng ngay, không giữ lại
        var options = json.Value.JsonSerializerOptions;

        async Task Write(object line)
        {
            await Response.WriteAsync(JsonSerializer.Serialize(line, options) + "\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        try
        {
            var result = await chat.ChatAsync(userId.Value, request, ct, e => Write(e));
            await Write(new { type = "final", response = result });
        }
        catch (HttpRequestException ex)
        {
            // Đã bắt đầu gửi nên không đổi được mã trạng thái: báo lỗi bằng một dòng
            logger.LogError(ex, "Advisor service call failed");
            await Write(new { type = "error", message = "Advisor service is unavailable." });
        }
        return new EmptyResult();
    }
}
