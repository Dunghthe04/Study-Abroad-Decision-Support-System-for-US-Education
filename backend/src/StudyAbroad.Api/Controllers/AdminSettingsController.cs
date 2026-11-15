using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.Settings;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Tham số hệ thống trên trang quản trị. Bảng quy đổi điểm grade_scale.10: moderator sửa, admin chỉ xem
/// (quyết định của nhóm: moderator giữ chuẩn, admin giám sát).
/// </summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Moderator}")]
[Route("api/v1/admin/settings")]
public class AdminSettingsController(IGradeScaleSettingService gradeScale) : ControllerBase
{
    [HttpGet("grade-scale")]
    public Task<GradeScaleSettingDto> GetGradeScale(CancellationToken ct) => gradeScale.GetAsync(ct);

    /// <summary>Lịch sử sửa bảng theo trang, mới nhất ở trên.</summary>
    [HttpGet("grade-scale/history")]
    public Task<PagedResult<GradeScaleHistoryItemDto>> GetGradeScaleHistory(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default) =>
        gradeScale.GetHistoryAsync(page, pageSize, ct);

    /// <summary>Chỉ moderator (hai Authorize cộng lại: thuộc admin/moderator VÀ là moderator).</summary>
    [HttpPut("grade-scale")]
    [Authorize(Roles = UserRoles.Moderator)]
    public async Task<ActionResult<GradeScaleSavedDto>> UpdateGradeScale(
        [FromBody] UpdateGradeScaleRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();

        var result = await gradeScale.UpdateAsync(request, actorId, ct);
        return result.Status switch
        {
            GradeScaleUpdateStatus.Invalid => BadRequest(new { errors = result.Errors }),
            GradeScaleUpdateStatus.Conflict => Conflict(new { errors = result.Errors, current = result.Value }),
            _ => Ok(new GradeScaleSavedDto(result.Value!, result.RecomputedProfiles)),
        };
    }
}
