using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Advisor.History;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Lịch sử chat của chính người đang đăng nhập: danh sách phiên cũ và các tin của một phiên, để mở lại và hỏi tiếp.
/// Chỉ đọc. Cùng điều kiện tuổi như chat (dưới 16 cần phụ huynh đồng ý). Phụ huynh đang nhập hộ con thì phiên là của con
/// (giống chat). Phiên của người khác trả 404 như phiên không tồn tại. Phản hồi không lưu bộ nhớ đệm (dữ liệu cá nhân).
/// </summary>
[ApiController]
[Authorize(Policy = AgeFeatures.ProcessStudentData)]
[Route("api/v1/advisor/sessions")]
public class ChatHistoryController(IChatHistoryService history) : ControllerBase
{
    /// <summary>Phiên mới trao đổi nhất trước (limit mặc định 20, tối đa 50). cursor: nextCursor của lần trước.</summary>
    [HttpGet]
    public async Task<ActionResult<ChatSessionPageDto>> List(
        [FromQuery] string? cursor, [FromQuery] int limit = ChatHistoryService.DefaultSessions, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        try
        {
            var page = await history.ListSessionsAsync(userId, cursor, limit, ct);
            Response.Headers.CacheControl = "no-store";
            return Ok(page);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Các tin của một phiên, cũ trước mới sau (limit mặc định 100, tối đa 200): mặc định là các tin mới nhất,
    /// before = olderCursor của lần trước để tải các tin cũ hơn. Chỉ có chữ: nút, công cụ, thẻ số liệu không được lưu.
    /// </summary>
    [HttpGet("{sessionId:guid}/messages")]
    public async Task<ActionResult<ChatHistoryMessagesDto>> Messages(
        Guid sessionId, [FromQuery] string? before, [FromQuery] int limit = ChatHistoryService.DefaultMessages, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        try
        {
            var messages = await history.GetMessagesAsync(userId, sessionId, before, limit, ct);
            if (messages is null)
                return NotFound();
            Response.Headers.CacheControl = "no-store";
            return Ok(messages);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
