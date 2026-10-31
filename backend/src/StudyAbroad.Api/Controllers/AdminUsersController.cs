using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [USAS-362] [AC-4] API Quản trị tài khoản người dùng (RBAC).
/// Chỉ tài khoản có vai trò 'admin' (UserRoles.Admin) mới có quyền truy cập.
/// Nếu học sinh, phụ huynh hoặc trung tâm gọi endpoint này, middleware sẽ chặn và trả về HTTP 403 Forbidden.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/users")]
public class AdminUsersController(IAuthService auth) : ControllerBase
{
    /// <summary>[UC-60] Xem danh sách tài khoản — tìm, lọc, phân trang (chỉ Admin).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserDto>>> GetAdminUserAccountList(
        [FromQuery] AdminUserAccountListQuery query,
        CancellationToken ct)
    {
        var result = await auth.GetAdminUserAccountListAsync(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [AC-4] Lấy chi tiết thông tin một người dùng theo ID (chỉ dành cho Admin).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> GetUserById(Guid id, CancellationToken ct)
    {
        var user = await auth.GetMeAsync(id, ct);
        return user is not null ? Ok(user) : NotFound();
    }

    /// <summary>[UC-63] Tạo tài khoản moderator (khách không tự đăng ký được role này).</summary>
    [HttpPost("moderators")]
    public async Task<ActionResult<UserDto>> CreateModerator(CreateModeratorAccountRequest request, CancellationToken ct)
    {
        var result = await auth.CreateModeratorAccountAsync(request, ct);
        if (result.Error != AuthError.None || result.User is null)
            return ValidationProblem(result.Message);

        return CreatedAtAction(nameof(GetUserById), new { id = result.User.Id }, result.User);
    }

    /// <summary>[UC-61] Khóa tài khoản kèm lý do (ghi audit).</summary>
    [HttpPost("{id:guid}/lock")]
    public async Task<ActionResult<UserDto>> LockUser(Guid id, LockUserAccountRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();

        var result = await auth.LockUserAccountAsync(id, request, adminId, ct);
        if (result.Error != AuthError.None || result.User is null)
            return ValidationProblem(result.Message);

        return Ok(result.User);
    }
}
