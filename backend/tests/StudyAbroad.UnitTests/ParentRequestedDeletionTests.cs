using StudyAbroad.Application.Auth;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-75 1a/4b/4c, UC-111 3a] Người đại diện rút đồng ý và yêu cầu xóa tài khoản của con dưới 16.</summary>
public class ParentRequestedDeletionTests
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
    private readonly FakeEmailSender _emails = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeAuditLogRepository _auditLogs = new();

    private AuthService CreateService() => new(
        _repo, new FakeHasher(), new FakeTokenService(), _emails, _links, _auditLogs,
        new ParentLinkService(_links, _repo, _emails, _auditLogs), new FakeUnitOfWork(), new FakeLeadWithdrawalRepository(),
        accountDeletion: new AccountDeletionOptions { RetentionPeriod = TimeSpan.FromDays(30) });

    private static DateOnly BornYearsAgo(int years) =>
        AgePolicy.TodayInVietnam(DateTime.UtcNow).AddDays(-10).AddYears(-years);

    private User AddUser(string email, string role, int age)
    {
        var user = new User
        {
            Email = email,
            FullName = "User " + email,
            Role = role,
            Status = UserStatuses.Active,
            PasswordHash = "hashed:password123",
            DateOfBirth = BornYearsAgo(age),
        };
        _repo.Users.Add(user);
        return user;
    }

    private ParentStudentLink AddActiveLink(User student, User parent)
    {
        var consent = new Consent { UserId = student.Id, Purpose = "parent_consent", Granted = true, GrantedByUserId = parent.Id };
        _links.Consents.Add(consent);
        var link = new ParentStudentLink
        {
            StudentUserId = student.Id,
            ParentEmail = parent.Email,
            ParentUserId = parent.Id,
            Status = ParentLinkStatuses.Active,
            AcceptedAt = DateTime.UtcNow.AddDays(-5),
            ConsentId = consent.Id,
        };
        _links.Links.Add(link);
        return link;
    }

    private (User Student, User Parent, ParentStudentLink Link) Family(int studentAge = 14)
    {
        var student = AddUser("kid@test.com", UserRoles.Student, studentAge);
        var parent = AddUser("mom@test.com", UserRoles.Parent, 40);
        return (student, parent, AddActiveLink(student, parent));
    }

    private static ParentDeletionRequester Requester(User parent) => new(parent.Email, parent.Id);

    [Fact]
    public async Task Delete_Under16_SoftDeletes_RevokesAsParent_AndEmailsBoth()
    {
        var (student, parent, link) = Family();

        var result = await CreateService().DeleteChildAccountByParentAsync(student.Id, Requester(parent));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Deleted, student.Status);
        Assert.Equal(parent.Email, student.DeletionRequestedByEmail);
        var stored = _links.Links.Single(l => l.Id == link.Id);
        Assert.Equal(ParentLinkStatuses.Revoked, stored.Status);
        Assert.Equal(parent.Id, stored.RevokedByUserId);
        Assert.False(_links.Consents.Single().Granted);
        Assert.Contains(_emails.SentEmails, e => e.To == student.Email && e.Purpose == "child_deleted_by_parent");
        Assert.Contains(_emails.SentEmails, e => e.To == parent.Email && e.Purpose == "parent_child_deletion_confirmed");
        // Người yêu cầu đã nhận mail xác nhận riêng, không nhận thêm mail "đã hủy liên kết"
        Assert.DoesNotContain(_emails.SentEmails, e => e.To == parent.Email && e.Purpose == "parent_unlinked");
        var log = Assert.Single(_auditLogs.Entries);
        Assert.Equal("user.delete_account_by_parent", log.Action);
        Assert.Equal(parent.Id, log.ActorUserId);
        Assert.DoesNotContain(parent.Email, log.DetailJson);
    }

    [Fact]
    public async Task Delete_IssuesTwoHashedTokens_ExpiringAtDeletionDate()
    {
        var (student, parent, _) = Family();

        await CreateService().DeleteChildAccountByParentAsync(student.Id, Requester(parent));

        var codes = _emails.SentEmails.Single(e => e.Purpose == "parent_child_deletion_confirmed").Code.Split('|');
        var reconsent = Assert.Single(_repo.Otps, o => o.Purpose == OtpPurposes.ParentRestoreReconsent);
        var restoreOnly = Assert.Single(_repo.Otps, o => o.Purpose == OtpPurposes.ParentRestoreOnly);
        Assert.Equal(AuthService.HashOtp(codes[0]), reconsent.CodeHash);
        Assert.Equal(AuthService.HashOtp(codes[1]), restoreOnly.CodeHash);
        Assert.Equal(student.DeletedAt!.Value.AddDays(30), reconsent.ExpiresAt);
        Assert.Equal(reconsent.ExpiresAt, restoreOnly.ExpiresAt);
    }

    [Fact]
    public async Task Delete_OtherParentStillGetsUnlinkNotice()
    {
        var (student, parent, _) = Family();
        var dad = AddUser("dad@test.com", UserRoles.Parent, 42);
        var dadLink = AddActiveLink(student, dad);

        await CreateService().DeleteChildAccountByParentAsync(student.Id, Requester(parent));

        Assert.Equal(ParentLinkStatuses.Revoked, _links.Links.Single(l => l.Id == dadLink.Id).Status);
        Assert.Contains(_emails.SentEmails, e => e.To == dad.Email && e.Purpose == "parent_unlinked");
    }

    [Fact]
    public async Task Delete_Student16OrOlder_Refused()
    {
        var (student, parent, link) = Family(studentAge: 16);

        var result = await CreateService().DeleteChildAccountByParentAsync(student.Id, Requester(parent));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Equal(UserStatuses.Active, student.Status);
        Assert.Equal(ParentLinkStatuses.Active, _links.Links.Single(l => l.Id == link.Id).Status);
        Assert.Empty(_emails.SentEmails);
    }

    [Fact]
    public async Task Delete_AlreadyPendingDeletion_Refused()
    {
        var (student, parent, _) = Family();
        var service = CreateService();
        await service.DeleteChildAccountByParentAsync(student.Id, Requester(parent));

        var second = await service.DeleteChildAccountByParentAsync(student.Id, Requester(parent));

        Assert.Equal(AuthError.Validation, second.Error);
    }

    [Fact]
    public async Task Login_Under16_DeletedByParent_NoRestoreToken()
    {
        var (student, parent, _) = Family();
        var service = CreateService();
        await service.DeleteChildAccountByParentAsync(student.Id, Requester(parent));

        var login = await service.LoginAsync(new LoginRequest(student.Email, "password123"));

        Assert.Equal(AuthError.PendingDeletion, login.Error);
        Assert.True(login.PendingDeletion!.RequestedByParent);
        Assert.Null(login.PendingDeletion.RestoreToken);
        Assert.Contains("người đại diện", login.Message);
        Assert.DoesNotContain(_repo.Otps, o => o.Purpose == OtpPurposes.RestoreAccount);
    }

    [Fact]
    public async Task Restore_Under16_DeletedByParent_RefusedEvenWithToken()
    {
        var (student, parent, _) = Family();
        var service = CreateService();
        await service.DeleteChildAccountByParentAsync(student.Id, Requester(parent));
        // Mã khôi phục lọt ra bằng cách nào đó vẫn không dùng được khi còn dưới 16
        var leaked = "ma-khoi-phuc";
        await _repo.SaveOtpAsync(new OtpToken
        {
            UserId = student.Id, Purpose = OtpPurposes.RestoreAccount, CodeHash = AuthService.HashOtp(leaked),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        });

        var result = await service.RestoreAccountAsync(new RestoreAccountRequest(student.Email, leaked));

        Assert.Equal(AuthError.OtpInvalid, result.Error);
        Assert.Equal(UserStatuses.Deleted, student.Status);
    }

    [Fact]
    public async Task TurnedSixteenWhileWaiting_CanSelfRestore_AndParentButtonsStopWorking()
    {
        var (student, parent, _) = Family();
        var service = CreateService();
        await service.DeleteChildAccountByParentAsync(student.Id, Requester(parent));
        student.DateOfBirth = BornYearsAgo(16);

        var login = await service.LoginAsync(new LoginRequest(student.Email, "password123"));
        Assert.False(login.PendingDeletion!.RequestedByParent);
        var result = await service.RestoreAccountAsync(new RestoreAccountRequest(student.Email, login.PendingDeletion.RestoreToken!));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Active, student.Status);
        Assert.Null(student.DeletionRequestedByEmail);
        Assert.All(_repo.Otps.Where(o => o.Purpose is OtpPurposes.ParentRestoreReconsent or OtpPurposes.ParentRestoreOnly),
            o => Assert.NotNull(o.UsedAt));
    }

    [Fact]
    public async Task SelfDelete_StillLeavesColumnEmpty_AndCanSelfRestore()
    {
        var (student, _, _) = Family();
        var service = CreateService();

        await service.AnonymizeUserAccountAsync(student.Id);
        var login = await service.LoginAsync(new LoginRequest(student.Email, "password123"));

        Assert.Null(student.DeletionRequestedByEmail);
        Assert.False(login.PendingDeletion!.RequestedByParent);
        Assert.False(string.IsNullOrEmpty(login.PendingDeletion.RestoreToken));
        Assert.Equal("user.delete_account", _auditLogs.Entries.Single().Action);
    }
}
