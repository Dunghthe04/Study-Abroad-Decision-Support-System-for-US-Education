using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-91] Số điện thoại trung tâm là thông tin liên hệ, đổi trực tiếp, không xác thực bằng SMS OTP.</summary>
public class CenterPhoneChangeTests
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

    private CenterAccountService Service() => new(
        _users, new FakeHasher(), _email, _audit, _centers,
        new AuthService(
            _users, new FakeHasher(), new FakeTokenService(), _email, _links, _audit,
            new ParentLinkService(_links, _users, _email, _audit), new FakeUnitOfWork(), new FakeLeadWithdrawalRepository(),
            studyCenters: _centers));

    private (User User, StudyCenter Center) Setup(string phone = "0912345678")
    {
        var user = new User { Id = Guid.NewGuid(), Email = "tt@example.com", FullName = "TT", Role = UserRoles.Center, Status = UserStatuses.Active, Phone = phone };
        _users.Users.Add(user);
        var center = new StudyCenter { Id = Guid.NewGuid(), Name = "TT", OwnerUserId = user.Id, Phone = phone, EmailVerifiedAt = DateTime.UtcNow };
        _centers.Centers.Add(center);
        return (user, center);
    }

    [Fact]
    public async Task ChangePhone_ValidNewNumber_UpdatesCenterAndUserWithoutAnyOtp()
    {
        var (user, center) = Setup();

        var result = await Service().UpdatePhoneAsync(user.Id, new RequestContactChangeDto("+84987654321"));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(user.Phone, center.Phone);
        Assert.NotEqual("0912345678", center.Phone);
        Assert.Empty(_users.Otps);   // không còn SMS OTP
        var log = Assert.Single(_audit.Entries, l => l.Action == "center.phone_changed");
        Assert.DoesNotContain(center.Phone!, log.DetailJson);   // số trong nhật ký được che
    }

    [Theory]
    [InlineData("0912345678")]   // trùng số hiện tại
    [InlineData("abc")]          // sai định dạng
    [InlineData("")]
    public async Task ChangePhone_SameNumberOrInvalidFormat_IsRejectedAndNothingChanges(string newValue)
    {
        var (user, center) = Setup();

        var result = await Service().UpdatePhoneAsync(user.Id, new RequestContactChangeDto(newValue));

        Assert.NotEqual(AuthError.None, result.Error);
        Assert.Equal("0912345678", center.Phone);
        Assert.DoesNotContain(_audit.Entries, l => l.Action == "center.phone_changed");
    }

    [Fact]
    public async Task ChangePhone_NumberUsedByAnotherAccount_IsRejected()
    {
        var (user, center) = Setup();
        _users.Users.Add(new User { Id = Guid.NewGuid(), Email = "khac@example.com", FullName = "K", Role = UserRoles.Student, Status = UserStatuses.Active, Phone = "0987654321" });

        var result = await Service().UpdatePhoneAsync(user.Id, new RequestContactChangeDto("0987654321"));

        Assert.NotEqual(AuthError.None, result.Error);
        Assert.Equal("0912345678", center.Phone);
    }
}
