using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Dashboard;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>
    /// [UC-94] Lấy tổng quan Dashboard KPI cho học sinh (hoặc phụ huynh đang acting thay cho con managed).
    /// </summary>
    [HttpGet("me")]
    [Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
    public async Task<ActionResult<StudentDashboardDto>> GetMyDashboard(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();

        var dashboard = await dashboardService.GetStudentDashboardAsync(userId, ct);
        return Ok(dashboard);
    }

    /// <summary>
    /// [UC-94] Lấy tổng quan Dashboard kèm chỉ số tài chính cho phụ huynh xem một người con cụ thể.
    /// </summary>
    [HttpGet("children/{studentId:guid}")]
    [Authorize(Roles = UserRoles.Parent)]
    public async Task<ActionResult<ParentChildDashboardDto>> GetChildDashboard(Guid studentId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } parentId)
            return Unauthorized();

        var result = await dashboardService.GetParentChildDashboardAsync(parentId, studentId, ct);
        if (result is null)
            return NotFound(new { message = "Không tìm thấy thông tin của con hoặc liên kết chưa hiệu lực." });

        return Ok(result);
    }
}
