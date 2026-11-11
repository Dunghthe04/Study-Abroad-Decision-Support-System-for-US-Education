using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Admin.AiUsage;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Token và thời gian của các lần gọi AI cho trang quản trị. Chỉ Admin: đây là việc giám sát chi phí và chất lượng hệ thống,
/// Moderator (giữ chuẩn dữ liệu, rà câu hỏi) không cần xem. Mọi khoảng ngày tính cả hai đầu, theo ngày UTC, mặc định 30 ngày gần nhất.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/ai-usage")]
public class AdminAiUsageController(IAiUsageReportService report) : ControllerBase
{
    /// <summary>Tổng quan cả hệ thống: tổng, theo nguồn (local hay cloud), theo tính năng, theo ngày.</summary>
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

    /// <summary>Bảng xếp hạng người dùng theo token giảm dần, phân trang (pageSize mặc định 20, tối đa 100).</summary>
    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AiUsageUserRankDto>>> GetUsers(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            return Ok(await report.GetUsersAsync(from, to, page, pageSize, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Tổng quan token riêng của một người dùng (cùng cấu trúc với tổng quan hệ thống). 404 nếu không có người dùng đó.</summary>
    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<AiUsageUserOverviewDto>> GetUserOverview(
        Guid userId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        try
        {
            var overview = await report.GetUserOverviewAsync(userId, from, to, ct);
            return overview is null ? NotFound() : Ok(overview);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
