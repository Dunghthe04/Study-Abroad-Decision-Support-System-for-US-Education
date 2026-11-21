using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Admin.AiQuality;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Báo cáo chất lượng AI cho trang quản trị (UC-90): tỉ lệ trả lời được, không trả lời được, từ chối, lỗi, thời gian phản hồi.
/// Chỉ Admin, như trang token AI. Không có nội dung chat. Khoảng ngày tính cả hai đầu, theo ngày UTC, mặc định 30 ngày gần nhất.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/ai-quality")]
public class AdminAiQualityController(IAiQualityService quality) : ControllerBase
{
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
}
