using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.Settings;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Costs;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Tham số hệ thống trên trang quản trị. Bảng quy đổi điểm grade_scale.10: moderator sửa, admin chỉ xem
/// (quyết định của nhóm: moderator giữ chuẩn, admin giám sát).
/// </summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Moderator}")]
[Route("api/v1/admin/settings")]
public class AdminSettingsController(
    IGradeScaleSettingService gradeScale,
    ICostParametersSettingService costParameters,
    IExchangeRateProvider exchangeRates) : ControllerBase
{
    /// <summary>
    /// [UC-65] Tỉ giá USD sang VNĐ đang dùng: giá trị, thời điểm cập nhật gần nhất, nguồn, số ngày từ lần cập nhật, cảnh báo dữ liệu cũ hoặc đang dùng
    /// tỉ giá dự phòng. Chỉ xem: tỉ giá tự cập nhật mỗi ngày từ nguồn công khai, Moderator không sửa.
    /// </summary>
    [HttpGet("exchange-rate")]
    public async Task<ExchangeRateStatusDto> GetExchangeRate(CancellationToken ct)
    {
        var quote = await exchangeRates.GetQuoteAsync(ct);
        var parameters = await costParameters.GetCurrentParametersAsync(ct);
        return ExchangeRateStatus.From(quote, parameters.BankSellRateSpread, DateTimeOffset.UtcNow);
    }

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

    [HttpGet("cost-parameters")]
    public Task<CostParametersSettingDto> GetCostParameters(CancellationToken ct) => costParameters.GetAsync(ct);

    [HttpPut("cost-parameters")]
    [Authorize(Roles = UserRoles.Moderator)]
    public async Task<ActionResult<CostParametersSettingDto>> UpdateCostParameters(
        [FromBody] UpdateCostParametersRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId) return Unauthorized();

        var result = await costParameters.UpdateAsync(request, actorId, ct);
        return result.Status switch
        {
            CostParametersUpdateStatus.Invalid => BadRequest(new { errors = result.Errors }),
            CostParametersUpdateStatus.Conflict => Conflict(new { errors = result.Errors, current = result.Value }),
            _ => Ok(result.Value!),
        };
    }
}
