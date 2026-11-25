using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-91] Xem và lọc nhật ký truy cập dữ liệu nhạy cảm (tài liệu xác minh trung tâm, tin nhắn bị báo cáo, xuất mẫu ở UC-96) và nhật ký thao tác.
/// Chỉ dành cho Administrator. Quy tắc: nhật ký bất biến, không sửa và không xóa được.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/access-logs")]
public class AdminAccessLogsController(IAccessLogService accessLogs) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách nhật ký truy cập theo bộ lọc: danh mục thao tác, mã action, người thực hiện, khoảng thời gian.
    /// Sắp xếp mới nhất trước, phân trang mặc định 20 dòng.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AccessLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AccessLogDto>>> GetLogs(
        [FromQuery] string? category,
        [FromQuery] string? action,
        [FromQuery] string? search,
        [FromQuery] Guid? actorUserId,
        [FromQuery] string? actorRole,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await accessLogs.GetAccessLogsAsync(
            category, action, search, actorUserId, actorRole, from, to, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Thống kê tổng quan số lượt truy cập dữ liệu nhạy cảm theo các nhóm thao tác.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AccessLogSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccessLogSummaryDto>> GetSummary(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct = default)
    {
        var summary = await accessLogs.GetSummaryAsync(from, to, ct);
        return Ok(summary);
    }
}

