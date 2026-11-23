using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [USAS-362] [AC-4] API Quản trị tài khoản người dùng (RBAC).
/// Chỉ tài khoản có vai trò 'admin' truy cập (UC-60..63, quyết định D2 bản 14/11: Moderator không quản lý tài khoản).
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
        // [1.5c] Con managed không phải tài khoản thật: admin xem theo id cũng nhận 404. Chặn ở đây, không sửa GetMeAsync vì /me còn dùng khi phụ huynh làm thay
        return user is not null && user.Status != UserStatuses.Managed ? Ok(user) : NotFound();
    }

    /// <summary>[UC-63] Tạo tài khoản moderator (chỉ Admin mới được tạo).</summary>
    [HttpPost("moderators")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<UserDto>> CreateModerator(CreateModeratorAccountRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var result = await auth.CreateModeratorAccountAsync(request, actorId, ct);
        if (result.Error != AuthError.None || result.User is null)
            return ValidationProblem(result.Message);

        return CreatedAtAction(nameof(GetUserById), new { id = result.User.Id }, result.User);
    }


    /// <summary>[UC-61] Khóa tài khoản kèm lý do (ghi audit - Admin hoặc Moderator khóa tài khoản cấp dưới).</summary>
    [HttpPost("{id:guid}/lock")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<UserDto>> LockUser(Guid id, LockUserAccountRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var result = await auth.LockUserAccountAsync(id, request, actorId, ct);
        if (result.Error != AuthError.None || result.User is null)
            return ValidationProblem(result.Message);

        return Ok(result.User);
    }

    /// <summary>[UC-62] Mở khóa tài khoản kèm lý do (ghi audit - Admin hoặc Moderator mở khóa tài khoản cấp dưới).</summary>
    [HttpPost("{id:guid}/unlock")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<UserDto>> UnlockUser(Guid id, UnlockUserAccountRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var result = await auth.UnlockUserAccountAsync(id, request, actorId, ct);
        if (result.Error != AuthError.None || result.User is null)
            return ValidationProblem(result.Message);

        return Ok(result.User);
    }

    /// <summary>[UC-113] Sửa ngày sinh Học sinh / Phụ huynh theo yêu cầu hỗ trợ, bắt buộc lý do, ghi audit giá trị cũ và mới (chỉ Admin).</summary>
    [HttpPut("{id:guid}/birth-date")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<ActionResult<UserDto>> UpdateBirthDate(Guid id, UpdateUserBirthDateRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var result = await auth.UpdateUserBirthDateAsync(id, request, actorId, ct);
        if (result.Error != AuthError.None || result.User is null)
            return ValidationProblem(result.Message);

        return Ok(result.User);
    }
}
