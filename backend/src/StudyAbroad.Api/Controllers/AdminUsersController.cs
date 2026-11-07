using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [USAS-362] [AC-4] API Quản trị tài khoản người dùng (RBAC).
/// Cho phép các tài khoản có vai trò 'admin' hoặc 'moderator' truy cập phục vụ quản lý và kiểm duyệt tài khoản (UC-60..63).
/// Nếu học sinh, phụ huynh hoặc trung tâm gọi endpoint này, middleware sẽ chặn và trả về HTTP 403 Forbidden.
/// </summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Admin},{UserRoles.Moderator}")]
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

    /// <summary>[UC-63] Tạo tài khoản moderator (chỉ Admin mới được tạo).</summary>
    [HttpPost("moderators")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<UserDto>> CreateModerator(CreateModeratorAccountRequest request, CancellationToken ct)
    {
        var result = await auth.CreateModeratorAccountAsync(request, ct);
        if (result.Error != AuthError.None || result.User is null)
            return ValidationProblem(result.Message);

        return CreatedAtAction(nameof(GetUserById), new { id = result.User.Id }, result.User);
    }

    /// <summary>[Q11 / UC-71 Phụ trợ] Tạo tài khoản người dùng cho Trung tâm tư vấn (chỉ Moderator mới được tạo).</summary>
    [HttpPost("centers")]
    [Authorize(Roles = UserRoles.Moderator)]
    public async Task<ActionResult<UserDto>> CreateCenter(CreateCenterAccountRequest request, CancellationToken ct)
    {
        var result = await auth.CreateCenterAccountAsync(request, ct);
        if (result.Error != AuthError.None || result.User is null)
            return ValidationProblem(result.Message);

        return CreatedAtAction(nameof(GetUserById), new { id = result.User.Id }, result.User);
    }

    /// <summary>[UC-61] Khóa tài khoản kèm lý do (ghi audit - chỉ Admin mới được khóa).</summary>
    [HttpPost("{id:guid}/lock")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<UserDto>> LockUser(Guid id, LockUserAccountRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();

        var result = await auth.LockUserAccountAsync(id, request, adminId, ct);
        if (result.Error != AuthError.None || result.User is null)
            return ValidationProblem(result.Message);

        return Ok(result.User);
    }

    /// <summary>[UC-62] Mở khóa tài khoản kèm lý do (ghi audit - chỉ Admin mới được mở khóa).</summary>
    [HttpPost("{id:guid}/unlock")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<UserDto>> UnlockUser(Guid id, UnlockUserAccountRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } adminId)
            return Unauthorized();

        var result = await auth.UnlockUserAccountAsync(id, request, adminId, ct);
        if (result.Error != AuthError.None || result.User is null)
            return ValidationProblem(result.Message);

        return Ok(result.User);
    }
}
