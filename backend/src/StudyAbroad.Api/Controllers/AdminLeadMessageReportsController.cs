using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-58] Admin xử lý tin nhắn yêu cầu liên hệ bị báo cáo (thô tục, quấy rối...): bỏ qua báo cáo, hoặc khóa tài khoản
/// người gửi (đồng thời gỡ toàn bộ tin nhắn của tài khoản đó). Moderator không còn tham gia luồng này.
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
        RunAsync(async _ => Ok(await leadService.GetReportedMessagesAsync(status, page, pageSize, ct)));

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
