using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Api.Controllers;

[ApiController]
[Route("api/v1/admin/center-verifications")]
[Authorize(Roles = UserRoles.Moderator)]
public class AdminCenterVerificationsController(
    IStudyCenterService centerService,
    IAuditLogRepository auditLogs,
    IWebHostEnvironment env) : ControllerBase
{
    /// <summary>[UC-59] Lấy danh sách hàng đợi yêu cầu xác minh trung tâm (lọc theo status, tìm kiếm, phân trang).</summary>
    [HttpGet]
    public Task<PagedResult<CenterVerificationDto>> GetRequests(
        [FromQuery] CenterVerificationQuery query,
        CancellationToken ct) =>
        centerService.GetVerificationRequestsAsync(query, ct);

    /// <summary>[UC-59] Xem chi tiết hồ sơ xác minh của một trung tâm kèm danh sách tài liệu.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CenterVerificationDto>> GetById(Guid id, CancellationToken ct)
    {
        var item = await centerService.GetVerificationRequestByIdAsync(id, ct);
        return item is null ? NotFound("Không tìm thấy yêu cầu xác minh.") : Ok(item);
    }

    /// <summary>[BR-C5 / UC-59] Xem hoặc tải tài liệu minh chứng kèm ghi nhật ký kiểm toán bắt buộc.</summary>
    [HttpGet("{id:guid}/documents/{documentId:guid}/file")]
    public async Task<IActionResult> GetDocumentFile(
        Guid id,
        Guid documentId,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var doc = await centerService.GetVerificationDocumentAsync(documentId, ct);
        if (doc is null || doc.RequestId != id)
            return NotFound("Không tìm thấy tài liệu.");

        // Ghi nhật ký audit bắt buộc theo BR-C5: mỗi lần xem giấy tờ phải ghi log
        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = actorId,
            Action = "center.document_viewed",
            EntityType = "CenterVerificationDocument",
            EntityId = doc.Id.ToString(),
            DetailJson = $"{{\"requestId\":\"{id}\",\"documentType\":\"{doc.DocumentType}\",\"fileName\":\"{doc.FileName}\"}}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        }, ct);

        // Đọc tệp từ đĩa:
        // doc.FileUrl có dạng /api/v1/center/verification/documents/{centerId}/{safeFileName}
        var urlParts = doc.FileUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var fileName = urlParts.Length > 0 ? urlParts[^1] : doc.FileName;
        var centerId = doc.CenterId.ToString();

        var localPath = Path.Combine(env.ContentRootPath, "uploads", "verifications", centerId, fileName);
        if (!System.IO.File.Exists(localPath))
            return NotFound("Tệp tài liệu không tồn tại trên hệ thống lưu trữ.");

        var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, doc.ContentType, doc.FileName);
    }

    /// <summary>[UC-59] Moderator duyệt, từ chối, yêu cầu bổ sung hoặc thu hồi dấu xác minh.</summary>
    [HttpPost("{id:guid}/review")]
    public async Task<ActionResult<StudyCenterDto>> Review(
        Guid id,
        [FromBody] ReviewCenterVerificationRequest request,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var result = await centerService.ReviewVerificationAsync(id, request, actorId, ct);
        if (!result.Success || result.Center is null)
            return ValidationProblem(result.ErrorMessage);

        return Ok(result.Center);
    }
}

