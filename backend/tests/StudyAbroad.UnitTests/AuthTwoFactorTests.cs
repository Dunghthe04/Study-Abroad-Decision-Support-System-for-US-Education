using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OtpNet;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Auth;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>
/// [UC-03 3d, E4] Đăng nhập hai bước TOTP cho Moderator, Administrator (AUTH-075, 076, 209, 210, 211).
/// Dùng TotpService thật (Otp.NET) để mã tính trong test giống mã ứng dụng xác thực sinh ra.
/// </summary>
public class AuthTwoFactorTests
{
    private const string Password = "Password@123";

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;
        public bool VerifyPassword(string hash, string password) => hash == "hashed:" + password;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public (string Token, DateTime ExpiresAt) GenerateToken(User user) =>
            ("token:" + user.Email + ":" + Guid.NewGuid(), DateTime.UtcNow.AddMinutes(15));

        public string HashToken(string token) => "hash:" + token;
    }

    private sealed class FakeTwoFactorRepository(FakeUserRepository repo) : ITwoFactorRepository
    {
        public Task<int> CountUnusedRecoveryCodesAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(repo.Otps.Count(o => o.UserId == userId && o.Purpose == OtpPurposes.RecoveryCode && o.UsedAt == null));
    }

    private readonly FakeUserRepository _repo = new();
    private readonly FakeEmailSender _email = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeAuditLogRepository _audit = new();
    private readonly TotpService _totp = new(
        Options.Create(new TotpOptions()),
        Options.Create(new JwtOptions { SecretKey = "jwt-secret-for-tests-at-least-32-characters" }),
        NullLogger<TotpService>.Instance);

    private AuthService CreateService(bool withTotp = true) => new(
        _repo, new FakeHasher(), new FakeTokenService(), _email, _links, _audit,
        new ParentLinkService(_links, _repo, _email, _audit), new FakeUnitOfWork(), new FakeLeadWithdrawalRepository(),
        totp: withTotp ? _totp : null, twoFactor: withTotp ? new FakeTwoFactorRepository(_repo) : null);

    private User AddUser(string role, string email = "staff@test.com")
    {
        var user = new User
        {
            Email = email,
            PasswordHash = "hashed:" + Password,
            FullName = "Staff User",
            Role = role,
            Status = UserStatuses.Active,
        };
        _repo.Users.Add(user);
        return user;
    }

    private static string CodeNow(string secret) => new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(DateTime.UtcNow);

    private static string WrongCode(string secret)
    {
        var right = CodeNow(secret);
        return ((int.Parse(right) + 500_000) % 1_000_000).ToString("D6");
    }

    private string SecretOf(User user) => _totp.Unprotect(user.TotpSecret!)!;

    /// <summary>Đăng nhập lần đầu và hoàn tất thiết lập, trả 10 mã khôi phục.</summary>
    private async Task<IReadOnlyList<string>> SetUpAsync(AuthService service, User user)
    {
        var step1 = await service.LoginAsync(new LoginRequest(user.Email, Password));
        var step2 = await service.VerifyTwoFactorAsync(new TwoFactorLoginRequest(step1.TwoFactor!.Challenge, CodeNow(step1.TwoFactor.Setup!.Secret), null));
        Assert.Equal(AuthError.None, step2.Error);
        return step2.RecoveryCodes!;
    }

    // AUTH-075, AUTH-076: mật khẩu đúng chưa tạo phiên, phải qua bước mã
    [Theory]
    [InlineData(UserRoles.Moderator)]
    [InlineData(UserRoles.Admin)]
    public async Task Login_ModeratorOrAdmin_CorrectPassword_ReturnsChallenge_NoSession(string role)
    {
        AddUser(role);
        var result = await CreateService().LoginAsync(new LoginRequest("staff@test.com", Password));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Null(result.Response);
        Assert.NotNull(result.TwoFactor);
        Assert.False(string.IsNullOrEmpty(result.TwoFactor!.Challenge));
        Assert.Empty(_repo.Sessions);
    }

    // AUTH-209: lần đầu có khóa + otpauth để vẽ QR; mã đúng tạo phiên và cấp 10 mã khôi phục
    [Fact]
    public async Task FirstLogin_ShowsSetup_CorrectCode_CreatesSession_And10RecoveryCodes()
    {
        var user = AddUser(UserRoles.Moderator);
        var service = CreateService();

        var step1 = await service.LoginAsync(new LoginRequest(user.Email, Password));
        var setup = step1.TwoFactor!.Setup;
        Assert.NotNull(setup);
        Assert.StartsWith("otpauth://totp/", setup!.OtpAuthUri);
        Assert.Null(user.TotpEnabledAt);
        Assert.NotEqual(setup.Secret, user.TotpSecret); // lưu bản đã mã hóa

        var step2 = await service.VerifyTwoFactorAsync(new TwoFactorLoginRequest(step1.TwoFactor.Challenge, CodeNow(setup.Secret), null));

        Assert.Equal(AuthError.None, step2.Error);
        Assert.NotNull(step2.Response);
        Assert.Single(_repo.Sessions);
        Assert.NotNull(user.TotpEnabledAt);
        Assert.Equal(AuthService.RecoveryCodeCount, step2.RecoveryCodes!.Count);
        Assert.All(step2.RecoveryCodes, c => Assert.Matches("^[A-Z2-9]{5}-[A-Z2-9]{5}$", c));
        Assert.Equal(10, _repo.Otps.Count(o => o.UserId == user.Id && o.Purpose == OtpPurposes.RecoveryCode && o.UsedAt == null));
        Assert.Contains(_audit.Entries, a => a.Action == "user.two_factor_enabled");
    }

    // AUTH-209: đăng nhập lại chỉ hỏi mã, không hiện QR, không cấp lại mã khôi phục
    [Fact]
    public async Task SecondLogin_NoSetup_NoNewRecoveryCodes()
    {
        var user = AddUser(UserRoles.Admin);
        var service = CreateService();
        await SetUpAsync(service, user);
        var secretAfterSetup = user.TotpSecret;

        var step1 = await service.LoginAsync(new LoginRequest(user.Email, Password));
        Assert.Null(step1.TwoFactor!.Setup);
        Assert.Equal(secretAfterSetup, user.TotpSecret);

        var step2 = await service.VerifyTwoFactorAsync(new TwoFactorLoginRequest(step1.TwoFactor.Challenge, CodeNow(SecretOf(user)), null));
        Assert.Equal(AuthError.None, step2.Error);
        Assert.Null(step2.RecoveryCodes);
    }

    [Fact]
    public async Task WrongCode_GenericError_NoSession_CounterIncreases()
    {
        var user = AddUser(UserRoles.Moderator);
        var service = CreateService();
        var step1 = await service.LoginAsync(new LoginRequest(user.Email, Password));

        var result = await service.VerifyTwoFactorAsync(new TwoFactorLoginRequest(step1.TwoFactor!.Challenge, WrongCode(SecretOf(user)), null));

        Assert.Equal(AuthError.TwoFactorCodeInvalid, result.Error);
        Assert.Equal(AuthService.TwoFactorCodeInvalidMessage, result.Message);
        Assert.Empty(_repo.Sessions);
        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.Null(user.TotpEnabledAt);
    }

    // AUTH-210: sai mật khẩu 4 lần, mật khẩu đúng, mã sai "000000" -> lỗi chung và khóa 15 phút, có mail;
    // sau đó mật khẩu đúng báo khóa tạm thời
    [Fact]
    public async Task FourWrongPasswords_ThenWrongTotp_TempLocks_WithGenericError_AndEmail()
    {
        var user = AddUser(UserRoles.Moderator);
        var service = CreateService();
        await SetUpAsync(service, user);

        for (var i = 0; i < 4; i++)
            Assert.Equal(AuthError.InvalidCredentials, (await service.LoginAsync(new LoginRequest(user.Email, "wrong"))).Error);
        Assert.Equal(4, user.FailedLoginAttempts);

        var step1 = await service.LoginAsync(new LoginRequest(user.Email, Password));
        Assert.NotNull(step1.TwoFactor);
        Assert.Equal(4, user.FailedLoginAttempts); // mật khẩu đúng không xóa bộ đếm

        var code = CodeNow(SecretOf(user)) == "000000" ? "999999" : "000000";
        var result = await service.VerifyTwoFactorAsync(new TwoFactorLoginRequest(step1.TwoFactor!.Challenge, code, null));

        Assert.Equal(AuthError.TwoFactorCodeInvalid, result.Error);
        Assert.Equal(UserStatuses.TempLocked, user.Status);
        Assert.NotNull(user.LockoutEnd);
        Assert.Contains(_email.SentEmails, m => m.To == user.Email && m.Purpose == "temp_locked_notice");

        var again = await service.LoginAsync(new LoginRequest(user.Email, Password));
        Assert.Equal(AuthError.TempLocked, again.Error);
        Assert.Single(_repo.Sessions); // chỉ phiên của lần thiết lập
    }

    // AUTH-211: mã khôi phục dùng một lần; dùng lại bị từ chối với lỗi chung; mỗi lần dùng có email báo
    [Fact]
    public async Task RecoveryCode_WorksOnce_SendsEmail_ReuseRejected()
    {
        var user = AddUser(UserRoles.Moderator);
        var service = CreateService();
        var codes = await SetUpAsync(service, user);
        var sessionsBefore = _repo.Sessions.Count;

        var step1 = await service.LoginAsync(new LoginRequest(user.Email, Password));
        // Gõ chữ thường, có khoảng trắng vẫn nhận
        var typed = " " + codes[0].ToLowerInvariant() + " ";
        var ok = await service.VerifyTwoFactorAsync(new TwoFactorLoginRequest(step1.TwoFactor!.Challenge, null, typed), "1.2.3.4");
        Assert.Equal(AuthError.None, ok.Error);
        Assert.Equal(sessionsBefore + 1, _repo.Sessions.Count);
        Assert.Contains(_email.SentEmails, m => m.To == user.Email && m.Purpose == "recovery_code_used" && m.Code.StartsWith("remaining:9;"));
        Assert.Contains(_audit.Entries, a => a.Action == "user.login_recovery_code");

        var step1b = await service.LoginAsync(new LoginRequest(user.Email, Password));
        var reuse = await service.VerifyTwoFactorAsync(new TwoFactorLoginRequest(step1b.TwoFactor!.Challenge, null, codes[0]));
        Assert.Equal(AuthError.TwoFactorCodeInvalid, reuse.Error);
        Assert.Equal(AuthService.TwoFactorCodeInvalidMessage, reuse.Message);
        Assert.Equal(sessionsBefore + 1, _repo.Sessions.Count);
    }

    [Fact]
    public async Task RecoveryCode_NotAcceptedBeforeSetupFinished()
    {
        var user = AddUser(UserRoles.Moderator);
        var other = AddUser(UserRoles.Moderator, "other@test.com");
        var service = CreateService();
        var otherCodes = await SetUpAsync(service, other);

        var step1 = await service.LoginAsync(new LoginRequest(user.Email, Password));
        var result = await service.VerifyTwoFactorAsync(new TwoFactorLoginRequest(step1.TwoFactor!.Challenge, null, otherCodes[0]));

        Assert.Equal(AuthError.TwoFactorCodeInvalid, result.Error);
        // Mã của tài khoản khác không bị tiêu
        Assert.Equal(10, _repo.Otps.Count(o => o.UserId == other.Id && o.Purpose == OtpPurposes.RecoveryCode && o.UsedAt == null));
    }

    [Fact]
    public async Task Challenge_UsedExpiredOrUnknown_IsRejected()
    {
        var user = AddUser(UserRoles.Admin);
        var service = CreateService();
        await SetUpAsync(service, user);

        // Đã dùng
        var step1 = await service.LoginAsync(new LoginRequest(user.Email, Password));
        Assert.Equal(AuthError.None, (await service.VerifyTwoFactorAsync(new TwoFactorLoginRequest(step1.TwoFactor!.Challenge, CodeNow(SecretOf(user)), null))).Error);
        var reused = await service.VerifyTwoFactorAsync(new TwoFactorLoginRequest(step1.TwoFactor.Challenge, CodeNow(SecretOf(user)), null));
        Assert.Equal(AuthError.TwoFactorChallengeInvalid, reused.Error);

        // Hết hạn
        var step1b = await service.LoginAsync(new LoginRequest(user.Email, Password));
        foreach (var t in _repo.Otps.Where(o => o.Purpose == OtpPurposes.LoginTwoFactor && o.UsedAt == null))
            t.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        var expired = await service.VerifyTwoFactorAsync(new TwoFactorLoginRequest(step1b.TwoFactor!.Challenge, CodeNow(SecretOf(user)), null));
        Assert.Equal(AuthError.TwoFactorChallengeInvalid, expired.Error);

        // Không tồn tại
        var unknown = await service.VerifyTwoFactorAsync(new TwoFactorLoginRequest("not-a-challenge", "123456", null));
        Assert.Equal(AuthError.TwoFactorChallengeInvalid, unknown.Error);
    }

    [Fact]
    public async Task NewPasswordStep_InvalidatesOlderChallenge()
    {
        var user = AddUser(UserRoles.Moderator);
        var service = CreateService();
        await SetUpAsync(service, user);

        var first = await service.LoginAsync(new LoginRequest(user.Email, Password));
        var second = await service.LoginAsync(new LoginRequest(user.Email, Password));

        var old = await service.VerifyTwoFactorAsync(new TwoFactorLoginRequest(first.TwoFactor!.Challenge, CodeNow(SecretOf(user)), null));
        Assert.Equal(AuthError.TwoFactorChallengeInvalid, old.Error);
        var fresh = await service.VerifyTwoFactorAsync(new TwoFactorLoginRequest(second.TwoFactor!.Challenge, CodeNow(SecretOf(user)), null));
        Assert.Equal(AuthError.None, fresh.Error);
    }

    [Theory]
    [InlineData(UserRoles.Student)]
    [InlineData(UserRoles.Parent)]
    [InlineData(UserRoles.Center)]
    public async Task OtherRoles_LoginInOneStep(string role)
    {
        var user = AddUser(role);
        var result = await CreateService().LoginAsync(new LoginRequest(user.Email, Password));

        Assert.Null(result.TwoFactor);
        Assert.NotNull(result.Response);
        Assert.Null(user.TotpSecret);
    }

    [Fact]
    public async Task WithoutTotpService_ModeratorLogsInAsBefore()
    {
        AddUser(UserRoles.Moderator);
        var result = await CreateService(withTotp: false).LoginAsync(new LoginRequest("staff@test.com", Password));

        Assert.Null(result.TwoFactor);
        Assert.NotNull(result.Response);
    }
}
