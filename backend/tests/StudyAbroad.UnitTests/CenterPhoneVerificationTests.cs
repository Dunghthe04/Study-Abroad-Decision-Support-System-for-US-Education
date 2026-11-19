using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-91] Xác thực số điện thoại trung tâm bằng OTP qua SMS và đổi số điện thoại qua OTP gửi tới số mới.</summary>
public class CenterPhoneVerificationTests
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

    private sealed class FakeSms : ISmsSender
    {
        public List<(string Phone, string Code)> Sent { get; } = [];
        public bool Fail { get; set; }

        public Task SendOtpAsync(string phone, string code, int expiryMinutes, CancellationToken ct = default)
        {
            if (Fail) throw new InvalidOperationException("no provider");
            Sent.Add((phone, code));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCenters : IStudyCenterRepository
    {
        public List<StudyCenter> Centers { get; } = [];
        public Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<StudyCenter>(Centers, 1, Centers.Count, Centers.Count));
        public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));
        public Task<StudyCenter?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<StudyCenter>>(Centers);
        public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(false);
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
    private readonly FakeSms _sms = new();

    private AuthService Service(ISmsSender? sms = null) => new(
        _users, new FakeHasher(), new FakeTokenService(), _email, _links, _audit,
        new ParentLinkService(_links, _users, _email, _audit), new FakeUnitOfWork(), new FakeLeadWithdrawalRepository(),
        studyCenters: _centers, smsSender: sms ?? _sms);

    private (User User, StudyCenter Center) Setup(string phone = "0912345678", bool phoneVerified = false)
    {
        var user = new User { Id = Guid.NewGuid(), Email = "tt@example.com", FullName = "TT", Role = UserRoles.Center, Status = UserStatuses.Active, Phone = phone };
        _users.Users.Add(user);
        var center = new StudyCenter
        {
            Id = Guid.NewGuid(), Name = "TT", OwnerUserId = user.Id, Phone = phone,
            EmailVerifiedAt = DateTime.UtcNow, PhoneVerifiedAt = phoneVerified ? DateTime.UtcNow : null,
        };
        _centers.Centers.Add(center);
        return (user, center);
    }

    [Fact]
    public async Task RequestThenVerify_CorrectCode_MarksPhoneVerified()
    {
        var (user, center) = Setup();
        var service = Service();

        var sent = await service.RequestCenterPhoneVerificationAsync(user.Id);
        Assert.Equal(AuthError.None, sent.Error);
        var (phone, code) = Assert.Single(_sms.Sent);
        Assert.Equal("0912345678", phone);
        Assert.DoesNotContain(code, sent.Message); // mã không bao giờ trả về qua API
        Assert.Equal(OtpPurposes.VerifyPhone, Assert.Single(_users.Otps).Purpose);

        var result = await service.VerifyCenterPhoneAsync(user.Id, code);

        Assert.Equal(AuthError.None, result.Error);
        Assert.NotNull(center.PhoneVerifiedAt);
        Assert.Contains(_audit.Entries, l => l.Action == "center.phone_verified");
    }

    [Fact]
    public async Task Verify_WrongCodeTooManyTimes_InvalidatesCodeEvenIfCorrectLater()
    {
        var (user, center) = Setup();
        var service = Service();
        await service.RequestCenterPhoneVerificationAsync(user.Id);
        var code = _sms.Sent[0].Code;
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 0; i < AuthService.MaxOtpAttempts; i++)
            Assert.Equal(AuthError.OtpInvalid, (await service.VerifyCenterPhoneAsync(user.Id, wrong)).Error);

        Assert.Equal(AuthError.OtpInvalid, (await service.VerifyCenterPhoneAsync(user.Id, code)).Error);
        Assert.Null(center.PhoneVerifiedAt);
    }

    [Fact]
    public async Task Verify_ExpiredCode_IsRejected()
    {
        var (user, center) = Setup();
        var service = Service();
        await service.RequestCenterPhoneVerificationAsync(user.Id);
        _users.Otps[0].ExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        var result = await service.VerifyCenterPhoneAsync(user.Id, _sms.Sent[0].Code);

        Assert.Equal(AuthError.OtpInvalid, result.Error);
        Assert.Null(center.PhoneVerifiedAt);
    }

    [Fact]
    public async Task Request_TwiceWithinSpacing_IsRejectedAndSendsOnlyOnce()
    {
        var (user, _) = Setup();
        var service = Service();

        await service.RequestCenterPhoneVerificationAsync(user.Id);
        var second = await service.RequestCenterPhoneVerificationAsync(user.Id);

        Assert.Equal(AuthError.Validation, second.Error);
        Assert.Single(_sms.Sent);
    }

    [Fact]
    public async Task Request_AlreadyVerifiedOrSmsUnavailable_IsRejected()
    {
        var (verifiedUser, _) = Setup(phone: "0911111111", phoneVerified: true);
        Assert.Equal(AuthError.Validation, (await Service().RequestCenterPhoneVerificationAsync(verifiedUser.Id)).Error);

        var (user, _) = Setup(phone: "0922222222");
        var failing = new FakeSms { Fail = true };
        var result = await Service(failing).RequestCenterPhoneVerificationAsync(user.Id);
        Assert.Equal(AuthError.Validation, result.Error);
        Assert.All(_users.Otps.Where(o => o.UserId == user.Id), o => Assert.NotNull(o.UsedAt)); // mã không gửi được bị vô hiệu
    }

    [Fact]
    public async Task ChangePhone_RequiresOtpSentToTheNewNumber_AndCodeIsBoundToThatNumber()
    {
        var (user, center) = Setup(phoneVerified: true);
        var service = Service();

        var req = await service.RequestCenterPhoneChangeAsync(user.Id, new RequestContactChangeDto("0987654321"));
        Assert.Equal(AuthError.None, req.Error);
        Assert.Equal("0912345678", center.Phone); // chưa đổi trước khi nhập đúng mã
        var (phone, code) = Assert.Single(_sms.Sent);
        Assert.Equal("0987654321", phone);

        // Mã của số A không dùng để xác nhận số B
        var wrongNumber = await service.ConfirmCenterPhoneChangeAsync(user.Id, new ConfirmContactChangeDto("0977777777", code));
        Assert.Equal(AuthError.OtpInvalid, wrongNumber.Error);

        var ok = await service.ConfirmCenterPhoneChangeAsync(user.Id, new ConfirmContactChangeDto("0987654321", code));
        Assert.Equal(AuthError.None, ok.Error);
        Assert.Equal("0987654321", center.Phone);
        Assert.Equal("0987654321", user.Phone);
        Assert.NotNull(center.PhoneVerifiedAt);
    }

    [Fact]
    public async Task ChangePhone_SameNumberOrInvalidFormat_IsRejectedWithoutSending()
    {
        var (user, _) = Setup();
        var service = Service();

        Assert.Equal(AuthError.Validation, (await service.RequestCenterPhoneChangeAsync(user.Id, new RequestContactChangeDto("0912345678"))).Error);
        Assert.Equal(AuthError.Validation, (await service.RequestCenterPhoneChangeAsync(user.Id, new RequestContactChangeDto("123"))).Error);
        Assert.Empty(_sms.Sent);
    }
}
