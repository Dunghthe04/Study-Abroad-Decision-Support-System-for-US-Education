using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>Khu "Con của tôi": phụ huynh xem dữ liệu của con đã liên kết, và gợi ý trường mục tiêu cho con.
/// Không liên kết với con đó thì trả 404 (không lộ id có tồn tại hay không), giống UC-86/87.</summary>
[ApiController]
[Authorize(Roles = UserRoles.Parent)]
[Route("api/v1/parent/children")]
public class ParentChildrenController(ParentChildrenService children) : ControllerBase
{
    // GET /api/v1/parent/children/{studentId}/target-schools
    [HttpGet("{studentId:guid}/target-schools")]
    public async Task<IActionResult> GetTargetSchools(Guid studentId, CancellationToken ct)
    {
        var parentId = User.GetUserId();
        if (parentId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var list = await children.GetTargetSchoolsAsync(parentId.Value, studentId, ct);
        if (list is null)
            return NotFound(new { message = "Không tìm thấy học sinh." });

        return Ok(list);
    }

    // POST /api/v1/parent/children/{studentId}/target-schools: gợi ý một trường cho con (con thêm hoặc bỏ qua)
    [HttpPost("{studentId:guid}/target-schools")]
    public async Task<IActionResult> SuggestTargetSchool(Guid studentId, [FromBody] SuggestTargetSchoolRequest request, CancellationToken ct)
    {
        var parentId = User.GetUserId();
        if (parentId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var result = await children.SuggestTargetSchoolAsync(parentId.Value, studentId, request, ct);
        if (result.NotLinked)
            return NotFound(new { message = "Không tìm thấy học sinh." });
        if (result.Error is not null)
            return BadRequest(new { message = result.Error });

        return Ok(result.TargetSchool);
    }

    // DELETE /api/v1/parent/children/{studentId}/target-schools/{id}: rút lại gợi ý con chưa xem
    [HttpDelete("{studentId:guid}/target-schools/{id:guid}")]
    public async Task<IActionResult> WithdrawSuggestion(Guid studentId, Guid id, CancellationToken ct)
    {
        var parentId = User.GetUserId();
        if (parentId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        return await children.WithdrawSuggestionAsync(parentId.Value, studentId, id, ct) switch
        {
            null => NotFound(new { message = "Không tìm thấy học sinh." }),
            false => NotFound(new { message = "Không còn gợi ý này. Có thể con đã thêm hoặc bỏ qua." }),
            true => NoContent(),
        };
    }
}
