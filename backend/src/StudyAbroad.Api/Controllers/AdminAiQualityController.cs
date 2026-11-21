using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.AiQuality;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Báo cáo chất lượng AI cho trang quản trị (UC-90): tỉ lệ trả lời được, không trả lời được, từ chối, lỗi, thời gian phản hồi,
/// và hội thoại mẫu của nhóm có vấn đề. Chỉ Admin, như trang token AI. Khoảng ngày tính cả hai đầu, theo ngày UTC, mặc định 30 ngày gần nhất.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/ai-quality")]
public class AdminAiQualityController(IAiQualityService quality, IAiQualitySampleService samples) : ControllerBase
{
    /// <summary>Báo cáo tổng hợp. Chỉ là số, không có nội dung chat.</summary>
    [HttpGet]
    public async Task<ActionResult<AiQualityReport>> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        try
        {
            return Ok(await quality.GetReportAsync(from, to, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Hội thoại mẫu của nhóm có vấn đề (outcome = no_answer, refused hoặc failed), mới nhất trước, phân trang (pageSize mặc định 10, tối đa 20).
    /// Đây là dữ liệu cá nhân của học sinh: nội dung đã che thông tin cá nhân, không có mã người dùng hay mã phiên, hội thoại của người dưới 16 tuổi
    /// bị ẩn (hiddenCount cho biết có bao nhiêu), và mỗi lần xem được ghi nhật ký trước khi trả nội dung (ghi lỗi thì không trả).
    /// Nhóm trả lời được không có ở đây (dùng duyệt mẫu). Loại hoặc khoảng ngày sai trả 400.
    /// </summary>
    [HttpGet("samples")]
    public async Task<ActionResult<AiQualitySamplePage>> GetSamples(
        [FromQuery] string? outcome, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var result = await samples.GetPageAsync(
                actorId, from, to, outcome ?? string.Empty, page, pageSize,
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
            Response.Headers.CacheControl = "no-store";   // nội dung chat không được lưu ở bộ nhớ đệm của trình duyệt hay proxy
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
