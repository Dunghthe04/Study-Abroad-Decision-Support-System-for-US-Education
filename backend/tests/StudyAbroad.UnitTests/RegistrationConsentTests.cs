using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Policies;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

internal sealed class FakePublishedPolicies : IPublishedPolicyReader
{
    private readonly Dictionary<string, List<PublishedPolicyHeader>> _byKind = [];

    public FakePublishedPolicies Add(string kind, string version, bool required = false)
    {
        if (!_byKind.TryGetValue(kind, out var list)) _byKind[kind] = list = [];
        list.Add(new PublishedPolicyHeader(version, required, $"Tiêu đề {kind}", null));
        return this;
    }

    public Task<IReadOnlyList<PublishedPolicyHeader>> ListPublishedAsync(string kind, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PublishedPolicyHeader>>(_byKind.TryGetValue(kind, out var l) ? l.ToList() : []);
}

internal sealed class FakeConsentStore : IConsentRepository
{
    public List<Consent> Rows { get; } = [];

    public Task<Consent?> GetLatestAsync(Guid userId, string purpose, CancellationToken ct = default) =>
        Task.FromResult(Rows.Where(c => c.UserId == userId && c.Purpose == purpose).OrderByDescending(c => c.CreatedAt).FirstOrDefault());

    public Task AddAsync(Consent consent, CancellationToken ct = default)
    {
        Rows.Add(consent);
        return Task.CompletedTask;
    }
}

// [UC-104] Đồng ý Điều khoản và Chính sách bảo mật lúc đăng ký: kiểm tra trước, ghi cùng lúc tạo tài khoản.
public class RegistrationConsentTests
{
    private const string Terms = PolicyKinds.TermsOfUse;
    private const string Privacy = PolicyKinds.PrivacyPolicy;

    private readonly FakePublishedPolicies _published = new();
    private readonly FakeConsentStore _consents = new();

    private RegistrationConsent Create() => new(_published, _consents);

    private static List<AcceptPolicyItem> Both(string v = "2026-11") => [new(Terms, v), new(Privacy, v)];

    // ------------------------------------------------------------ kiểm tra

    [Fact]
    public async Task Both_current_versions_are_valid()
    {
        _published.Add(Terms, "2026-11").Add(Privacy, "2026-11");
        Assert.Null(await Create().ValidateAsync(Both()));
    }

    [Fact]
    public async Task Current_version_is_the_newest_even_when_older_ones_exist()
    {
        _published.Add(Terms, "2026-11").Add(Terms, "2026-11-02", required: true).Add(Privacy, "2026-11");
        Assert.Null(await Create().ValidateAsync([new(Terms, "2026-11-02"), new(Privacy, "2026-11")]));
        Assert.Equal(PolicyAcceptanceService.PolicyChangedMessage, await Create().ValidateAsync(Both("2026-11")));
    }

    [Fact]
    public async Task Nothing_published_and_nothing_sent_is_valid()
    {
        Assert.Null(await Create().ValidateAsync(null));
        Assert.Null(await Create().ValidateAsync([]));
    }

    [Fact]
    public async Task Nothing_sent_while_documents_are_published_is_refused()
    {
        _published.Add(Terms, "2026-11").Add(Privacy, "2026-11");
        Assert.Equal(RegistrationConsent.MissingMessage, await Create().ValidateAsync(null));
        Assert.Equal(RegistrationConsent.MissingMessage, await Create().ValidateAsync([]));
    }

    [Fact]
    public async Task Only_one_of_two_published_documents_is_refused()
    {
        _published.Add(Terms, "2026-11").Add(Privacy, "2026-11");
        Assert.Equal(RegistrationConsent.MissingMessage, await Create().ValidateAsync([new(Terms, "2026-11")]));
    }

    [Fact]
    public async Task A_blank_version_counts_as_missing()
    {
        _published.Add(Terms, "2026-11").Add(Privacy, "2026-11");
        Assert.Equal(RegistrationConsent.MissingMessage, await Create().ValidateAsync([new(Terms, " "), new(Privacy, "2026-11")]));
    }

    [Fact]
    public async Task A_version_that_is_no_longer_current_must_be_read_again()
    {
        _published.Add(Terms, "2026-11-02").Add(Privacy, "2026-11");
        Assert.Equal(PolicyAcceptanceService.PolicyChangedMessage, await Create().ValidateAsync(Both("2026-11")));
    }

    [Fact]
    public async Task An_unpublished_kind_is_not_required_but_a_version_for_it_is_refused()
    {
        _published.Add(Terms, "2026-11");   // chính sách bảo mật chưa công bố
        Assert.Null(await Create().ValidateAsync([new(Terms, "2026-11")]));
        Assert.Equal(PolicyAcceptanceService.PolicyChangedMessage, await Create().ValidateAsync(Both()));
    }

    [Theory]
    [InlineData(PolicyKinds.ChildDataConsent)]
    [InlineData(PolicyKinds.ForumRules)]
    [InlineData("khong_co")]
    public async Task Other_kinds_are_not_accepted_here(string kind)
    {
        _published.Add(Terms, "2026-11").Add(Privacy, "2026-11");
        Assert.Equal(RegistrationConsent.InvalidMessage, await Create().ValidateAsync([.. Both(), new AcceptPolicyItem(kind, "2026-11")]));
    }

    [Fact]
    public async Task The_same_kind_twice_is_refused()
    {
        _published.Add(Terms, "2026-11").Add(Privacy, "2026-11");
        Assert.Equal(RegistrationConsent.InvalidMessage, await Create().ValidateAsync([.. Both(), new AcceptPolicyItem(Terms, "2026-11")]));
    }

    // ------------------------------------------------------------ ghi

    [Fact]
    public async Task Recording_writes_one_row_per_document_with_version_and_ip()
    {
        var user = Guid.NewGuid();

        await Create().RecordAsync(user, Both(), "198.51.100.4");

        Assert.Equal([Terms, Privacy], _consents.Rows.Select(c => c.Purpose));
        Assert.All(_consents.Rows, c =>
        {
            Assert.Equal(user, c.UserId);
            Assert.True(c.Granted);
            Assert.Equal("2026-11", c.PolicyVersion);
            Assert.Equal("198.51.100.4", c.GrantedIp);
        });
    }

    [Fact]
    public async Task Recording_nothing_writes_nothing()
    {
        await Create().RecordAsync(Guid.NewGuid(), null, null);
        Assert.Empty(_consents.Rows);
    }
}

// [UC-104] Đăng ký học sinh / phụ huynh qua AuthService: không đồng ý thì không có tài khoản.
public class AuthRegisterConsentTests
{
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;
        public bool VerifyPassword(string hash, string password) => hash == "hashed:" + password;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public (string Token, DateTime ExpiresAt) GenerateToken(User user) => ("token:" + user.Email, DateTime.UtcNow.AddDays(7));
        public string HashToken(string token) => "hash:" + token;
    }

    private readonly FakeUserRepository _repo = new();
    private readonly FakeEmailSender _emailSender = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeAuditLogRepository _auditLogs = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakePublishedPolicies _published = new();
    private readonly FakeConsentStore _consents = new();

    private AuthService CreateService() => new(
        _repo, new FakeHasher(), new FakeTokenService(), _emailSender, _links, _auditLogs,
        new ParentLinkService(_links, _repo, _emailSender, _auditLogs), _unitOfWork, new FakeLeadWithdrawalRepository(),
        registrationConsent: new RegistrationConsent(_published, _consents));

    private static RegisterRequest Request(IReadOnlyList<AcceptPolicyItem>? accepted, string email = "a@test.com") =>
        new(email, "password123", "Nguyen Van A", UserRoles.Parent,
            AgePolicy.TodayInVietnam(DateTime.UtcNow).AddYears(-30).AddDays(-10), AcceptedPolicies: accepted);

    private static List<AcceptPolicyItem> Both(string v = "2026-11") =>
        [new(PolicyKinds.TermsOfUse, v), new(PolicyKinds.PrivacyPolicy, v)];

    private void PublishBoth(string v = "2026-11") =>
        _published.Add(PolicyKinds.TermsOfUse, v).Add(PolicyKinds.PrivacyPolicy, v);

    [Fact]
    public async Task Register_WithConsent_CreatesTheAccountAndRecordsTheEvidenceForIt()
    {
        PublishBoth();

        var result = await CreateService().RegisterAsync(Request(Both()), ipAddress: "203.0.113.5");

        Assert.Equal(AuthError.None, result.Error);
        var user = Assert.Single(_repo.Users);
        Assert.Equal(2, _consents.Rows.Count);
        Assert.All(_consents.Rows, c =>
        {
            Assert.Equal(user.Id, c.UserId);          // bằng chứng gắn đúng tài khoản vừa tạo
            Assert.Equal("2026-11", c.PolicyVersion);
            Assert.Equal("203.0.113.5", c.GrantedIp);
        });
        Assert.Single(_emailSender.SentEmails);
        Assert.Equal(1, _unitOfWork.Calls);           // tài khoản và bằng chứng cùng một giao dịch
    }

    [Fact]
    public async Task Register_WithoutConsent_CreatesNothingAndSendsNoEmail()
    {
        PublishBoth();

        var result = await CreateService().RegisterAsync(Request(null));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Equal(RegistrationConsent.MissingMessage, result.Message);
        Assert.Empty(_repo.Users);
        Assert.Empty(_consents.Rows);
        Assert.Empty(_emailSender.SentEmails);
    }

    [Fact]
    public async Task Register_WithAnOldVersion_IsRefusedSoTheUserReadsTheNewText()
    {
        PublishBoth("2026-11-02");

        var result = await CreateService().RegisterAsync(Request(Both("2026-11")));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Equal(PolicyAcceptanceService.PolicyChangedMessage, result.Message);
        Assert.Empty(_repo.Users);
        Assert.Empty(_consents.Rows);
    }

    [Fact]
    public async Task Register_WhenNothingIsPublished_StillWorks()
    {
        var result = await CreateService().RegisterAsync(Request(null));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Single(_repo.Users);
        Assert.Empty(_consents.Rows);
    }

    [Fact]
    public async Task Register_ExistingEmail_GetsTheSameConsentErrorAndNoNotice()
    {
        // Lỗi đồng ý không phụ thuộc email đã tồn tại hay chưa, nên không dò được tài khoản qua nó
        PublishBoth();
        _repo.Users.Add(new User { Email = "a@test.com", FullName = "Có sẵn", Role = UserRoles.Parent, Status = UserStatuses.Active, PasswordHash = "x" });

        var result = await CreateService().RegisterAsync(Request(null));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_emailSender.SentEmails);
    }

    [Fact]
    public async Task Register_ExistingEmail_WithConsent_WritesNoEvidence()
    {
        PublishBoth();
        _repo.Users.Add(new User { Email = "a@test.com", FullName = "Có sẵn", Role = UserRoles.Parent, Status = UserStatuses.Active, PasswordHash = "x" });

        var result = await CreateService().RegisterAsync(Request(Both()));

        Assert.Equal(AuthError.None, result.Error);   // cùng thông báo như đăng ký mới (chống dò tài khoản)
        Assert.Empty(_consents.Rows);                 // không tạo tài khoản mới nên không có bằng chứng nào để ghi
    }
}
