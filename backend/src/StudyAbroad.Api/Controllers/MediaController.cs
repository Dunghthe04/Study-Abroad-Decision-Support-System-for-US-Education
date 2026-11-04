using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Api.MediaUploads;
using StudyAbroad.Api.RateLimiting;
using StudyAbroad.Application.MediaUploads;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Infrastructure.MediaUploads;

namespace StudyAbroad.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Moderator}")]
[Route("api/v1/media")]
public sealed class MediaController(IServiceProvider services, IOptions<CloudinaryOptions> options) : ControllerBase
{
    [HttpPost("images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageUploadPolicy.MaxRequestBytes)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = ImageUploadPolicy.MaxFileBytes,
        ValueLengthLimit = 1024,
        ValueCountLimit = 4,
        MultipartHeadersLengthLimit = 4096,
        MultipartBoundaryLengthLimit = 128)]
    [EnableRateLimiting(MediaUploadRateLimit.PolicyName)]
    [ProducesResponseType(typeof(MediaUploadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MediaUploadError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MediaUploadError), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(MediaUploadError), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(MediaUploadError), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(MediaUploadError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadImage([FromForm] UploadImageForm request, CancellationToken ct)
    {
        if (User.GetUserId() is null)
            return Unauthorized(new MediaUploadError("A valid authenticated user is required."));
        if (Request.ContentLength > ImageUploadPolicy.MaxRequestBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new MediaUploadError("The upload request is too large."));

        var purposeLimit = ImageUploadPolicy.GetPurposeLimit(request.Purpose);
        if (purposeLimit is null)
            return BadRequest(new MediaUploadError("Purpose must be school-logo or school-cover."));
        if (request.File is null || request.File.Length == 0)
            return BadRequest(new MediaUploadError("One non-empty image file is required."));
        if (Request.Form.Files.Count != 1)
            return BadRequest(new MediaUploadError("Exactly one image file is required."));
        if (request.File.Length > purposeLimit)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new MediaUploadError("The image exceeds the size limit for this purpose."));

        try
        {
            var maxSizeBytes = Math.Min(options.Value.MaxFileSizeBytes, purposeLimit.Value);
            if (request.File.Length > maxSizeBytes)
                return StatusCode(StatusCodes.Status413PayloadTooLarge, new MediaUploadError("The image exceeds the configured file size limit."));

            var uploader = services.GetRequiredService<IMediaUploader>();
            await using var stream = request.File.OpenReadStream();
            var result = await uploader.UploadImageAsync(
                new MediaUploadRequest(stream, request.File.FileName, request.File.ContentType, maxSizeBytes), ct);
            return Ok(result);
        }
        catch (OptionsValidationException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new MediaUploadError("Image uploads are not configured. Contact an administrator."));
        }
        catch (MediaUploadSizeLimitException)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new MediaUploadError("The image exceeds the allowed file size limit."));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new MediaUploadError(exception.Message));
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new MediaUploadError("The image provider could not complete the upload. Please try again."));
        }
    }
}
