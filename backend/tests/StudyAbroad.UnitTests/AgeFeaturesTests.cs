using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using StudyAbroad.Api.Authorization;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// [Q11] Chặn chức năng theo tuổi: mọi dòng trong AgeFeatures.Rules tự được kiểm tra, thêm dòng mới không cần viết thêm test.
public class AgeFeaturesTests
{
    private static readonly DateOnly Today = AgePolicy.TodayInVietnam(DateTime.UtcNow);
    private static readonly int[] Ages = [10, 15, 16, 17, 18, 30];

    private readonly FakeUserRepository _users = new();
    private readonly FakeParentLinkRepository _links = new();

    private static DateOnly BornYearsAgo(int years) => Today.AddDays(-10).AddYears(-years);

    public static TheoryData<string, int, bool> RuleMatrix()
    {
        var data = new TheoryData<string, int, bool>();
        foreach (var feature in AgeFeatures.Rules.Keys)
            foreach (var age in Ages)
            {
                data.Add(feature, age, false);
                data.Add(feature, age, true);
            }
        return data;
    }

    private User AddUser(string role, int? age, string email = "u@test.com")
    {
        var user = new User
        {
            Email = email, FullName = "U", Role = role, Status = UserStatuses.Active, PasswordHash = "x",
            DateOfBirth = age is { } a ? BornYearsAgo(a) : null,
        };
        _users.Users.Add(user);
        return user;
    }

    // Link active + consent granted giống kết quả ParentLinkService.AcceptAsync
    private void GrantConsent(User student, bool granted = true, string status = ParentLinkStatuses.Active)
    {
        var consent = new Consent
        {
            UserId = student.Id, Purpose = ConsentPurposes.ChildDataProcessing, Granted = granted, PolicyVersion = "2026-10",
        };
        _links.Consents.Add(consent);
        _links.Links.Add(new ParentStudentLink
        {
            StudentUserId = student.Id, ParentEmail = "me@test.com", Status = status, ConsentId = consent.Id,
        });
    }

    private async Task<AuthorizationHandlerContext> AuthorizeAsync(User user, string feature)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Role, user.Role)], "test"));
        var context = new AuthorizationHandlerContext([new AgeFeatureRequirement(feature)], principal, null);
        await new AgeFeatureHandler(_users, _links).HandleAsync(context);
        return context;
    }

    [Fact]
    public void Rules_MatchAgreedPolicy()
    {
        Assert.Equal(new AgeRule(16, ParentConsentUnlocks: true), AgeFeatures.Rules[AgeFeatures.ProcessStudentData]);
        Assert.Equal(new AgeRule(18, ParentConsentUnlocks: false), AgeFeatures.Rules[AgeFeatures.ShareWithCenter]);
    }

    [Theory]
    [MemberData(nameof(RuleMatrix))]
    public void Check_Student_FollowsRuleTable(string feature, int age, bool consent)
    {
        var rule = AgeFeatures.Rules[feature];
        var expectBlocked = age < rule.MinAge && !(rule.ParentConsentUnlocks && consent);

        var block = AgeFeatures.Check(feature, UserRoles.Student, BornYearsAgo(age), Today, consent);

        Assert.Equal(expectBlocked, block != null);
        if (block != null) Assert.Equal(rule.ParentConsentUnlocks, block.NeedParentConsent);
    }

    [Theory]
    [InlineData(10, false, new[] { AgeFeatures.ProcessStudentData, AgeFeatures.ShareWithCenter })]
    [InlineData(15, true, new[] { AgeFeatures.ShareWithCenter })]
    [InlineData(16, false, new[] { AgeFeatures.ShareWithCenter })]
    [InlineData(17, false, new[] { AgeFeatures.ShareWithCenter })]
    [InlineData(18, false, new string[0])]
    public void GetBlocked_ConcreteCases(int age, bool consent, string[] expected)
    {
        var blocked = AgeFeatures.GetBlocked(UserRoles.Student, BornYearsAgo(age), Today, consent);
        Assert.Equal(expected.OrderBy(x => x), blocked.OrderBy(x => x));
    }

    [Theory]
    [InlineData(UserRoles.Parent)]
    [InlineData(UserRoles.Center)]
    [InlineData(UserRoles.Admin)]
    public void Check_NonStudent_NeverBlocked(string role)
    {
        foreach (var feature in AgeFeatures.Rules.Keys)
            Assert.Null(AgeFeatures.Check(feature, role, BornYearsAgo(10), Today, hasParentConsent: false));
    }

    [Fact]
    public void Check_StudentWithoutDateOfBirth_NeverBlocked()
    {
        Assert.Empty(AgeFeatures.GetBlocked(UserRoles.Student, null, Today, hasParentConsent: false));
    }

    [Fact]
    public void Check_UnknownFeature_Throws()
    {
        Assert.Throws<ArgumentException>(() => AgeFeatures.Check("khong_co", UserRoles.Student, null, Today, false));
    }

    [Fact]
    public void Check_SixteenthBirthdayToday_Allowed()
    {
        var dob = Today.AddYears(-16);
        Assert.Null(AgeFeatures.Check(AgeFeatures.ProcessStudentData, UserRoles.Student, dob, Today, false));
        Assert.NotNull(AgeFeatures.Check(AgeFeatures.ProcessStudentData, UserRoles.Student, dob.AddDays(1), Today, false));
    }

    [Theory]
    [MemberData(nameof(RuleMatrix))]
    public async Task Handler_AgreesWithBlockedFeatures(string feature, int age, bool consent)
    {
        var student = AddUser(UserRoles.Student, age);
        if (consent) GrantConsent(student);

        var context = await AuthorizeAsync(student, feature);
        var access = await AgeAccess.EvaluateAsync(student, _links, DateTime.UtcNow);

        var blocked = access.BlockedFeatures.Contains(feature);
        Assert.Equal(!blocked, context.HasSucceeded);
        Assert.Equal(blocked, context.HasFailed);
        if (blocked)
        {
            var reason = Assert.IsType<AgeRestrictedFailureReason>(Assert.Single(context.FailureReasons));
            Assert.Equal(feature, reason.Block.Feature);
        }
    }

    [Fact]
    public async Task Handler_RevokedOrUngrantedConsent_StillBlocksChild()
    {
        var student = AddUser(UserRoles.Student, 14);
        GrantConsent(student, granted: false, status: ParentLinkStatuses.Revoked);
        GrantConsent(student, granted: false); // link active nhưng consent đã rút

        var context = await AuthorizeAsync(student, AgeFeatures.ProcessStudentData);

        Assert.True(context.HasFailed);
        var reason = Assert.IsType<AgeRestrictedFailureReason>(Assert.Single(context.FailureReasons));
        Assert.True(reason.Block.NeedParentConsent);
    }

    [Fact]
    public async Task Handler_LegacyStudentWithoutDateOfBirth_Succeeds()
    {
        var student = AddUser(UserRoles.Student, age: null);
        foreach (var feature in AgeFeatures.Rules.Keys)
            Assert.True((await AuthorizeAsync(student, feature)).HasSucceeded);
    }

    [Theory]
    [InlineData(14, false, false, ParentConsentStatuses.None, AgeGroups.Child)]
    [InlineData(14, true, false, ParentConsentStatuses.Pending, AgeGroups.Child)]
    [InlineData(14, false, true, ParentConsentStatuses.Granted, AgeGroups.Child)]
    [InlineData(17, false, false, ParentConsentStatuses.None, AgeGroups.Minor)]
    [InlineData(19, false, false, ParentConsentStatuses.NotRequired, AgeGroups.Adult)]
    public async Task Evaluate_ReturnsConsentStatus(int age, bool pendingInvite, bool granted, string expectedStatus, string expectedGroup)
    {
        var student = AddUser(UserRoles.Student, age);
        if (pendingInvite) _links.Links.Add(new ParentStudentLink { StudentUserId = student.Id, ParentEmail = "me@test.com" });
        if (granted) GrantConsent(student);

        var access = await AgeAccess.EvaluateAsync(student, _links, DateTime.UtcNow);

        Assert.Equal(expectedStatus, access.ParentConsentStatus);
        Assert.Equal(expectedGroup, access.AgeGroup);
    }

    [Fact]
    public async Task Evaluate_Parent_NotRequiredAndNoAgeGroup()
    {
        var parent = AddUser(UserRoles.Parent, 40);
        var access = await AgeAccess.EvaluateAsync(parent, _links, DateTime.UtcNow);

        Assert.Null(access.AgeGroup);
        Assert.Equal(ParentConsentStatuses.NotRequired, access.ParentConsentStatus);
        Assert.Empty(access.BlockedFeatures);
    }

    [Fact]
    public async Task ResultHandler_AgeRestricted_Writes403WithCode()
    {
        var reason = new AgeRestrictedFailureReason(new AgeFeatureHandler(_users, _links),
            new AgeBlock(AgeFeatures.ProcessStudentData, NeedParentConsent: true));
        var http = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        http.Response.Body = new MemoryStream();
        var nextCalled = false;

        await new AgeRestrictionResultHandler().HandleAsync(_ => { nextCalled = true; return Task.CompletedTask; }, http,
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(),
            PolicyAuthorizationResult.Forbid(AuthorizationFailure.Failed([reason])));

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, http.Response.StatusCode);
        http.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(http.Response.Body);
        Assert.Equal(AgeRestrictionResultHandler.ErrorCode, json.RootElement.GetProperty("code").GetString());
        Assert.Equal(AgeFeatures.ProcessStudentData, json.RootElement.GetProperty("feature").GetString());
        Assert.True(json.RootElement.GetProperty("needParentConsent").GetBoolean());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("detail").GetString()));
    }
}
