using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.Policies;
using StudyAbroad.Application.Policies;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.UnitTests;

// [UC-104] API: cần đăng nhập, ghi đồng ý chỉ cho học sinh/phụ huynh/trung tâm, mã trạng thái đúng với từng lỗi.
public class PolicyAcceptanceApiTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly PolicyAcceptanceStatusDto Pending = new(true, false, []);

    private sealed class StubAcceptance : IPolicyAcceptanceService
    {
        public Exception? Fail { get; set; }
        public Guid? LastUser { get; private set; }
        public string? LastRole { get; private set; }
        public string? LastIp { get; private set; }
        public int WriteCalls { get; private set; }

        public Task<PolicyAcceptanceStatusDto> GetStatusAsync(Guid userId, string role, CancellationToken ct = default)
        {
            LastUser = userId; LastRole = role;
            return Task.FromResult(Pending);
        }

        public Task<PolicyAcceptanceStatusDto> AcceptAsync(Guid userId, string role, AcceptPoliciesRequest request, string? ipAddress, CancellationToken ct = default)
        {
            WriteCalls++;
            LastUser = userId; LastRole = role; LastIp = ipAddress;
            return Fail is not null ? throw Fail : Task.FromResult(Pending);
        }
    }

    private static PolicyAcceptanceController For(StubAcceptance stub, Guid? userId, string role = UserRoles.Student)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");
        if (userId is { } id)
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role)], "test"));
        return new PolicyAcceptanceController(stub) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    private static readonly AcceptPoliciesRequest Body = new([new AcceptPolicyItem(PolicyKinds.TermsOfUse, "2026-11")]);

    [Fact]
    public void Controller_NeedsLogin_AtThePolicyAcceptancePath()
    {
        Assert.NotNull(typeof(PolicyAcceptanceController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(typeof(PolicyAcceptanceController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("api/v1/policy-acceptance", typeof(PolicyAcceptanceController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    [Fact]
    public void Writing_IsOnlyForStudentParentAndCenter_ButReadingIsForEveryLoggedInUser()
    {
        var accept = typeof(PolicyAcceptanceController).GetMethod(nameof(PolicyAcceptanceController.Accept))!;
        var roles = accept.GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');
        Assert.Equal([UserRoles.Student, UserRoles.Parent, UserRoles.Center], roles);   // admin và moderator nhận 403

        var status = typeof(PolicyAcceptanceController).GetMethod(nameof(PolicyAcceptanceController.GetStatus))!;
        Assert.Null(status.GetCustomAttribute<AuthorizeAttribute>());   // đọc: ai đăng nhập cũng gọi được, để giao diện luôn hỏi được
    }

    [Fact]
    public async Task WithoutALoggedInUser_BothEndpointsAreUnauthorized_AndNothingIsCalled()
    {
        var stub = new StubAcceptance();
        var c = For(stub, null);

        Assert.IsType<UnauthorizedResult>((await c.GetStatus(default)).Result);
        Assert.IsType<UnauthorizedResult>((await c.Accept(Body, default)).Result);
        Assert.Null(stub.LastUser);
    }

    [Fact]
    public async Task GetStatus_PassesTheCallerAndRoleOn_AndIsNeverCached()
    {
        var stub = new StubAcceptance();
        var c = For(stub, User, UserRoles.Parent);

        var result = await c.GetStatus(default);

        Assert.Same(Pending, Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(User, stub.LastUser);
        Assert.Equal(UserRoles.Parent, stub.LastRole);
        Assert.Equal("no-store", c.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Accept_PassesCallerRoleAndIpOn()
    {
        var stub = new StubAcceptance();

        var result = await For(stub, User, UserRoles.Center).Accept(Body, default);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(User, stub.LastUser);
        Assert.Equal(UserRoles.Center, stub.LastRole);
        Assert.Equal("203.0.113.7", stub.LastIp);
    }

    [Fact]
    public async Task Accept_BadData_Returns400()
    {
        var stub = new StubAcceptance { Fail = new ArgumentException("Chưa chọn tài liệu nào để đồng ý.") };

        var result = await For(stub, User).Accept(Body, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Accept_WhenTheVersionChanged_Returns409WithTheStableCode()
    {
        var stub = new StubAcceptance { Fail = new PolicyRuleException("policy_changed", "Nội dung vừa được cập nhật.") };

        var conflict = Assert.IsType<ConflictObjectResult>((await For(stub, User).Accept(Body, default)).Result);

        Assert.Contains("policy_changed", System.Text.Json.JsonSerializer.Serialize(conflict.Value));
    }
}
