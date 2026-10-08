using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Profile.Academic;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [USAS-365] API Controller phục vụ quản lý bảng điểm học sinh và phân tích năng lực học thuật:
/// - Nhập/sửa bảng điểm từng môn theo học kỳ
/// - Kích hoạt thuật toán quy đổi GPA 4.0 và phân nhóm môn
/// - Truy vấn kết quả phân tích học thuật và cấu hình thang điểm WES
/// </summary>
[ApiController]
[Route("api/v1/profile/academic")]
public class AcademicAnalysisController : ControllerBase
{
    private readonly IAcademicAnalysisService _service;

    public AcademicAnalysisController(IAcademicAnalysisService service)
    {
        _service = service;
    }

    /// <summary>
    /// [USAS-365] Lấy danh sách bảng điểm chi tiết của học sinh.
    /// </summary>
    [HttpGet("scores")]
    [ProducesResponseType(typeof(IReadOnlyList<TranscriptScoreDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScores(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _service.GetScoresAsync(userId, ct);
        return Ok(result);
    }

    /// <summary>
    /// [USAS-365] Lưu danh sách đầu điểm bảng điểm (hàng loạt).
    /// </summary>
    [HttpPost("scores")]
    [ProducesResponseType(typeof(IReadOnlyList<TranscriptScoreDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveScores([FromBody] BatchTranscriptScoresRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _service.SaveScoresAsync(userId, request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Dữ liệu bảng điểm không hợp lệ"
            );
        }
    }

    /// <summary>
    /// [USAS-365] Xóa một đầu điểm theo Id trong bảng điểm.
    /// </summary>
    [HttpDelete("scores/{scoreId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteScore(Guid scoreId, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _service.DeleteScoreAsync(userId, scoreId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Không tìm thấy hồ sơ"
            );
        }
    }

    /// <summary>
    /// [USAS-365] Kích hoạt thuật toán phân tích điểm học thuật:
    /// - Quy đổi GPA 4.0 chuẩn WES
    /// - Tính Unweighted & Weighted GPA
    /// - Gom nhóm STEM, Xã hội, Ngoại ngữ
    /// - Phân tích xu hướng 3 năm
    /// - Lưu vào analysis_results (kind = 'gpa') và cập nhật overall_gpa
    /// </summary>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(AcademicAnalysisResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AnalyzeAcademic(CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var analysis = await _service.AnalyzeAcademicProfileAsync(userId, ct);
            return Ok(analysis);
        }
        catch (Exception ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Lỗi trong quá trình phân tích học thuật"
            );
        }
    }

    /// <summary>
    /// [USAS-365] Lấy kết quả phân tích học thuật mới nhất của học sinh.
    /// </summary>
    [HttpGet("analysis")]
    [ProducesResponseType(typeof(AcademicAnalysisResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestAnalysis(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var analysis = await _service.GetLatestAnalysisAsync(userId, ct);
        if (analysis == null)
            return NotFound(new { message = "Chưa có kết quả phân tích học thuật nào cho hồ sơ này." });

        return Ok(analysis);
    }

    /// <summary>
    /// [USAS-365] Lấy cấu hình bảng quy đổi thang điểm hiện hành.
    /// </summary>
    [HttpGet("scale-config")]
    [ProducesResponseType(typeof(GradeScaleConfigDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScaleConfig(CancellationToken ct)
    {
        var config = await _service.GetGradeScaleConfigAsync(ct);
        return Ok(config);
    }

    /// <summary>
    /// [USAS-365] Helper lấy UserId từ Claims (hoặc header dev).
    /// Tuyệt đối không nhận userId từ body request để chống tấn công IDOR.
    /// </summary>
    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(claim, out var id))
            return id;

        if (Request.Headers.TryGetValue("X-User-Id", out var headerVal) &&
            Guid.TryParse(headerVal.FirstOrDefault(), out var headerId))
        {
            return headerId;
        }

        // Fallback tài khoản dev mặc định
        return Guid.Parse("00000000-0000-0000-0000-000000000001");
    }
}
