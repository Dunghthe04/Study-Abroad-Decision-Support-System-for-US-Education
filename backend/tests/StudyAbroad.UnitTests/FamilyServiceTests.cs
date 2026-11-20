using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Billing.Family;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Gói Gia đình (UC-126..131): mời, hủy, chấp nhận, từ chối, gỡ, rời, danh sách có phần trăm. Dùng EntitlementService thật
// trên một kho trong bộ nhớ (cùng danh sách cho IBillingRepository và IFamilyRepository), nên chuyển kỳ và nhóm hết hiệu lực chạy thật.
public class FamilyServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 3, 0, 0, DateTimeKind.Utc);

    // ---- Get (UC-129): F31, F32, riêng tư

    [Fact]
    public async Task Get_NotInFamily_Returns404()
    {
        var env = new Env(withFamily: false);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.GetAsync(env.Student.Id, UserRoles.Student, Now));

        Assert.Equal(("not_in_family", 404), (ex.Code, ex.StatusCode));
    }

    [Fact]
    public async Task Get_Owner_ListsOwnerFirst_ThenMembers_SeatsCountPendingInvites_InvitesWithEmail()
    {
        var env = new Env();
        env.AddActive(env.Student, FamilySeats.Student);
        var (pending, _) = env.AddPending("p2@x.vn", FamilySeats.Parent);

        var dto = await env.Service.GetAsync(env.Owner.Id, UserRoles.Parent, Now);

        Assert.True(dto.IsOwner);
        Assert.Equal(env.Group.Id, dto.GroupId);
        Assert.Equal("Chủ gói", dto.OwnerName);
        Assert.Equal(["owner", "student"], dto.Members.Select(m => m.Role));
        Assert.Equal(env.Owner.Id, dto.Members[0].MemberId);                     // chủ gói: id người dùng
        Assert.Equal(env.Group.CreatedAt, dto.Members[0].JoinedAt);
        Assert.True(dto.Members[0].IsMe);
        Assert.False(dto.Members[1].IsMe);
        Assert.NotEqual(env.Student.Id, dto.Members[1].MemberId);                // thành viên: id dòng family_members
        Assert.Equal("Học sinh A", dto.Members[1].Name);
        Assert.Equal((1, 1), (dto.Seats.Students.Used, dto.Seats.Students.Max));
        Assert.Equal((1, 2), (dto.Seats.Parents.Used, dto.Seats.Parents.Max));    // lời mời đang chờ giữ chỗ
        var invite = Assert.Single(dto.Invites);
        Assert.Equal((pending.Id, "p2@x.vn", FamilySeats.Parent), (invite.InviteId, invite.Email, invite.SeatRole));
        Assert.Equal(("Gia đình 1 tháng", 0), (dto.Cycle!.PlanName, dto.Cycle.UsedPercent));
    }

    [Fact]
    public async Task F31_MemberGet_HasNamesRolesShares_NoInvitesNoEmails()
    {
        var env = new Env();
        env.AddActive(env.Student, FamilySeats.Student);
        env.AddPending("secret-invite@x.vn", FamilySeats.Parent);

        var dto = await env.Service.GetAsync(env.Student.Id, UserRoles.Student, Now);
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.False(dto.IsOwner);
        Assert.Empty(dto.Invites);
        Assert.Equal(["owner", "student"], dto.Members.Select(m => m.Role));
        Assert.Contains(dto.Members, m => m is { IsMe: true, Role: "student" });
        Assert.Equal(2, dto.Seats.Parents.Max);
        Assert.Equal(1, dto.Seats.Parents.Used);                                  // chỗ đang giữ thì thành viên vẫn thấy số lượng
        Assert.DoesNotContain("@", json);                                         // không email của ai (kể cả lời mời)
        Assert.Contains("\"invites\":[]", json);
    }

    [Fact]
    public async Task F32_Shares_FloorOfTokensOverQuota_AddUpToPoolPercentWithinRounding()
    {
        var env = new Env();
        var student = env.AddActive(env.Student, FamilySeats.Student);
        var parent = env.AddActive(env.Parent, FamilySeats.Parent);
        env.Use(env.Owner.Id, 100_000);   // 20%
        env.Use(env.Student.Id, 150_000); // 30%
        env.Use(env.Parent.Id, 49_999);   // 9,9998% → 9

        var dto = await env.Service.GetAsync(env.Owner.Id, UserRoles.Parent, Now);

        Assert.Equal([20, 30, 9], dto.Members.Select(m => m.SharePercent));
        Assert.Equal(59, dto.Cycle!.UsedPercent);                                  // 299.999 / 500.000
        Assert.InRange(dto.Members.Sum(m => m.SharePercent), dto.Cycle.UsedPercent - dto.Members.Count, dto.Cycle.UsedPercent);
        Assert.NotEqual(student.Id, parent.Id);
    }

    [Fact]
    public async Task Shares_IgnoreOtherCyclesAndNonClaudeCalls_AndClampAt100()
    {
        var env = new Env();
        env.AddActive(env.Student, FamilySeats.Student);
        env.Store.Calls.Add(new AiCall { UserId = env.Student.Id, Kind = "ollama", TotalTokens = 400_000, UserSubscriptionId = env.Cycle.Id });
        env.Store.Calls.Add(new AiCall { UserId = env.Student.Id, Kind = BillingRules.MeteredAiKind, TotalTokens = 400_000, UserSubscriptionId = Guid.NewGuid() });
        env.Use(env.Owner.Id, 900_000);   // vượt hạn mức: hiện 100

        var dto = await env.Service.GetAsync(env.Student.Id, UserRoles.Student, Now);

        Assert.Equal([100, 0], dto.Members.Select(m => m.SharePercent));
        Assert.Equal(100, dto.Cycle!.UsedPercent);
    }

    [Fact]
    public async Task Get_ExpiredPendingInvite_IsNotListedAndDoesNotHoldASeat()
    {
        var env = new Env();
        env.AddPending("old@x.vn", FamilySeats.Student, expiresAt: Now.AddMinutes(-1));

        var dto = await env.Service.GetAsync(env.Owner.Id, UserRoles.Parent, Now);

        Assert.Empty(dto.Invites);
        Assert.Equal(0, dto.Seats.Students.Used);
    }

    [Fact]
    public async Task Get_CanResendAt_FollowsTheSameRuleAsResend()
    {
        var env = new Env();
        var (fresh, _) = env.AddPending("a@x.vn", FamilySeats.Parent, sentAt: Now.AddSeconds(-10));
        var (old, _) = env.AddPending("b@x.vn", FamilySeats.Parent, sentAt: Now.AddMinutes(-5));

        var dto = await env.Service.GetAsync(env.Owner.Id, UserRoles.Parent, Now);

        Assert.Equal(Now.AddSeconds(50), dto.Invites.Single(i => i.InviteId == fresh.Id).CanResendAt);
        Assert.Null(dto.Invites.Single(i => i.InviteId == old.Id).CanResendAt);
    }

    [Fact]
    public void T1_FamilyDtosHaveNoTokenFields()
    {
        Type[] dtos = [typeof(FamilyDto), typeof(FamilyCycleDto), typeof(FamilySeatsDto), typeof(FamilySeatCountDto), typeof(FamilyMemberDto), typeof(FamilyInviteDto), typeof(FamilyInviteLookupDto)];
        foreach (var type in dtos)
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                Assert.DoesNotContain("token", property.Name, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Invite (UC-126): F2 đến F6

    [Fact]
    public async Task F2_InviteExistingAccount_CreatesPendingRow_SendsMailAndNotification()
    {
        var env = new Env();

        await env.Service.InviteAsync(env.Owner.Id, "  Hoc.Sinh.A@X.vn ", FamilySeats.Student, Now);

        var row = Assert.Single(env.Store.Members);
        var token = env.TokenOf("hoc.sinh.a@x.vn");
        Assert.Equal(("hoc.sinh.a@x.vn", FamilySeats.Student, FamilyMemberStatuses.Pending), (row.InvitedEmail, row.SeatRole, row.Status));
        Assert.Equal(FamilyInviteToken.Hash(token), row.TokenHash);               // DB chỉ giữ hash; token gốc chỉ có trong mail
        Assert.Equal((Now.AddDays(7), 1, Now, Now), (row.ExpiresAt!.Value, row.InviteSentCount, row.LastInviteSentAt!.Value, row.InvitedAt));
        Assert.Null(row.UserId);
        var note = Assert.Single(env.Notes.Sent);
        Assert.Equal((env.Student.Id, NotificationTypes.FamilyInvite, $"/family/invite?token={token}"), (note.UserId, note.Type, note.LinkUrl));
        Assert.Equal("Lời mời tham gia gói Gia đình", note.Title);
        Assert.Equal("Chủ gói mời bạn tham gia gói Gia đình.", note.Body);
        Assert.Equal(1, env.Uow.Calls);
        Assert.Contains(env.Group.Id, env.Store.LockedGroups);                    // khóa dòng nhóm (fake chỉ cho khóa trong transaction)
    }

    [Fact]
    public async Task F3_InviteEmailWithNoAccount_SendsMailOnly_SameOutcome()
    {
        var env = new Env();

        await env.Service.InviteAsync(env.Owner.Id, "nobody@x.vn", FamilySeats.Parent, Now);

        Assert.NotEmpty(env.TokenOf("nobody@x.vn"));
        Assert.Empty(env.Notes.Sent);
        Assert.Single(env.Store.Members);
    }

    [Fact]
    public async Task F4_InviteOwnEmail_InvalidEmail_AnyCase()
    {
        var env = new Env();

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.InviteAsync(env.Owner.Id, " OWNER@x.vn", FamilySeats.Parent, Now));

        Assert.Equal(("invalid_email", 400), (ex.Code, ex.StatusCode));
        Assert.Empty(env.Store.Members);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public async Task Invite_MalformedEmail_InvalidEmail(string email)
    {
        var env = new Env();

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.InviteAsync(env.Owner.Id, email, FamilySeats.Parent, Now));

        Assert.Equal("invalid_email", ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("owner")]
    [InlineData("Student")]
    public async Task Invite_UnknownSeatRole_InvalidValue(string seatRole)
    {
        var env = new Env();

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.InviteAsync(env.Owner.Id, "x@x.vn", seatRole, Now));

        Assert.Equal(("invalid_value", 400), (ex.Code, ex.StatusCode));
    }

    [Fact]
    public async Task F5_SeatFull_PendingInvitesCount_StudentOne_ParentTwo()
    {
        var env = new Env();
        await env.Service.InviteAsync(env.Owner.Id, "s1@x.vn", FamilySeats.Student, Now);
        await env.Service.InviteAsync(env.Owner.Id, "p1@x.vn", FamilySeats.Parent, Now);
        await env.Service.InviteAsync(env.Owner.Id, "p2@x.vn", FamilySeats.Parent, Now);

        var student = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.InviteAsync(env.Owner.Id, "s2@x.vn", FamilySeats.Student, Now));
        var parent = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.InviteAsync(env.Owner.Id, "p3@x.vn", FamilySeats.Parent, Now));

        Assert.Equal(("seat_full", 409), (student.Code, student.StatusCode));
        Assert.Equal("seat_full", parent.Code);
        Assert.Equal(3, env.Store.Members.Count);
    }

    [Fact]
    public async Task F5_SeatFull_ActiveMembersCountToo()
    {
        var env = new Env();
        env.AddActive(env.Student, FamilySeats.Student);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.InviteAsync(env.Owner.Id, "s2@x.vn", FamilySeats.Student, Now));

        Assert.Equal("seat_full", ex.Code);
    }

    [Fact]
    public async Task F6_SameEmailAlreadyPending_InviteExists()
    {
        var env = new Env();
        await env.Service.InviteAsync(env.Owner.Id, "s1@x.vn", FamilySeats.Student, Now);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.InviteAsync(env.Owner.Id, "S1@x.vn", FamilySeats.Student, Now));

        Assert.Equal(("invite_exists", 409), (ex.Code, ex.StatusCode));
    }

    [Fact]
    public async Task Invite_EmailOfAnActiveMember_InviteExists()
    {
        var env = new Env();
        env.AddActive(env.Student, FamilySeats.Student);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.InviteAsync(env.Owner.Id, env.Student.Email, FamilySeats.Parent, Now));

        Assert.Equal("invite_exists", ex.Code);
    }

    [Fact]
    public async Task Invite_ByMember_Forbidden_NotOwner()
    {
        var env = new Env();
        env.AddActive(env.Student, FamilySeats.Student);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.InviteAsync(env.Student.Id, "x@x.vn", FamilySeats.Parent, Now));

        Assert.Equal(("not_owner", 403), (ex.Code, ex.StatusCode));
    }

    [Fact]
    public async Task Invite_WithoutAFamily_NoFamily404()
    {
        var env = new Env(withFamily: false);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.InviteAsync(env.Student.Id, "x@x.vn", FamilySeats.Parent, Now));

        Assert.Equal(("no_family", 404), (ex.Code, ex.StatusCode));
    }

    [Fact]
    public async Task Invite_OwnerWhoseFamilyCycleEnded_NoFamily()
    {
        var env = new Env();
        env.Cycle.EndsAt = Now.AddHours(-1);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.InviteAsync(env.Owner.Id, "x@x.vn", FamilySeats.Parent, Now));

        Assert.Equal("no_family", ex.Code);
        Assert.Equal(FamilyGroupStatuses.Ended, env.Group.Status);
    }

    [Fact]
    public async Task F13_InviteOverAStaleExpiredPending_ExpiresItFirst_SoTheSeatAndEmailAreFree()
    {
        var env = new Env();
        var (stale, _) = env.AddPending("s1@x.vn", FamilySeats.Student, expiresAt: Now.AddMinutes(-1));

        await env.Service.InviteAsync(env.Owner.Id, "s1@x.vn", FamilySeats.Student, Now);

        Assert.Equal((FamilyMemberStatuses.Expired, (string?)null), (stale.Status, stale.TokenHash));
        Assert.Equal(1, env.Store.Members.Count(m => m.Status == FamilyMemberStatuses.Pending));
    }

    // ---- Resend: F7

    [Fact]
    public async Task F7_ResendWithin60Seconds_ResendTooSoon429()
    {
        var env = new Env();
        var (invite, _) = env.AddPending("s1@x.vn", FamilySeats.Student, sentAt: Now.AddSeconds(-59));

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.ResendInviteAsync(env.Owner.Id, invite.Id, Now));

        Assert.Equal(("resend_too_soon", 429), (ex.Code, ex.StatusCode));
        Assert.Empty(env.Mail.SentEmails);
    }

    [Fact]
    public async Task F7_SixthSendWithin24Hours_ResendTooSoon_ButAfter24HoursTheCountResets()
    {
        var env = new Env();
        var (invite, _) = env.AddPending("s1@x.vn", FamilySeats.Student, sentAt: Now.AddHours(-3), sentCount: 5);

        var blocked = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.ResendInviteAsync(env.Owner.Id, invite.Id, Now));
        invite.LastInviteSentAt = Now.AddHours(-25);
        await env.Service.ResendInviteAsync(env.Owner.Id, invite.Id, Now);

        Assert.Equal("resend_too_soon", blocked.Code);
        Assert.Equal(1, invite.InviteSentCount);                                   // reset về 0 rồi tính lần gửi này
    }

    [Fact]
    public async Task Resend_IssuesANewToken_OldLinkDies_ExpiryExtended_MailSent()
    {
        var env = new Env();
        var (invite, oldToken) = env.AddPending("s1@x.vn", FamilySeats.Student, sentAt: Now.AddMinutes(-5), expiresAt: Now.AddDays(2));

        await env.Service.ResendInviteAsync(env.Owner.Id, invite.Id, Now);

        var newToken = env.TokenOf("s1@x.vn");
        Assert.NotEqual(oldToken, newToken);
        Assert.Equal(FamilyInviteToken.Hash(newToken), invite.TokenHash);
        Assert.Null(await env.Store.FindMemberByTokenHashAsync(FamilyInviteToken.Hash(oldToken)));
        Assert.Equal((Now.AddDays(7), 2, Now), (invite.ExpiresAt!.Value, invite.InviteSentCount, invite.LastInviteSentAt!.Value));
    }

    [Fact]
    public async Task Resend_NotPendingOrOtherGroup_NotPending_AndMemberCannotResend()
    {
        var env = new Env();
        var (cancelled, _) = env.AddPending("c@x.vn", FamilySeats.Student, sentAt: Now.AddHours(-1));
        cancelled.Status = FamilyMemberStatuses.Cancelled;
        var foreign = new FamilyMember { FamilyGroupId = Guid.NewGuid(), InvitedEmail = "f@x.vn", SeatRole = FamilySeats.Parent, Status = FamilyMemberStatuses.Pending, ExpiresAt = Now.AddDays(3) };
        env.Store.Members.Add(foreign);
        env.AddActive(env.Student, FamilySeats.Student);

        var a = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.ResendInviteAsync(env.Owner.Id, cancelled.Id, Now));
        var b = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.ResendInviteAsync(env.Owner.Id, foreign.Id, Now));
        var c = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.ResendInviteAsync(env.Student.Id, foreign.Id, Now));

        Assert.Equal(("not_pending", 409), (a.Code, a.StatusCode));
        Assert.Equal("not_pending", b.Code);
        Assert.Equal(("not_owner", 403), (c.Code, c.StatusCode));
    }

    // ---- Cancel (UC-127): F8

    [Fact]
    public async Task F8_CancelInvite_LinkDies_SeatFreed()
    {
        var env = new Env();
        var (invite, token) = env.AddPending("s1@x.vn", FamilySeats.Student);

        await env.Service.CancelInviteAsync(env.Owner.Id, invite.Id, Now);

        Assert.Equal((FamilyMemberStatuses.Cancelled, (string?)null, (DateTime?)Now), (invite.Status, invite.TokenHash, invite.EndedAt));
        var lookup = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.LookupAsync(token, Now));
        Assert.Equal("invite_invalid", lookup.Code);
        await env.Service.InviteAsync(env.Owner.Id, "s2@x.vn", FamilySeats.Student, Now);   // chỗ học sinh đã trống
        Assert.Empty(env.Mail.SentEmails.Where(m => m.To == "s1@x.vn"));                       // hủy không gửi mail
    }

    [Fact]
    public async Task Cancel_Twice_NotPending_AndMemberCannotCancel()
    {
        var env = new Env();
        var (invite, _) = env.AddPending("s1@x.vn", FamilySeats.Student);
        env.AddActive(env.Parent, FamilySeats.Parent);
        await env.Service.CancelInviteAsync(env.Owner.Id, invite.Id, Now);

        var again = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.CancelInviteAsync(env.Owner.Id, invite.Id, Now));
        var byMember = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.CancelInviteAsync(env.Parent.Id, invite.Id, Now));

        Assert.Equal(("not_pending", 409), (again.Code, again.StatusCode));
        Assert.Equal(("not_owner", 403), (byMember.Code, byMember.StatusCode));
    }

    // ---- Lookup

    [Theory]
    [InlineData(FamilyMemberStatuses.Pending, FamilyInviteStatuses.Pending)]
    [InlineData(FamilyMemberStatuses.Active, FamilyInviteStatuses.Accepted)]
    [InlineData(FamilyMemberStatuses.Removed, FamilyInviteStatuses.Accepted)]
    [InlineData(FamilyMemberStatuses.Left, FamilyInviteStatuses.Accepted)]
    [InlineData(FamilyMemberStatuses.Ended, FamilyInviteStatuses.Accepted)]
    [InlineData(FamilyMemberStatuses.Declined, FamilyInviteStatuses.Declined)]
    [InlineData(FamilyMemberStatuses.Cancelled, FamilyInviteStatuses.Cancelled)]
    [InlineData(FamilyMemberStatuses.Expired, FamilyInviteStatuses.Expired)]
    public async Task Lookup_StatusMapping(string memberStatus, string expected)
    {
        var env = new Env();
        var (invite, token) = env.AddPending("s1@x.vn", FamilySeats.Student);
        invite.Status = memberStatus;

        var dto = await env.Service.LookupAsync(token, Now);

        Assert.Equal(expected, dto.Status);
        Assert.Equal(("Chủ gói", FamilySeats.Student, invite.ExpiresAt), (dto.OwnerName, dto.SeatRole, (DateTime?)dto.ExpiresAt));
    }

    [Fact]
    public async Task Lookup_PendingPastExpiry_IsExpired()
    {
        var env = new Env();
        var (_, token) = env.AddPending("s1@x.vn", FamilySeats.Student, expiresAt: Now.AddSeconds(-1));

        Assert.Equal(FamilyInviteStatuses.Expired, (await env.Service.LookupAsync(token, Now)).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown-token")]
    public async Task Lookup_UnknownOrBlankToken_InvalidInvite404_NeverThrowsOnNull(string? token)
    {
        var env = new Env();

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.LookupAsync(token!, Now));

        Assert.Equal(("invite_invalid", 404), (ex.Code, ex.StatusCode));
    }

    // ---- Accept (UC-130): thứ tự kiểm tra, F9 đến F16, F34 đến F39

    [Fact]
    public async Task F2_Accept_JoinsAndNotifiesOwner_ReturnsFamilyDtoForTheCaller()
    {
        var env = new Env();
        var (invite, token) = env.AddPending(env.Student.Email, FamilySeats.Student);

        var dto = await env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now);

        Assert.Equal((FamilyMemberStatuses.Active, (Guid?)env.Student.Id, (DateTime?)Now), (invite.Status, invite.UserId, invite.JoinedAt));
        Assert.Equal(FamilyInviteToken.Hash(token), invite.TokenHash);             // giữ hash để link báo "đã chấp nhận"
        Assert.False(dto.IsOwner);
        Assert.Equal(["owner", "student"], dto.Members.Select(m => m.Role));
        Assert.Contains(dto.Members, m => m is { IsMe: true, Role: "student" });
        Assert.Empty(dto.Invites);
        var note = Assert.Single(env.Notes.Sent);
        Assert.Equal((env.Owner.Id, NotificationTypes.FamilyInviteAccepted), (note.UserId, note.Type));
        Assert.Equal(FamilyInviteStatuses.Accepted, (await env.Service.LookupAsync(token, Now)).Status);
    }

    [Fact]
    public async Task Accept_TokenWithWhitespaceAndInvitedEmailInOtherCase_Works()
    {
        var env = new Env();
        env.Student.Email = "Hoc.Sinh.A@X.vn";
        var (_, token) = env.AddPending("hoc.sinh.a@x.vn", FamilySeats.Student);

        var dto = await env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, $"  {token}\n", Now);

        Assert.Contains(dto.Members, m => m.IsMe);
    }

    [Fact]
    public async Task Accept_UnknownToken_InvalidInvite404()
    {
        var env = new Env();

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, "nope", Now));

        Assert.Equal(("invite_invalid", 404), (ex.Code, ex.StatusCode));
    }

    [Fact]
    public async Task Accept_SecondTime_NotPending409()
    {
        var env = new Env();
        var (_, token) = env.AddPending(env.Student.Email, FamilySeats.Student);
        await env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now));

        Assert.Equal(("not_pending", 409), (ex.Code, ex.StatusCode));
    }

    [Fact]
    public async Task Accept_PastExpiry_Gone410_AndTheExpiredMarkIsSavedOutsideTheTransaction()
    {
        var env = new Env();
        var (invite, token) = env.AddPending(env.Student.Email, FamilySeats.Student, expiresAt: Now.AddSeconds(-1));

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now));

        Assert.Equal(("invite_expired", 410), (ex.Code, ex.StatusCode));
        Assert.Equal((FamilyMemberStatuses.Expired, (string?)null), (invite.Status, invite.TokenHash));
        Assert.Equal([false], env.Store.SaveInTransaction);                       // ném trong transaction thì mất ghi: phải lưu trước khi ném
        Assert.Equal(0, env.Uow.Calls);
        Assert.Empty(env.Notes.Sent);
    }

    [Fact]
    public async Task Accept_GroupWithNoActiveCycleAnymore_Gone410_NotNotPending()
    {
        var env = new Env();
        var (_, token) = env.AddPending(env.Student.Email, FamilySeats.Student);
        env.Cycle.EndsAt = Now.AddHours(-1);   // kỳ cuối hết hạn, không có kỳ gia hạn; job chưa kịp kết thúc nhóm

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now));

        Assert.Equal(("invite_expired", 410), (ex.Code, ex.StatusCode));
    }

    [Fact]
    public async Task F9_WrongAccount_Forbidden403_NothingChanges()
    {
        var env = new Env();
        var (invite, token) = env.AddPending("someone-else@x.vn", FamilySeats.Student);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now));

        Assert.Equal(("wrong_account", 403), (ex.Code, ex.StatusCode));
        Assert.Equal(FamilyMemberStatuses.Pending, invite.Status);
    }

    [Fact]
    public async Task F10_RoleDiffersFromTheSeat_RoleMismatch409()
    {
        var env = new Env();
        var (_, token) = env.AddPending(env.Parent.Email, FamilySeats.Student);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.AcceptAsync(env.Parent.Id, UserRoles.Parent, token, Now));

        Assert.Equal(("role_mismatch", 409), (ex.Code, ex.StatusCode));
    }

    [Theory]
    [InlineData(UserRoles.Admin)]
    [InlineData(UserRoles.Moderator)]
    [InlineData(UserRoles.Center)]
    public async Task F16_StaffAndCenterEmailInvited_CannotAccept_RoleMismatch(string role)
    {
        var env = new Env();
        var staff = env.AddUser("Nhân viên", role, $"{role}@x.vn");
        var (invite, token) = env.AddPending(staff.Email, FamilySeats.Parent);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.AcceptAsync(staff.Id, role, token, Now));

        Assert.Equal("role_mismatch", ex.Code);
        Assert.Equal(FamilyMemberStatuses.Pending, invite.Status);
    }

    [Fact]
    public async Task F11_InviteeIsActiveInAnotherCurrentFamily_AlreadyInFamily409()
    {
        var env = new Env();
        env.AddOtherFamily(member: env.Student);
        var (_, token) = env.AddPending(env.Student.Email, FamilySeats.Student);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now));

        Assert.Equal(("already_in_family", 409), (ex.Code, ex.StatusCode));
    }

    [Fact]
    public async Task F36_InviteeOwnsACurrentFamily_AlreadyInFamily()
    {
        var env = new Env();
        env.AddOtherFamily(owner: env.Parent);
        var (_, token) = env.AddPending(env.Parent.Email, FamilySeats.Parent);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.AcceptAsync(env.Parent.Id, UserRoles.Parent, token, Now));

        Assert.Equal("already_in_family", ex.Code);
    }

    [Fact]
    public async Task F12_InviteesOtherFamilyHasLapsed_AcceptSucceeds()
    {
        var env = new Env();
        var other = env.AddOtherFamily(member: env.Student);
        other.EndsAt = Now.AddHours(-2);   // gói kia đã hết hạn: summary kết thúc tư cách thành viên
        var (_, token) = env.AddPending(env.Student.Email, FamilySeats.Student);

        var dto = await env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now);

        Assert.Equal(env.Group.Id, dto.GroupId);
    }

    [Fact]
    public async Task F37_InviteeHasAPendingFamilyOrder_PendingFamilyOrder409()
    {
        var env = new Env();
        env.Store.Orders.Add(new PaymentTransaction { UserId = env.Student.Id, Status = OrderStatuses.Pending, PlanKind = PlanKinds.Family, OrderCode = 1 });
        var (_, token) = env.AddPending(env.Student.Email, FamilySeats.Student);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now));

        Assert.Equal(("pending_family_order", 409), (ex.Code, ex.StatusCode));
    }

    [Fact]
    public async Task F39_InviteeHasAPendingPersonalOrder_StillJoins()
    {
        var env = new Env();
        env.Store.Orders.Add(new PaymentTransaction { UserId = env.Student.Id, Status = OrderStatuses.Pending, PlanKind = PlanKinds.Personal, OrderCode = 1 });
        var (_, token) = env.AddPending(env.Student.Email, FamilySeats.Student);

        var dto = await env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now);

        Assert.Contains(dto.Members, m => m.IsMe);
    }

    [Fact]
    public async Task F34_InviteeWithAnActivePersonalCycle_Joins_PersonalSpentFirstThenFamily()
    {
        var env = new Env();
        var personal = env.Store.AddCycle(env.Student.Id, "personal-1m", Now.AddDays(-1));
        env.Store.AddCycle(env.Student.Id, "personal-3m", null, status: CycleStatuses.Queued);   // F35: hàng chờ không bị ảnh hưởng
        var (_, token) = env.AddPending(env.Student.Email, FamilySeats.Student);

        await env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now);
        var entitlements = new EntitlementService(env.Store);
        var first = await entitlements.ResolveChatChargeAsync(env.Student.Id, UserRoles.Student, Now);
        env.Store.Calls.Add(new AiCall { UserId = env.Student.Id, Kind = BillingRules.MeteredAiKind, TotalTokens = 500_000, UserSubscriptionId = personal.Id, PlanCode = "personal-1m" });
        env.Store.Cycles.Single(c => c.Kind == PlanKinds.Personal && c.Status == CycleStatuses.Queued).Status = CycleStatuses.Ended;   // bỏ hàng chờ để thấy rơi về Family
        var second = await entitlements.ResolveChatChargeAsync(env.Student.Id, UserRoles.Student, Now);

        Assert.Equal(personal.Id, first.CycleId);
        Assert.Equal(PlanKinds.Family, second.PoolKind);
        Assert.Equal(env.Cycle.Id, second.CycleId);
        Assert.Equal(1, env.Store.Members.Count(m => m.Status == FamilyMemberStatuses.Active));
    }

    [Fact]
    public async Task F14_SeatTakenByOthersAtAcceptTime_SeatFull409()
    {
        var env = new Env();
        env.AddActive(env.Student2, FamilySeats.Student);                          // trạng thái quá đầy (hai bên cùng giành chỗ cuối)
        var (invite, token) = env.AddPending(env.Student.Email, FamilySeats.Student);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now));

        Assert.Equal(("seat_full", 409), (ex.Code, ex.StatusCode));
        Assert.Equal(FamilyMemberStatuses.Pending, invite.Status);
        Assert.Contains(env.Group.Id, env.Store.LockedGroups);
    }

    [Fact]
    public async Task F14_AcceptingTheOnlyPendingInvite_DoesNotCountItselfAsTakingTheSeat()
    {
        var env = new Env();
        var (_, token) = env.AddPending(env.Student.Email, FamilySeats.Student);

        await env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now);

        Assert.Equal(1, await env.Store.CountSeatsAsync(env.Group.Id, FamilySeats.Student, null, Now));
    }

    [Fact]
    public async Task Accept_RowChangedByAnotherRequest_MapsToNotPending()
    {
        var env = new Env();
        var (_, token) = env.AddPending(env.Student.Email, FamilySeats.Student);
        env.Store.ConflictOnNextSave = true;   // xmin không khớp: owner vừa hủy lời mời

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now));

        Assert.Equal(("not_pending", 409), (ex.Code, ex.StatusCode));
    }

    [Fact]
    public async Task F15_StudentUnder16_Joins()
    {
        var env = new Env();
        env.Student.DateOfBirth = new DateOnly(2013, 1, 1);
        var (_, token) = env.AddPending(env.Student.Email, FamilySeats.Student);

        var dto = await env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, token, Now);

        Assert.Contains(dto.Members, m => m.IsMe);
    }

    // ---- Decline

    [Fact]
    public async Task Decline_MarksDeclined_ClearsTheLink_NotifiesOwner_FreesTheSeat()
    {
        var env = new Env();
        var (invite, token) = env.AddPending(env.Student.Email, FamilySeats.Student);

        await env.Service.DeclineAsync(env.Student.Id, token, Now);

        Assert.Equal((FamilyMemberStatuses.Declined, (string?)null, (DateTime?)Now), (invite.Status, invite.TokenHash, invite.EndedAt));
        var note = Assert.Single(env.Notes.Sent);
        Assert.Equal((env.Owner.Id, NotificationTypes.FamilyInviteDeclined), (note.UserId, note.Type));
        await env.Service.InviteAsync(env.Owner.Id, env.Student.Email, FamilySeats.Student, Now);   // mời lại được
    }

    [Fact]
    public async Task Decline_Unknown_NotPending_WrongAccount_AreRejected()
    {
        var env = new Env();
        var (invite, token) = env.AddPending("someone-else@x.vn", FamilySeats.Student);

        var unknown = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.DeclineAsync(env.Student.Id, "nope", Now));
        var wrong = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.DeclineAsync(env.Student.Id, token, Now));
        invite.Status = FamilyMemberStatuses.Active;
        var notPending = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.DeclineAsync(env.Student.Id, token, Now));

        Assert.Equal(("invite_invalid", 404), (unknown.Code, unknown.StatusCode));
        Assert.Equal(("wrong_account", 403), (wrong.Code, wrong.StatusCode));
        Assert.Equal(("not_pending", 409), (notPending.Code, notPending.StatusCode));
    }

    // ---- Remove (UC-128): F22, F24, F25

    [Fact]
    public async Task F22_OwnerRemovesMember_RemovedNotified_TokensStayInThePool()
    {
        var env = new Env();
        var member = env.AddActive(env.Student, FamilySeats.Student);
        env.Use(env.Student.Id, 250_000);

        await env.Service.RemoveMemberAsync(env.Owner.Id, member.Id, Now);

        Assert.Equal((FamilyMemberStatuses.Removed, (string?)null, (DateTime?)Now), (member.Status, member.TokenHash, member.EndedAt));
        var note = Assert.Single(env.Notes.Sent);
        Assert.Equal((env.Student.Id, NotificationTypes.FamilyMemberRemoved, "Bạn đã được gỡ khỏi gói Gia đình của Chủ gói."), (note.UserId, note.Type, note.Body));
        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.GetAsync(env.Student.Id, UserRoles.Student, Now));
        Assert.Equal("not_in_family", ex.Code);
        Assert.Equal(50, (await env.Service.GetAsync(env.Owner.Id, UserRoles.Parent, Now)).Cycle!.UsedPercent);   // token đã dùng vẫn tính
    }

    [Fact]
    public async Task F24_RemovedMemberReinvitedAndRejoins_PoolIsNotReset()
    {
        var env = new Env();
        var member = env.AddActive(env.Student, FamilySeats.Student);
        env.Use(env.Student.Id, 100_000);
        await env.Service.RemoveMemberAsync(env.Owner.Id, member.Id, Now);

        await env.Service.InviteAsync(env.Owner.Id, env.Student.Email, FamilySeats.Student, Now);
        var dto = await env.Service.AcceptAsync(env.Student.Id, UserRoles.Student, env.TokenOf(env.Student.Email), Now);

        Assert.Equal(20, dto.Members.Single(m => m.IsMe).SharePercent);
        Assert.Equal(20, dto.Cycle!.UsedPercent);
    }

    [Fact]
    public async Task F25_RemoveTheOwner_CannotRemoveOwner409()
    {
        var env = new Env();

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.RemoveMemberAsync(env.Owner.Id, env.Owner.Id, Now));

        Assert.Equal(("cannot_remove_owner", 409), (ex.Code, ex.StatusCode));
    }

    [Fact]
    public async Task Remove_PendingInviteOrOtherGroupsMemberOrUnknown_MemberNotFound404()
    {
        var env = new Env();
        var (pending, _) = env.AddPending("s1@x.vn", FamilySeats.Student);
        var foreign = new FamilyMember { FamilyGroupId = Guid.NewGuid(), UserId = Guid.NewGuid(), InvitedEmail = "f@x.vn", SeatRole = FamilySeats.Parent, Status = FamilyMemberStatuses.Active };
        env.Store.Members.Add(foreign);

        foreach (var id in new[] { pending.Id, foreign.Id, Guid.NewGuid() })
        {
            var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.RemoveMemberAsync(env.Owner.Id, id, Now));
            Assert.Equal(("member_not_found", 404), (ex.Code, ex.StatusCode));
        }
        Assert.Empty(env.Notes.Sent);
    }

    [Fact]
    public async Task Remove_ByMemberOrWithoutFamily_Forbidden_NotOwner_NoFamily()
    {
        var env = new Env();
        var member = env.AddActive(env.Student, FamilySeats.Student);
        var stranger = env.AddUser("Người lạ", UserRoles.Parent, "stranger@x.vn");

        var byMember = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.RemoveMemberAsync(env.Student.Id, member.Id, Now));
        var none = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.RemoveMemberAsync(stranger.Id, member.Id, Now));

        Assert.Equal(("not_owner", 403), (byMember.Code, byMember.StatusCode));
        Assert.Equal(("no_family", 404), (none.Code, none.StatusCode));
        Assert.Equal(FamilyMemberStatuses.Active, member.Status);
    }

    [Fact]
    public async Task Remove_FreesTheSeat_SoAnotherCanBeInvited()
    {
        var env = new Env();
        var member = env.AddActive(env.Student, FamilySeats.Student);
        await env.Service.RemoveMemberAsync(env.Owner.Id, member.Id, Now);

        await env.Service.InviteAsync(env.Owner.Id, "new-student@x.vn", FamilySeats.Student, Now);

        Assert.Equal(1, env.Store.Members.Count(m => m.Status == FamilyMemberStatuses.Pending));
    }

    // ---- Leave (UC-131): F23, F25

    [Fact]
    public async Task F23_MemberLeaves_LeftOwnerNotified_SeatFreed_OldLinkStillSaysAccepted()
    {
        var env = new Env();
        var member = env.AddActive(env.Student, FamilySeats.Student, token: out var token);

        await env.Service.LeaveAsync(env.Student.Id, Now);

        Assert.Equal((FamilyMemberStatuses.Left, (DateTime?)Now), (member.Status, member.EndedAt));
        var note = Assert.Single(env.Notes.Sent);
        Assert.Equal((env.Owner.Id, NotificationTypes.FamilyMemberLeft), (note.UserId, note.Type));
        Assert.Equal(FamilyInviteStatuses.Accepted, (await env.Service.LookupAsync(token, Now)).Status);
        await env.Service.InviteAsync(env.Owner.Id, "new-student@x.vn", FamilySeats.Student, Now);
    }

    [Fact]
    public async Task F25_OwnerLeaves_OwnerCannotLeave409()
    {
        var env = new Env();

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.LeaveAsync(env.Owner.Id, Now));

        Assert.Equal(("owner_cannot_leave", 409), (ex.Code, ex.StatusCode));
    }

    [Fact]
    public async Task Leave_NotInAFamily_NotInFamily404()
    {
        var env = new Env(withFamily: false);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.LeaveAsync(env.Student.Id, Now));

        Assert.Equal(("not_in_family", 404), (ex.Code, ex.StatusCode));
    }

    // ---- Gia hạn, hết hạn, job: F26, F27, F13

    [Fact]
    public async Task F26_QueuedRenewalTakesOver_MembersAndPendingInvitesCarryOver()
    {
        var env = new Env();
        env.AddActive(env.Student, FamilySeats.Student);
        var (invite, _) = env.AddPending("p2@x.vn", FamilySeats.Parent);
        env.Cycle.EndsAt = Now.AddMinutes(-30);
        var renewal = env.Store.AddCycle(env.Owner.Id, "family-3m", null, env.Group.Id, CycleStatuses.Queued);

        var dto = await env.Service.GetAsync(env.Student.Id, UserRoles.Student, Now);

        Assert.Equal(env.Group.Id, dto.GroupId);
        Assert.Equal(CycleStatuses.Active, renewal.Status);
        Assert.Equal("Gia đình 3 tháng", dto.Cycle!.PlanName);
        Assert.Equal(FamilyMemberStatuses.Pending, invite.Status);
        Assert.Equal(2, dto.Members.Count);
    }

    [Fact]
    public async Task F27_FamilyEndsWithNothingQueued_MembershipsAndInvitesEnd()
    {
        var env = new Env();
        var member = env.AddActive(env.Student, FamilySeats.Student);
        var (invite, token) = env.AddPending("p2@x.vn", FamilySeats.Parent);
        env.Cycle.EndsAt = Now.AddMinutes(-1);

        var ex = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.GetAsync(env.Student.Id, UserRoles.Student, Now));

        Assert.Equal("not_in_family", ex.Code);
        Assert.Equal((FamilyMemberStatuses.Ended, FamilyMemberStatuses.Ended), (member.Status, invite.Status));
        var lookup = await Assert.ThrowsAsync<BillingRuleException>(() => env.Service.LookupAsync(token, Now));
        Assert.Equal("invite_invalid", lookup.Code);
    }

    [Fact]
    public async Task F13_ExpireInvites_PendingPastExpiry_ExpiredAndLinkCleared_ReturnsCount()
    {
        var env = new Env();
        var (due, _) = env.AddPending("a@x.vn", FamilySeats.Parent, expiresAt: Now);                // đúng lúc hết hạn
        var (late, _) = env.AddPending("b@x.vn", FamilySeats.Parent, expiresAt: Now.AddMinutes(-90));
        var (future, _) = env.AddPending("c@x.vn", FamilySeats.Student, expiresAt: Now.AddSeconds(1));
        var active = env.AddActive(env.Student, FamilySeats.Student);
        active.ExpiresAt = Now.AddDays(-1);                                                         // thành viên active không bị đụng

        var count = await env.Service.ExpireInvitesAsync(Now);

        Assert.Equal(2, count);
        Assert.All(new[] { due, late }, m => Assert.Equal((FamilyMemberStatuses.Expired, (string?)null), (m.Status, m.TokenHash)));
        Assert.Equal(FamilyMemberStatuses.Pending, future.Status);
        Assert.Equal(FamilyMemberStatuses.Active, active.Status);
        Assert.Equal(0, await env.Service.ExpireInvitesAsync(Now));
    }

    // ---- token và controller

    [Fact]
    public void Token_IsUrlSafe43Chars_HashIsUppercaseHexOfTrimmedToken()
    {
        var token = FamilyInviteToken.Generate();

        Assert.Equal(43, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", token);
        Assert.Equal(64, FamilyInviteToken.Hash(token).Length);
        Assert.Matches("^[0-9A-F]+$", FamilyInviteToken.Hash(token));
        Assert.Equal(FamilyInviteToken.Hash(token), FamilyInviteToken.Hash($" {token}\n"));
        Assert.NotEqual(token, FamilyInviteToken.Generate());
    }

    [Fact]
    public void NextResendAt_Rules()
    {
        var m = new FamilyMember { LastInviteSentAt = Now.AddSeconds(-10), InviteSentCount = 1 };
        Assert.Equal(Now.AddSeconds(50), FamilyInviteToken.NextResendAt(m, Now));       // 60 giây sau lần gửi cuối
        m.LastInviteSentAt = Now.AddSeconds(-60);
        Assert.Null(FamilyInviteToken.NextResendAt(m, Now));                            // đủ 60 giây
        m.InviteSentCount = 5; m.LastInviteSentAt = Now.AddHours(-2);
        Assert.Equal(Now.AddHours(22), FamilyInviteToken.NextResendAt(m, Now));         // đủ 5 lần: đợi tròn 24 giờ
        m.LastInviteSentAt = Now.AddHours(-24);
        Assert.Null(FamilyInviteToken.NextResendAt(m, Now));                            // quá 24 giờ: bộ đếm reset
        Assert.Null(FamilyInviteToken.NextResendAt(new FamilyMember(), Now));
    }

    [Fact]
    public void Controller_RequiresStudentOrParent_OnlyLookupIsAnonymous_RoutesMatchTheContract()
    {
        var type = typeof(FamilyController);
        var authorize = type.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal("student,parent", authorize!.Roles);
        Assert.Equal("api/v1/family", type.GetCustomAttribute<RouteAttribute>()!.Template);

        var anonymous = type.GetMethods().Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null).Select(m => m.Name);
        Assert.Equal(["LookupInvite"], anonymous);

        string Route(string method) =>
            type.GetMethod(method)!.GetCustomAttributes().OfType<HttpMethodAttribute>().Select(a => $"{a.HttpMethods.Single()} {a.Template}").Single();
        Assert.Equal("GET ", Route("Get"));
        Assert.Equal("POST invites", Route("Invite"));
        Assert.Equal("POST invites/{inviteId:guid}/resend", Route("Resend"));
        Assert.Equal("DELETE invites/{inviteId:guid}", Route("CancelInvite"));
        Assert.Equal("POST invites/lookup", Route("LookupInvite"));
        Assert.Equal("POST invites/accept", Route("AcceptInvite"));
        Assert.Equal("POST invites/decline", Route("DeclineInvite"));
        Assert.Equal("DELETE members/{memberId:guid}", Route("RemoveMember"));
        Assert.Equal("POST leave", Route("Leave"));
    }

    // ---- môi trường thử

    private sealed class Env
    {
        public FakeFamilyStore Store { get; } = new();
        public FakeUserRepository Users { get; } = new();
        public RecordingNotifications Notes { get; } = new();
        public FakeEmailSender Mail { get; } = new();
        public TxUnitOfWork Uow { get; }
        public FamilyService Service { get; }
        public User Owner { get; }
        public User Student { get; }
        public User Student2 { get; }
        public User Parent { get; }
        public FamilyGroup Group { get; }
        public UserSubscription Cycle { get; }

        public Env(bool withFamily = true)
        {
            Store.Users = Users;
            Owner = AddUser("Chủ gói", UserRoles.Parent, "owner@x.vn");
            Student = AddUser("Học sinh A", UserRoles.Student, "hoc.sinh.a@x.vn");
            Student2 = AddUser("Học sinh B", UserRoles.Student, "hoc.sinh.b@x.vn");
            Parent = AddUser("Phụ huynh P", UserRoles.Parent, "parent@x.vn");
            Group = new FamilyGroup { OwnerUserId = Owner.Id, Status = FamilyGroupStatuses.Active, CreatedAt = Now.AddDays(-2) };
            Cycle = null!;
            if (withFamily)
            {
                Store.Groups.Add(Group);
                Cycle = Store.AddCycle(Owner.Id, "family-1m", Now.AddDays(-2), Group.Id);
            }
            Uow = new TxUnitOfWork(Store);
            Service = new FamilyService(Store, Store, new EntitlementService(Store), Users, Notes, Mail, Uow);
        }

        public User AddUser(string name, string role, string email)
        {
            var user = new User { FullName = name, Role = role, Email = email, Status = UserStatuses.Active };
            Users.Users.Add(user);
            return user;
        }

        public FamilyMember AddActive(User user, string seatRole) => AddActive(user, seatRole, out _);

        public FamilyMember AddActive(User user, string seatRole, out string token)
        {
            token = FamilyInviteToken.Generate();
            var member = new FamilyMember
            {
                FamilyGroupId = Group.Id, UserId = user.Id, InvitedEmail = user.Email.ToLowerInvariant(), SeatRole = seatRole, Status = FamilyMemberStatuses.Active,
                TokenHash = FamilyInviteToken.Hash(token), ExpiresAt = Now.AddDays(5), InvitedAt = Now.AddDays(-1), JoinedAt = Now.AddHours(-12),
            };
            Store.Members.Add(member);
            return member;
        }

        public (FamilyMember Member, string Token) AddPending(string email, string seatRole, DateTime? expiresAt = null, DateTime? sentAt = null, int sentCount = 1)
        {
            var token = FamilyInviteToken.Generate();
            var member = new FamilyMember
            {
                FamilyGroupId = Group.Id, InvitedEmail = email.ToLowerInvariant(), SeatRole = seatRole, Status = FamilyMemberStatuses.Pending,
                TokenHash = FamilyInviteToken.Hash(token), ExpiresAt = expiresAt ?? Now.AddDays(6), InvitedAt = sentAt ?? Now.AddMinutes(-10),
                LastInviteSentAt = sentAt ?? Now.AddMinutes(-10), InviteSentCount = sentCount,
            };
            Store.Members.Add(member);
            return (member, token);
        }

        /// <summary>Một nhóm khác (chủ khác) còn kỳ chạy; owner hoặc member (nếu có) là người cần "đã ở trong gói khác".</summary>
        public UserSubscription AddOtherFamily(User? owner = null, User? member = null)
        {
            var other = new FamilyGroup { OwnerUserId = (owner ?? AddUser("Chủ khác", UserRoles.Parent, $"other-{Guid.NewGuid():N}@x.vn")).Id, Status = FamilyGroupStatuses.Active, CreatedAt = Now.AddDays(-3) };
            Store.Groups.Add(other);
            if (member is not null)
                Store.Members.Add(new FamilyMember { FamilyGroupId = other.Id, UserId = member.Id, InvitedEmail = member.Email, SeatRole = FamilySeats.Student, Status = FamilyMemberStatuses.Active });
            return Store.AddCycle(other.OwnerUserId, "family-1m", Now.AddDays(-3), other.Id);
        }

        public void Use(Guid userId, int tokens) =>
            Store.Calls.Add(new AiCall { UserId = userId, Kind = BillingRules.MeteredAiKind, TotalTokens = tokens, UserSubscriptionId = Cycle.Id, PlanCode = "family-1m", CreatedAt = Now.AddHours(-1) });

        /// <summary>Token trong mail mời mới nhất gửi tới email.</summary>
        public string TokenOf(string email) => Mail.SentEmails.Last(m => m.To == email && m.Purpose == "family_invite").Code;
    }

    private sealed class TxUnitOfWork(FakeFamilyStore store) : IUnitOfWork
    {
        public int Calls { get; private set; }

        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
        {
            Calls++;
            store.InTransaction = true;
            try { await work(ct); }
            finally { store.InTransaction = false; }
        }
    }

    private sealed class RecordingNotifications : INotificationService
    {
        public List<(Guid UserId, string Type, string Title, string? Body, string? LinkUrl)> Sent { get; } = [];

        public Task NotifyAsync(Guid userId, string type, string title, string? body, string? linkUrl, CancellationToken ct = default)
        {
            Sent.Add((userId, type, title, body, linkUrl));
            return Task.CompletedTask;
        }

        public Task<PagedResult<NotificationDto>> GetMineAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    /// <summary>
    /// Kho trong bộ nhớ cho cả IBillingRepository (EntitlementService thật) và IFamilyRepository. Giữ các ràng buộc quan trọng của DB:
    /// khóa nhóm chỉ hợp lệ trong transaction, index "một thành viên active mỗi người" và "một lời mời pending mỗi email".
    /// </summary>
    private sealed class FakeFamilyStore : IBillingRepository, IFamilyRepository
    {
        public FakeUserRepository Users { get; set; } = new();
        public List<SubscriptionPlan> Plans { get; } =
        [
            new() { Code = "free", Name = "Miễn phí", Kind = PlanKinds.Free, TokenQuota = 120_000 },
            new() { Code = "personal-1m", Name = "Cá nhân 1 tháng", Kind = PlanKinds.Personal, PriceVnd = 79_000, DurationDays = 30, TokenQuota = 500_000 },
            new() { Code = "personal-3m", Name = "Cá nhân 3 tháng", Kind = PlanKinds.Personal, PriceVnd = 199_000, DurationDays = 90, TokenQuota = 1_800_000 },
            new() { Code = "family-1m", Name = "Gia đình 1 tháng", Kind = PlanKinds.Family, PriceVnd = 99_000, DurationDays = 30, TokenQuota = 500_000 },
            new() { Code = "family-3m", Name = "Gia đình 3 tháng", Kind = PlanKinds.Family, PriceVnd = 249_000, DurationDays = 90, TokenQuota = 1_800_000 },
        ];
        public List<UserSubscription> Cycles { get; } = [];
        public List<FamilyGroup> Groups { get; } = [];
        public List<FamilyMember> Members { get; } = [];
        public List<AiCall> Calls { get; } = [];
        public List<PaymentTransaction> Orders { get; } = [];
        public HashSet<Guid> LockedGroups { get; } = [];
        public List<bool> SaveInTransaction { get; } = [];
        public bool InTransaction { get; set; }
        public bool ConflictOnNextSave { get; set; }

        public UserSubscription AddCycle(Guid owner, string planCode, DateTime? startsAt, Guid? groupId = null, string status = CycleStatuses.Active)
        {
            var plan = Plans.Single(p => p.Code == planCode);
            var cycle = new UserSubscription
            {
                UserId = owner, SubscriptionPlanId = plan.Id, Kind = plan.Kind, Status = status, FamilyGroupId = groupId, StartsAt = startsAt,
                EndsAt = startsAt?.AddDays(plan.DurationDays!.Value), DurationDays = plan.DurationDays!.Value, TokenQuota = plan.TokenQuota, PlanName = plan.Name,
            };
            Cycles.Add(cycle);
            return cycle;
        }

        // ---- IBillingRepository (như EntitlementServiceTests.FakeBilling)

        public Task<IReadOnlyList<SubscriptionPlan>> GetPlansAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SubscriptionPlan>>(Plans.ToList());

        public Task<List<UserSubscription>> GetOpenCyclesAsync(Guid ownerUserId, string kind, CancellationToken ct = default) =>
            Task.FromResult(Cycles.Where(c => c.UserId == ownerUserId && c.Kind == kind
                && (c.Status == CycleStatuses.Active || c.Status == CycleStatuses.Queued)).ToList());

        public Task<FamilyGroup?> FindActiveOwnedGroupAsync(Guid ownerUserId, CancellationToken ct = default) =>
            Task.FromResult(Groups.FirstOrDefault(g => g.OwnerUserId == ownerUserId && g.Status == FamilyGroupStatuses.Active));

        public Task<FamilyGroup?> FindActiveMemberGroupAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Groups.FirstOrDefault(g => g.Status == FamilyGroupStatuses.Active
                && Members.Any(m => m.FamilyGroupId == g.Id && m.UserId == userId && m.Status == FamilyMemberStatuses.Active)));

        public Task<List<FamilyMember>> GetOpenMembersAsync(Guid familyGroupId, CancellationToken ct = default) =>
            Task.FromResult(Members.Where(m => m.FamilyGroupId == familyGroupId
                && (m.Status == FamilyMemberStatuses.Pending || m.Status == FamilyMemberStatuses.Active)).ToList());

        public Task<long> SumCycleTokensAsync(Guid cycleId, CancellationToken ct = default) =>
            Task.FromResult(Calls.AsQueryable().Where(MeteringFilters.Cycle(cycleId)).Sum(a => (long)(a.TotalTokens ?? 0)));

        public Task<long> SumFreeTokensAsync(Guid userId, DateTime fromUtc, CancellationToken ct = default) =>
            Task.FromResult(Calls.AsQueryable().Where(MeteringFilters.Free(userId, fromUtc)).Sum(a => (long)(a.TotalTokens ?? 0)));

        public Task<string?> GetUserNameAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Users.Users.FirstOrDefault(u => u.Id == userId)?.FullName);

        public Task<long?> FindPendingOrderCodeAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Orders.Where(o => o.UserId == userId && o.Status == OrderStatuses.Pending).Select(o => (long?)o.OrderCode).FirstOrDefault());

        public Task SaveStepsAsync(IReadOnlyList<Action> steps, CancellationToken ct = default)
        {
            foreach (var step in steps)
                step();
            return Task.CompletedTask;
        }

        // ---- IFamilyRepository

        public Task<bool> LockActiveGroupAsync(Guid groupId, CancellationToken ct = default)
        {
            Assert.True(InTransaction, "Khóa dòng nhóm phải chạy trong transaction (ngoài transaction khóa nhả ngay)");
            LockedGroups.Add(groupId);
            return Task.FromResult(Groups.Any(g => g.Id == groupId && g.Status == FamilyGroupStatuses.Active));
        }

        public Task<FamilyMember?> FindMemberAsync(Guid memberId, CancellationToken ct = default) =>
            Task.FromResult(Members.FirstOrDefault(m => m.Id == memberId));

        public Task<FamilyMember?> FindMemberByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
            Task.FromResult(Members.FirstOrDefault(m => m.TokenHash == tokenHash));

        public Task<FamilyMember?> FindActiveMembershipAsync(Guid groupId, Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Members.FirstOrDefault(m => m.FamilyGroupId == groupId && m.UserId == userId && m.Status == FamilyMemberStatuses.Active));

        public Task<FamilyGroup?> FindGroupAsync(Guid groupId, CancellationToken ct = default) =>
            Task.FromResult(Groups.FirstOrDefault(g => g.Id == groupId));

        public Task<int> CountSeatsAsync(Guid groupId, string seatRole, Guid? excludeMemberId, DateTime nowUtc, CancellationToken ct = default) =>
            Task.FromResult(Members.Count(m => m.FamilyGroupId == groupId && m.SeatRole == seatRole && m.Id != excludeMemberId
                && (m.Status == FamilyMemberStatuses.Active || (m.Status == FamilyMemberStatuses.Pending && m.ExpiresAt > nowUtc))));

        public Task<bool> HasOpenInviteForEmailAsync(Guid groupId, string email, CancellationToken ct = default) =>
            Task.FromResult(Members.Any(m => m.FamilyGroupId == groupId && m.InvitedEmail == email
                && (m.Status == FamilyMemberStatuses.Pending || m.Status == FamilyMemberStatuses.Active)));

        public Task<bool> HasPendingFamilyOrderAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Orders.Any(o => o.UserId == userId && o.Status == OrderStatuses.Pending && o.PlanKind == PlanKinds.Family));

        public Task<Dictionary<Guid, long>> SumTokensByUserAsync(Guid cycleId, CancellationToken ct = default) =>
            Task.FromResult(Calls.AsQueryable().Where(MeteringFilters.Cycle(cycleId)).Where(a => a.UserId != null)
                .GroupBy(a => a.UserId!.Value).ToDictionary(g => g.Key, g => g.Sum(a => (long)(a.TotalTokens ?? 0))));

        public Task AddMemberAsync(FamilyMember member, CancellationToken ct = default)
        {
            Members.Add(member);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct = default)
        {
            SaveInTransaction.Add(InTransaction);
            if (ConflictOnNextSave)
            {
                ConflictOnNextSave = false;
                throw new ConcurrencyConflictException("xmin không khớp");
            }
            // Hai index duy nhất của DB: ném đúng lỗi nghiệp vụ như FamilyRepository.SaveAsync
            if (Members.Where(m => m.Status == FamilyMemberStatuses.Active && m.UserId != null).GroupBy(m => m.UserId).Any(g => g.Count() > 1))
                throw FamilyErrors.AlreadyInFamily();
            if (Members.Where(m => m.Status == FamilyMemberStatuses.Pending).GroupBy(m => (m.FamilyGroupId, m.InvitedEmail)).Any(g => g.Count() > 1))
                throw FamilyErrors.InviteExists();
            return Task.CompletedTask;
        }

        public Task<int> ExpireDueInvitesAsync(DateTime nowUtc, Guid? groupId = null, CancellationToken ct = default)
        {
            var due = Members.Where(m => m.Status == FamilyMemberStatuses.Pending && m.ExpiresAt <= nowUtc && (groupId == null || m.FamilyGroupId == groupId)).ToList();
            foreach (var m in due)
            {
                m.Status = FamilyMemberStatuses.Expired;
                m.TokenHash = null;
                m.EndedAt = nowUtc;
            }
            return Task.FromResult(due.Count);
        }
    }
}
