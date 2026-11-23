using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Costs;

namespace StudyAbroad.Api.Controllers;

/// <summary>[USAS-2029][USAS-2066][USAS-2067] UC-26/27 ước tính và so sánh chi phí trường, UC-28 chi phí sinh hoạt theo bang.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/costs")]
public class CostsController(ICostService service) : ControllerBase
{
    /// <summary>codes=A (UC-26) hoặc codes=A,B,C (UC-27, tối đa 4). Khách vẫn xem được, chỉ không có ngân sách từ hồ sơ.</summary>
    [HttpGet("estimate")]
    public async Task<ActionResult<CostEstimateResponse>> Estimate(
        [FromQuery] string codes, [FromQuery] string? studyLevel, [FromQuery] decimal? scholarshipUsdPerYear,
        [FromQuery] decimal? budgetUsd, CancellationToken ct)
    {
        var list = (codes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Count is < 1 or > 4)
            return Problem("Chọn từ 1 đến 4 trường.", statusCode: StatusCodes.Status400BadRequest);
        return Ok(await service.EstimateAsync(list, studyLevel, scholarshipUsdPerYear, budgetUsd, User.GetUserId(), ct));
    }

    [HttpGet("state-living-costs")]
    public async Task<ActionResult<IReadOnlyList<StateLivingCostDto>>> StateLivingCosts(CancellationToken ct) =>
        Ok(await service.GetStateLivingCostsAsync(ct));
}
