using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-42 / UC-73] API Gửi và Quản lý yêu cầu tư vấn & Chia sẻ hồ sơ cho Trung tâm du học (Lead Request).
/// - Học sinh từ 16 tuổi tự gửi (16–17 tuổi phải tick xác nhận); dưới 16 tuổi thì phụ huynh đã liên kết gửi thay.
/// - Mọi quy tắc tuổi, liên kết, nhóm dữ liệu, giới hạn gửi nằm ở LeadService; controller chỉ đổi lỗi thành HTTP.
/// - Trung tâm tư vấn xem các yêu cầu nhận được, chỉ xem được thông tin chi tiết nếu học sinh đồng ý chia sẻ.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/leads")]
public class LeadsController(ILeadService leadService) : ControllerBase
{
    /// <summary>
    /// [UC-42] Gửi yêu cầu tư vấn tới trung tâm du học.
    /// Vi phạm quy tắc trả <c>{ code, message }</c>: 400 (thiếu dữ liệu/đồng ý), 403 (tuổi, vai trò, chưa liên kết),
    /// 429 (đã gửi trung tâm này trong 7 ngày — kèm <c>retryAt</c> — hoặc quá 5 lần/ngày).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateLead([FromBody] CreateLeadRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await leadService.CreateLeadAsync(userId.Value, request, ip, ct);
            return CreatedAtAction(nameof(GetLeadById), new { id = result.Id }, result);
        }
        catch (LeadRuleException ex)
        {
            return ex.RetryAfterUtc is { } retryAt
                ? StatusCode(ex.Status, new { code = ex.Code, message = ex.Message, retryAt })
                : StatusCode(ex.Status, new { code = ex.Code, message = ex.Message });
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
    /// [UC-42 Phụ trợ] Lấy chi tiết yêu cầu vừa tạo (học sinh của lead hoặc phụ huynh đã gửi thay).
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

    /// <summary>
    /// [UC-44] Danh sách yêu cầu liên hệ đã gửi, mới nhất trước.
    /// Học sinh từ 16 tuổi: lead của mình (kể cả phụ huynh gửi thay). Phụ huynh: lead mình gửi và lead của con đang liên kết.
    /// Học sinh dưới 16 tuổi trả 403 <c>{ code: "age_restricted", message }</c>.
    /// </summary>
    [HttpGet("sent")]
    [Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
    [ProducesResponseType(typeof(PagedResult<SentLeadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSentLeads(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            return Ok(await leadService.GetSentLeadsAsync(userId.Value, page, pageSize, ct));
        }
        catch (LeadRuleException ex)
        {
            return StatusCode(ex.Status, new { code = ex.Code, message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Không tìm thấy", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
        }
    }

    /// <summary>
    /// [Nhắn tin phía người gửi] Đọc tin nhắn trong một yêu cầu liên hệ đã gửi.
    /// Quyền xem giống UC-44. Lead không có quyền trả 404 (chống IDOR). Học sinh dưới 16 tuổi trả 403 <c>age_restricted</c>.
    /// </summary>
    [HttpGet("sent/{id:guid}/messages")]
    [Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
    [ProducesResponseType(typeof(LeadMessagesThreadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetSentLeadMessages(Guid id, CancellationToken ct) =>
        ExecuteSentLeadActionAsync(async userId =>
            Ok(await leadService.GetSentLeadMessagesAsync(userId, id, ct)));

    /// <summary>
    /// [Nhắn tin phía người gửi] Học sinh / phụ huynh gửi tin trong yêu cầu liên hệ đã gửi.
    /// Tin có số điện thoại hoặc link ngoài vẫn lưu nhưng bị gắn cờ. Lead đã hoàn tất trả 400 <c>lead_closed</c>.
    /// </summary>
    [HttpPost("sent/{id:guid}/messages")]
    [Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
    [ProducesResponseType(typeof(SendLeadMessageResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> SendSentLeadMessage(
        Guid id,
        [FromBody] SendLeadMessageRequest request,
        CancellationToken ct) =>
        ExecuteSentLeadActionAsync(async userId =>
        {
            var result = await leadService.SendSentLeadMessageAsync(userId, id, request?.Content ?? string.Empty, ct);
            return CreatedAtAction(nameof(GetSentLeadMessages), new { id }, result);
        });

    // Lỗi trả { code, message } giống GET /leads/sent để frontend đọc một kiểu
    private async Task<IActionResult> ExecuteSentLeadActionAsync(Func<Guid, Task<IActionResult>> action)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            return await action(userId.Value);
        }
        catch (LeadRuleException ex)
        {
            return StatusCode(ex.Status, new { code = ex.Code, message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "not_found", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "invalid_content", message = ex.Message });
        }
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

