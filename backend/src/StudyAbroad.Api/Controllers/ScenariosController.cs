using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>UC-86: lưu và mở lại kịch bản what-if. Học sinh lưu, đổi tên, xóa; phụ huynh có liên kết chỉ được xem.
/// Không có quyền hoặc không tìm thấy đều trả 404, để người khác không dò được id kịch bản.</summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
[Route("api/v1/scenarios")]
public class ScenariosController(ScenarioService scenarios) : ControllerBase
{
    // Cùng các trường với SimulateRequest bên RecommendationsController, thêm tên kịch bản
    public record SaveScenarioRequest(
        string? Title, decimal? Gpa, decimal? Sat, decimal? Act, decimal? Ielts, decimal? BudgetUsd,
        string? ScholarshipNeed, int? EcTier, IReadOnlyList<int>? Pref, decimal? Toefl = null, decimal? Duolingo = null);

    public record RenameScenarioRequest(string? Title);

    private const string NotFoundMessage = "Không tìm thấy kịch bản.";

    // Lưu kịch bản đang xem. Chỉ học sinh, và cần đủ điều kiện xử lý dữ liệu như khi chạy giả định.
    [Authorize(Roles = UserRoles.Student)]
    [Authorize(Policy = AgeFeatures.ProcessStudentData)]
    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveScenarioRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        if (request.Pref is { } pref && (pref.Count != 3 || pref.Any(n => n is < 0 or > 4)))
            return BadRequest(new { message = "Ưu tiên phải là 3 số từ 0 đến 4: chi phí, học bổng, chất lượng." });

        var overrides = new SimulationOverrides(
            request.Gpa, request.Sat, request.Act, request.Ielts, request.BudgetUsd,
            request.ScholarshipNeed, request.EcTier, request.Toefl, request.Duolingo);
        try
        {
            var saved = await scenarios.SaveAsync(userId.Value, request.Title, overrides, request.Pref, ct);
            return CreatedAtAction(nameof(Get), new { id = saved.Id }, saved);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Chưa") || ex.Message.StartsWith("Hồ sơ"))
        {
            // Ví dụ chưa có hồ sơ nên không tính được bản chụp
            return BadRequest(new { message = ex.Message });
        }
    }

    // Bỏ trống studentId: danh sách của mình. Phụ huynh truyền studentId của con đã liên kết.
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? studentId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var list = await scenarios.ListAsync(userId.Value, studentId, ct);
        return list is null ? NotFound(new { message = "Không tìm thấy học sinh." }) : Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var scenario = await scenarios.GetAsync(userId.Value, id, ct);
        return scenario is null ? NotFound(new { message = NotFoundMessage }) : Ok(scenario);
    }

    [Authorize(Roles = UserRoles.Student)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameScenarioRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            var renamed = await scenarios.RenameAsync(userId.Value, id, request.Title, ct);
            return renamed is null ? NotFound(new { message = NotFoundMessage }) : Ok(renamed);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = UserRoles.Student)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        return await scenarios.DeleteAsync(userId.Value, id, ct)
            ? NoContent()
            : NotFound(new { message = NotFoundMessage });
    }
}
