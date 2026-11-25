using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.BirthDateChanges;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>[UC-150] Administrator xem, duyệt hoặc từ chối yêu cầu sửa ngày sinh.</summary>
[ApiController]
[Route("api/v1/admin/birth-date-requests")]
[Authorize(Roles = UserRoles.Admin)]
public class AdminBirthDateRequestsController(IAdminBirthDateChangeService birthDateChanges) : ControllerBase
{
    /// <summary>Mặc định Chờ duyệt, cũ nhất trước. status=all lấy mọi trạng thái.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await birthDateChanges.ListAsync(status, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        ToResult(await birthDateChanges.GetAsync(id, ct));

    /// <summary>Mở minh chứng; mỗi lần mở ghi một dòng nhật ký truy cập (UC-91).</summary>
    [HttpGet("{id:guid}/evidence")]
    public async Task<IActionResult> GetEvidence(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        var result = await birthDateChanges.OpenEvidenceAsync(id, adminId, RequestInfo(), ct);
        if (!result.Success || result.Value is not { } file)
            return ToProblem(result.Error, result.Message);
        Response.Headers.CacheControl = "no-store";
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        return ToResult(await birthDateChanges.ApproveAsync(id, adminId, RequestInfo(), ct));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectBirthDateRequestBody? body, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();
        return ToResult(await birthDateChanges.RejectAsync(id, adminId, body?.Reason, RequestInfo(), ct));
    }

    private AuditRequestInfo RequestInfo() =>
        new(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());

    private IActionResult ToResult<T>(BirthDateChangeResult<T> result) =>
        result.Success ? Ok(new { message = result.Message, data = result.Value }) : ToProblem(result.Error, result.Message);

    private ObjectResult ToProblem(BirthDateChangeError error, string message) => error switch
    {
        BirthDateChangeError.NotFound => Problem(message, statusCode: StatusCodes.Status404NotFound),
        // E1: yêu cầu không còn Chờ duyệt
        BirthDateChangeError.Conflict => Problem(message, statusCode: StatusCodes.Status409Conflict),
        BirthDateChangeError.Forbidden => Problem(message, statusCode: StatusCodes.Status403Forbidden),
        _ => Problem(message, statusCode: StatusCodes.Status400BadRequest),
    };
}

public sealed record RejectBirthDateRequestBody(string? Reason);
