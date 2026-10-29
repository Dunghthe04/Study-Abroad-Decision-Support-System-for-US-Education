using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

public class AdminUsersControllerTests
{
    private sealed class FakeAuthService : IAuthService
    {
        public List<UserDto> Users { get; set; } = [];

        public Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> ChangePasswordAsync(Guid userId, string currentRawToken, ChangePasswordRequest request, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<AuthResult> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<bool> LogoutAsync(string rawToken, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<UserDto?> GetMeAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));

        public Task<List<UserDto>> GetAllUsersAsync(CancellationToken ct = default) =>
            Task.FromResult(Users);

        public AdminUserAccountListQuery? LastAccountListQuery { get; private set; }

        public Task<PagedResult<UserDto>> GetAdminUserAccountListAsync(AdminUserAccountListQuery query, CancellationToken ct = default)
        {
            LastAccountListQuery = query;
            IEnumerable<UserDto> q = Users;
            if (!string.IsNullOrEmpty(query.Search))
            {
                var s = query.Search;
                q = q.Where(u =>
                    u.Email.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    u.FullName.Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(query.Role))
                q = q.Where(u => u.Role == query.Role);
            if (!string.IsNullOrEmpty(query.Status))
                q = q.Where(u => u.Status == query.Status);

            var list = q.ToList();
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedResult<UserDto>(items, page, pageSize, list.Count));
        }

        public CreateModeratorAccountRequest? LastCreateModeratorRequest { get; private set; }

        public Task<AuthResult> CreateModeratorAccountAsync(CreateModeratorAccountRequest request, CancellationToken ct = default)
        {
            LastCreateModeratorRequest = request;
            var user = new UserDto(Guid.NewGuid(), request.Email, request.FullName, UserRoles.Moderator, UserStatuses.Active, request.Phone);
            Users.Add(user);
            return Task.FromResult(AuthResult.Ok(user, "Đã tạo tài khoản moderator."));
        }

        public (Guid TargetId, LockUserAccountRequest Request, Guid ActorId)? LastLockCall { get; private set; }

        public Task<AuthResult> LockUserAccountAsync(
            Guid targetUserId,
            LockUserAccountRequest request,
            Guid actorAdminId,
            CancellationToken ct = default)
        {
            LastLockCall = (targetUserId, request, actorAdminId);
            var user = Users.FirstOrDefault(u => u.Id == targetUserId);
            if (user is null)
                return Task.FromResult(AuthResult.Fail(AuthError.Validation, "Không tìm thấy tài khoản."));

            // Fake in-memory: giả lập khóa (UPDATE status), KHÔNG xóa tài khoản — production dùng UpdateUserAsync.
            // UserDto là record bất biến: không sửa user.Status trực tiếp; `with` tạo object mới cùng Id.
            // Remove + Add: thay phần tử cũ trong List bằng bản mới (đổi Status), không phải xóa user rồi tạo user khác.
            var updated = user with { Status = UserStatuses.Locked };
            Users.Remove(user);
            Users.Add(updated);
            return Task.FromResult(AuthResult.Ok(updated, "Đã khóa tài khoản."));
        }
    }

    [Fact]
    public void AdminUsersController_HasAdminAuthorizeAttribute()
    {
        // [AC-4] Đảm bảo controller được bảo vệ bởi [Authorize(Roles = "admin")]
        var authAttr = typeof(AdminUsersController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authAttr);
        Assert.Equal(UserRoles.Admin, authAttr.Roles);
    }

    [Fact]
    public async Task GetAdminUserAccountList_ReturnsPagedUsers()
    {
        var fakeAuth = new FakeAuthService
        {
            Users =
            [
                new UserDto(Guid.NewGuid(), "admin@test.com", "Admin User", UserRoles.Admin, UserStatuses.Active, null),
                new UserDto(Guid.NewGuid(), "student@test.com", "Student User", UserRoles.Student, UserStatuses.Active, "0912345678")
            ]
        };
        var controller = new AdminUsersController(fakeAuth);

        var result = await controller.GetAdminUserAccountList(new AdminUserAccountListQuery(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<PagedResult<UserDto>>(okResult.Value);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task GetUserById_WhenExists_ReturnsOkResult()
    {
        var userId = Guid.NewGuid();
        var fakeAuth = new FakeAuthService
        {
            Users =
            [
                new UserDto(userId, "student@test.com", "Student User", UserRoles.Student, UserStatuses.Active, "0912345678")
            ]
        };
        var controller = new AdminUsersController(fakeAuth);

        var result = await controller.GetUserById(userId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var user = Assert.IsType<UserDto>(okResult.Value);
        Assert.Equal(userId, user.Id);
        Assert.Equal("0912345678", user.Phone);
    }

    [Fact]
    public async Task GetUserById_WhenNotFound_ReturnsNotFound()
    {
        var fakeAuth = new FakeAuthService();
        var controller = new AdminUsersController(fakeAuth);

        var result = await controller.GetUserById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateModerator_WhenValid_ReturnsCreatedAtRoute()
    {
        var fakeAuth = new FakeAuthService();
        var controller = new AdminUsersController(fakeAuth);
        var request = new CreateModeratorAccountRequest("mod@test.com", "Password1!", "Moderator User", "0912345678");

        var result = await controller.CreateModerator(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var user = Assert.IsType<UserDto>(created.Value);
        Assert.Equal(UserRoles.Moderator, user.Role);
        Assert.Equal("mod@test.com", user.Email);
    }

    [Fact]
    public async Task LockUser_WhenValid_ReturnsOk()
    {
        var adminId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var fakeAuth = new FakeAuthService
        {
            Users =
            [
                new UserDto(studentId, "student@test.com", "Student", UserRoles.Student, UserStatuses.Active, null),
            ],
        };
        var controller = new AdminUsersController(fakeAuth)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
                    ], authenticationType: "test")),
                },
            },
        };

        var result = await controller.LockUser(studentId, new LockUserAccountRequest("Lý do khóa"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var user = Assert.IsType<UserDto>(ok.Value);
        Assert.Equal(UserStatuses.Locked, user.Status);
        Assert.NotNull(fakeAuth.LastLockCall);
        Assert.Equal(adminId, fakeAuth.LastLockCall.Value.ActorId);
    }
}
