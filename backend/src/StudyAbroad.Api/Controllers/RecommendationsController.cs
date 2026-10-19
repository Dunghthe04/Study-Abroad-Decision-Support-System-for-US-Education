using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
[Route("api/v1/recommendations")]
public class RecommendationsController(RecommendationService recommendations) : ControllerBase
{
    // pref bỏ trống thì service dùng [2, 2, 2].
    public record CreateRecommendationRequest(IReadOnlyList<int>? Pref);

    public record RecommendationResponse(
        Guid Id,
        DateTime CreatedAt,
        string StudyLevel,
        string? AlgorithmVersion,
        JsonElement Criteria,
        JsonElement Items);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRecommendationRequest? request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        if (request?.Pref is { } pref && (pref.Count != 3 || pref.Any(n => n is < 0 or > 4)))
            return BadRequest(new { message = "Ưu tiên phải là 3 số từ 0 đến 4: chi phí, học bổng, chất lượng." });

        try
        {
            var saved = await recommendations.CreateAsync(userId.Value, request?.Pref, ct);
            return Ok(ToResponse(saved));
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Chưa") || ex.Message.StartsWith("Hồ sơ"))
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "Kết quả gợi ý không hợp lệ");
        }
        catch (HttpRequestException)
        {
            return Problem(detail: "Dịch vụ gợi ý đang bận.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("latest")]
    public async Task<IActionResult> Latest(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var saved = await recommendations.GetLatestAsync(userId.Value, ct);
        return saved is null
            ? NotFound(new { message = "Chưa có lần gợi ý nào." })
            : Ok(ToResponse(saved));
    }

    private static RecommendationResponse ToResponse(Recommendation row)
    {
        using var criteria = JsonDocument.Parse(row.CriteriaJson);
        using var items = JsonDocument.Parse(row.ItemsJson);
        return new RecommendationResponse(
            row.Id,
            row.CreatedAt,
            row.StudyLevel,
            row.AlgorithmVersion,
            criteria.RootElement.Clone(),
            items.RootElement.Clone());
    }
}
