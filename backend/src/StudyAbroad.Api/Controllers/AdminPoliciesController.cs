using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.Policies;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-103] Quản lý chính sách và điều khoản theo phiên bản. Chỉ Administrator (người khác nhận 403).
/// Quy trình: tạo nháp (chép bản đang hiệu lực) → sửa nháp → công bố. Bản đã công bố KHÔNG sửa được (409 policy_locked):
/// muốn đổi nội dung thì tạo bản mới. Mọi thao tác thay đổi đều được ghi nhật ký.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/policies")]
public class AdminPoliciesController(IPolicyService policies) : ControllerBase
{
    public sealed record CreateDraftRequest(string Kind);

    /// <summary>Lịch sử các phiên bản, bản nháp và mới nhất trước, không kèm toàn văn. Lọc theo loại bằng ?kind=; loại sai trả 400.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PolicyDocumentSummaryDto>>> List([FromQuery] string? kind, CancellationToken ct)
    {
        try
        {
            return Ok(await policies.ListAsync(kind, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Một phiên bản kèm toàn văn. 404 nếu không có.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PolicyDocumentDto>> Get(Guid id, CancellationToken ct)
    {
        var doc = await policies.GetAsync(id, ct);
        return doc is null ? NotFound() : Ok(doc);
    }

    /// <summary>
    /// Tạo bản nháp của một loại (chép toàn văn bản đang hiệu lực). 201 khi tạo mới; 200 và trả lại bản nháp đang có nếu loại đó đã có nháp.
    /// Loại sai trả 400.
    /// </summary>
    [HttpPost("drafts")]
    public async Task<ActionResult<PolicyDraftResult>> CreateDraft([FromBody] CreateDraftRequest request, CancellationToken ct)
    {
        if (Actor() is not { } actor) return Unauthorized();
        try
        {
            var result = await policies.CreateDraftAsync(actor, request.Kind, ct);
            return result.Created
                ? CreatedAtAction(nameof(Get), new { id = result.Document.Id }, result)
                : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (PolicyRuleException ex)
        {
            return Conflict(new { code = ex.Code, error = ex.Message });
        }
    }

    /// <summary>Lưu nội dung vào bản nháp. 404 nếu không có; 409 policy_locked nếu đã công bố; 400 nếu tiêu đề hoặc độ dài sai.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PolicyDocumentDto>> UpdateDraft(Guid id, [FromBody] UpdatePolicyDraftRequest request, CancellationToken ct)
    {
        if (Actor() is not { } actor) return Unauthorized();
        try
        {
            var doc = await policies.UpdateDraftAsync(actor, id, request, ct);
            return doc is null ? NotFound() : Ok(doc);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (PolicyRuleException ex)
        {
            return Conflict(new { code = ex.Code, error = ex.Message });
        }
    }

    /// <summary>
    /// Công bố bản nháp: đặt số phiên bản, khóa, chuyển bản đang hiệu lực thành hết hiệu lực. 400 nếu thiếu tiêu đề, toàn văn hoặc tóm tắt thay đổi
    /// (không đổi gì); 404 nếu không có; 409 nếu không phải bản nháp (policy_locked) hoặc có người vừa đổi (policy_conflict).
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<PolicyDocumentDto>> Publish(Guid id, CancellationToken ct)
    {
        if (Actor() is not { } actor) return Unauthorized();
        try
        {
            var doc = await policies.PublishAsync(actor, id, ct);
            return doc is null ? NotFound() : Ok(doc);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (PolicyRuleException ex)
        {
            return Conflict(new { code = ex.Code, error = ex.Message });
        }
    }

    /// <summary>Bỏ bản nháp (xóa hẳn). 204 khi xong; 404 nếu không có; 409 policy_locked nếu là bản đã công bố.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DiscardDraft(Guid id, CancellationToken ct)
    {
        if (Actor() is not { } actor) return Unauthorized();
        try
        {
            return await policies.DiscardDraftAsync(actor, id, ct) ? NoContent() : NotFound();
        }
        catch (PolicyRuleException ex)
        {
            return Conflict(new { code = ex.Code, error = ex.Message });
        }
    }

    private PolicyActor? Actor() =>
        User.GetUserId() is { } id
            ? new PolicyActor(id, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString())
            : null;
}
