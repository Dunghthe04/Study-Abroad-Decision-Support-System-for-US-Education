using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-58] Admin xử lý tin nhắn yêu cầu liên hệ bị báo cáo (thô tục, quấy rối...): đọc tin kèm ngữ cảnh liền kề, rồi gỡ tin,
/// cảnh cáo, khóa tài khoản người gửi (đồng thời gỡ toàn bộ tin nhắn của tài khoản đó) hoặc bác bỏ báo cáo.
/// Người báo cáo được thông báo kết quả. Moderator không còn tham gia luồng này.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/lead-message-reports")]
public class AdminLeadMessageReportsController(ILeadService leadService, IAuthService auth) : ControllerBase
{
    /// <summary>Hàng đợi báo cáo, mới báo trước. status: pending (mặc định) | resolved | all.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminLeadMessageReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> List(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        RunAsync(async adminId => Ok(await leadService.GetReportedMessagesAsync(status, page, pageSize, ct, adminId)));

    /// <summary>
    /// Tin bị báo cáo kèm một số tin ngữ cảnh liền kề (số lượng là tham số hệ thống). Admin không đọc toàn bộ cuộc trò chuyện.
    /// Mỗi lần đọc ghi nhật ký truy cập.
    /// </summary>
    [HttpGet("{leadId:guid}/{messageId:guid}/context")]
    [ProducesResponseType(typeof(AdminLeadMessageReportContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetContext(Guid leadId, Guid messageId, CancellationToken ct) =>
        RunAsync(async adminId => Ok(await leadService.GetReportContextAsync(adminId, leadId, messageId, ct)));

    /// <summary>Gỡ riêng tin nhắn vi phạm (không khóa tài khoản). Lý do bắt buộc.</summary>
    [HttpPost("{leadId:guid}/{messageId:guid}/remove")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Remove(Guid leadId, Guid messageId, [FromBody] ResolveReportedMessageRequest? request, CancellationToken ct) =>
        RunAsync(async adminId =>
        {
            await leadService.RemoveReportedMessageAsync(adminId, leadId, messageId, request?.Reason, ct);
            return Ok(new { message = "Đã gỡ tin nhắn." });
        });

    /// <summary>Cảnh cáo người gửi tin bị báo cáo, tin vẫn hiện. Lý do bắt buộc.</summary>
    [HttpPost("{leadId:guid}/{messageId:guid}/warn")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Warn(Guid leadId, Guid messageId, [FromBody] ResolveReportedMessageRequest? request, CancellationToken ct) =>
        RunAsync(async adminId =>
        {
            await leadService.WarnReportedSenderAsync(adminId, leadId, messageId, request?.Reason, ct);
            return Ok(new { message = "Đã cảnh cáo người gửi." });
        });

    /// <summary>Admin thấy không vi phạm: bỏ báo cáo, tin vẫn hiện.</summary>
    [HttpPost("{leadId:guid}/{messageId:guid}/dismiss")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Dismiss(Guid leadId, Guid messageId, CancellationToken ct) =>
        RunAsync(async adminId =>
        {
            await leadService.DismissReportAsync(adminId, leadId, messageId, ct);
            return Ok(new { message = "Đã bỏ báo cáo." });
        });

    /// <summary>Khóa tài khoản người gửi tin bị báo cáo và gỡ toàn bộ tin nhắn của họ. Lý do bắt buộc.</summary>
    [HttpPost("{leadId:guid}/{messageId:guid}/lock-sender")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> LockSender(Guid leadId, Guid messageId, [FromBody] LockReportedSenderRequest? request, CancellationToken ct) =>
        RunAsync(async adminId =>
        {
            var senderId = await leadService.GetMessageSenderIdAsync(leadId, messageId, ct);
            var reason = request?.Reason?.Trim() ?? string.Empty;

            var lockResult = await auth.LockUserAccountAsync(senderId, new LockUserAccountRequest(reason), adminId, ct);
            if (lockResult.Error != AuthError.None)
                return ValidationProblem(lockResult.Message);

            var removed = await leadService.RemoveAllMessagesOfLockedSenderAsync(adminId, senderId, reason, ct);
            await leadService.MarkReportResolvedByLockAsync(adminId, leadId, messageId, ct);
            return Ok(new { message = $"Đã khóa tài khoản và gỡ {removed} tin nhắn." });
        });

    private async Task<IActionResult> RunAsync(Func<Guid, Task<IActionResult>> action)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();

        try
        {
            return await action(adminId);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Không tìm thấy", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
        }
    }
}
