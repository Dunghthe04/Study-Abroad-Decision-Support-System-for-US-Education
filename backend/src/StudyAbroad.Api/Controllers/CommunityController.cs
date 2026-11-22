using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Api.RateLimiting;
using StudyAbroad.Application.Community;
using StudyAbroad.Application.MediaUploads;
using StudyAbroad.Infrastructure.MediaUploads;

namespace StudyAbroad.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/community")]
public sealed class CommunityController(
    ICommunityService community,
    IServiceProvider? services = null,
    IOptions<CloudinaryOptions>? cloudinaryOptions = null) : ControllerBase
{
    private const int MaxChatImageBytes = 5 * 1024 * 1024;

    [HttpGet("messages")]
    [ProducesResponseType(typeof(IReadOnlyList<CommunityMessageDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> List(
        [FromQuery] DateTime? before,
        [FromQuery] Guid? beforeId,
        [FromQuery] DateTime? after,
        [FromQuery] Guid? afterId,
        [FromQuery] int take = 50,
        CancellationToken ct = default) =>
        RunAsync(async userId => Ok(await community.ListMessagesAsync(userId, before, beforeId, after, afterId, take, ct)));

    [HttpGet("restriction")]
    [ProducesResponseType(typeof(CommunityRestrictionDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Restriction(CancellationToken ct) =>
        RunAsync(async userId => Ok(await community.RestrictionStatusAsync(userId, ct)));

    [HttpPost("messages")]
    [EnableRateLimiting(CommunityPostRateLimit.PolicyName)]
    [ProducesResponseType(typeof(CommunityMessageDto), StatusCodes.Status201Created)]
    public Task<IActionResult> Send([FromBody] SendCommunityMessageRequest request, CancellationToken ct) =>
        RunAsync(async userId =>
        {
            var dto = await community.SendAsync(userId, request, ct);
            return StatusCode(StatusCodes.Status201Created, dto);
        });

    [HttpPost("messages/image")]
    [EnableRateLimiting(CommunityPostRateLimit.PolicyName)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(CommunityMessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> SendImage([FromForm] SendCommunityImageMessageForm form, CancellationToken ct) =>
        RunAsync(async userId =>
        {
            var factory = CreateAttachmentFactory(form.File);
            var dto = await community.SendImageAsync(userId, form.Content, form.ReplyToId, factory, ct);
            return StatusCode(StatusCodes.Status201Created, dto);
        });

    [HttpPost("messages/{id:guid}/report")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> Report(Guid id, [FromBody] ReportCommunityMessageRequest request, CancellationToken ct) =>
        RunAsync(async userId =>
        {
            await community.ReportAsync(userId, id, request, ct);
            return NoContent();
        });

    private Func<CancellationToken, Task<CommunityAttachment>> CreateAttachmentFactory(IFormFile? file)
    {
        if (file is null || file.Length == 0 || Request.Form.Files.Count != 1)
            throw new ArgumentException("Vui lòng chọn đúng một ảnh để gửi.");
        if (file.Length > MaxChatImageBytes)
            throw new ArgumentException($"Ảnh tối đa {MaxChatImageBytes / (1024 * 1024)}MB.");

        return async ct =>
        {
            if (services is null || cloudinaryOptions is null)
                throw new CommunityRuleException("upload_unavailable", StatusCodes.Status503ServiceUnavailable, "Chưa bật tính năng gửi ảnh.");

            try
            {
                var uploader = services.GetRequiredService<IMediaUploader>();
                var max = Math.Min(cloudinaryOptions.Value.MaxFileSizeBytes, MaxChatImageBytes);
                await using var stream = file.OpenReadStream();
                var result = await uploader.UploadImageAsync(
                    new MediaUploadRequest(stream, file.FileName, file.ContentType, max, FolderSuffix: "community-chat", ModerateContent: true), ct);
                return new CommunityAttachment(result.SecureUrl, result.PublicId);
            }
            catch (MediaContentRejectedException ex)
            {
                throw new CommunityRuleException("image_rejected", StatusCodes.Status400BadRequest, ex.Message);
            }
            catch (MediaUploadSizeLimitException)
            {
                throw new CommunityRuleException("image_too_large", StatusCodes.Status400BadRequest, "Ảnh vượt quá dung lượng cho phép.");
            }
            catch (ArgumentException)
            {
                throw new CommunityRuleException("image_invalid", StatusCodes.Status400BadRequest, "Ảnh không hợp lệ. Chỉ nhận JPG, PNG hoặc WebP, không phải ảnh động.");
            }
            catch (OptionsValidationException)
            {
                throw new CommunityRuleException("upload_unavailable", StatusCodes.Status503ServiceUnavailable, "Chưa bật tính năng gửi ảnh.");
            }
        };
    }

    private async Task<IActionResult> RunAsync(Func<Guid, Task<IActionResult>> action)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });
        try
        {
            return await action(userId);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (CommunityRuleException ex)
        {
            return StatusCode(ex.Status, new { code = ex.Code, message = ex.Message });
        }
    }
}

public sealed class SendCommunityImageMessageForm
{
    public IFormFile? File { get; set; }
    public string? Content { get; set; }
    public Guid? ReplyToId { get; set; }
}
