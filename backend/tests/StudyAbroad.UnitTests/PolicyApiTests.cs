using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.Policies;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// [UC-103] API: quản lý chỉ Administrator, mã trạng thái đúng với từng lỗi, trang công khai chỉ thấy bản đang hiệu lực.
public class PolicyApiTests
{
    private static readonly Guid Admin = Guid.NewGuid();

    private static PolicyDocumentDto Doc(string status = PolicyStatuses.Draft, string? version = null) => new(
        Guid.NewGuid(), PolicyKinds.TermsOfUse, version, status, "Điều khoản", "Nội dung", "Tóm tắt", false, null, Admin,
        DateTime.UtcNow, status == PolicyStatuses.Draft ? null : DateTime.UtcNow, null, null);

    // Dịch vụ giả: mỗi thao tác trả hoặc ném thứ được đặt sẵn, và ghi lại người gọi.
    private sealed class StubPolicies : IPolicyService
    {
        public Exception? Fail { get; set; }
        public bool CreatedNew { get; set; } = true;
        public PolicyDocumentDto? Found { get; set; } = Doc();
        public PublicPolicyDto? Current { get; set; }
        public PolicyActor? LastActor { get; private set; }
        public string? LastKind { get; private set; }

        private T Run<T>(PolicyActor? actor, T value)
        {
            LastActor = actor;
            return Fail is not null ? throw Fail : value;
        }

        public Task<PolicyDraftResult> CreateDraftAsync(PolicyActor actor, string kind, CancellationToken ct = default)
        {
            LastKind = kind;
            return Task.FromResult(Run(actor, new PolicyDraftResult(Found!, CreatedNew)));
        }
        public Task<PolicyDocumentDto?> UpdateDraftAsync(PolicyActor actor, Guid id, UpdatePolicyDraftRequest request, CancellationToken ct = default) =>
            Task.FromResult(Run(actor, Found));
        public Task<PolicyDocumentDto?> PublishAsync(PolicyActor actor, Guid id, CancellationToken ct = default) => Task.FromResult(Run(actor, Found));
        public Task<bool> DiscardDraftAsync(PolicyActor actor, Guid id, CancellationToken ct = default) => Task.FromResult(Run(actor, Found is not null));
        public Task<PolicyDocumentDto?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Run(null, Found));
        public Task<PublicPolicyDto?> GetPublishedAsync(string kind, CancellationToken ct = default)
        {
            LastKind = kind;
            return Task.FromResult(Run(null, Current));
        }
        public Task<IReadOnlyList<PolicyDocumentSummaryDto>> ListAsync(string? kind, CancellationToken ct = default)
        {
            LastKind = kind;
            return Task.FromResult(Run<IReadOnlyList<PolicyDocumentSummaryDto>>(null, []));
        }
    }

    private static AdminPoliciesController AdminFor(StubPolicies stub, Guid? userId)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.9");
        http.Request.Headers.UserAgent = "unit-test";
        if (userId is { } id) http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"));
        return new AdminPoliciesController(stub) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    private static readonly UpdatePolicyDraftRequest Edit = new("Tiêu đề", "Nội dung", "Tóm tắt", true);

    // ------------------------------------------------------------ quyền và đường dẫn

    [Fact]
    public void AdminController_IsAdminOnly_AtThePoliciesPath()
    {
        Assert.Equal(UserRoles.Admin, typeof(AdminPoliciesController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);   // Moderator không vào
        Assert.Equal("api/v1/admin/policies", typeof(AdminPoliciesController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    [Fact]
    public void PublicController_IsOpenToEveryone_ButOnlyReadsOneVersion()
    {
        Assert.NotNull(typeof(PoliciesController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("api/v1/policies", typeof(PoliciesController).GetCustomAttribute<RouteAttribute>()!.Template);
        var methods = typeof(PoliciesController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var only = Assert.Single(methods);
        Assert.NotNull(only.GetCustomAttribute<HttpGetAttribute>());       // không có thao tác ghi nào ở trang công khai
    }

    [Fact]
    public async Task EveryWriteEndpoint_WithoutALoggedInUser_IsUnauthorized_AndNothingIsCalled()
    {
        var stub = new StubPolicies();
        var c = AdminFor(stub, null);

        Assert.IsType<UnauthorizedResult>((await c.CreateDraft(new("terms_of_use"), default)).Result);
        Assert.IsType<UnauthorizedResult>((await c.UpdateDraft(Guid.NewGuid(), Edit, default)).Result);
        Assert.IsType<UnauthorizedResult>((await c.Publish(Guid.NewGuid(), default)).Result);
        Assert.IsType<UnauthorizedResult>(await c.DiscardDraft(Guid.NewGuid(), default));
        Assert.Null(stub.LastActor);
        Assert.Null(stub.LastKind);
    }

    // ------------------------------------------------------------ tạo nháp

    [Fact]
    public async Task CreateDraft_Returns201_WhenNew_AndPassesTheAdminIpAndAgentOn()
    {
        var stub = new StubPolicies();
        var result = await AdminFor(stub, Admin).CreateDraft(new("terms_of_use"), default);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.IsType<PolicyDraftResult>(created.Value);
        Assert.Equal(Admin, stub.LastActor!.UserId);
        Assert.Equal("10.0.0.9", stub.LastActor.IpAddress);
        Assert.Equal("unit-test", stub.LastActor.UserAgent);
        Assert.Equal("terms_of_use", stub.LastKind);
    }

    [Fact]
    public async Task CreateDraft_Returns200_WhenADraftAlreadyExists()
    {
        var result = await AdminFor(new StubPolicies { CreatedNew = false }, Admin).CreateDraft(new("terms_of_use"), default);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.False(Assert.IsType<PolicyDraftResult>(ok.Value).Created);
    }

    [Fact]
    public async Task CreateDraft_WithABadKind_Is400()
    {
        var result = await AdminFor(new StubPolicies { Fail = new ArgumentException("Loại không hợp lệ") }, Admin).CreateDraft(new("x"), default);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ------------------------------------------------------------ sửa, công bố, bỏ

    [Fact]
    public async Task Update_Ok_NotFound_BadRequest_AndLockedIs409WithItsCode()
    {
        Assert.IsType<OkObjectResult>((await AdminFor(new StubPolicies(), Admin).UpdateDraft(Guid.NewGuid(), Edit, default)).Result);
        Assert.IsType<NotFoundResult>((await AdminFor(new StubPolicies { Found = null }, Admin).UpdateDraft(Guid.NewGuid(), Edit, default)).Result);
        Assert.IsType<BadRequestObjectResult>((await AdminFor(new StubPolicies { Fail = new ArgumentException("Tiêu đề trống") }, Admin)
            .UpdateDraft(Guid.NewGuid(), Edit, default)).Result);

        var locked = await AdminFor(new StubPolicies { Fail = new PolicyRuleException("policy_locked", "Bản đã công bố không sửa được.") }, Admin)
            .UpdateDraft(Guid.NewGuid(), Edit, default);
        var conflict = Assert.IsType<ConflictObjectResult>(locked.Result);
        Assert.Contains("policy_locked", System.Text.Json.JsonSerializer.Serialize(conflict.Value));
    }

    [Fact]
    public async Task Publish_Ok_NotFound_BadRequestForMissingParts_AndConflictForLockedOrRace()
    {
        var published = Doc(PolicyStatuses.Published, "2026-11");
        var ok = await AdminFor(new StubPolicies { Found = published }, Admin).Publish(published.Id, default);
        Assert.Equal("2026-11", Assert.IsType<PolicyDocumentDto>(Assert.IsType<OkObjectResult>(ok.Result).Value).Version);

        Assert.IsType<NotFoundResult>((await AdminFor(new StubPolicies { Found = null }, Admin).Publish(Guid.NewGuid(), default)).Result);
        Assert.IsType<BadRequestObjectResult>((await AdminFor(new StubPolicies { Fail = new ArgumentException("Cần tóm tắt thay đổi") }, Admin)
            .Publish(Guid.NewGuid(), default)).Result);
        foreach (var code in new[] { "policy_locked", "policy_conflict" })
            Assert.IsType<ConflictObjectResult>((await AdminFor(new StubPolicies { Fail = new PolicyRuleException(code, "x") }, Admin)
                .Publish(Guid.NewGuid(), default)).Result);
    }

    [Fact]
    public async Task Discard_Is204_404_OrConflictWhenPublished()
    {
        Assert.IsType<NoContentResult>(await AdminFor(new StubPolicies(), Admin).DiscardDraft(Guid.NewGuid(), default));
        Assert.IsType<NotFoundResult>(await AdminFor(new StubPolicies { Found = null }, Admin).DiscardDraft(Guid.NewGuid(), default));
        Assert.IsType<ConflictObjectResult>(await AdminFor(new StubPolicies { Fail = new PolicyRuleException("policy_locked", "x") }, Admin)
            .DiscardDraft(Guid.NewGuid(), default));
    }

    // ------------------------------------------------------------ đọc

    [Fact]
    public async Task ListAndGet_AreForAdmins_AndFilterByKind()
    {
        var stub = new StubPolicies();
        var list = await AdminFor(stub, Admin).List("privacy_policy", default);
        Assert.IsType<OkObjectResult>(list.Result);
        Assert.Equal("privacy_policy", stub.LastKind);

        Assert.IsType<BadRequestObjectResult>((await AdminFor(new StubPolicies { Fail = new ArgumentException("sai loại") }, Admin).List("x", default)).Result);
        Assert.IsType<OkObjectResult>((await AdminFor(stub, Admin).Get(Guid.NewGuid(), default)).Result);
        Assert.IsType<NotFoundResult>((await AdminFor(new StubPolicies { Found = null }, Admin).Get(Guid.NewGuid(), default)).Result);
    }

    // ------------------------------------------------------------ trang công khai

    [Fact]
    public async Task Public_ReturnsTheCurrentVersion_WithoutAuthorOrInternalIds_AndIsNotCached()
    {
        var current = new PublicPolicyDto("terms_of_use", "2026-11", "Điều khoản", "Toàn văn", "Thêm mục mới", true, DateTime.UtcNow);
        var http = new DefaultHttpContext();
        var controller = new PoliciesController(new StubPolicies { Current = current }) { ControllerContext = new ControllerContext { HttpContext = http } };

        var result = await controller.GetCurrent("terms_of_use", default);

        Assert.Same(current, Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("no-cache", http.Response.Headers.CacheControl.ToString());
        var names = typeof(PublicPolicyDto).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("Id", names);
        Assert.DoesNotContain("CreatedByUserId", names);
        Assert.DoesNotContain("PublishedByUserId", names);
        Assert.DoesNotContain("Status", names);     // chỉ bản đang hiệu lực mới ra đây
    }

    [Fact]
    public async Task Public_WhenNothingIsPublished_Is404()
    {
        var controller = new PoliciesController(new StubPolicies { Current = null }) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<NotFoundResult>((await controller.GetCurrent("forum_rules", default)).Result);
    }
}
