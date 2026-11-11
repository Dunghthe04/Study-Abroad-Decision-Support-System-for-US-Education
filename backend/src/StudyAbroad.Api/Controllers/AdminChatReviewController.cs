using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.ChatReview;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Hàng đợi câu cần xem: các lượt chat hệ thống không trả lời được (no_answer) hoặc bị lỗi (failed), để biết hệ thống đang không làm được gì.
/// Hiện CHỈ Admin (quyết định tạm thời của nhóm); sau này muốn mở cho Moderator thì đổi phân quyền ở đây.
/// Danh sách KHÔNG có nội dung chat (chỉ mã và số đo); nội dung xem ở chi tiết, có ghi lại người xem.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/chat-review")]
public class AdminChatReviewController(IChatReviewService review) : ControllerBase
{
    /// <summary>
    /// Danh sách câu cần xem, mới nhất trước. status mặc định open (open, resolved, ignored); outcome bỏ trống là no_answer và failed;
    /// from, to bỏ trống là không giới hạn ngày (ngày UTC, tính cả hai đầu); pageSize mặc định 20, tối đa 100.
    /// </summary>
    [HttpGet("queue")]
    public async Task<ActionResult<PagedResult<ChatReviewItemDto>>> GetQueue(
        [FromQuery] string? status, [FromQuery] string? outcome, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            return Ok(await review.GetQueueAsync(status, outcome, from, to, page, pageSize, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Chi tiết một câu: nội dung câu hỏi, nhãn model gán đầy đủ, câu hệ thống đã trả lời, vài tin trước đó. Đây là dữ liệu cá nhân của học sinh:
    /// mỗi lần xem được ghi vào nhật ký (ai, lúc nào, từ đâu), ghi lỗi thì không trả nội dung. Chỉ xem được tin thuộc hàng đợi, tin khác trả 404.
    /// </summary>
    [HttpGet("items/{messageId:guid}")]
    public async Task<ActionResult<ChatReviewDetailDto>> GetDetail(Guid messageId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var detail = await review.GetDetailAsync(
            messageId, actorId, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Số câu theo trạng thái xử lý (open, resolved, ignored) và số câu open theo kết quả, cho các tab của hàng đợi.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ChatReviewSummaryDto>> GetSummary(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        try
        {
            return Ok(await review.GetSummaryAsync(from, to, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
