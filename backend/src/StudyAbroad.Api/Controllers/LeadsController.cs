using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Api.RateLimiting;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Leads;
using StudyAbroad.Application.MediaUploads;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Infrastructure.MediaUploads;

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
public class LeadsController(
    ILeadService leadService,
    IServiceProvider? services = null,
    IOptions<CloudinaryOptions>? cloudinaryOptions = null) : ControllerBase
{
    /// <summary>Ảnh trong chat: tối đa 5MB, chỉ JPEG / PNG / WebP (uploader kiểm tra thêm nội dung thật, kích thước điểm ảnh, ảnh động).</summary>
    public const int MaxChatImageBytes = 5 * 1024 * 1024;

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
    /// Nhắn tự do, không còn bộ lọc nội dung. Lead đã hoàn tất trả 400 <c>lead_closed</c>.
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
            var result = await leadService.SendSentLeadMessageAsync(userId, id, request?.Content ?? string.Empty, ct: ct);
            return CreatedAtAction(nameof(GetSentLeadMessages), new { id }, result);
        });

    /// <summary>
    /// Học sinh / phụ huynh gửi một ảnh (kèm chú thích tùy chọn) trong yêu cầu liên hệ đã gửi.
    /// Ảnh được kiểm tra định dạng, nội dung thật và qua kiểm duyệt nội dung nhạy cảm trước khi lưu.
    /// </summary>
    [HttpPost("sent/{id:guid}/messages/image")]
    [Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxChatImageBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxChatImageBytes + 64 * 1024, ValueLengthLimit = 16 * 1024, ValueCountLimit = 4)]
    [EnableRateLimiting(MediaUploadRateLimit.PolicyName)]
    [ProducesResponseType(typeof(SendLeadMessageResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> SendSentLeadImageMessage(Guid id, [FromForm] SendLeadImageMessageForm form, CancellationToken ct) =>
        ExecuteSentLeadActionAsync(async userId =>
        {
            var factory = CreateAttachmentFactory(form.File);
            var result = await leadService.SendSentLeadMessageAsync(userId, id, form.Content ?? string.Empty, factory, ct);
            return CreatedAtAction(nameof(GetSentLeadMessages), new { id }, result);
        });

    /// <summary>
    /// [UC-58] Học sinh / phụ huynh báo cáo một tin của trung tâm trong yêu cầu của mình. Admin nhận thông báo và xem xét.
    /// </summary>
    [HttpPost("sent/{id:guid}/messages/{messageId:guid}/report")]
    [Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
    [ProducesResponseType(typeof(LeadMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> ReportSentLeadMessage(
        Guid id,
        Guid messageId,
        [FromBody] ReportLeadMessageRequest? request,
        CancellationToken ct) =>
        ExecuteSentLeadActionAsync(async userId =>
            Ok(await leadService.ReportSentLeadMessageAsync(userId, id, messageId, request?.Reason, ct)));

    private Func<CancellationToken, Task<LeadMessageAttachment>> CreateAttachmentFactory(IFormFile? file)
    {
        if (file is null || file.Length == 0 || Request.Form.Files.Count != 1)
            throw new ArgumentException("Vui lòng chọn đúng một ảnh để gửi.");
        if (file.Length > MaxChatImageBytes)
            throw new ArgumentException($"Ảnh tối đa {MaxChatImageBytes / (1024 * 1024)}MB.");

        return async ct =>
        {
            if (services is null || cloudinaryOptions is null)
                throw new LeadRuleException("upload_unavailable", StatusCodes.Status503ServiceUnavailable, "Chưa bật tính năng gửi ảnh.");

            try
            {
                var uploader = services.GetRequiredService<IMediaUploader>();
                var max = Math.Min(cloudinaryOptions.Value.MaxFileSizeBytes, MaxChatImageBytes);
                await using var stream = file.OpenReadStream();
                var result = await uploader.UploadImageAsync(
                    new MediaUploadRequest(stream, file.FileName, file.ContentType, max, FolderSuffix: "lead-chat", ModerateContent: true), ct);
                return new LeadMessageAttachment(result.SecureUrl, result.PublicId);
            }
            catch (MediaContentRejectedException ex)
            {
                throw new LeadRuleException("image_rejected", StatusCodes.Status400BadRequest, ex.Message);
            }
            catch (MediaUploadSizeLimitException)
            {
                throw new LeadRuleException("image_too_large", StatusCodes.Status400BadRequest, "Ảnh vượt quá dung lượng cho phép.");
            }
            catch (ArgumentException)
            {
                throw new LeadRuleException("image_invalid", StatusCodes.Status400BadRequest, "Ảnh không hợp lệ. Chỉ nhận JPG, PNG hoặc WebP, không phải ảnh động.");
            }
            catch (OptionsValidationException)
            {
                throw new LeadRuleException("upload_unavailable", StatusCodes.Status503ServiceUnavailable, "Chưa bật tính năng gửi ảnh.");
            }
            catch (InvalidOperationException)
            {
                throw new LeadRuleException("upload_failed", StatusCodes.Status502BadGateway, "Không tải ảnh lên được, vui lòng thử lại.");
            }
        };
    }

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
        catch (LeadRuleException ex)
        {
            return StatusCode(ex.Status, new { code = ex.Code, message = ex.Message });
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
    /// Nhắn tự do, không còn bộ lọc nội dung; lưu vào bảng lead_messages.
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
                ct: ct);

            return CreatedAtAction(nameof(GetReceivedLeadMessages), new { id }, result);
        });

    /// <summary>Trung tâm gửi một ảnh (kèm chú thích tùy chọn) trong yêu cầu liên hệ. Cùng kiểm tra bảo mật như phía học sinh.</summary>
    [HttpPost("received/{id:guid}/messages/image")]
    [Authorize(Roles = UserRoles.Center)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxChatImageBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxChatImageBytes + 64 * 1024, ValueLengthLimit = 16 * 1024, ValueCountLimit = 4)]
    [EnableRateLimiting(MediaUploadRateLimit.PolicyName)]
    [ProducesResponseType(typeof(SendLeadMessageResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> SendReceivedLeadImageMessage(Guid id, [FromForm] SendLeadImageMessageForm form, CancellationToken ct) =>
        ExecuteCenterLeadActionAsync(async (userId, userEmail, userRole) =>
        {
            var factory = CreateAttachmentFactory(form.File);
            var result = await leadService.SendLeadMessageForCenterUserAsync(
                userId, userEmail, userRole, id, form.Content ?? string.Empty, factory, ct);
            return CreatedAtAction(nameof(GetReceivedLeadMessages), new { id }, result);
        });

    /// <summary>
    /// [UC-58] Trung tâm báo cáo một tin của học sinh / phụ huynh (thô tục, quấy rối...). Admin nhận thông báo và xem xét.
    /// </summary>
    [HttpPost("received/{id:guid}/messages/{messageId:guid}/report")]
    [Authorize(Roles = UserRoles.Center)]
    [ProducesResponseType(typeof(LeadMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> ReportReceivedLeadMessage(
        Guid id,
        Guid messageId,
        [FromBody] ReportLeadMessageRequest? request,
        CancellationToken ct) =>
        ExecuteCenterLeadActionAsync(async (userId, userEmail, userRole) =>
            Ok(await leadService.ReportReceivedLeadMessageAsync(userId, userEmail, userRole, id, messageId, request?.Reason, ct)));
}

/// <summary>Form multipart gửi ảnh trong chat (wrapper để Swagger sinh được schema).</summary>
public sealed class SendLeadImageMessageForm
{
    public IFormFile? File { get; set; }
    public string? Content { get; set; }
}
