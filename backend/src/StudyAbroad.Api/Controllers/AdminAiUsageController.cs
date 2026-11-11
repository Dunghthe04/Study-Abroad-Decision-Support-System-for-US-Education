using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Admin.AiUsage;
using StudyAbroad.Application.Advisor;
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
public class AdminAiUsageController(IAiUsageReportService report, AiPricing pricing) : ControllerBase
{
    /// <summary>Bảng giá token đang áp dụng (chỉ đọc). Giá đọc từ cấu hình (appsettings hoặc biến môi trường AiPricing__...).</summary>
    [HttpGet("pricing")]
    public ActionResult<IReadOnlyList<AiPricing.PriceInfo>> GetPricing() => Ok(pricing.Catalog());

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

    /// <summary>
    /// Các lượt chat của một người (mới nhất trước), mỗi lượt kèm tổng token, số lần gọi, lỗi, thời gian, kết quả.
    /// Không có nội dung chat. 404 nếu không có người dùng đó.
    /// </summary>
    [HttpGet("users/{userId:guid}/turns")]
    public async Task<ActionResult<PagedResult<AiUsageTurnRowDto>>> GetUserTurns(
        Guid userId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var turns = await report.GetUserTurnsAsync(userId, from, to, page, pageSize, ct);
            return turns is null ? NotFound() : Ok(turns);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Chi tiết một lượt chat: từng lần gọi AI (nguồn, tính năng, model, token, thời gian, lỗi). Không có nội dung chat. 404 nếu không có.</summary>
    [HttpGet("turns/{messageId:guid}")]
    public async Task<ActionResult<AiUsageTurnDetailDto>> GetTurn(Guid messageId, CancellationToken ct)
    {
        var turn = await report.GetTurnAsync(messageId, ct);
        return turn is null ? NotFound() : Ok(turn);
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
