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

    // ===== [UC-75 4b] Người đại diện bấm nút trong mail để hủy yêu cầu xóa =====

    private ChildAccountDeletionService CreateRestoreService() =>
        new(_repo, new ParentLinkService(_links, _repo, _emails, _auditLogs), new FakeUnitOfWork(), _auditLogs, _emails, _links, CreateService());

    /// <summary>Xóa theo yêu cầu phụ huynh rồi trả hai mã trong mail (đồng ý lại, chỉ hủy xóa).</summary>
    private async Task<(User Student, User Parent, string Reconsent, string RestoreOnly)> DeletedFamilyAsync(int studentAge = 14)
    {
        var (student, parent, _) = Family(studentAge);
        await CreateService().DeleteChildAccountByParentAsync(student.Id, Requester(parent));
        var codes = _emails.SentEmails.Single(e => e.Purpose == "parent_child_deletion_confirmed").Code.Split('|');
        _emails.SentEmails.Clear();
        _auditLogs.Entries.Clear();
        return (student, parent, codes[0], codes[1]);
    }

    [Fact]
    public async Task RestoreLookup_ShowsStudentModeAndDeletionDate()
    {
        var (student, _, reconsent, restoreOnly) = await DeletedFamilyAsync();
        var service = CreateRestoreService();

        var a = await service.GetRestoreInfoAsync(reconsent);
        var b = await service.GetRestoreInfoAsync(restoreOnly);

        Assert.Equal(student.FullName, a.Value!.StudentName);
        Assert.Equal(ParentRestoreModes.Reconsent, a.Value.Mode);
        Assert.True(a.Value.CanReconsent);
        Assert.Equal(student.DeletedAt!.Value.AddDays(30), a.Value.DeletionDate);
        Assert.Equal(ParentRestoreModes.RestoreOnly, b.Value!.Mode);
        Assert.Equal(ParentLinkError.NotFound, (await service.GetRestoreInfoAsync("ma-sai")).Error);
    }

    [Fact]
    public async Task RestoreReconsent_ReactivatesAndCreatesNewLinkWithRequesterOnly()
    {
        var (student, parent, reconsent, restoreOnly) = await DeletedFamilyAsync();
        var dad = AddUser("dad@test.com", UserRoles.Parent, 42);

        var result = await CreateRestoreService().RestoreAsync(reconsent, isGuardian: true, seenPolicyVersion: null, "1.2.3.4");

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(UserStatuses.Active, student.Status);
        Assert.Null(student.DeletedAt);
        Assert.Null(student.DeletionRequestedByEmail);
        var active = Assert.Single(_links.Links, l => l.Status == ParentLinkStatuses.Active);
        Assert.Equal(parent.Id, active.ParentUserId);
        Assert.NotNull(active.RevokeTokenHash);
        var consent = _links.Consents.Single(c => c.Id == active.ConsentId);
        Assert.True(consent.Granted);
        Assert.Equal("1.2.3.4", consent.GrantedIp);
        Assert.DoesNotContain(_links.Links, l => l.ParentUserId == dad.Id);
        Assert.Contains(_emails.SentEmails, e => e.To == parent.Email && e.Purpose == "parent_consent_confirmed");
        Assert.Contains(_emails.SentEmails, e => e.To == student.Email && e.Purpose == "child_restored_by_parent" && e.Code == "reconsent");
        var log = Assert.Single(_auditLogs.Entries);
        Assert.Equal(ChildAccountDeletionService.RestoreAuditAction, log.Action);
        Assert.Equal(parent.Id, log.ActorUserId);
        // Nút còn lại hết tác dụng
        Assert.Equal(ParentLinkError.NotFound, (await CreateRestoreService().RestoreAsync(restoreOnly, false, null, null)).Error);
    }

    [Fact]
    public async Task RestoreOnly_ReactivatesWithoutConsent_AndStudentCanLogin()
    {
        var (student, _, reconsent, restoreOnly) = await DeletedFamilyAsync();

        var result = await CreateRestoreService().RestoreAsync(restoreOnly, isGuardian: false, seenPolicyVersion: null, null);

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(UserStatuses.Active, student.Status);
        Assert.DoesNotContain(_links.Links, l => l.Status == ParentLinkStatuses.Active);
        Assert.False(await _links.HasGrantedConsentAsync(student.Id));
        Assert.Contains(_emails.SentEmails, e => e.To == student.Email && e.Purpose == "child_restored_by_parent" && e.Code == "restore_only");
        Assert.Equal(AuthError.None, (await CreateService().LoginAsync(new LoginRequest(student.Email, "password123"))).Error);
        Assert.Equal(ParentLinkError.NotFound, (await CreateRestoreService().GetRestoreInfoAsync(reconsent)).Error);
    }

    [Fact]
    public async Task RestoreReconsent_RequiresGuardianTick_AndCurrentPolicyVersion()
    {
        var (student, _, reconsent, _) = await DeletedFamilyAsync();
        var service = CreateRestoreService();

        var noTick = await service.RestoreAsync(reconsent, isGuardian: false, seenPolicyVersion: null, null);
        var oldPolicy = await service.RestoreAsync(reconsent, isGuardian: true, seenPolicyVersion: "ban-cu", null);

        Assert.Equal(ParentLinkError.Validation, noTick.Error);
        Assert.Equal(ParentLinkError.Validation, oldPolicy.Error);
        Assert.Equal(UserStatuses.Deleted, student.Status);
        // Mã chưa bị dùng mất khi bấm lỗi
        Assert.Equal(ParentLinkError.None, (await service.GetRestoreInfoAsync(reconsent)).Error);
    }

    [Fact]
    public async Task RestoreReconsent_StudentTurned16_Refused_RestoreOnlyStillWorks()
    {
        var (student, _, reconsent, restoreOnly) = await DeletedFamilyAsync();
        student.DateOfBirth = BornYearsAgo(16);
        var service = CreateRestoreService();

        Assert.False((await service.GetRestoreInfoAsync(reconsent)).Value!.CanReconsent);
        Assert.Equal(ParentLinkError.Forbidden, (await service.RestoreAsync(reconsent, true, null, null)).Error);
        Assert.Equal(ParentLinkError.None, (await service.RestoreAsync(restoreOnly, false, null, null)).Error);
        Assert.Equal(UserStatuses.Active, student.Status);
    }

    [Fact]
    public async Task Restore_AfterStudentSelfRestoredAt16_ParentButtonsDead()
    {
        var (student, _, reconsent, restoreOnly) = await DeletedFamilyAsync();
        student.DateOfBirth = BornYearsAgo(16);
        var auth = CreateService();
        var login = await auth.LoginAsync(new LoginRequest(student.Email, "password123"));
        await auth.RestoreAccountAsync(new RestoreAccountRequest(student.Email, login.PendingDeletion!.RestoreToken!));

        Assert.Equal(ParentLinkError.NotFound, (await CreateRestoreService().GetRestoreInfoAsync(reconsent)).Error);
        Assert.Equal(ParentLinkError.NotFound, (await CreateRestoreService().RestoreAsync(restoreOnly, false, null, null)).Error);
    }

    // ===== [UC-111 3a / UC-75 1a] Rút đồng ý kèm xóa tài khoản con =====

    [Fact]
    public async Task RevokeAndDeleteByToken_DeletesChild_RevokesConsent_SendsRestoreButtons()
    {
        var (student, parent, link) = Family();
        const string revokeToken = "ma-rut-dong-y";
        link.RevokeTokenHash = ParentInvite.HashToken(revokeToken);

        var result = await CreateRestoreService().RevokeAndDeleteByTokenAsync(revokeToken, "1.2.3.4");

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(UserStatuses.Deleted, student.Status);
        Assert.Equal(parent.Email, student.DeletionRequestedByEmail);
        Assert.Equal(ParentLinkStatuses.Revoked, _links.Links.Single(l => l.Id == link.Id).Status);
        Assert.False(await _links.HasGrantedConsentAsync(student.Id));
        Assert.Contains(_emails.SentEmails, e => e.To == parent.Email && e.Purpose == "parent_child_deletion_confirmed");
        // Link đã dùng thì hết tác dụng
        Assert.Equal(ParentLinkError.NotFound, (await CreateRestoreService().RevokeAndDeleteByTokenAsync(revokeToken, null)).Error);
    }

    [Fact]
    public async Task RevokeAndDeleteByToken_WrongToken_NotFound_Child16_Forbidden()
    {
        var (student, _, link) = Family(16);
        link.RevokeTokenHash = ParentInvite.HashToken("ma");
        var service = CreateRestoreService();

        Assert.Equal(ParentLinkError.NotFound, (await service.RevokeAndDeleteByTokenAsync("sai", null)).Error);
        Assert.Equal(ParentLinkError.Forbidden, (await service.RevokeAndDeleteByTokenAsync("ma", null)).Error);
        Assert.Equal(UserStatuses.Active, student.Status);
        Assert.Equal(ParentLinkStatuses.Active, link.Status);
    }

    [Fact]
    public async Task RevokeAndDelete_LoggedInParent_OwnActiveLinkOnly()
    {
        var (student, parent, link) = Family();
        var stranger = AddUser("stranger@test.com", UserRoles.Parent, 40);
        var service = CreateRestoreService();

        var byStranger = await service.RevokeAndDeleteAsync(stranger.Id, link.Id, null);
        Assert.Equal(ParentLinkError.NotFound, byStranger.Error);
        Assert.Equal(UserStatuses.Active, student.Status);

        var byParent = await service.RevokeAndDeleteAsync(parent.Id, link.Id, null);
        Assert.Equal(ParentLinkError.None, byParent.Error);
        Assert.Equal(UserStatuses.Deleted, student.Status);
        Assert.Equal(ParentLinkError.NotFound, (await service.RevokeAndDeleteAsync(parent.Id, link.Id, null)).Error);
    }

    [Fact]
    public async Task Restore_ExpiredToken_NotFound()
    {
        var (student, _, _, restoreOnly) = await DeletedFamilyAsync();
        foreach (var otp in _repo.Otps.Where(o => o.UserId == student.Id))
            otp.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        var result = await CreateRestoreService().RestoreAsync(restoreOnly, false, null, null);

        Assert.Equal(ParentLinkError.NotFound, result.Error);
        Assert.Equal(UserStatuses.Deleted, student.Status);
    }
}
