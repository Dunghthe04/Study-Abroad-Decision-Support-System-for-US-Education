using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Auth;
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

        public Task<AuthResult> UnlockAccountAsync(UnlockAccountRequest request, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<bool> LogoutAsync(string rawToken, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<UserDto?> GetMeAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));

        public Task<List<UserDto>> GetAllUsersAsync(CancellationToken ct = default) =>
            Task.FromResult(Users);
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
    public async Task GetAllUsers_ReturnsOkResultWithListOfUsers()
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

        var result = await controller.GetAllUsers(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedUsers = Assert.IsType<List<UserDto>>(okResult.Value);
        Assert.Equal(2, returnedUsers.Count);
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
}
