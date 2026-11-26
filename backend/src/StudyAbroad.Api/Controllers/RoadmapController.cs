using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Dashboard.Roadmap;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Lộ trình theo trường mục tiêu ở trang Tổng quan (docs/KE_HOACH_LO_TRINH_THEO_TRUONG_MUC_TIEU.md).
/// Chung tiền tố api/v1/dashboard với DashboardController.
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
public class RoadmapController(IRoadmapService roadmapService) : ControllerBase
{
    /// <summary>
    /// Lộ trình của học sinh (hoặc con managed khi phụ huynh đang thao tác thay). Chưa có hồ sơ -> 204.
    /// </summary>
    [HttpGet("roadmap")]
    [Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
    public async Task<ActionResult<RoadmapDto>> GetRoadmap(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var roadmap = await roadmapService.GetRoadmapAsync(userId, ct);
        return roadmap is null ? NoContent() : Ok(roadmap);
    }

    /// <summary>Phụ huynh chỉ xem lộ trình của con đã liên kết. Không có liên kết -> 404, con chưa có hồ sơ -> 204.</summary>
    [HttpGet("children/{studentId:guid}/roadmap")]
    [Authorize(Roles = UserRoles.Parent)]
    public async Task<ActionResult<RoadmapDto>> GetChildRoadmap(Guid studentId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } parentId)
            return Unauthorized();

        var (linked, roadmap) = await roadmapService.GetChildRoadmapAsync(parentId, studentId, ct);
        if (!linked)
            return NotFound(new { message = "Không tìm thấy thông tin của con hoặc liên kết chưa hiệu lực." });
        return roadmap is null ? NoContent() : Ok(roadmap);
    }

    /// <summary>
    /// Lưu tỉnh/thành học sinh đang sống (ô chọn ở Tổng quan hoặc câu hỏi trong chat); province rỗng = xóa.
    /// Dùng để gợi ý trung tâm gần trong lộ trình. Phụ huynh đang thao tác thay con managed cũng gọi được.
    /// </summary>
    [HttpPut("province")]
    [Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
    public async Task<IActionResult> SetProvince([FromBody] SetProvinceRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var result = await roadmapService.SetProvinceAsync(userId, request.Province, ct);
        return result switch
        {
            SetProvinceResult.InvalidProvince => BadRequest(new { message = "Tỉnh/thành không nằm trong danh sách." }),
            SetProvinceResult.NoProfile => NotFound(new { message = "Chưa có hồ sơ để lưu tỉnh/thành." }),
            _ => NoContent(),
        };
    }

    /// <summary>Danh sách 34 tỉnh/thành để hiện ô chọn.</summary>
    [HttpGet("provinces")]
    [AllowAnonymous]
    public ActionResult<IReadOnlyList<string>> GetProvinces() => Ok(VietnamProvinces.All);
}

public record SetProvinceRequest(string? Province);
