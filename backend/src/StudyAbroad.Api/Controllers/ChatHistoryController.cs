using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Advisor.History;
using StudyAbroad.Application.ParentLinks;
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
public class ChatHistoryController(IChatHistoryService history, IChatPendingQuestion pending) : ControllerBase
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

    /// <summary>Đổi tên một phiên (tối đa 100 ký tự, không để trống). Không đổi thứ tự trong lịch sử. 404 nếu không có, của người khác hoặc đã xóa.</summary>
    [HttpPatch("{sessionId:guid}")]
    public async Task<ActionResult<RenamedChatSessionDto>> Rename(Guid sessionId, [FromBody] RenameChatSessionRequest request, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        try
        {
            var title = await history.RenameAsync(userId, sessionId, request.Title, ct);
            return title is null ? NotFound() : Ok(new RenamedChatSessionDto(sessionId, title));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Xóa một phiên khỏi lịch sử (xóa mềm): không hiện, không mở lại, không hỏi tiếp được; tin nhắn vẫn giữ cho thống kê.
    /// 204 khi xóa xong; 404 nếu không có, của người khác hoặc đã xóa trước đó.
    /// </summary>
    [HttpDelete("{sessionId:guid}")]
    public async Task<IActionResult> Delete(Guid sessionId, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return await history.DeleteAsync(userId, sessionId, ct) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Câu hồ sơ đang chờ của phiên (bậc học, ngành, các câu hồ sơ, bảng rà soát), kèm nút và công cụ dựng lại để giao diện gắn dưới
    /// tin cuối khi mở lại phiên. pending = false nếu không có câu hồ sơ nào đang chờ. Chỉ đọc: không đổi phiên, không gọi AI, không tính lượt.
    /// </summary>
    [HttpGet("{sessionId:guid}/pending")]
    public async Task<ActionResult<PendingQuestionDto>> Pending(Guid sessionId, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        // Phụ huynh đang nhập hộ con: câu ngân sách hỏi số tiền chính xác như trong chat (cờ đọc từ token, không tin trình duyệt)
        var exactBudget = ActingSessionGuard.ReadActingParentId(User) is not null;
        var question = await pending.PendingQuestionAsync(userId, sessionId, exactBudget, ct);
        if (question is null)
            return NotFound();
        Response.Headers.CacheControl = "no-store";
        return Ok(question);
    }
}
