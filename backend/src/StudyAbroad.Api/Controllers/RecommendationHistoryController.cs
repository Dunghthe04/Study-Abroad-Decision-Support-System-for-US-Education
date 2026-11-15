using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Api.Controllers;

/// <summary>UC-87: xem lịch sử gợi ý và so sánh hai lần chạy. Chỉ đọc.
/// Học sinh xem của mình; phụ huynh có liên kết đang hiệu lực xem của con.
/// Không có quyền hoặc không tìm thấy đều trả 404, để người khác không dò được id lần chạy.</summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
[Route("api/v1/recommendations/history")]
public class RecommendationHistoryController(RecommendationHistoryService history) : ControllerBase
{
    private const string NotFoundMessage = "Không tìm thấy lần gợi ý.";

    // Bỏ trống studentId: lịch sử của mình. Phụ huynh truyền studentId của con đã liên kết.
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? studentId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var runs = await history.ListAsync(userId.Value, studentId, ct);
        return runs is null ? NotFound(new { message = "Không tìm thấy học sinh." }) : Ok(runs);
    }

    // Một lần chạy đầy đủ, cùng khuôn với GET api/v1/recommendations/latest để frontend dùng lại thẻ trường
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var row = await history.GetAsync(userId.Value, id, ct);
        return row is null ? NotFound(new { message = NotFoundMessage }) : Ok(ToResponse(row));
    }

    // So sánh hai lần chạy của cùng một học sinh: from là lần trước, to là lần sau
    [HttpGet("compare")]
    public async Task<IActionResult> Compare([FromQuery] Guid from, [FromQuery] Guid to, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        if (from == Guid.Empty || to == Guid.Empty || from == to)
            return BadRequest(new { message = "Hãy chọn hai lần gợi ý khác nhau để so sánh." });

        var result = await history.CompareAsync(userId.Value, from, to, ct);
        return result is null ? NotFound(new { message = NotFoundMessage }) : Ok(result);
    }

    // Cùng cách đổi như RecommendationsController (file của luồng gợi ý), dùng lại kiểu RecommendationResponse của nó
    private static RecommendationsController.RecommendationResponse ToResponse(Recommendation row)
    {
        using var criteria = JsonDocument.Parse(row.CriteriaJson);
        using var items = JsonDocument.Parse(row.ItemsJson);
        return new RecommendationsController.RecommendationResponse(
            row.Id, row.CreatedAt, row.StudyLevel, row.AlgorithmVersion,
            criteria.RootElement.Clone(), items.RootElement.Clone());
    }
}
