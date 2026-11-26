using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Api.RateLimiting;
using StudyAbroad.Application.BirthDateChanges;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-105 / UC-107] Chủ tài khoản Học sinh / Phụ huynh gửi, xem và hủy yêu cầu sửa ngày sinh.
/// Không nằm trong ActingScope.ChildControllers nên phụ huynh đang thao tác thay con vẫn chỉ sửa được ngày sinh của chính mình.
/// </summary>
[ApiController]
[Route("api/v1/account/birth-date-requests")]
[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
public class BirthDateRequestsController(IBirthDateChangeService birthDateChanges, BirthDateChangeOptions options) : ControllerBase
{
    /// <summary>Trần cho cả request multipart; dung lượng tệp thật kiểm theo cấu hình BirthDateChange:MaxEvidenceBytes.</summary>
    private const long MaxRequestBytes = 10 * 1024 * 1024;

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return ToResult(await birthDateChanges.GetCurrentAsync(userId, ct));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes, ValueLengthLimit = 16 * 1024, ValueCountLimit = 8)]
    [EnableRateLimiting(MediaUploadRateLimit.PolicyName)]
    public async Task<IActionResult> Submit([FromForm] SubmitBirthDateChangeForm form, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        BirthDateEvidenceUpload? evidence = null;
        if (form.Evidence is { Length: > 0 } file)
        {
            // Báo quá dung lượng trước khi đọc cả tệp vào bộ nhớ
            if (file.Length > options.MaxEvidenceBytes)
                return Problem($"Tệp minh chứng tối đa {options.MaxEvidenceBytes / (1024 * 1024)} MB.", statusCode: StatusCodes.Status400BadRequest);
            using var buffer = new MemoryStream((int)file.Length);
            await using (var stream = file.OpenReadStream())
                await stream.CopyToAsync(buffer, ct);
            evidence = new BirthDateEvidenceUpload(buffer.ToArray(), file.FileName);
        }

        var request = new SubmitBirthDateChangeRequest(form.BirthDate, form.Reason, form.EvidenceType, evidence, form.RepresentativeEmail);
        return ToResult(await birthDateChanges.SubmitAsync(userId, request, ct));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return ToResult(await birthDateChanges.CancelAsync(userId, id, ct));
    }

    private IActionResult ToResult<T>(BirthDateChangeResult<T> result) => result.Error switch
    {
        BirthDateChangeError.None => Ok(new { message = result.Message, data = result.Value }),
        // E5: trả kèm yêu cầu đang chờ để giao diện hiện lại
        BirthDateChangeError.Conflict => Conflict(new { message = result.Message, data = result.Value }),
        BirthDateChangeError.NotFound => Problem(result.Message, statusCode: StatusCodes.Status404NotFound),
        BirthDateChangeError.Forbidden => Problem(result.Message, statusCode: StatusCodes.Status403Forbidden),
        _ => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest),
    };
}

/// <summary>Form multipart gửi yêu cầu sửa ngày sinh (wrapper để Swagger sinh được schema).</summary>
public sealed class SubmitBirthDateChangeForm
{
    public DateOnly? BirthDate { get; set; }
    public string? Reason { get; set; }
    public string? EvidenceType { get; set; }
    public IFormFile? Evidence { get; set; }
    public string? RepresentativeEmail { get; set; }
}
