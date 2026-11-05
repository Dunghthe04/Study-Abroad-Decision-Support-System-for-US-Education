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

    public record RepickRequest(IReadOnlyList<int> Pref);

    // Nhóm đích: reach | match | safety. Bỏ trống thì nhóm cao hơn một bậc so với hiện tại.
    public record GoalSeekRequest(string? Target);

    // Giá trị giả định của trình mô phỏng; trường nào bỏ trống thì giữ nguyên hồ sơ thật.
    public record SimulateRequest(
        decimal? Gpa, decimal? Sat, decimal? Act, decimal? Ielts,
        decimal? BudgetUsd, string? ScholarshipNeed, int? EcTier, IReadOnlyList<int>? Pref,
        decimal? Toefl = null, decimal? Duolingo = null);

    public record SimulationResponse(
        SimulationProfile Profile, GroupSummary Baseline, GroupSummary Simulated, IReadOnlyList<SchoolChange> Changes,
        JsonElement Criteria, JsonElement Items);


    [Authorize(Policy = AgeFeatures.ProcessStudentData)] // [Q11] dưới 16 cần phụ huynh đồng ý
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

        // Đổi nút ưu tiên: chỉ chạy lại lượt chọn trên số đã lưu.
    [Authorize(Policy = AgeFeatures.ProcessStudentData)] // [Q11] dưới 16 cần phụ huynh đồng ý
    [HttpPost("repick")]
    public async Task<IActionResult> Repick([FromBody] RepickRequest? request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        if (request?.Pref is not { Count: 3 } pref || pref.Any(n => n is < 0 or > 4))
            return BadRequest(new { message = "Ưu tiên phải là 3 số từ 0 đến 4: chi phí, học bổng, chất lượng." });

        try
        {
            var saved = await recommendations.RepickAsync(userId.Value, pref, ct);
            return Ok(ToResponse(saved));
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Chưa"))
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

    // Trình mô phỏng hồ sơ: tính lại với GPA, SAT, ngân sách... giả định. Không lưu, không gọi advisor.
    [Authorize(Policy = AgeFeatures.ProcessStudentData)] // [Q11] dưới 16 cần phụ huynh đồng ý
    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate([FromBody] SimulateRequest? request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        request ??= new SimulateRequest(null, null, null, null, null, null, null, null);
        if (request.Pref is { } pref && (pref.Count != 3 || pref.Any(n => n is < 0 or > 4)))
            return BadRequest(new { message = "Ưu tiên phải là 3 số từ 0 đến 4: chi phí, học bổng, chất lượng." });

        try
        {
            var overrides = new SimulationOverrides(
                request.Gpa, request.Sat, request.Act, request.Ielts, request.BudgetUsd, request.ScholarshipNeed, request.EcTier,
                request.Toefl, request.Duolingo);
            var result = await recommendations.SimulateAsync(userId.Value, overrides, request.Pref, ct);
            using var criteria = JsonDocument.Parse(result.CriteriaJson);
            using var items = JsonDocument.Parse(result.ItemsJson);
            return Ok(new SimulationResponse(
                result.Profile, result.Baseline, result.Simulated, result.Changes,
                criteria.RootElement.Clone(), items.RootElement.Clone()));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Chưa") || ex.Message.StartsWith("Hồ sơ"))
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Danh sách trường cho ô chọn có gợi ý trong chat (giao diện lọc ngay trên máy khi học sinh gõ).
    [Authorize(Policy = AgeFeatures.ProcessStudentData)] // [Q11] dưới 16 cần phụ huynh đồng ý
    [HttpGet("schools/names")]
    public async Task<IActionResult> SchoolNames(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });
        return Ok(await recommendations.ListSchoolOptionsAsync(userId.Value, ct));
    }

    // "Cần gì để vào trường này": mức tối thiểu của SAT, GPA, ngoại khóa để trường đạt nhóm đích, kèm tài chính và trường thay thế.
    // Tính từ hồ sơ thật, không lưu, không gọi advisor.
    [Authorize(Policy = AgeFeatures.ProcessStudentData)] // [Q11] dưới 16 cần phụ huynh đồng ý
    [HttpPost("schools/{code}/goal-seek")]
    public async Task<IActionResult> GoalSeek(string code, [FromBody] GoalSeekRequest? request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            return Ok(await recommendations.GoalSeekAsync(userId.Value, code, request?.Target, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Chưa") || ex.Message.StartsWith("Hồ sơ"))
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Mở chi tiết một trường: Qwen viết lời tư vấn từ số đã lưu.
    [Authorize(Policy = AgeFeatures.ProcessStudentData)] // [Q11] dưới 16 cần phụ huynh đồng ý
    [HttpPost("schools/{code}/explain")]
    public async Task<IActionResult> Explain(string code, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            var explanation = await recommendations.ExplainAsync(userId.Value, code, ct);
            return explanation.Status == "OK"
                ? Ok(explanation)
                : Problem(detail: "Chưa viết được lời tư vấn, vui lòng thử lại.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Chưa"))
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "Kết quả tư vấn không hợp lệ");
        }
        catch (HttpRequestException)
        {
            return Problem(detail: "Dịch vụ gợi ý đang bận.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

}
