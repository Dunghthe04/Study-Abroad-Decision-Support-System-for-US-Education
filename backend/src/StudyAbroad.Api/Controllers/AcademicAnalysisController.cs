using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Profile.Academic;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [USAS-365] API Controller phục vụ quản lý bảng điểm học sinh và phân tích năng lực học thuật:
/// - Nhập/sửa bảng điểm từng môn theo học kỳ
/// - Kích hoạt thuật toán quy đổi GPA 4.0 và phân nhóm môn
/// - Truy vấn kết quả phân tích học thuật và cấu hình thang điểm WES
/// Áp dụng cơ chế bảo mật chống IDOR: Chỉ truy vấn và cập nhật dữ liệu của tài khoản hiện tại qua JWT Claims.
/// </summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
[Authorize(Policy = AgeFeatures.ProcessStudentData)]
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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetScores(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var result = await _service.GetScoresAsync(userId.Value, ct);
        return Ok(result);
    }

    /// <summary>
    /// [USAS-365] Lưu danh sách đầu điểm bảng điểm (hàng loạt).
    /// </summary>
    [HttpPost("scores")]
    [ProducesResponseType(typeof(IReadOnlyList<TranscriptScoreDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveScores([FromBody] BatchTranscriptScoresRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            var result = await _service.SaveScoresAsync(userId.Value, request, ct);
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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteScore(Guid scoreId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            await _service.DeleteScoreAsync(userId.Value, scoreId, ct);
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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AnalyzeAcademic(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        try
        {
            var analysis = await _service.AnalyzeAcademicProfileAsync(userId.Value, ct);
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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetLatestAnalysis(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var analysis = await _service.GetLatestAnalysisAsync(userId.Value, ct);
        if (analysis == null)
            return NotFound(new { message = "Chưa có kết quả phân tích học thuật nào cho hồ sơ này." });

        return Ok(analysis);
    }

    /// <summary>
    /// [USAS-365] Lấy cấu hình bảng quy đổi thang điểm hiện hành.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("scale-config")]
    [ProducesResponseType(typeof(GradeScaleConfigDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScaleConfig(CancellationToken ct)
    {
        var config = await _service.GetGradeScaleConfigAsync(ct);
        return Ok(config);
    }

    /// <summary>
    /// [USAS-365] Helper lấy UserId từ Claims phiên đăng nhập an toàn.
    /// Tuyệt đối không nhận userId từ body request hoặc header X-User-Id để chống tấn công IDOR.
    /// </summary>
    private Guid? GetCurrentUserId() => User.GetUserId();
}
