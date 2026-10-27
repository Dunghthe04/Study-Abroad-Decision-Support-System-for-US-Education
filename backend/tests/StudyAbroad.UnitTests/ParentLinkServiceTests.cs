using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class ParentLinkServiceTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakeEmailSender _emails = new();
    private readonly FakeParentLinkRepository _links = new();

    private ParentLinkService CreateService() => new(_links, _users, _emails);

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

    private ParentStudentLink Stored(Guid id) => _links.Links.Single(l => l.Id == id);

    private async Task<(User Student, User Parent, ParentStudentLink Link)> AcceptedLinkAsync(int studentAge)
    {
        var student = AddUser("kid@test.com", UserRoles.Student, studentAge);
        var parent = AddUser("me@test.com", UserRoles.Parent, 40);
        var (link, token) = AddPendingLink(student);
        var result = await CreateService().AcceptAsync(parent.Id, token, "1.2.3.4");
        Assert.Equal(ParentLinkError.None, result.Error);
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

    [Fact]
    public async Task Accept_CreatesConsentGrantedByParent_AndActivatesLink()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var parent = AddUser("me@test.com", UserRoles.Parent, 40);
        var (link, token) = AddPendingLink(student);

        var result = await CreateService().AcceptAsync(parent.Id, token, "1.2.3.4");

        Assert.Equal(ParentLinkError.None, result.Error);
        var consent = Assert.Single(_links.Consents);
        Assert.Equal(student.Id, consent.UserId);
        Assert.Equal(ConsentPurposes.ChildDataProcessing, consent.Purpose);
        Assert.True(consent.Granted);
        Assert.Equal(ConsentPurposes.ChildDataPolicyVersion, consent.PolicyVersion);
        Assert.Equal(parent.Id, consent.GrantedByUserId);
        Assert.Equal("1.2.3.4", consent.GrantedIp);

        var stored = Stored(link.Id);
        Assert.Equal(ParentLinkStatuses.Active, stored.Status);
        Assert.Equal(parent.Id, stored.ParentUserId);
        Assert.Equal(consent.Id, stored.ConsentId);
        Assert.NotNull(stored.AcceptedAt);
        Assert.Null(stored.TokenHash);

        Assert.Contains(_emails.SentEmails, e => e.To == student.Email && e.Purpose == "parent_linked");
    }

    [Fact]
    public async Task Accept_TokenOnlyUsableOnce()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var parent = AddUser("me@test.com", UserRoles.Parent, 40);
        var (_, token) = AddPendingLink(student);
        var service = CreateService();
        await service.AcceptAsync(parent.Id, token, null);

        var again = await service.AcceptAsync(parent.Id, token, null);

        Assert.Equal(ParentLinkError.NotFound, again.Error);
        Assert.Single(_links.Consents);
    }

    [Fact]
    public async Task Accept_ParentEmailMismatch_Forbidden()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var other = AddUser("other@test.com", UserRoles.Parent, 40);
        var (link, token) = AddPendingLink(student);

        var result = await CreateService().AcceptAsync(other.Id, token, null);

        Assert.Equal(ParentLinkError.Forbidden, result.Error);
        Assert.Empty(_links.Consents);
        Assert.Equal(ParentLinkStatuses.Pending, Stored(link.Id).Status);
    }

    [Fact]
    public async Task Accept_NonParentRole_Forbidden()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var sameEmailStudent = AddUser("me@test.com", UserRoles.Student, 40);
        var (_, token) = AddPendingLink(student);

        var result = await CreateService().AcceptAsync(sameEmailStudent.Id, token, null);

        Assert.Equal(ParentLinkError.Forbidden, result.Error);
    }

    [Fact]
    public async Task Accept_ParentUnder18_Forbidden()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var parent = AddUser("me@test.com", UserRoles.Parent, 17);
        var (_, token) = AddPendingLink(student);

        var result = await CreateService().AcceptAsync(parent.Id, token, null);

        Assert.Equal(ParentLinkError.Forbidden, result.Error);
    }

    [Fact]
    public async Task Accept_Expired_NoConsent()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var parent = AddUser("me@test.com", UserRoles.Parent, 40);
        var (_, token) = AddPendingLink(student, sentAt: DateTime.UtcNow.AddDays(-ParentInvite.ExpiryDays - 1));

        var result = await CreateService().AcceptAsync(parent.Id, token, null);

        Assert.Equal(ParentLinkError.Expired, result.Error);
        Assert.Empty(_links.Consents);
    }

    [Fact]
    public async Task Accept_ConcurrentWrite_Conflict()
    {
        var student = AddUser("kid@test.com", UserRoles.Student, 14);
        var parent = AddUser("me@test.com", UserRoles.Parent, 40);
        var (link, token) = AddPendingLink(student);
        _links.ConflictsToThrow = 1;

        var result = await CreateService().AcceptAsync(parent.Id, token, null);

        Assert.Equal(ParentLinkError.Conflict, result.Error);
        Assert.Empty(_links.Consents);
        Assert.Equal(ParentLinkStatuses.Pending, Stored(link.Id).Status);
        Assert.DoesNotContain(_emails.SentEmails, e => e.Purpose == "parent_linked");
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
        Assert.True(child.CanRevoke);
    }
}
