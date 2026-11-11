using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-42 / UC-73] API Gửi và Quản lý yêu cầu tư vấn & Chia sẻ hồ sơ cho Trung tâm du học (Lead Request).
/// Tuân thủ chính sách bảo vệ dữ liệu trẻ em Q11:
/// - Học sinh/phụ huynh có thể gửi yêu cầu tư vấn.
/// - Nếu chọn chia sẻ hồ sơ (SharedProfile = true), người dùng dưới 18 tuổi bị chặn bởi policy AgeFeatures.ShareWithCenter.
/// - Trung tâm tư vấn xem các yêu cầu nhận được, chỉ xem được thông tin chi tiết nếu học sinh đồng ý chia sẻ.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/leads")]
public class LeadsController(
    ILeadService leadService,
    IAuthorizationService authService) : ControllerBase
{
    /// <summary>
    /// [UC-42] Gửi yêu cầu tư vấn tới trung tâm du học.
    /// Nếu SharedProfile = true: Bắt buộc thỏa mãn policy AgeFeatures.ShareWithCenter (dưới 18 tuổi bị từ chối).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateLead([FromBody] CreateLeadRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        // [Q11] Kiểm tra độ tuổi nếu học sinh muốn chia sẻ hồ sơ cho trung tâm
        if (request.SharedProfile)
        {
            var authResult = await authService.AuthorizeAsync(User, AgeFeatures.ShareWithCenter);
            if (!authResult.Succeeded)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    code = "age_restricted",
                    feature = AgeFeatures.ShareWithCenter,
                    message = "Theo Luật Trẻ em 2016 và chính sách bảo vệ dữ liệu, người dùng dưới 18 tuổi chưa được phép chia sẻ hồ sơ cho trung tâm."
                });
            }
        }

        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await leadService.CreateLeadAsync(userId.Value, request, ip, ct);
            return CreatedAtAction(nameof(GetLeadById), new { id = result.Id }, result);
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

    /// <summary>
    /// [UC-42 Phụ trợ] Lấy chi tiết yêu cầu vừa tạo.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLeadById(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var lead = await leadService.GetLeadByIdAsync(userId.Value, id, ct);
        return lead is null ? NotFound() : Ok(lead);
    }

    private async Task<IActionResult> ExecuteCenterLeadActionAsync(
        Func<Guid, string?, string, Task<IActionResult>> action)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var userEmail = User.GetEmail();
        var userRole = User.GetRole() ?? UserRoles.Center;

        try
        {
            return await action(userId.Value, userEmail, userRole);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Không tìm thấy", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// [UC-73] Lấy danh sách yêu cầu tư vấn / liên hệ nhận được của trung tâm tư vấn.
    /// Cho phép lọc theo trạng thái (new | contacted | closed), tìm kiếm từ khóa, và phân trang.
    /// Dành cho Trung tâm tư vấn (chỉ xem được yêu cầu gửi đến trung tâm mình), Moderator và Admin.
    /// </summary>
    [HttpGet("received")]
    [Authorize(Roles = $"{UserRoles.Center},{UserRoles.Moderator},{UserRoles.Admin}")]
    [ProducesResponseType(typeof(CenterLeadsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> GetReceivedLeads(
        [FromQuery] Guid? studyCenterId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default) =>
        ExecuteCenterLeadActionAsync(async (userId, userEmail, userRole) =>
        {
            var result = await leadService.GetReceivedLeadsForCenterUserAsync(
                userId,
                userEmail,
                userRole,
                studyCenterId,
                status,
                search,
                page,
                pageSize,
                ct);

            return Ok(result);
        });

    /// <summary>
    /// [UC-73] Xem chi tiết một yêu cầu liên hệ nhận được.
    /// Áp dụng bảo vệ dữ liệu: Chỉ trả về thông tin cá nhân và học thuật nếu học sinh chọn SharedProfile = true.
    /// </summary>
    [HttpGet("received/{id:guid}")]
    [Authorize(Roles = $"{UserRoles.Center},{UserRoles.Moderator},{UserRoles.Admin}")]
    [ProducesResponseType(typeof(CenterReceivedLeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetReceivedLeadDetail(Guid id, CancellationToken ct) =>
        ExecuteCenterLeadActionAsync(async (userId, userEmail, userRole) =>
        {
            var lead = await leadService.GetReceivedLeadDetailForCenterUserAsync(
                userId,
                userEmail,
                userRole,
                id,
                ct);

            return lead is null
                ? NotFound(new ProblemDetails { Title = "Không tìm thấy", Detail = "Yêu cầu liên hệ không tồn tại hoặc bạn không có quyền xem.", Status = StatusCodes.Status404NotFound })
                : Ok(lead);
        });

    /// <summary>
    /// [UC-73] Cập nhật trạng thái yêu cầu liên hệ (new | contacted | closed).
    /// </summary>
    [HttpPatch("received/{id:guid}/status")]
    [Authorize(Roles = $"{UserRoles.Center},{UserRoles.Moderator},{UserRoles.Admin}")]
    [ProducesResponseType(typeof(CenterReceivedLeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UpdateLeadStatus(
        Guid id,
        [FromBody] UpdateLeadStatusRequest request,
        CancellationToken ct) =>
        ExecuteCenterLeadActionAsync(async (userId, userEmail, userRole) =>
        {
            var result = await leadService.UpdateReceivedLeadStatusAsync(
                userId,
                userEmail,
                userRole,
                id,
                request.Status,
                ct);

            return Ok(result);
        });

    /// <summary>
    /// [USAS-2053 / UC-74] Đọc luồng tin nhắn trao đổi với học sinh/phụ huynh trong một yêu cầu liên hệ.
    /// Trung tâm chỉ xem được yêu cầu gửi đến đúng trung tâm mình (Anti-IDOR).
    /// </summary>
    [HttpGet("received/{id:guid}/messages")]
    [Authorize(Roles = $"{UserRoles.Center},{UserRoles.Moderator},{UserRoles.Admin}")]
    [ProducesResponseType(typeof(LeadMessagesThreadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetReceivedLeadMessages(Guid id, CancellationToken ct) =>
        ExecuteCenterLeadActionAsync(async (userId, userEmail, userRole) =>
        {
            var thread = await leadService.GetReceivedLeadMessagesForCenterUserAsync(
                userId,
                userEmail,
                userRole,
                id,
                ct);

            return Ok(thread);
        });

    /// <summary>
    /// [USAS-2053 / UC-74] Trung tâm gửi tin nhắn trả lời trong một yêu cầu liên hệ.
    /// Áp dụng bộ lọc nội dung (tự gắn cờ nếu chứa số điện thoại hoặc quảng cáo) và lưu vào bảng lead_messages.
    /// </summary>
    [HttpPost("received/{id:guid}/messages")]
    [Authorize(Roles = $"{UserRoles.Center},{UserRoles.Moderator},{UserRoles.Admin}")]
    [ProducesResponseType(typeof(SendLeadMessageResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> SendReceivedLeadMessage(
        Guid id,
        [FromBody] SendLeadMessageRequest request,
        CancellationToken ct) =>
        ExecuteCenterLeadActionAsync(async (userId, userEmail, userRole) =>
        {
            var result = await leadService.SendLeadMessageForCenterUserAsync(
                userId,
                userEmail,
                userRole,
                id,
                request?.Content ?? string.Empty,
                ct);

            return CreatedAtAction(nameof(GetReceivedLeadMessages), new { id }, result);
        });
}

