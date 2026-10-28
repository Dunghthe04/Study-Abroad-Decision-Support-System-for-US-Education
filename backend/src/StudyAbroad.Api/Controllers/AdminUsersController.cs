using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Auth;
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
    /// <summary>
    /// [AC-4] Lấy danh sách tất cả tài khoản trong hệ thống (chỉ dành cho Admin).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAllUsers(CancellationToken ct)
    {
        var users = await auth.GetAllUsersAsync(ct);
        return Ok(users);
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
}
