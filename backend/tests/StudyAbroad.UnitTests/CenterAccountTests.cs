using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.Policies;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>
/// [UC-64] Trung tâm tự đăng ký tài khoản và [UC-91] xác thực email trung tâm (OTP khi đăng ký và khi đổi email liên lạc), qua CenterAccountService.
/// </summary>
public class CenterAccountTests
{
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;
        public bool VerifyPassword(string hash, string password) => hash == "hashed:" + password;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public (string Token, DateTime ExpiresAt) GenerateToken(User user) => ("token", DateTime.UtcNow.AddDays(1));
        public string HashToken(string token) => "hash:" + token;
    }

    private sealed class FakeCenters : IStudyCenterRepository
    {
        public List<StudyCenter> Centers { get; } = [];
        public Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<StudyCenter>(Centers, 1, Centers.Count, Centers.Count));
        public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));
        public Task<StudyCenter?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<StudyCenter>>(Centers);
        public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(Centers.Any(c => c.Code == code));
        public Task AddAsync(StudyCenter center, CancellationToken ct = default) { Centers.Add(center); return Task.CompletedTask; }
        public Task UpdateAsync(StudyCenter center, CancellationToken ct = default) => Task.CompletedTask;
        public Task<StudyCenter?> GetByOwnerOrEmailAsync(Guid ownerUserId, string? email, CancellationToken ct = default) =>
            Task.FromResult(Centers.FirstOrDefault(c => c.OwnerUserId == ownerUserId));
    }

    private readonly FakeUserRepository _users = new();
    private readonly FakeEmailSender _email = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeAuditLogRepository _audit = new();
    private readonly FakeCenters _centers = new();

    private CenterAccountService Service() => new(
        _users, new FakeHasher(), _email, _audit, _centers,
        new AuthService(
            _users, new FakeHasher(), new FakeTokenService(), _email, _links, _audit,
            new ParentLinkService(_links, _users, _email, _audit), new FakeUnitOfWork(), new FakeLeadWithdrawalRepository(),
            studyCenters: _centers));

    private static RegisterCenterRequest Request(
        string email = "TrungTam@Example.com",
        string password = "Password@123",
        string? confirm = null,
        string centerName = "Trung tâm Du học ABC",
        string phone = "0912345678",
        string registrant = "Nguyễn Văn A",
        bool agreeTerms = true,
        bool agreeCommitment = true) =>
        new(email, password, confirm ?? password, centerName, phone, registrant, "Giám đốc", agreeTerms, agreeCommitment);

    // ===== UC-64 Đăng ký tài khoản trung tâm =====

    [Fact]
    public async Task RegisterCenter_Valid_CreatesUnverifiedAccountAndCenterAndSendsOtp()
    {
        var result = await Service().RegisterAsync(Request());

        Assert.Equal(AuthError.None, result.Error);
        var user = Assert.Single(_users.Users);
        Assert.Equal("trungtam@example.com", user.Email);
        Assert.Equal(UserRoles.Center, user.Role);
        Assert.Equal(UserStatuses.Unverified, user.Status);
        Assert.Equal("hashed:Password@123", user.PasswordHash);
        var center = Assert.Single(_centers.Centers);
        Assert.StartsWith("CTR_", center.Code);
        Assert.Equal(user.Id, center.OwnerUserId);
        Assert.Equal(CenterVerificationStatuses.Unverified, center.VerificationStatus);
        Assert.NotNull(center.TermsAgreedAt);
        Assert.NotNull(center.CommitmentAgreedAt);
        Assert.Null(center.EmailVerifiedAt);
        var mail = Assert.Single(_email.SentEmails);
        Assert.Equal(("trungtam@example.com", OtpPurposes.VerifyEmail), (mail.To, mail.Purpose));
        Assert.Contains(_audit.Entries, l => l.Action == "center.register");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task RegisterCenter_TermsOrTruthfulnessCommitmentNotAccepted_IsRejected(bool terms, bool commitment)
    {
        var result = await Service().RegisterAsync(Request(agreeTerms: terms, agreeCommitment: commitment));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_users.Users);
        Assert.Empty(_centers.Centers);
        Assert.Empty(_email.SentEmails);
    }

    // ------------------------------------------------------------ [UC-104] đồng ý Điều khoản và Chính sách bảo mật

    private readonly FakePublishedPolicies _published = new();
    private readonly FakeConsentStore _consents = new();
    private readonly FakeUnitOfWork _uow = new();

    private CenterAccountService ServiceWithConsent() => new(
        _users, new FakeHasher(), _email, _audit, _centers,
        new AuthService(
            _users, new FakeHasher(), new FakeTokenService(), _email, _links, _audit,
            new ParentLinkService(_links, _users, _email, _audit), new FakeUnitOfWork(), new FakeLeadWithdrawalRepository(),
            studyCenters: _centers),
        new RegistrationConsent(_published, _consents), _uow);

    private static RegisterCenterRequest WithPolicies(IReadOnlyList<AcceptPolicyItem>? accepted) =>
        Request() with { AcceptedPolicies = accepted };

    [Fact]
    public async Task RegisterCenter_WithConsent_RecordsTheEvidenceForTheNewAccount()
    {
        _published.Add(PolicyKinds.TermsOfUse, "2026-11").Add(PolicyKinds.PrivacyPolicy, "2026-11");

        var result = await ServiceWithConsent().RegisterAsync(
            WithPolicies([new(PolicyKinds.TermsOfUse, "2026-11"), new(PolicyKinds.PrivacyPolicy, "2026-11")]), ipAddress: "203.0.113.8");

        Assert.Equal(AuthError.None, result.Error);
        var user = Assert.Single(_users.Users);
        Assert.Equal(2, _consents.Rows.Count);
        Assert.All(_consents.Rows, c =>
        {
            Assert.Equal(user.Id, c.UserId);
            Assert.Equal("2026-11", c.PolicyVersion);
            Assert.Equal("203.0.113.8", c.GrantedIp);
        });
        Assert.Equal(1, _uow.Calls);   // tài khoản, trung tâm, bằng chứng cùng một giao dịch
    }

    [Fact]
    public async Task RegisterCenter_WithoutConsentVersions_CreatesNothing()
    {
        _published.Add(PolicyKinds.TermsOfUse, "2026-11").Add(PolicyKinds.PrivacyPolicy, "2026-11");

        var result = await ServiceWithConsent().RegisterAsync(WithPolicies(null));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Equal(RegistrationConsent.MissingMessage, result.Message);
        Assert.Empty(_users.Users);
        Assert.Empty(_centers.Centers);
        Assert.Empty(_consents.Rows);
        Assert.Empty(_email.SentEmails);
    }

    [Fact]
    public async Task RegisterCenter_WithAnOldVersion_IsRefused()
    {
        _published.Add(PolicyKinds.TermsOfUse, "2026-11-02").Add(PolicyKinds.PrivacyPolicy, "2026-11");

        var result = await ServiceWithConsent().RegisterAsync(
            WithPolicies([new(PolicyKinds.TermsOfUse, "2026-11"), new(PolicyKinds.PrivacyPolicy, "2026-11")]));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_users.Users);
        Assert.Empty(_consents.Rows);
    }

    [Fact]
    public async Task RegisterCenter_PasswordConfirmationMismatch_IsRejected()
    {
        var result = await Service().RegisterAsync(Request(password: "Password@123", confirm: "Password@124"));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task RegisterCenter_PasswordShorterThanMinimum_IsRejected()
    {
        var result = await Service().RegisterAsync(Request(password: "short1"));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_users.Users);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public async Task RegisterCenter_InvalidEmail_IsRejected(string email)
    {
        var result = await Service().RegisterAsync(Request(email: email));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_users.Users);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("abcdefghij")]
    [InlineData("")]
    public async Task RegisterCenter_InvalidVietnamesePhone_IsRejected(string phone)
    {
        var result = await Service().RegisterAsync(Request(phone: phone));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task RegisterCenter_EmptyCenterNameOrRegistrant_IsRejected()
    {
        var service = Service();

        Assert.Equal(AuthError.Validation, (await service.RegisterAsync(Request(centerName: "  "))).Error);
        Assert.Equal(AuthError.Validation, (await service.RegisterAsync(Request(registrant: ""))).Error);
        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task RegisterCenter_EmailAlreadyUsed_IsRejectedAndUnverifiedOwnerIsToldToVerify()
    {
        var service = Service();
        Assert.Equal(AuthError.None, (await service.RegisterAsync(Request())).Error);

        var whileUnverified = await service.RegisterAsync(Request(phone: "0987654321"));
        _users.Users[0].Status = UserStatuses.Active;
        var whileActive = await service.RegisterAsync(Request(phone: "0987654321"));

        Assert.Equal(AuthError.Validation, whileUnverified.Error);
        Assert.Contains("chưa xác thực", whileUnverified.Message);
        Assert.Equal(AuthError.Validation, whileActive.Error);
        Assert.Contains("đã được sử dụng", whileActive.Message);
        Assert.Single(_users.Users);
    }

    [Fact]
    public async Task RegisterCenter_PhoneAlreadyUsedByAnotherAccount_IsRejected()
    {
        _users.Users.Add(new User { Id = Guid.NewGuid(), Email = "khac@example.com", FullName = "K", Role = UserRoles.Student, Status = UserStatuses.Active, Phone = "0912345678" });

        var result = await Service().RegisterAsync(Request());

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Contains("đã được sử dụng", result.Message);
        Assert.Empty(_centers.Centers);
    }

    // ===== UC-91 Xác thực email trung tâm khi đăng ký =====

    [Fact]
    public async Task VerifyCenterEmail_CorrectOtp_ActivatesAccountAndMarksCenterEmailVerified()
    {
        var service = Service();
        await service.RegisterAsync(Request());
        var code = _email.SentEmails[0].Code;

        var result = await service.VerifyEmailAsync(new VerifyCenterEmailRequest("trungtam@example.com", code));

        Assert.Equal(AuthError.None, result.Error);
        Assert.NotNull(result.Response);
        Assert.Equal(UserStatuses.Active, _users.Users[0].Status);
        var center = Assert.Single(_centers.Centers);
        Assert.NotNull(center.EmailVerifiedAt);
        // Xác thực email không tự cấp dấu xác minh
        Assert.Equal(CenterVerificationStatuses.Unverified, center.VerificationStatus);
        Assert.Contains(_audit.Entries, l => l.Action == "center.email_verified");
    }

    [Fact]
    public async Task VerifyCenterEmail_WrongOtpFiveTimes_InvalidatesCodeEvenIfCorrectLater()
    {
        var service = Service();
        await service.RegisterAsync(Request());
        var code = _email.SentEmails[0].Code;
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 0; i < AuthService.MaxOtpAttempts; i++)
            Assert.Equal(AuthError.OtpInvalid, (await service.VerifyEmailAsync(new VerifyCenterEmailRequest("trungtam@example.com", wrong))).Error);

        var late = await service.VerifyEmailAsync(new VerifyCenterEmailRequest("trungtam@example.com", code));

        Assert.Equal(AuthError.OtpInvalid, late.Error);
        Assert.Equal(UserStatuses.Unverified, _users.Users[0].Status);
        Assert.Null(_centers.Centers[0].EmailVerifiedAt);
    }

    [Fact]
    public async Task VerifyCenterEmail_ExpiredOtp_IsRejected()
    {
        var service = Service();
        await service.RegisterAsync(Request());
        _users.Otps[0].ExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        var result = await service.VerifyEmailAsync(new VerifyCenterEmailRequest("trungtam@example.com", _email.SentEmails[0].Code));

        Assert.Equal(AuthError.OtpInvalid, result.Error);
        Assert.Equal(UserStatuses.Unverified, _users.Users[0].Status);
    }

    [Fact]
    public async Task VerifyCenterEmail_UnknownEmailOrAlreadyActive_GetsTheSameGenericError()
    {
        var service = Service();
        await service.RegisterAsync(Request());
        var code = _email.SentEmails[0].Code;
        _users.Users[0].Status = UserStatuses.Active;

        var unknown = await service.VerifyEmailAsync(new VerifyCenterEmailRequest("nobody@example.com", code));
        var alreadyActive = await service.VerifyEmailAsync(new VerifyCenterEmailRequest("trungtam@example.com", code));

        Assert.Equal(AuthError.OtpInvalid, unknown.Error);
        Assert.Equal(AuthError.OtpInvalid, alreadyActive.Error);
        Assert.Equal(unknown.Message, alreadyActive.Message);
    }

    // ===== UC-91 Đổi email liên lạc bằng OTP gửi tới email mới =====

    private async Task<(CenterAccountService Service, User User, StudyCenter Center)> ActiveCenter()
    {
        var service = Service();
        await service.RegisterAsync(Request());
        var code = _email.SentEmails[0].Code;
        await service.VerifyEmailAsync(new VerifyCenterEmailRequest("trungtam@example.com", code));
        _email.SentEmails.Clear();
        _users.Otps.Clear();
        return (service, _users.Users[0], _centers.Centers[0]);
    }

    [Fact]
    public async Task ChangeCenterEmail_RequestThenConfirm_UpdatesContactEmailButNotLoginEmail()
    {
        var (service, user, center) = await ActiveCenter();

        var request = await service.RequestEmailChangeAsync(user.Id, new RequestContactChangeDto("New.Contact@Example.com"));
        var mail = Assert.Single(_email.SentEmails);
        var confirm = await service.ConfirmEmailChangeAsync(user.Id, new ConfirmContactChangeDto("new.contact@example.com", mail.Code));

        Assert.Equal(AuthError.None, request.Error);
        Assert.Equal("new.contact@example.com", mail.To);   // OTP goes to the NEW address
        Assert.Equal(AuthError.None, confirm.Error);
        Assert.Equal("new.contact@example.com", center.Email);
        Assert.NotNull(center.EmailVerifiedAt);
        Assert.Equal("trungtam@example.com", user.Email);   // login email is unchanged
        Assert.Contains(_audit.Entries, l => l.Action == "center.email_change_requested");
        Assert.Contains(_audit.Entries, l => l.Action == "center.email_changed");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    [InlineData("trungtam@example.com")]   // same as the current contact email
    public async Task ChangeCenterEmail_InvalidOrSameEmail_IsRejectedWithoutSendingOtp(string newEmail)
    {
        var (service, user, center) = await ActiveCenter();

        var result = await service.RequestEmailChangeAsync(user.Id, new RequestContactChangeDto(newEmail));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_email.SentEmails);
        Assert.Equal("trungtam@example.com", center.Email);
    }

    [Fact]
    public async Task ChangeCenterEmail_EmailUsedByAnotherAccount_IsRejected()
    {
        var (service, user, _) = await ActiveCenter();
        _users.Users.Add(new User { Id = Guid.NewGuid(), Email = "taken@example.com", FullName = "T", Role = UserRoles.Student, Status = UserStatuses.Active });

        var result = await service.RequestEmailChangeAsync(user.Id, new RequestContactChangeDto("taken@example.com"));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_email.SentEmails);
    }

    [Fact]
    public async Task ChangeCenterEmail_SecondRequestWithinSpacing_IsRateLimited()
    {
        var (service, user, _) = await ActiveCenter();

        var first = await service.RequestEmailChangeAsync(user.Id, new RequestContactChangeDto("a@example.com"));
        var second = await service.RequestEmailChangeAsync(user.Id, new RequestContactChangeDto("b@example.com"));

        Assert.Equal(AuthError.None, first.Error);
        Assert.Equal(AuthError.Validation, second.Error);
        Assert.Single(_email.SentEmails);
    }

    [Fact]
    public async Task ChangeCenterEmail_WrongCodeOrCodeForAnotherAddress_IsRejectedAndEmailUnchanged()
    {
        var (service, user, center) = await ActiveCenter();
        await service.RequestEmailChangeAsync(user.Id, new RequestContactChangeDto("a@example.com"));
        var code = _email.SentEmails[0].Code;
        var wrong = code == "000000" ? "111111" : "000000";

        var wrongCode = await service.ConfirmEmailChangeAsync(user.Id, new ConfirmContactChangeDto("a@example.com", wrong));
        var otherAddress = await service.ConfirmEmailChangeAsync(user.Id, new ConfirmContactChangeDto("b@example.com", code));

        Assert.Equal(AuthError.OtpInvalid, wrongCode.Error);
        Assert.Equal(AuthError.OtpInvalid, otherAddress.Error);
        Assert.Equal("trungtam@example.com", center.Email);
    }

    // ===== Vị trí của API: đăng ký trung tâm thuộc nhóm trung tâm, không nằm trong /auth =====

    [Fact]
    public void CenterAccountController_IsAnonymousAndLivesUnderCenterAccountsRoute()
    {
        var type = typeof(CenterAccountController);

        Assert.Equal("api/v1/center-accounts", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.NotNull(type.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("register", type.GetMethod(nameof(CenterAccountController.Register))!.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Equal("verify-email", type.GetMethod(nameof(CenterAccountController.VerifyEmail))!.GetCustomAttribute<HttpPostAttribute>()!.Template);
    }

    [Fact]
    public void AuthController_NoLongerExposesCenterRegistrationOrCenterEmailVerification()
    {
        var names = typeof(AuthController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).ToList();

        Assert.DoesNotContain("RegisterCenter", names);
        Assert.DoesNotContain("VerifyCenterEmail", names);
    }}
