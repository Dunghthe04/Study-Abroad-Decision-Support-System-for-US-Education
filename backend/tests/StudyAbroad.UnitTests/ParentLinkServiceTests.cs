using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class ParentLinkServiceTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakeEmailSender _emails = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeAuditLogRepository _audit = new();

    /// <summary>Token thu hồi gốc lấy từ mail xác nhận gửi phụ huynh trong AcceptedLinkAsync (UC-117).</summary>
    private string _revokeToken = "";

    private ParentLinkService CreateService() => new(_links, _users, _emails, _audit);

    private static DateOnly BornYearsAgo(int years) =>
        AgePolicy.TodayInVietnam(DateTime.UtcNow).AddDays(-10).AddYears(-years);

    private User AddUser(string email, string role, int? age)
    {
        var user = new User
        {
            Email = email,
            FullName = "User " + email,
            Role = role,
            Status = UserStatuses.Active,
            PasswordHash = "x",
            DateOfBirth = age is { } a ? BornYearsAgo(a) : null,
        };
        _users.Users.Add(user);
        return user;
    }

    // Lời mời đang chờ, đã gửi 1 lần; trả token gốc như trong mail
    private (ParentStudentLink Link, string Token) AddPendingLink(User student, string parentEmail = "me@test.com", DateTime? sentAt = null)
    {
        var link = new ParentStudentLink { StudentUserId = student.Id, ParentEmail = parentEmail };
        var token = ParentInvite.Issue(link, sentAt ?? DateTime.UtcNow.AddMinutes(-5));
        _links.Links.Add(link);
        return (link, token);
    }

    private const string ParentConsentConfirmedPurpose = "parent_consent_confirmed";

    private ParentStudentLink Stored(Guid id) => _links.Links.Single(l => l.Id == id);

    private async Task<(User Student, User Parent, ParentStudentLink Link)> AcceptedLinkAsync(int studentAge)
    {
        var student = AddUser("kid@test.com", UserRoles.Student, studentAge);
        var parent = AddUser("me@test.com", UserRoles.Parent, 40);
        var (link, token) = AddPendingLink(student);
        var result = await CreateService().AcceptAsync(token, isGuardian: true, "1.2.3.4");
        Assert.Equal(ParentLinkError.None, result.Error);
        _revokeToken = _emails.SentEmails.Single(e => e.Purpose == ParentConsentConfirmedPurpose).Code;
        _emails.SentEmails.Clear();
        return (student, parent, Stored(link.Id));
    }

    [Fact]
    public async Task GetInvite_ValidToken_ReturnsStudentInfo()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (_, token) = AddPendingLink(student);

        var result = await CreateService().GetInviteAsync(token);

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(student.FullName, result.Value!.StudentName);
        Assert.Equal(14, result.Value.StudentAge);
        Assert.Equal("me@test.com", result.Value.ParentEmail);
        Assert.Equal(ConsentPurposes.ChildDataPolicyVersion, result.Value.PolicyVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("wrong-token")]
    public async Task GetInvite_InvalidToken_NotFound(string token)
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        AddPendingLink(student);

        var result = await CreateService().GetInviteAsync(token);

        Assert.Equal(ParentLinkError.NotFound, result.Error);
    }

    [Fact]
    public async Task GetInvite_ExpiredToken_Expired()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (_, token) = AddPendingLink(student, sentAt: DateTime.UtcNow.AddDays(-ParentInvite.ExpiryDays - 1));

        var result = await CreateService().GetInviteAsync(token);

        Assert.Equal(ParentLinkError.Expired, result.Error);
    }

    // ===== [UC-115] Phụ huynh đồng ý / từ chối qua link trong mail, không cần tài khoản =====

    [Fact]
    public async Task Accept_NoParentAccount_CreatesConsentWithEmail_AndActivatesLink()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, token) = AddPendingLink(student);

        var result = await CreateService().AcceptAsync(token, isGuardian: true, "1.2.3.4");

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(ParentInviteStates.Accepted, result.Value!.State);
        var consent = Assert.Single(_links.Consents);
        Assert.Equal(student.Id, consent.UserId);
        Assert.Equal(ConsentPurposes.ChildDataProcessing, consent.Purpose);
        Assert.True(consent.Granted);
        Assert.Equal(ConsentPurposes.ChildDataPolicyVersion, consent.PolicyVersion);
        Assert.Null(consent.GrantedByUserId);
        Assert.Equal("me@test.com", consent.GrantedByEmail);
        Assert.Equal("1.2.3.4", consent.GrantedIp);

        var stored = Stored(link.Id);
        Assert.Equal(ParentLinkStatuses.Active, stored.Status);
        Assert.Null(stored.ParentUserId);
        Assert.Equal(consent.Id, stored.ConsentId);
        Assert.NotNull(stored.AcceptedAt);
        // Giữ hash để mở lại link vẫn thấy kết quả
        Assert.NotNull(stored.TokenHash);
        Assert.True(await _links.HasGrantedConsentAsync(student.Id));

        Assert.Contains(_emails.SentEmails, e => e.To == student.Email && e.Purpose == "parent_linked");
        Assert.Contains(_emails.SentEmails, e => e.To == "me@test.com" && e.Purpose == ParentConsentConfirmedPurpose);
    }

    [Fact]
    public async Task Accept_EmailIsParentAccount_LinksAccount()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var parent = AddUser("me@test.com", UserRoles.Parent, 40);
        var (link, token) = AddPendingLink(student);

        await CreateService().AcceptAsync(token, isGuardian: true, null);

        Assert.Equal(parent.Id, Stored(link.Id).ParentUserId);
        Assert.Equal(parent.Id, Assert.Single(_links.Consents).GrantedByUserId);
    }

    [Fact]
    public async Task Accept_NotGuardian_Validation()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, token) = AddPendingLink(student);

        var result = await CreateService().AcceptAsync(token, isGuardian: false, null);

        Assert.Equal(ParentLinkError.Validation, result.Error);
        Assert.Empty(_links.Consents);
        Assert.Equal(ParentLinkStatuses.Pending, Stored(link.Id).Status);
    }

    [Fact]
    public async Task Reject_SetsRejected_RecordsConsentFalse_AndEmailsStudent()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, token) = AddPendingLink(student);

        var result = await CreateService().RejectAsync(token, "1.2.3.4");

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(ParentInviteStates.Rejected, result.Value!.State);
        var stored = Stored(link.Id);
        Assert.Equal(ParentLinkStatuses.Rejected, stored.Status);
        Assert.Null(stored.RevokeTokenHash);
        var consent = Assert.Single(_links.Consents);
        Assert.False(consent.Granted);
        Assert.Equal("me@test.com", consent.GrantedByEmail);
        Assert.False(await _links.HasGrantedConsentAsync(student.Id));
        var mail = Assert.Single(_emails.SentEmails);
        Assert.Equal(student.Email, mail.To);
        Assert.Equal("parent_rejected", mail.Purpose);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UsedLink_LookupShowsResult_SecondAnswerConflict(bool acceptFirst)
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (_, token) = AddPendingLink(student);
        var service = CreateService();
        if (acceptFirst)
            await service.AcceptAsync(token, isGuardian: true, null);
        else
            await service.RejectAsync(token, null);

        var info = await service.GetInviteAsync(token);
        var again = await service.AcceptAsync(token, isGuardian: true, null);
        var againReject = await service.RejectAsync(token, null);

        Assert.Equal(ParentLinkError.None, info.Error);
        Assert.Equal(acceptFirst ? ParentInviteStates.Accepted : ParentInviteStates.Rejected, info.Value!.State);
        Assert.NotNull(info.Value.DecidedAt);
        Assert.Equal(ParentLinkError.Conflict, again.Error);
        Assert.Equal(ParentLinkError.Conflict, againReject.Error);
        Assert.Single(_links.Consents);
    }

    [Fact]
    public async Task Accept_Expired_NoConsent_AndLinkMarkedExpired()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, token) = AddPendingLink(student, sentAt: DateTime.UtcNow.AddDays(-ParentInvite.ExpiryDays - 1));

        var result = await CreateService().AcceptAsync(token, isGuardian: true, null);

        Assert.Equal(ParentLinkError.Expired, result.Error);
        Assert.Empty(_links.Consents);
        Assert.Equal(ParentLinkStatuses.Expired, Stored(link.Id).Status);
    }

    [Fact]
    public async Task Accept_ConcurrentWrite_Conflict()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, token) = AddPendingLink(student);
        _links.ConflictsToThrow = 1;

        var result = await CreateService().AcceptAsync(token, isGuardian: true, null);

        Assert.Equal(ParentLinkError.Conflict, result.Error);
        Assert.Empty(_links.Consents);
        Assert.Equal(ParentLinkStatuses.Pending, Stored(link.Id).Status);
        Assert.DoesNotContain(_emails.SentEmails, e => e.Purpose == "parent_linked");
    }

    [Fact]
    public async Task Resend_AfterReject_BackToPending_NewTokenWorks()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, token) = AddPendingLink(student, sentAt: DateTime.UtcNow.AddMinutes(-5));
        var service = CreateService();
        await service.RejectAsync(token, null);
        _emails.SentEmails.Clear();

        var result = await service.ResendAsync(student.Id);

        Assert.Equal(ParentLinkError.None, result.Error);
        var stored = Stored(link.Id);
        Assert.Equal(ParentLinkStatuses.Pending, stored.Status);
        Assert.Null(stored.ConsentId);
        var sent = Assert.Single(_emails.SentEmails);
        Assert.Equal(ParentLinkError.None, (await service.AcceptAsync(sent.Code, isGuardian: true, null)).Error);
        Assert.Equal(ParentLinkStatuses.Active, Stored(link.Id).Status);
        // Consent từ chối lần trước vẫn giữ làm bằng chứng
        Assert.Equal(2, _links.Consents.Count);
    }

    [Fact]
    public async Task Resend_AfterExpiry_BackToPending()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, token) = AddPendingLink(student, sentAt: DateTime.UtcNow.AddDays(-ParentInvite.ExpiryDays - 1));
        var service = CreateService();
        await service.GetInviteAsync(token);
        Assert.Equal(ParentLinkStatuses.Expired, Stored(link.Id).Status);

        var result = await service.ResendAsync(student.Id);

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(ParentLinkStatuses.Pending, Stored(link.Id).Status);
    }

    [Fact]
    public async Task ChangeParentEmail_AfterReject_RevokesRejectedLink()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (old, token) = AddPendingLink(student);
        await CreateService().RejectAsync(token, null);

        var result = await CreateService().ChangeParentEmailAsync(student.Id, "dad@test.com");

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(ParentLinkStatuses.Revoked, Stored(old.Id).Status);
        Assert.Single(_links.Links, l => l.Status == ParentLinkStatuses.Pending);
    }

    [Fact]
    public async Task Revoke_ByParent_SetsConsentFalse_AndNotifiesStudent()
    {
        var (student, parent, link) = await AcceptedLinkAsync(studentAge: 14);

        var result = await CreateService().RevokeAsync(parent.Id, link.Id);

        Assert.Equal(ParentLinkError.None, result.Error);
        var stored = Stored(link.Id);
        Assert.Equal(ParentLinkStatuses.Revoked, stored.Status);
        Assert.Equal(parent.Id, stored.RevokedByUserId);
        Assert.NotNull(stored.RevokedAt);
        Assert.False(Assert.Single(_links.Consents).Granted);
        Assert.Contains(_emails.SentEmails, e => e.To == student.Email && e.Purpose == "parent_unlinked");
    }

    [Fact]
    public async Task Revoke_StudentUnder16_CannotRevokeActiveLink()
    {
        var (student, _, link) = await AcceptedLinkAsync(studentAge: 14);

        var result = await CreateService().RevokeAsync(student.Id, link.Id);

        Assert.Equal(ParentLinkError.Forbidden, result.Error);
        Assert.Equal(ParentLinkStatuses.Active, Stored(link.Id).Status);
        Assert.True(Assert.Single(_links.Consents).Granted);
    }

    [Fact]
    public async Task Revoke_Student16Plus_CanRevoke_AndNotifiesParent()
    {
        var (student, parent, link) = await AcceptedLinkAsync(studentAge: 16);

        var result = await CreateService().RevokeAsync(student.Id, link.Id);

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(ParentLinkStatuses.Revoked, Stored(link.Id).Status);
        Assert.Contains(_emails.SentEmails, e => e.To == parent.Email && e.Purpose == "parent_unlinked");
    }

    [Fact]
    public async Task Revoke_StudentUnder16_CanCancelPendingInvite()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, _) = AddPendingLink(student);

        var result = await CreateService().RevokeAsync(student.Id, link.Id);

        Assert.Equal(ParentLinkError.None, result.Error);
        var stored = Stored(link.Id);
        Assert.Equal(ParentLinkStatuses.Revoked, stored.Status);
        Assert.Null(stored.TokenHash);
        Assert.Empty(_emails.SentEmails);
    }

    [Fact]
    public async Task RevokeAllForAccountDeletion_ParentSide_RevokesActiveLink_AndReturnsNoticeForStudent()
    {
        var (student, parent, link) = await AcceptedLinkAsync(studentAge: 14);

        var notices = await CreateService().RevokeAllForAccountDeletionAsync(parent.Id);

        var stored = Stored(link.Id);
        Assert.Equal(ParentLinkStatuses.Revoked, stored.Status);
        Assert.Equal(parent.Id, stored.RevokedByUserId);
        Assert.False(Assert.Single(_links.Consents).Granted);
        var notice = Assert.Single(notices);
        Assert.Equal(student.Email, notice.ToEmail);
        Assert.Equal(student.FullName, notice.StudentName);
        Assert.Equal(parent.FullName, notice.ParentName);
        // Không tự gửi mail: người gọi gửi sau khi transaction commit
        Assert.Empty(_emails.SentEmails);
    }

    [Fact]
    public async Task RevokeAllForAccountDeletion_StudentPendingInvite_RevokedWithoutNotice()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, _) = AddPendingLink(student);

        var notices = await CreateService().RevokeAllForAccountDeletionAsync(student.Id);

        Assert.Equal(ParentLinkStatuses.Revoked, Stored(link.Id).Status);
        Assert.Empty(notices);
    }

    [Fact]
    public async Task Revoke_OtherUser_NotFound()
    {
        var (_, _, link) = await AcceptedLinkAsync(studentAge: 14);
        var stranger = AddUser("x@test.com", UserRoles.Parent, 40);

        var result = await CreateService().RevokeAsync(stranger.Id, link.Id);

        Assert.Equal(ParentLinkError.NotFound, result.Error);
        Assert.Equal(ParentLinkStatuses.Active, Stored(link.Id).Status);
    }

    [Fact]
    public async Task Revoke_AlreadyRevoked_NotFound()
    {
        var (_, parent, link) = await AcceptedLinkAsync(studentAge: 14);
        var service = CreateService();
        await service.RevokeAsync(parent.Id, link.Id);

        var again = await service.RevokeAsync(parent.Id, link.Id);

        Assert.Equal(ParentLinkError.NotFound, again.Error);
    }

    [Fact]
    public async Task Resend_IssuesNewToken_OldTokenInvalid()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, oldToken) = AddPendingLink(student);
        var service = CreateService();

        var result = await service.ResendAsync(student.Id);

        Assert.Equal(ParentLinkError.None, result.Error);
        var sent = Assert.Single(_emails.SentEmails);
        Assert.Equal("me@test.com", sent.To);
        Assert.Equal("parent_invite", sent.Purpose);
        Assert.Equal(2, Stored(link.Id).InviteSentCount);
        Assert.Equal(ParentLinkError.NotFound, (await service.GetInviteAsync(oldToken)).Error);
        Assert.Equal(ParentLinkError.None, (await service.GetInviteAsync(sent.Code)).Error);
    }

    [Fact]
    public async Task Resend_WithinCooldown_TooManyRequests()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        AddPendingLink(student, sentAt: DateTime.UtcNow.AddSeconds(-10));

        var result = await CreateService().ResendAsync(student.Id);

        Assert.Equal(ParentLinkError.TooManyRequests, result.Error);
        Assert.Empty(_emails.SentEmails);
    }

    [Fact]
    public async Task Resend_DailyLimit_TooManyRequests()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, _) = AddPendingLink(student);
        link.InviteSentCount = ParentInvite.MaxSendsPerDay;

        var result = await CreateService().ResendAsync(student.Id);

        Assert.Equal(ParentLinkError.TooManyRequests, result.Error);
    }

    [Fact]
    public async Task Resend_After24h_CounterResets()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, _) = AddPendingLink(student, sentAt: DateTime.UtcNow.AddDays(-2));
        link.InviteSentCount = ParentInvite.MaxSendsPerDay;

        var result = await CreateService().ResendAsync(student.Id);

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(1, Stored(link.Id).InviteSentCount);
    }

    [Fact]
    public async Task Resend_NoPendingInvite_NotFound()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);

        var result = await CreateService().ResendAsync(student.Id);

        Assert.Equal(ParentLinkError.NotFound, result.Error);
    }

    [Fact]
    public async Task ChangeParentEmail_RevokesOldPending_AndInvitesNewEmail()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (old, _) = AddPendingLink(student);

        var result = await CreateService().ChangeParentEmailAsync(student.Id, "  Dad@Test.com ");

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(ParentLinkStatuses.Revoked, Stored(old.Id).Status);
        Assert.Null(Stored(old.Id).TokenHash);
        var created = Assert.Single(_links.Links, l => l.Status == ParentLinkStatuses.Pending);
        Assert.Equal("dad@test.com", created.ParentEmail);
        Assert.Equal(1, created.InviteSentCount);
        var sent = Assert.Single(_emails.SentEmails);
        Assert.Equal("dad@test.com", sent.To);
        Assert.Equal(ParentInvite.HashToken(sent.Code), created.TokenHash);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("kid@test.com")]
    public async Task ChangeParentEmail_InvalidOrSameAsStudent_Validation(string email)
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);

        var result = await CreateService().ChangeParentEmailAsync(student.Id, email);

        Assert.Equal(ParentLinkError.Validation, result.Error);
        Assert.Empty(_links.Links);
    }

    [Fact]
    public async Task ChangeParentEmail_AdultStudent_Validation()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 18);

        var result = await CreateService().ChangeParentEmailAsync(student.Id, "dad@test.com");

        Assert.Equal(ParentLinkError.Validation, result.Error);
    }

    [Fact]
    public async Task ChangeParentEmail_AlreadyActiveEmail_Conflict()
    {
        var (student, _, _) = await AcceptedLinkAsync(studentAge: 14);

        var result = await CreateService().ChangeParentEmailAsync(student.Id, "me@test.com");

        Assert.Equal(ParentLinkError.Conflict, result.Error);
    }

    [Fact]
    public async Task ChangeParentEmail_DailyLimit_TooManyRequests()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        for (var i = 0; i < ParentLinkService.MaxNewLinksPerDay; i++)
            _links.Links.Add(new ParentStudentLink { StudentUserId = student.Id, ParentEmail = $"p{i}@test.com", Status = ParentLinkStatuses.Revoked });

        var result = await CreateService().ChangeParentEmailAsync(student.Id, "dad@test.com");

        Assert.Equal(ParentLinkError.TooManyRequests, result.Error);
    }

    [Fact]
    public async Task ChangeParentEmail_ParentRole_Forbidden()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent, 40);

        var result = await CreateService().ChangeParentEmailAsync(parent.Id, "dad@test.com");

        Assert.Equal(ParentLinkError.Forbidden, result.Error);
    }

    [Fact]
    public async Task GetMine_Student_SeesPendingAndActive_WithCanRevoke()
    {
        var (student, parent, active) = await AcceptedLinkAsync(studentAge: 14);
        var (pending, _) = AddPendingLink(student, "dad@test.com");
        _links.Links.Add(new ParentStudentLink { StudentUserId = student.Id, ParentEmail = "old@test.com", Status = ParentLinkStatuses.Revoked });

        var result = await CreateService().GetMineAsync(student.Id);

        Assert.Equal(2, result.Value!.Count);
        var a = result.Value.Single(l => l.Id == active.Id);
        Assert.Equal(parent.FullName, a.ParentName);
        Assert.False(a.CanRevoke); // dưới 16 không tự rút đồng ý
        Assert.True(result.Value.Single(l => l.Id == pending.Id).CanRevoke);
    }

    [Fact]
    public async Task GetMine_Parent_SeesLinkedChildren()
    {
        var (student, parent, _) = await AcceptedLinkAsync(studentAge: 14);

        var result = await CreateService().GetMineAsync(parent.Id);

        var child = Assert.Single(result.Value!);
        Assert.Equal(student.FullName, child.StudentName);
        Assert.Equal(14, child.StudentAge);
        Assert.Equal(student.Id, child.StudentUserId);
        Assert.True(child.CanRevoke);
    }

    // ===== [UC-117] Phụ huynh rút đồng ý qua link trong mail, không cần đăng nhập =====

    [Fact]
    public async Task Accept_StoresRevokeTokenHash_AndEmailsParentWithRevokeLink()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var parent = AddUser("me@test.com", UserRoles.Parent, 40);
        var (link, token) = AddPendingLink(student);

        await CreateService().AcceptAsync(token, isGuardian: true, "1.2.3.4");

        var mail = Assert.Single(_emails.SentEmails, e => e.Purpose == ParentConsentConfirmedPurpose);
        Assert.Equal(parent.Email, mail.To);
        Assert.False(string.IsNullOrEmpty(mail.Code));
        // DB chỉ giữ hash, không giữ token gốc
        var stored = Stored(link.Id);
        Assert.Equal(ParentInvite.HashToken(mail.Code), stored.RevokeTokenHash);
        Assert.NotEqual(mail.Code, stored.RevokeTokenHash);
        // Mail báo học sinh vẫn gửi như cũ
        Assert.Contains(_emails.SentEmails, e => e.To == student.Email && e.Purpose == "parent_linked");
    }

    [Fact]
    public async Task GetRevokeInfo_ValidToken_ReturnsStudentNameAndAcceptedDate()
    {
        var (student, _, link) = await AcceptedLinkAsync(studentAge: 14);

        var result = await CreateService().GetRevokeInfoAsync(_revokeToken);

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(student.FullName, result.Value!.StudentName);
        Assert.Equal(link.AcceptedAt, result.Value.AcceptedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("wrong-token")]
    public async Task GetRevokeInfo_InvalidToken_NotFoundWithoutInfo(string token)
    {
        await AcceptedLinkAsync(studentAge: 14);

        var result = await CreateService().GetRevokeInfoAsync(token);

        Assert.Equal(ParentLinkError.NotFound, result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetRevokeInfo_TooLongToken_NotFound()
    {
        await AcceptedLinkAsync(studentAge: 14);

        var result = await CreateService().GetRevokeInfoAsync(new string('a', 129));

        Assert.Equal(ParentLinkError.NotFound, result.Error);
    }

    [Fact]
    public async Task GetRevokeInfo_StudentNow16_Forbidden()
    {
        var (student, _, _) = await AcceptedLinkAsync(studentAge: 14);
        student.DateOfBirth = BornYearsAgo(16);

        var result = await CreateService().GetRevokeInfoAsync(_revokeToken);

        Assert.Equal(ParentLinkError.Forbidden, result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task RevokeByToken_RevokesLink_SetsConsentFalse_NotifiesStudent_AndAudits()
    {
        var (student, parent, link) = await AcceptedLinkAsync(studentAge: 14);

        var result = await CreateService().RevokeByTokenAsync(_revokeToken, "5.6.7.8");

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(student.FullName, result.Value!.StudentName);
        var stored = Stored(link.Id);
        Assert.Equal(ParentLinkStatuses.Revoked, stored.Status);
        Assert.Equal(parent.Id, stored.RevokedByUserId);
        Assert.NotNull(stored.RevokedAt);
        Assert.Null(stored.RevokeTokenHash);
        Assert.False(Assert.Single(_links.Consents).Granted);
        Assert.False(await _links.HasGrantedConsentAsync(student.Id));
        Assert.Contains(_emails.SentEmails, e => e.To == student.Email && e.Purpose == "parent_unlinked");

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(ParentLinkService.RevokeByEmailAuditAction, entry.Action);
        Assert.Equal(parent.Id, entry.ActorUserId);
        Assert.Equal(link.Id.ToString(), entry.EntityId);
        Assert.Equal("5.6.7.8", entry.IpAddress);
        // Không ghi email phụ huynh vào audit
        Assert.DoesNotContain(parent.Email, entry.DetailJson ?? "");
    }

    [Fact]
    public async Task RevokeByToken_ParentWithoutAccount_Revokes()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, token) = AddPendingLink(student);
        await CreateService().AcceptAsync(token, isGuardian: true, null);
        var revokeToken = _emails.SentEmails.Single(e => e.Purpose == ParentConsentConfirmedPurpose).Code;
        _emails.SentEmails.Clear();

        var result = await CreateService().RevokeByTokenAsync(revokeToken, null);

        Assert.Equal(ParentLinkError.None, result.Error);
        var stored = Stored(link.Id);
        Assert.Equal(ParentLinkStatuses.Revoked, stored.Status);
        Assert.Null(stored.RevokedByUserId);
        Assert.False(Assert.Single(_links.Consents).Granted);
        Assert.Null(Assert.Single(_audit.Entries).ActorUserId);
        Assert.Contains(_emails.SentEmails, e => e.To == student.Email && e.Purpose == "parent_unlinked");
    }

    // ===== [UC-75 / UC-115] Phụ huynh đồng ý qua mail rồi mới đăng ký tài khoản: tự liên kết =====

    [Fact]
    public async Task LinkParentAccount_SameEmail_AttachesActiveLinks_KeepsConsent_EmailsStudent()
    {
        var kid1 = AddUser("kid1@test.com", UserRoles.Student, 14);
        var kid2 = AddUser("kid2@test.com", UserRoles.Student, 12);
        var (link1, token1) = AddPendingLink(kid1);
        var (link2, token2) = AddPendingLink(kid2);
        var service = CreateService();
        await service.AcceptAsync(token1, isGuardian: true, null);
        await service.AcceptAsync(token2, isGuardian: true, null);
        var parent = AddUser("me@test.com", UserRoles.Parent, 40);
        _emails.SentEmails.Clear();

        await service.LinkParentAccountAsync(parent);

        Assert.Equal(parent.Id, Stored(link1.Id).ParentUserId);
        Assert.Equal(parent.Id, Stored(link2.Id).ParentUserId);
        Assert.All(_links.Consents, c => Assert.Null(c.GrantedByUserId));
        Assert.Equal(2, (await service.GetMineAsync(parent.Id)).Value!.Count);
        Assert.Equal(2, _emails.SentEmails.Count(e => e.Purpose == "parent_account_linked"));
        Assert.Contains(_emails.SentEmails, e => e.To == kid1.Email);
        Assert.Contains(_emails.SentEmails, e => e.To == kid2.Email);
    }

    [Fact]
    public async Task LinkParentAccount_OnlyActiveLinks_PendingRejectedRevokedUntouched()
    {
        var kid = AddUser("kid@test.com", UserRoles.Student, 14);
        var (pending, _) = AddPendingLink(kid);
        var (rejected, rejectToken) = AddPendingLink(kid);
        var service = CreateService();
        await service.RejectAsync(rejectToken, null);
        var revoked = new ParentStudentLink { StudentUserId = kid.Id, ParentEmail = "me@test.com", Status = ParentLinkStatuses.Revoked };
        _links.Links.Add(revoked);
        var parent = AddUser("me@test.com", UserRoles.Parent, 40);
        _emails.SentEmails.Clear();

        await service.LinkParentAccountAsync(parent);

        Assert.Null(Stored(pending.Id).ParentUserId);
        Assert.Null(Stored(rejected.Id).ParentUserId);
        Assert.Null(Stored(revoked.Id).ParentUserId);
        Assert.Empty(_emails.SentEmails);
    }

    [Fact]
    public async Task LinkParentAccount_OtherEmailOrNotParent_NothingLinked()
    {
        var kid = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, token) = AddPendingLink(kid);
        var service = CreateService();
        await service.AcceptAsync(token, isGuardian: true, null);
        var otherParent = AddUser("dad@test.com", UserRoles.Parent, 40);
        var sameEmailStudent = new User { Email = "me@test.com", FullName = "S", Role = UserRoles.Student };

        await service.LinkParentAccountAsync(otherParent);
        await service.LinkParentAccountAsync(sameEmailStudent);

        Assert.Null(Stored(link.Id).ParentUserId);
    }

    [Fact]
    public async Task LinkParentAccount_ConcurrentWrite_SkipsWithoutEmail()
    {
        var kid = AddUser("kid@test.com", UserRoles.Student, 14);
        var (link, token) = AddPendingLink(kid);
        var service = CreateService();
        await service.AcceptAsync(token, isGuardian: true, null);
        var parent = AddUser("me@test.com", UserRoles.Parent, 40);
        _emails.SentEmails.Clear();
        _links.ConflictsToThrow = 1;

        await service.LinkParentAccountAsync(parent);

        Assert.Null(Stored(link.Id).ParentUserId);
        Assert.Empty(_emails.SentEmails);
    }

    [Fact]
    public async Task RevokeByToken_UsedTwice_SecondTimeNotFound()
    {
        await AcceptedLinkAsync(studentAge: 14);
        await CreateService().RevokeByTokenAsync(_revokeToken, null);
        _emails.SentEmails.Clear();

        var result = await CreateService().RevokeByTokenAsync(_revokeToken, null);

        Assert.Equal(ParentLinkError.NotFound, result.Error);
        Assert.Empty(_emails.SentEmails);
        Assert.Single(_audit.Entries);
    }

    [Fact]
    public async Task RevokeByToken_AfterInAppRevoke_TokenDead()
    {
        var (_, parent, link) = await AcceptedLinkAsync(studentAge: 14);
        await CreateService().RevokeAsync(parent.Id, link.Id);

        Assert.Null(Stored(link.Id).RevokeTokenHash);
        var result = await CreateService().RevokeByTokenAsync(_revokeToken, null);

        Assert.Equal(ParentLinkError.NotFound, result.Error);
    }

    [Fact]
    public async Task RevokeByToken_AfterAccountDeletion_TokenDead()
    {
        var (student, _, link) = await AcceptedLinkAsync(studentAge: 14);
        await CreateService().RevokeAllForAccountDeletionAsync(student.Id);

        Assert.Null(Stored(link.Id).RevokeTokenHash);
        var result = await CreateService().RevokeByTokenAsync(_revokeToken, null);

        Assert.Equal(ParentLinkError.NotFound, result.Error);
    }

    [Fact]
    public async Task RevokeByToken_StudentNow16_ForbiddenAndLinkUntouched()
    {
        var (student, _, link) = await AcceptedLinkAsync(studentAge: 14);
        student.DateOfBirth = BornYearsAgo(16);

        var result = await CreateService().RevokeByTokenAsync(_revokeToken, null);

        Assert.Equal(ParentLinkError.Forbidden, result.Error);
        Assert.Equal(ParentLinkStatuses.Active, Stored(link.Id).Status);
        Assert.True(Assert.Single(_links.Consents).Granted);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task RevokeByToken_ConcurrentWrite_Conflict()
    {
        var (_, _, link) = await AcceptedLinkAsync(studentAge: 14);
        _links.ConflictsToThrow = 1;

        var result = await CreateService().RevokeByTokenAsync(_revokeToken, null);

        Assert.Equal(ParentLinkError.Conflict, result.Error);
        Assert.Equal(ParentLinkStatuses.Active, Stored(link.Id).Status);
        Assert.Empty(_emails.SentEmails);
        Assert.Empty(_audit.Entries);
    }

    [Theory]
    [InlineData("")]
    [InlineData("wrong-token")]
    public async Task RevokeByToken_InvalidToken_NotFound(string token)
    {
        var (_, _, link) = await AcceptedLinkAsync(studentAge: 14);

        var result = await CreateService().RevokeByTokenAsync(token, null);

        Assert.Equal(ParentLinkError.NotFound, result.Error);
        Assert.Equal(ParentLinkStatuses.Active, Stored(link.Id).Status);
    }

    // ===== Email mời đã là tài khoản học sinh / trung tâm / quản trị: chặn ở trang đồng ý, vẫn cho Từ chối =====

    [Theory]
    [InlineData(UserRoles.Student)]
    [InlineData(UserRoles.Center)]
    [InlineData(UserRoles.Admin)]
    public async Task GetInvite_EmailBelongsToNonParentAccount_SetsBlockedReason(string role)
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        AddUser("me@test.com", role, 20);
        var (_, token) = AddPendingLink(student);

        var result = await CreateService().GetInviteAsync(token);

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.BlockedReason));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(UserRoles.Parent)]
    public async Task GetInvite_NoAccountOrParentAccount_NotBlocked(string? role)
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        if (role != null) AddUser("me@test.com", role, 40);
        var (_, token) = AddPendingLink(student);

        var result = await CreateService().GetInviteAsync(token);

        Assert.Null(result.Value!.BlockedReason);
    }

    [Fact]
    public async Task Accept_EmailIsStudentAccount_RefusedAndLinkStaysPending()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        AddUser("me@test.com", UserRoles.Student, 17);
        var (link, token) = AddPendingLink(student);

        var result = await CreateService().AcceptAsync(token, isGuardian: true, "1.2.3.4");

        Assert.Equal(ParentLinkError.Validation, result.Error);
        Assert.Contains("học sinh", result.Message);
        Assert.Empty(_links.Consents);
        Assert.Equal(ParentLinkStatuses.Pending, Stored(link.Id).Status);
    }

    [Fact]
    public async Task Reject_EmailIsStudentAccount_StillRecorded()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        AddUser("me@test.com", UserRoles.Student, 17);
        var (link, token) = AddPendingLink(student);

        var result = await CreateService().RejectAsync(token, "1.2.3.4");

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(ParentLinkStatuses.Rejected, Stored(link.Id).Status);
    }
}
