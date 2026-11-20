using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StudyAbroad.Api.Authorization;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.UnitTests;

// Cổng Premium cho gợi ý trường (billing-plan-v2: G6, G7, G9, G12): policy, handler và mã 403 premium_required.
public class PremiumAuthorizationTests
{
    private static readonly Guid Me = Guid.NewGuid();

    // IsPremiumAsync là hàm duy nhất handler gọi. DispatchProxy thay vì lớp giả đầy đủ để thêm hàm vào IEntitlementService không làm vỡ file này
    public class EntitlementStub : DispatchProxy
    {
        public Func<string, bool> IsPremium { get; set; } = _ => false;
        public List<(Guid UserId, string Role)> Asked { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IEntitlementService.IsPremiumAsync))
                throw new NotSupportedException(targetMethod?.Name);
            var (userId, role) = ((Guid)args![0]!, (string)args[1]!);
            Asked.Add((userId, role));
            return Task.FromResult(IsPremium(role));
        }
    }

    private static (PremiumFeatureHandler Handler, EntitlementStub Stub) HandlerFor(Func<string, bool> isPremium)
    {
        var proxy = DispatchProxy.Create<IEntitlementService, EntitlementStub>();
        var stub = (EntitlementStub)(object)proxy;
        stub.IsPremium = isPremium;
        return (new PremiumFeatureHandler(proxy), stub);
    }

    private static AuthorizationHandlerContext Context(string role, Guid? userId)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (userId is { } id)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, id.ToString()));
        return new AuthorizationHandlerContext(
            [new PremiumFeatureRequirement("recommendations")], new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), null);
    }

    [Fact]
    public async Task G6_Free_FailsWithThePremiumRequiredReason()
    {
        var (handler, _) = HandlerFor(_ => false);
        var context = Context(UserRoles.Student, Me);

        await handler.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.False(context.HasSucceeded);
        var reason = Assert.IsType<PremiumRequiredFailureReason>(Assert.Single(context.FailureReasons));
        Assert.Equal(("recommendations", "Tính năng này cần tài khoản USDSS Premium."), (reason.Feature, reason.Message));
    }

    [Fact]
    public async Task Premium_Succeeds_AndTheServiceIsAskedAboutTheTokenUserAndRole()
    {
        var (handler, stub) = HandlerFor(_ => true);
        var context = Context(UserRoles.Student, Me);   // không làm thay: id và vai trò trong token

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
        Assert.Equal([(Me, UserRoles.Student)], stub.Asked);
    }

    // Claim act (RFC 8693) như token phiên nhập hộ: {"sub":"<id phụ huynh>"}
    private static AuthorizationHandlerContext ActingContext(Guid childId, Guid parentId) =>
        new([new PremiumFeatureRequirement("recommendations")], new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, childId.ToString()),
            new Claim(ClaimTypes.Role, UserRoles.Student),
            new Claim("act", $$"""{"sub":"{{parentId}}"}"""),
        ], "test")), null);

    [Fact]
    public async Task ActingParentWithPremium_Succeeds_EvenThoughTheManagedChildIsFree()
    {
        var parent = Guid.NewGuid();
        // Chỉ phụ huynh có Premium: hỏi theo id con hay vai trò student thì Free
        var (handler, stub) = HandlerFor(role => role == UserRoles.Parent);
        var context = ActingContext(Me, parent);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.Equal([(parent, UserRoles.Parent)], stub.Asked);
    }

    [Fact]
    public async Task ActingParentWithoutPremium_Fails_AndTheParentIsTheOneAsked()
    {
        var parent = Guid.NewGuid();
        var (handler, stub) = HandlerFor(_ => false);
        var context = ActingContext(Me, parent);

        await handler.HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.IsType<PremiumRequiredFailureReason>(Assert.Single(context.FailureReasons));
        Assert.Equal([(parent, UserRoles.Parent)], stub.Asked);
    }

    [Fact]
    public async Task NotActing_StillAsksAboutTheTokenUserAndRole()
    {
        var (handler, stub) = HandlerFor(_ => true);
        var context = Context(UserRoles.Student, Me);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.Equal([(Me, UserRoles.Student)], stub.Asked);
    }

    [Theory]
    [InlineData(UserRoles.Admin)]
    [InlineData(UserRoles.Moderator)]
    [InlineData(UserRoles.Center)]
    public async Task G12_Staff_Succeeds_BecauseTheServiceTreatsThemAsPremium(string role)
    {
        var (handler, stub) = HandlerFor(r => r != UserRoles.Student && r != UserRoles.Parent);   // như EntitlementService.IsPremiumAsync cho nhân viên
        var context = Context(role, Me);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.Equal([(Me, role)], stub.Asked);
    }

    [Fact]
    public async Task NoUserId_DoesNothing_SoRequireAuthenticatedUserDecides()
    {
        var (handler, stub) = HandlerFor(_ => false);
        var context = Context(UserRoles.Student, null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed);
        Assert.Empty(stub.Asked);
    }

    // ---- 403 premium_required và thứ tự với chặn theo tuổi

    private static async Task<(int Status, JsonElement Body)> WriteAsync(params AuthorizationFailureReason[] reasons)
    {
        var http = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        http.Response.Body = new MemoryStream();
        var nextCalled = false;

        await new AgeRestrictionResultHandler().HandleAsync(_ => { nextCalled = true; return Task.CompletedTask; }, http,
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(),
            PolicyAuthorizationResult.Forbid(AuthorizationFailure.Failed(reasons)));

        Assert.False(nextCalled);
        http.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(http.Response.Body);
        return (http.Response.StatusCode, json.RootElement.Clone());
    }

    private static PremiumRequiredFailureReason PremiumReason() => new(HandlerFor(_ => false).Handler, "recommendations");

    private static AgeRestrictedFailureReason AgeReason() => new(
        PremiumReason().Handler, new AgeBlock(AgeFeatures.ProcessStudentData, NeedParentConsent: true));

    [Fact]
    public async Task G6_ResultHandler_WritesA403WithPremiumRequiredAndTheFeature()
    {
        var (status, body) = await WriteAsync(PremiumReason());

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal("premium_required", body.GetProperty("code").GetString());
        Assert.Equal("recommendations", body.GetProperty("feature").GetString());
        Assert.Equal("Tính năng này cần tài khoản USDSS Premium.", body.GetProperty("detail").GetString());
        Assert.Equal(403, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task G10_ResultHandler_AgeRestrictionWinsOverPremium_InEitherOrder()
    {
        var (_, premiumFirst) = await WriteAsync(PremiumReason(), AgeReason());
        var (_, ageFirst) = await WriteAsync(AgeReason(), PremiumReason());

        foreach (var body in new[] { premiumFirst, ageFirst })
        {
            Assert.Equal(AgeRestrictionResultHandler.ErrorCode, body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("needParentConsent").GetBoolean());
        }
    }

    // ---- Policy nằm đúng các route

    private static IEnumerable<string?> Policies(Type controller, string action) =>
        controller.GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy);

    [Theory]
    [InlineData(typeof(RecommendationsController), nameof(RecommendationsController.Create))]
    [InlineData(typeof(RecommendationsController), nameof(RecommendationsController.Repick))]
    [InlineData(typeof(RecommendationsController), nameof(RecommendationsController.Simulate))]
    [InlineData(typeof(ScenariosController), nameof(ScenariosController.Save))]
    public void G6_RecommendationWriteRoutes_RequirePremium_AndKeepTheAgePolicy(Type controller, string action)
    {
        var policies = Policies(controller, action).ToList();

        Assert.Contains(PremiumPolicies.Recommendations, policies);
        Assert.Contains(AgeFeatures.ProcessStudentData, policies);
    }

    [Theory]
    [InlineData(typeof(RecommendationsController), nameof(RecommendationsController.Latest))]
    [InlineData(typeof(RecommendationsController), nameof(RecommendationsController.SchoolNames))]
    [InlineData(typeof(ScenariosController), nameof(ScenariosController.List))]
    [InlineData(typeof(ScenariosController), nameof(ScenariosController.Get))]
    [InlineData(typeof(ScenariosController), nameof(ScenariosController.Rename))]
    [InlineData(typeof(ScenariosController), nameof(ScenariosController.Delete))]
    public void G7_ReadingAndManagingSavedResults_IsNotGated(Type controller, string action) =>
        Assert.DoesNotContain(PremiumPolicies.Recommendations, Policies(controller, action));
}
