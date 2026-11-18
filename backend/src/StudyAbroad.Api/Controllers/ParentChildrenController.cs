using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>Khu "Con của tôi": phụ huynh xem dữ liệu của con đã liên kết, chỉ đọc.
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
}
