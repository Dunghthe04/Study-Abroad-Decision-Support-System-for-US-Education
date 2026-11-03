using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-42] API Gửi yêu cầu tư vấn & Chia sẻ hồ sơ cho Trung tâm du học (Lead Request).
/// Tuân thủ chính sách bảo vệ dữ liệu trẻ em Q11:
/// - Học sinh/phụ huynh có thể gửi yêu cầu tư vấn.
/// - Nếu chọn chia sẻ hồ sơ (SharedProfile = true), người dùng dưới 18 tuổi bị chặn bởi policy AgeFeatures.ShareWithCenter.
/// </summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
[Route("api/v1/leads")]
public class LeadsController(
    ILeadService leadService,
    IAuthorizationService authService) : ControllerBase
{
    /// <summary>
    /// [UC-42] Gửi yêu cầu tư vấn tới trung tâm du học.
    /// Nếu SharedProfile = true: Bắt buộc thỏa mãn policy AgeFeatures.ShareWithCenter (dưới 18 tuổi bị từ chối).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateLead([FromBody] CreateLeadRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        // [Q11] Kiểm tra độ tuổi nếu học sinh muốn chia sẻ hồ sơ cho trung tâm
        if (request.SharedProfile)
        {
            var authResult = await authService.AuthorizeAsync(User, AgeFeatures.ShareWithCenter);
            if (!authResult.Succeeded)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    code = "age_restricted",
                    feature = AgeFeatures.ShareWithCenter,
                    message = "Theo Luật Trẻ em 2016 và chính sách bảo vệ dữ liệu, người dùng dưới 18 tuổi chưa được phép chia sẻ hồ sơ cho trung tâm."
                });
            }
        }

        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await leadService.CreateLeadAsync(userId.Value, request, ip, ct);
            return CreatedAtAction(nameof(GetLeadById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Không tìm thấy", Detail = ex.Message, Status = StatusCodes.Status404NotFound });
        }
    }

    /// <summary>
    /// [UC-42 Phụ trợ] Lấy chi tiết yêu cầu vừa tạo.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLeadById(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });

        var lead = await leadService.GetLeadByIdAsync(userId.Value, id, ct);
        return lead is null ? NotFound() : Ok(lead);
    }
}

