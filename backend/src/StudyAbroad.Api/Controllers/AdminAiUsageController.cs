using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Admin.AiUsage;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Token và thời gian của các lần gọi AI cho trang quản trị. Chỉ Admin: đây là việc giám sát chi phí và chất lượng hệ thống,
/// Moderator (giữ chuẩn dữ liệu, rà câu hỏi) không cần xem.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/ai-usage")]
public class AdminAiUsageController(IAiUsageReportService report) : ControllerBase
{
    /// <summary>Tổng quan: tổng, theo nguồn (local hay cloud), theo tính năng, theo ngày. Mặc định 30 ngày gần nhất theo ngày UTC.</summary>
    [HttpGet("overview")]
    public async Task<ActionResult<AiUsageOverviewDto>> GetOverview(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        try
        {
            return Ok(await report.GetOverviewAsync(from, to, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
