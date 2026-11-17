using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyAbroad.Api.RateLimiting;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

[ApiController]
[Route("api/v1/center")]
[Authorize(Roles = UserRoles.Center)]
public class CenterPortalController(
    IStudyCenterService centerService,
    IAuthService authService,
    IWebHostEnvironment env) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png"
    };

    private static readonly HashSet<string> AllowedDocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        CenterDocumentTypes.BusinessRegistration,
        CenterDocumentTypes.PowerOfAttorney,
        CenterDocumentTypes.PartnershipContract,
        CenterDocumentTypes.ConsultingLicense
    };

    private const long MaxFileBytes = 10 * 1024 * 1024; // 10MB theo quy tắc UC-71

    /// <summary>[UC-92] Xem hồ sơ thông tin của trung tâm hiện tại.</summary>
    [HttpGet("profile")]
    public async Task<ActionResult<StudyCenterDto>> GetProfile(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var center = await centerService.GetByUserIdAsync(userId, ct);
        return center is null ? NotFound("Không tìm thấy thông tin trung tâm.") : Ok(center);
    }

    /// <summary>[UC-92] Cập nhật thông tin công bố của trung tâm.</summary>
    [HttpPut("profile")]
    public async Task<ActionResult<StudyCenterDto>> UpdateProfile(
        [FromBody] UpdateCenterProfileRequest request,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var result = await centerService.UpdateCenterProfileAsync(userId, request, ct);
        if (!result.Success || result.Center is null)
            return ValidationProblem(result.ErrorMessage);

        return Ok(result.Center);
    }

    /// <summary>[UC-71, UC-91] Lấy trạng thái xác minh và tiến độ hoàn thiện hồ sơ của trung tâm.</summary>
    [HttpGet("verification/status")]
    public async Task<ActionResult<CenterVerificationStatusDto>> GetVerificationStatus(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var status = await centerService.GetVerificationStatusAsync(userId, ct);
        return Ok(status);
    }

    /// <summary>[UC-91] Gửi OTP qua SMS tới số điện thoại hiện tại của trung tâm để xác thực.</summary>
    [HttpPost("contact/phone/verify/request")]
    [EnableRateLimiting(AuthRateLimit.PolicyName)]
    public Task<IActionResult> RequestPhoneVerification(CancellationToken ct) =>
        RunContactChange(userId => authService.RequestCenterPhoneVerificationAsync(userId, ct));

    /// <summary>[UC-91] Nhập OTP SMS; đúng thì số điện thoại được ghi là đã xác thực.</summary>
    [HttpPost("contact/phone/verify")]
    [EnableRateLimiting(AuthRateLimit.PolicyName)]
    public Task<IActionResult> VerifyPhone([FromBody] VerifyContactCodeDto request, CancellationToken ct) =>
        RunContactChange(userId => authService.VerifyCenterPhoneAsync(userId, request?.Code, ct));

    /// <summary>[UC-91] Gửi OTP qua SMS tới số điện thoại MỚI để đổi số liên lạc.</summary>
    [HttpPost("contact/phone/request")]
    [EnableRateLimiting(AuthRateLimit.PolicyName)]
    public Task<IActionResult> RequestPhoneChange([FromBody] RequestContactChangeDto request, CancellationToken ct) =>
        RunContactChange(userId => authService.RequestCenterPhoneChangeAsync(userId, request, ct));

    /// <summary>[UC-91] Xác nhận OTP và đổi số điện thoại liên lạc (số mới được ghi là đã xác thực).</summary>
    [HttpPost("contact/phone/confirm")]
    [EnableRateLimiting(AuthRateLimit.PolicyName)]
    public Task<IActionResult> ConfirmPhoneChange([FromBody] ConfirmContactChangeDto request, CancellationToken ct) =>
        RunContactChange(userId => authService.ConfirmCenterPhoneChangeAsync(userId, request, ct));

    /// <summary>Gửi OTP tới email MỚI để đổi email liên lạc.</summary>
    [HttpPost("contact/email/request")]
    [EnableRateLimiting(AuthRateLimit.PolicyName)]
    public Task<IActionResult> RequestEmailChange([FromBody] RequestContactChangeDto request, CancellationToken ct) =>
        RunContactChange(userId => authService.RequestCenterEmailChangeAsync(userId, request, ct));

    /// <summary>Xác nhận OTP và đổi email liên lạc (email đăng nhập không đổi).</summary>
    [HttpPost("contact/email/confirm")]
    [EnableRateLimiting(AuthRateLimit.PolicyName)]
    public Task<IActionResult> ConfirmEmailChange([FromBody] ConfirmContactChangeDto request, CancellationToken ct) =>
        RunContactChange(userId => authService.ConfirmCenterEmailChangeAsync(userId, request, ct));

    private async Task<IActionResult> RunContactChange(Func<Guid, Task<AuthResult>> action)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var result = await action(userId);
        return result.Error switch
        {
            AuthError.None => Ok(new { message = result.Message }),
            _ => ValidationProblem(result.Message)
        };
    }

    /// <summary>[UC-71] Tải lên tài liệu minh chứng (GCN ĐKDN, ủy quyền, hợp đồng đối tác, giấy phép).</summary>
    [HttpPost("verification/upload-document")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<VerificationDocumentUploadDto>> UploadDocument(
        [FromForm] UploadVerificationDocumentForm form,
        CancellationToken ct)
    {
        var file = form.File!;
        var documentType = form.DocumentType;

        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var center = await centerService.GetByUserIdAsync(userId, ct);
        if (center is null)
            return NotFound("Không tìm thấy thông tin trung tâm.");

        if (file is null || file.Length == 0)
            return ValidationProblem("Vui lòng chọn tệp tài liệu để tải lên.");

        if (file.Length > MaxFileBytes)
            return ValidationProblem($"Dung lượng tệp tối đa cho phép là {MaxFileBytes / (1024 * 1024)}MB.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            return ValidationProblem("Định dạng tệp không hợp lệ. Chỉ chấp nhận tệp .pdf, .jpg, .jpeg, .png.");

        var cleanDocType = documentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedDocumentTypes.Contains(cleanDocType))
            return ValidationProblem("Loại tài liệu không hợp lệ.");

        // Lưu vào thư mục uploads/verifications/{centerId}
        var uploadFolder = Path.Combine(env.ContentRootPath, "uploads", "verifications", center.Id.ToString());
        Directory.CreateDirectory(uploadFolder);

        var safeFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        var fullPath = Path.Combine(uploadFolder, safeFileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream, ct);
        }

        var fileUrl = $"/api/v1/center/verification/documents/{center.Id}/{safeFileName}";

        return Ok(new VerificationDocumentUploadDto(
            cleanDocType,
            fileUrl,
            Path.GetFileName(file.FileName),
            file.Length,
            file.ContentType));
    }

    /// <summary>[UC-71] Nộp hồ sơ xác minh pháp lý trung tâm (chuyển trạng thái sang pending_review).</summary>
    [HttpPost("verification")]
    public async Task<ActionResult<StudyCenterDto>> SubmitVerification(
        [FromBody] SubmitVerificationDto request,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var result = await centerService.SubmitVerificationAsync(userId, request, ct);
        if (!result.Success || result.Center is null)
            return ValidationProblem(result.ErrorMessage);

        return Ok(result.Center);
    }
}


/// <summary>Form multipart cho tải tài liệu xác minh (wrapper để Swagger sinh được schema).</summary>
public sealed class UploadVerificationDocumentForm
{
    public IFormFile? File { get; set; }
    public string? DocumentType { get; set; }
}
