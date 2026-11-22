using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.Sensitivity;
using StudyAbroad.Application.Recommendations.Sensitivity;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-85] Phân tích độ nhạy tham số của thuật toán gợi ý trường. Chỉ Admin (người khác nhận 403, AF-4).
/// Chạy trên bộ hồ sơ giả lập, không có dữ liệu học sinh thật, không gọi AI, không đổi tham số đang chạy.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/sensitivity")]
public class AdminSensitivityController(ISensitivityRunService sensitivity) : ControllerBase
{
    /// <summary>Danh mục 14 tham số được phép phân tích, kèm giá trị đang chạy (để giao diện hiện ô chọn).</summary>
    [HttpGet("parameters")]
    public ActionResult<IReadOnlyList<SensitivityParameterInfo>> GetParameters() => Ok(sensitivity.Parameters());

    /// <summary>
    /// Chạy phân tích và lưu kết quả. Thân yêu cầu: các tham số, mức nhích (0.1 và/hoặc 0.2), hai ngưỡng nhãn (bỏ trống là 2% và 10%).
    /// Yêu cầu sai (không chọn tham số, tham số lạ, mức nhích khác 10%/20%, ngưỡng sai thứ tự) trả 400 kèm lý do và không lưu gì.
    /// </summary>
    [HttpPost("runs")]
    public async Task<ActionResult<SensitivityRunDetail>> Run([FromBody] SensitivityRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var detail = await sensitivity.RunAsync(actorId, request, ct);
            return CreatedAtAction(nameof(Get), new { id = detail.Summary.Id }, detail);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Lịch sử các lần chạy, mới nhất trước (mặc định 50, tối đa 200). Không kèm kết quả chi tiết.</summary>
    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<SensitivityRunListRow>>> List(
        [FromQuery] int limit = SensitivityRunService.DefaultListLimit, CancellationToken ct = default) =>
        Ok(await sensitivity.ListAsync(limit, ct));

    /// <summary>Chi tiết một lần chạy: yêu cầu, giá trị mọi tham số lúc chạy, kết quả từng tham số. 404 nếu không có.</summary>
    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<SensitivityRunDetail>> Get(Guid id, CancellationToken ct)
    {
        var detail = await sensitivity.GetAsync(id, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>
    /// So lần chạy id với lần chạy "with" (bỏ trống là lần ngay trước nó). Kết quả luôn xếp "trước → sau".
    /// Chưa có lần chạy trước thì trả so sánh rỗng (hasEarlierRun = false), không phải lỗi. Khác điều kiện đo thì có cảnh báo.
    /// 404 nếu một trong hai lần chạy không có; 400 nếu so một lần chạy với chính nó.
    /// </summary>
    [HttpGet("runs/{id:guid}/compare")]
    public async Task<ActionResult<SensitivityComparison>> Compare(Guid id, [FromQuery(Name = "with")] Guid? withId, CancellationToken ct)
    {
        try
        {
            var comparison = await sensitivity.CompareAsync(id, withId, ct);
            return comparison is null ? NotFound() : Ok(comparison);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Tải CSV của một lần chạy (chỉ số tổng hợp). Mỗi lần tải được ghi nhật ký. 404 nếu không có.</summary>
    [HttpGet("runs/{id:guid}/csv")]
    public async Task<IActionResult> ExportCsv(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var file = await sensitivity.ExportCsvAsync(actorId, id, ct);
        return file is null ? NotFound() : File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }
}
