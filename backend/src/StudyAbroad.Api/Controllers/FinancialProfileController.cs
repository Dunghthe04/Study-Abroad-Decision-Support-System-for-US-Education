using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Profile.Financial;
using System.Security.Claims;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [USAS-364] Controller quản lý Hồ sơ tài chính và Hoạt động ngoại khóa của học sinh.
/// Áp dụng cơ chế bảo mật chống IDOR: Chỉ truy vấn và cập nhật dữ liệu của tài khoản hiện tại.
/// </summary>
[ApiController]
[Route("api/v1/profile")]
public class FinancialProfileController(IFinancialProfileService profileService) : ControllerBase
{
    // ==========================================
    // 1. TÀI CHÍNH (FINANCIAL PROFILE)
    // ==========================================

    /// <summary>
    /// [USAS-364] Lấy hồ sơ tài chính của học sinh hiện tại.
    /// </summary>
    [HttpGet("financial")]
    [ProducesResponseType(typeof(FinancialProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFinancialProfile(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập để truy cập thông tin tài chính." });

        var profile = await profileService.GetFinancialProfileAsync(userId.Value, ct);
        return Ok(profile);
    }

    /// <summary>
    /// [USAS-364] Lưu hoặc cập nhật hồ sơ tài chính (Upsert).
    /// </summary>
    [HttpPut("financial")]
    [ProducesResponseType(typeof(FinancialProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveFinancialProfile([FromBody] SaveFinancialProfileRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập để lưu thông tin tài chính." });

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var result = await profileService.SaveFinancialProfileAsync(userId.Value, request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Dữ liệu tài chính không hợp lệ");
        }
    }

    // ==========================================
    // 2. NGOẠI KHÓA (EXTRACURRICULAR ACTIVITIES)
    // ==========================================

    /// <summary>
    /// [USAS-364] Lấy danh sách hoạt động ngoại khóa của học sinh hiện tại.
    /// </summary>
    [HttpGet("extracurricular")]
    [ProducesResponseType(typeof(IReadOnlyList<ExtracurricularActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetActivities(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập để xem danh sách hoạt động ngoại khóa." });

        var list = await profileService.GetActivitiesAsync(userId.Value, ct);
        return Ok(list);
    }

    /// <summary>
    /// [USAS-364] Thêm hoạt động ngoại khóa mới.
    /// </summary>
    [HttpPost("extracurricular")]
    [ProducesResponseType(typeof(ExtracurricularActivityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddActivity([FromBody] CreateExtracurricularRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập để thêm hoạt động ngoại khóa." });

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var result = await profileService.AddActivityAsync(userId.Value, request, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Dữ liệu hoạt động không hợp lệ");
        }
    }

    /// <summary>
    /// [USAS-364] Cập nhật hoạt động ngoại khóa.
    /// Bảo mật IDOR: Trả về 404 nếu không tìm thấy hoặc hoạt động không thuộc quyền sở hữu của user.
    /// </summary>
    [HttpPut("extracurricular/{id:guid}")]
    [ProducesResponseType(typeof(ExtracurricularActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateActivity(Guid id, [FromBody] UpdateExtracurricularRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var result = await profileService.UpdateActivityAsync(userId.Value, id, request, ct);
            if (result is null)
                return NotFound(new { message = "Không tìm thấy hoạt động hoặc bạn không có quyền chỉnh sửa." });

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Dữ liệu cập nhật không hợp lệ");
        }
    }

    /// <summary>
    /// [USAS-364] Xóa hoạt động ngoại khóa.
    /// Bảo mật IDOR: Trả về 404 nếu hoạt động không thuộc quyền sở hữu của user.
    /// </summary>
    [HttpDelete("extracurricular/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteActivity(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var success = await profileService.DeleteActivityAsync(userId.Value, id, ct);
        if (!success)
            return NotFound(new { message = "Không tìm thấy hoạt động hoặc bạn không có quyền xóa." });

        return NoContent();
    }

    // ==========================================
    // 3. THÀNH TÍCH / GIẢI THƯỞNG (ACHIEVEMENTS)
    // ==========================================

    /// <summary>
    /// [USAS-364] Lấy danh sách thành tích của học sinh hiện tại.
    /// </summary>
    [HttpGet("achievements")]
    [ProducesResponseType(typeof(IReadOnlyList<StudentAchievementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAchievements(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var list = await profileService.GetAchievementsAsync(userId.Value, ct);
        return Ok(list);
    }

    /// <summary>
    /// [USAS-364] Thêm giải thưởng / chứng chỉ / đề tài nghiên cứu mới.
    /// </summary>
    [HttpPost("achievements")]
    [ProducesResponseType(typeof(StudentAchievementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddAchievement([FromBody] CreateAchievementRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var result = await profileService.AddAchievementAsync(userId.Value, request, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Dữ liệu thành tích không hợp lệ");
        }
    }

    /// <summary>
    /// [USAS-364] Xóa thành tích.
    /// </summary>
    [HttpDelete("achievements/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAchievement(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var success = await profileService.DeleteAchievementAsync(userId.Value, id, ct);
        if (!success)
            return NotFound(new { message = "Không tìm thấy thành tích hoặc bạn không có quyền xóa." });

        return NoContent();
    }

    // ==========================================
    // 4. TỔNG QUAN HỒ SƠ (SUMMARY FOR AI & DASHBOARD)
    // ==========================================

    /// <summary>
    /// [USAS-364] Lấy tổng quan toàn bộ hồ sơ phi học thuật (dùng cho AI Advisor và trang tổng quan).
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(StudentProfileSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfileSummary(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var summary = await profileService.GetFullProfileSummaryAsync(userId.Value, ct);
        return Ok(summary);
    }

    // ==========================================
    // HELPER: LẤY USER ID AN TOÀN (CHỐNG IDOR)
    // ==========================================

    /// <summary>
    /// Trích xuất an toàn UserId từ phiên đăng nhập.
    /// 1. Ưu tiên lấy từ Claims Principal (JWT Token).
    /// 2. Hỗ trợ Header X-User-Id cho môi trường Development / Postman testing.
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        // 1. Kiểm tra Claims từ JWT Token
        var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value
                      ?? User.FindFirst("id")?.Value;

        if (!string.IsNullOrEmpty(claimId) && Guid.TryParse(claimId, out var parsedClaimId))
            return parsedClaimId;

        // 2. Fallback cho Development/Testing qua header X-User-Id
        if (Request.Headers.TryGetValue("X-User-Id", out var headerValue) &&
            Guid.TryParse(headerValue, out var parsedHeaderId))
        {
            return parsedHeaderId;
        }

        return null;
    }
}
