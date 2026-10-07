using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Domain.Constants;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [USAS-363] API Quản lý hồ sơ học thuật (Academic Profile & Transcripts).
/// Phân quyền: Học sinh (student) và Phụ huynh (parent).
/// Bảo mật chống IDOR: UserId được trích xuất an toàn từ Claims trong JWT Token (HttpOnly Cookie),
/// tuyệt đối không nhận userId từ route/body/query param của client.
/// </summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
[Route("api/v1/profile/academic")]
public class AcademicProfileController(IAcademicProfileService profileService) : ControllerBase
{
    /// <summary>
    /// Lấy hồ sơ học thuật và danh sách bảng điểm của người dùng hiện tại.
    /// Nếu hồ sơ chưa được tạo, hệ thống sẽ trả về dữ liệu mẫu rỗng để người dùng điền mới.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AcademicProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Không tìm thấy thông tin xác thực của người dùng." });
        }

        var result = await profileService.GetAcademicProfileAsync(userId.Value, ct);
        if (!result.Succeeded)
        {
            if (result.Error == AcademicProfileError.NotFound)
            {
                return NotFound(new { error = result.Message });
            }

            if (result.Error == AcademicProfileError.Forbidden)
            {
                return Forbid();
            }

            return BadRequest(new { error = result.Message });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Lưu hoặc cập nhật hồ sơ học thuật, bảng điểm từng học kỳ và chứng chỉ chuẩn hóa.
    /// Kiểm tra chặt chẽ thang điểm, giới hạn giá trị, trùng lặp môn học và tự động tính GPA chuẩn hóa.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(AcademicProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SaveMyProfile([FromBody] SaveAcademicProfileRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Không tìm thấy thông tin xác thực của người dùng." });
        }

        var result = await profileService.SaveAcademicProfileAsync(userId.Value, request, ct);
        if (!result.Succeeded)
        {
            if (result.Error == AcademicProfileError.NotFound)
            {
                return NotFound(new { error = result.Message });
            }

            if (result.Error == AcademicProfileError.Forbidden)
            {
                return Forbid();
            }

            return BadRequest(new { error = result.Message });
        }

        return Ok(result.Data);
    }
}
