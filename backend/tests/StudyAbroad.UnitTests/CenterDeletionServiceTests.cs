using Microsoft.AspNetCore.Authorization;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-93] Administrator xử lý yêu cầu xóa tài khoản và dữ liệu trung tâm.</summary>
public class CenterDeletionServiceTests
{
    private sealed class FakeCenterRepo : IStudyCenterRepository
    {
        public List<StudyCenter> Centers { get; } = [];
        public Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));
        public Task<StudyCenter?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));
        public Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(StudyCenter center, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(StudyCenter center, CancellationToken ct = default) => Task.CompletedTask;
        public Task<StudyCenter?> GetByOwnerOrEmailAsync(Guid ownerUserId, string? email, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeRequestRepo : ICenterDeletionRequestRepository
    {
        public List<CenterDeletionRequest> Items { get; } = [];
        public Task<CenterDeletionRequest?> GetOpenByCenterIdAsync(Guid studyCenterId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.StudyCenterId == studyCenterId && x.Status == CenterDeletionRequestStatuses.Pending));
        public Task<CenterDeletionRequest?> GetLatestByCenterIdAsync(Guid studyCenterId, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(x => x.StudyCenterId == studyCenterId).OrderByDescending(x => x.CreatedAt).FirstOrDefault());
        public Task<CenterDeletionRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task AddAsync(CenterDeletionRequest request, CancellationToken ct = default) { Items.Add(request); return Task.CompletedTask; }
        public Task UpdateAsync(CenterDeletionRequest request, CancellationToken ct = default) => Task.CompletedTask;
        public Task<PagedResult<CenterDeletionRequest>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeWorkRepo(FakeRequestRepo requests) : ICenterDeletionWorkRepository
    {
        public List<ClosedLeadRecipients> LeadsToClose { get; } = [];
        public List<Guid> ClosedForCenters { get; } = [];
        public List<Notification> Notifications { get; } = [];
        public List<Guid> PurgedCenters { get; } = [];
        public List<Guid> DeletedFileFolders { get; } = [];
        public string? LastSearchStatus { get; private set; }

        public Task<PagedResult<AdminCenterDeletionRequestDto>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default)
        {
            LastSearchStatus = status;
            return Task.FromResult(new PagedResult<AdminCenterDeletionRequestDto>([], page, pageSize, 0));
        }

        public Task<AdminCenterDeletionRequestDto?> GetViewAsync(Guid id, CancellationToken ct = default)
        {
            var r = requests.Items.FirstOrDefault(x => x.Id == id);
            return Task.FromResult(r is null ? null : new AdminCenterDeletionRequestDto(
                r.Id, r.StudyCenterId, "Trung tâm", null, null, "verified", r.RequestedByUserId, null, null, true,
                r.Reason, r.Status, r.CreatedAt, r.HandledByUserId, r.HandledAt, r.DecisionNote, r.ScheduledPurgeAt, r.CompletedAt));
        }

        public Task<IReadOnlyList<ClosedLeadRecipients>> CloseOpenLeadsAsync(Guid studyCenterId, DateTime now, CancellationToken ct = default)
        {
            ClosedForCenters.Add(studyCenterId);
            return Task.FromResult<IReadOnlyList<ClosedLeadRecipients>>(LeadsToClose.ToList());
        }

        public Task AddNotificationsAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct = default)
        {
            Notifications.AddRange(notifications);
            return Task.CompletedTask;
        }

        public Task<List<Guid>> GetDuePurgeRequestIdsAsync(DateTime now, CancellationToken ct = default) =>
            Task.FromResult(requests.Items
                .Where(r => r.Status == CenterDeletionRequestStatuses.Approved && r.ScheduledPurgeAt <= now)
                .Select(r => r.Id).ToList());

        public Task PurgeCenterDataAsync(Guid studyCenterId, DateTime now, CancellationToken ct = default)
        {
            PurgedCenters.Add(studyCenterId);
            return Task.CompletedTask;
        }

        public Task DeleteCenterFilesAsync(Guid studyCenterId, CancellationToken ct = default)
        {
            DeletedFileFolders.Add(studyCenterId);
            return Task.CompletedTask;
        }

        public int OpenReports { get; set; }
        public Task<int> CountOpenReportsAsync(Guid studyCenterId, CancellationToken ct = default) => Task.FromResult(OpenReports);

        /// <summary>Mô phỏng Admin bật giữ pháp lý giữa lần đọc và lúc khóa dòng trong transaction.</summary>
        public bool HeldAtLock { get; set; }
        public Task<bool> LockCenterIfPurgeableAsync(Guid studyCenterId, CancellationToken ct = default) => Task.FromResult(!HeldAtLock);

        public List<Guid> AnonymizedUsers { get; } = [];
        public Task AnonymizeOwnerAccountAsync(Guid userId, DateTime now, CancellationToken ct = default)
        {
            AnonymizedUsers.Add(userId);
            return Task.CompletedTask;
        }
    }

    private sealed class Setup
    {
        public FakeCenterRepo Centers { get; } = new();
        public FakeRequestRepo Requests { get; } = new();
        public FakeWorkRepo Work { get; }
        public FakeUserRepository Users { get; } = new();
        public FakeAuditLogRepository Audit { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public FakeEmailSender Email { get; } = new();
        public CenterDeletionService Service { get; }
        public User Owner { get; }
        public StudyCenter Center { get; }
        public CenterDeletionRequest Request { get; }

        public Setup()
        {
            Work = new FakeWorkRepo(Requests);
            Owner = new User { Id = Guid.NewGuid(), Email = "owner@center.vn", FullName = "Chủ trung tâm", Role = UserRoles.Center, Status = UserStatuses.Active };
            Users.Users.Add(Owner);
            Users.Sessions.Add(new UserSession { UserId = Owner.Id, IsRevoked = false });
            Center = new StudyCenter { Id = Guid.NewGuid(), Name = "Du Học Á Âu", Email = "info@aau.vn", OwnerUserId = Owner.Id, IsActive = true };
            Centers.Centers.Add(Center);
            Request = new CenterDeletionRequest { StudyCenterId = Center.Id, RequestedByUserId = Owner.Id, Reason = "Ngừng hoạt động" };
            Requests.Items.Add(Request);
            Service = new CenterDeletionService(Requests, Work, Centers, Users, Audit, UnitOfWork, Email);
        }
    }

    [Fact]
    public async Task Approve_LocksOwnerHidesCenterClosesLeadsNotifiesUsersAndSchedulesPurge()
    {
        var s = new Setup();
        var student = Guid.NewGuid();
        var parent = Guid.NewGuid();
        s.Work.LeadsToClose.Add(new ClosedLeadRecipients(Guid.NewGuid(), student, parent));
        s.Work.LeadsToClose.Add(new ClosedLeadRecipients(Guid.NewGuid(), student, null));
        var adminId = Guid.NewGuid();

        var result = await s.Service.ApproveAsync(adminId, s.Request.Id, "  Đã gọi điện xác nhận với đại diện pháp luật  ");

        Assert.True(result.Success);
        Assert.Equal(UserStatuses.Locked, s.Owner.Status);
        Assert.All(s.Users.Sessions, x => Assert.True(x.IsRevoked));
        Assert.False(s.Center.IsActive);
        Assert.Equal([s.Center.Id], s.Work.ClosedForCenters);
        // Mỗi người dùng chỉ nhận một thông báo dù có nhiều lead
        Assert.Equal(2, s.Work.Notifications.Count);
        Assert.Equal(new[] { parent, student }.OrderBy(x => x), s.Work.Notifications.Select(n => n.UserId).OrderBy(x => x));
        Assert.All(s.Work.Notifications, n => Assert.Equal(CenterDeletionService.NotificationType, n.Type));
        Assert.Equal(CenterDeletionRequestStatuses.Approved, s.Request.Status);
        Assert.Equal(adminId, s.Request.HandledByUserId);
        Assert.Equal("Đã gọi điện xác nhận với đại diện pháp luật", s.Request.DecisionNote);
        Assert.NotNull(s.Request.ScheduledPurgeAt);
        // Xóa hẳn vào hạn xử lý: 20 ngày kể từ khi nhận (Nghị định 356/2025 Điều 5 khoản 4)
        Assert.Equal(s.Request.CreatedAt.AddDays(CenterDeletionDeadlines.ProcessingDays), s.Request.ScheduledPurgeAt);
        Assert.Equal("center.deletion_approve", Assert.Single(s.Audit.Entries).Action);
        Assert.Equal(1, s.UnitOfWork.Calls);
        // Email tới người gửi và email liên hệ của trung tâm
        Assert.Equal(new[] { "info@aau.vn", "owner@center.vn" }, s.Email.CenterDeletionEmails.Select(e => e.To).OrderBy(x => x));
        Assert.All(s.Email.CenterDeletionEmails, e => Assert.Equal("approved", e.Outcome));
    }

    [Fact]
    public async Task Approve_RequiresIdentityNote_AndIsNotRepeatable()
    {
        var s = new Setup();

        var noNote = await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "   ");
        Assert.False(noNote.Success);
        Assert.Equal(CenterDeletionRequestStatuses.Pending, s.Request.Status);
        Assert.Equal(UserStatuses.Active, s.Owner.Status);

        Assert.True((await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "ok")).Success);
        var again = await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "ok");
        Assert.False(again.Success);
        Assert.Single(s.Audit.Entries);

        var missing = await s.Service.ApproveAsync(Guid.NewGuid(), Guid.NewGuid(), "ok");
        Assert.True(missing.NotFound);
    }

    [Fact]
    public async Task Reject_RequiresReason_KeepsAccountActive_EmailsReason()
    {
        var s = new Setup();

        Assert.False((await s.Service.RejectAsync(Guid.NewGuid(), s.Request.Id, "")).Success);

        var result = await s.Service.RejectAsync(Guid.NewGuid(), s.Request.Id, "Không xác minh được người gửi");

        Assert.True(result.Success);
        Assert.Equal(CenterDeletionRequestStatuses.Rejected, s.Request.Status);
        Assert.Equal("Không xác minh được người gửi", s.Request.DecisionNote);
        Assert.Equal(UserStatuses.Active, s.Owner.Status);
        Assert.True(s.Center.IsActive);
        Assert.Empty(s.Work.ClosedForCenters);
        Assert.Equal("center.deletion_reject", Assert.Single(s.Audit.Entries).Action);
        Assert.All(s.Email.CenterDeletionEmails, e => Assert.Equal(("rejected", "Không xác minh được người gửi"), (e.Outcome, e.Note)));
        Assert.False((await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "ok")).Success);
    }

    [Fact]
    public async Task PurgeDue_OnlyAfterDeadline_DeletesDataAnonymizesOwnerThenCompletes()
    {
        var s = new Setup();
        await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "ok");
        s.Email.CenterDeletionEmails.Clear();

        var early = await s.Service.PurgeDueAsync(DateTime.UtcNow.AddDays(19));
        Assert.Equal(0, early.Purged);
        Assert.Empty(s.Work.PurgedCenters);

        var due = await s.Service.PurgeDueAsync(DateTime.UtcNow.AddDays(31));

        Assert.Equal(1, due.Purged);
        Assert.Empty(due.Failures);
        Assert.Equal([s.Center.Id], s.Work.PurgedCenters);
        // Tài khoản chủ được ẩn danh hóa, không xóa dòng, để tin nhắn trung tâm đã gửi còn làm bằng chứng
        Assert.Equal([s.Owner.Id], s.Work.AnonymizedUsers);
        Assert.Equal([s.Center.Id], s.Work.DeletedFileFolders);
        Assert.Equal(CenterDeletionRequestStatuses.Completed, s.Request.Status);
        Assert.NotNull(s.Request.CompletedAt);
        Assert.Contains(s.Audit.Entries, e => e.Action == "center.purge");
        Assert.All(s.Email.CenterDeletionEmails, e => Assert.Equal("completed", e.Outcome));

        // Chạy lại không xóa lần hai
        var rerun = await s.Service.PurgeDueAsync(DateTime.UtcNow.AddDays(40));
        Assert.Equal(0, rerun.Purged);
        Assert.Single(s.Work.AnonymizedUsers);
    }

    [Fact]
    public async Task PurgeDue_SkipsCenterUnderLegalHold_ProtectsEvidence()
    {
        var s = new Setup();
        await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "ok");
        s.Center.IsLegalHold = true;
        s.Center.LegalHoldReason = "Đang phối hợp cơ quan điều tra xác minh khiếu nại lừa đảo";

        var result = await s.Service.PurgeDueAsync(DateTime.UtcNow.AddDays(35));

        Assert.Equal(0, result.Purged);
        Assert.Empty(s.Work.PurgedCenters);
        Assert.Empty(s.Work.AnonymizedUsers);
        Assert.Equal(CenterDeletionRequestStatuses.Approved, s.Request.Status);
    }

    [Fact]
    public async Task PurgeDue_SkipsWhenOnlyOwnerAccountIsHeld()
    {
        var s = new Setup();
        await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "ok");
        s.Owner.IsLegalHold = true;

        var result = await s.Service.PurgeDueAsync(DateTime.UtcNow.AddDays(35));

        Assert.Equal(0, result.Purged);
        Assert.Empty(s.Work.PurgedCenters);
        Assert.Equal(CenterDeletionRequestStatuses.Approved, s.Request.Status);
    }

    [Fact]
    public async Task PurgeDue_HoldSetDuringRun_IsRecheckedInsideTransaction()
    {
        var s = new Setup();
        await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "ok");
        s.Email.CenterDeletionEmails.Clear();
        s.Work.HeldAtLock = true;

        var result = await s.Service.PurgeDueAsync(DateTime.UtcNow.AddDays(35));

        Assert.Equal(0, result.Purged);
        Assert.Empty(s.Work.PurgedCenters);
        Assert.Empty(s.Work.AnonymizedUsers);
        Assert.Empty(s.Work.DeletedFileFolders);
        Assert.Empty(s.Email.CenterDeletionEmails);
        Assert.Equal(CenterDeletionRequestStatuses.Approved, s.Request.Status);
        Assert.DoesNotContain(s.Audit.Entries, e => e.Action == "center.purge");
    }

    [Fact]
    public async Task Approve_IsBlockedWhileCenterOrOwnerIsUnderLegalHold()
    {
        var s = new Setup();
        s.Center.IsLegalHold = true;

        var blocked = await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "ok");
        Assert.False(blocked.Success);
        Assert.Contains("giữ pháp lý", blocked.Error);
        Assert.Equal(CenterDeletionRequestStatuses.Pending, s.Request.Status);
        Assert.Equal(UserStatuses.Active, s.Owner.Status);
        Assert.True(s.Center.IsActive);
        Assert.Empty(s.Audit.Entries);

        s.Center.IsLegalHold = false;
        s.Owner.IsLegalHold = true;
        Assert.False((await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "ok")).Success);

        // Vẫn được từ chối yêu cầu khi đang giữ
        Assert.True((await s.Service.RejectAsync(Guid.NewGuid(), s.Request.Id, "Đang có khiếu nại cần xác minh")).Success);
    }

    [Fact]
    public async Task Approve_RecordsOpenReportCountInAudit()
    {
        var s = new Setup();
        s.Work.OpenReports = 3;

        Assert.True((await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "ok")).Success);

        Assert.Contains("\"openReports\":3", Assert.Single(s.Audit.Entries).DetailJson);
    }

    private static string? Detail(AuditLog audit, string property) =>
        System.Text.Json.JsonDocument.Parse(audit.DetailJson!).RootElement.GetProperty(property).GetString();

    [Fact]
    public async Task SetLegalHold_HoldsCenterAndOwner_AuditsReason_AndBlocksDeletion()
    {
        var s = new Setup();
        var adminId = Guid.NewGuid();

        Assert.False((await s.Service.SetLegalHoldAsync(adminId, s.Center.Id, "  ")).Success);
        Assert.False((await s.Service.SetLegalHoldAsync(adminId, s.Center.Id, new string('x', 501))).Success);
        Assert.True((await s.Service.SetLegalHoldAsync(adminId, Guid.NewGuid(), "x")).NotFound);

        var result = await s.Service.SetLegalHoldAsync(adminId, s.Center.Id, "  Công văn số 12/CSĐT về khiếu nại lừa đảo  ");

        Assert.True(result.Success);
        Assert.True(result.Hold!.IsLegalHold);
        Assert.Equal("Công văn số 12/CSĐT về khiếu nại lừa đảo", result.Hold.Reason);
        Assert.Equal(s.Request.Id, result.Hold.OpenDeletionRequestId);
        Assert.True(s.Center.IsLegalHold);
        Assert.Equal(adminId, s.Center.LegalHoldByUserId);
        Assert.NotNull(s.Center.LegalHoldAt);
        Assert.True(s.Owner.IsLegalHold);
        Assert.Equal("Công văn số 12/CSĐT về khiếu nại lừa đảo", s.Owner.LegalHoldReason);
        var audit = Assert.Single(s.Audit.Entries);
        Assert.Equal(("center.legal_hold_set", adminId, s.Center.Id.ToString()), (audit.Action, audit.ActorUserId, audit.EntityId));
        Assert.Equal("Công văn số 12/CSĐT về khiếu nại lừa đảo", Detail(audit, "reason"));

        Assert.False((await s.Service.SetLegalHoldAsync(adminId, s.Center.Id, "lần hai")).Success);
        Assert.False((await s.Service.ApproveAsync(adminId, s.Request.Id, "ok")).Success);
    }

    [Fact]
    public async Task ReleaseLegalHold_RequiresReason_ClearsBoth_AuditsAndAllowsPurgeAgain()
    {
        var s = new Setup();
        var adminId = Guid.NewGuid();
        await s.Service.ApproveAsync(adminId, s.Request.Id, "ok");
        await s.Service.SetLegalHoldAsync(adminId, s.Center.Id, "Đang điều tra");
        Assert.Equal(0, (await s.Service.PurgeDueAsync(DateTime.UtcNow.AddDays(35))).Purged);

        Assert.False((await s.Service.ReleaseLegalHoldAsync(adminId, s.Center.Id, "")).Success);
        var released = await s.Service.ReleaseLegalHoldAsync(adminId, s.Center.Id, "Cơ quan điều tra kết luận không có vi phạm");

        Assert.True(released.Success);
        Assert.False(released.Hold!.IsLegalHold);
        Assert.False(s.Center.IsLegalHold);
        Assert.Null(s.Center.LegalHoldReason);
        Assert.Null(s.Center.LegalHoldAt);
        Assert.False(s.Owner.IsLegalHold);
        var audit = s.Audit.Entries.Last();
        Assert.Equal("center.legal_hold_release", audit.Action);
        Assert.Equal("Đang điều tra", Detail(audit, "previousReason"));   // giữ lại lý do cũ để truy vết
        Assert.Equal("Cơ quan điều tra kết luận không có vi phạm", Detail(audit, "reason"));

        Assert.False((await s.Service.ReleaseLegalHoldAsync(adminId, s.Center.Id, "lần hai")).Success);
        Assert.Equal(1, (await s.Service.PurgeDueAsync(DateTime.UtcNow.AddDays(35))).Purged);
    }

    [Fact]
    public async Task Approve_OverdueRequest_StillGetsAtLeastOneDayBeforePurge()
    {
        var s = new Setup();
        s.Request.CreatedAt = DateTime.UtcNow.AddDays(-25);   // đã quá hạn 20 ngày

        Assert.True((await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "ok")).Success);

        Assert.True(s.Request.ScheduledPurgeAt >= DateTime.UtcNow.AddHours(23));
        Assert.Equal(0, (await s.Service.PurgeDueAsync(DateTime.UtcNow.AddHours(1))).Purged);
    }

    [Fact]
    public async Task Extend_OnceOnly_RequiresReason_EmailsRequesterAndMovesPurgeDate()
    {
        var s = new Setup();
        var adminId = Guid.NewGuid();
        var originalDue = CenterDeletionDeadlines.DueAt(s.Request);

        Assert.False((await s.Service.ExtendAsync(adminId, s.Request.Id, "  ")).Success);
        Assert.False((await s.Service.ExtendAsync(adminId, s.Request.Id, new string('x', 1001))).Success);
        Assert.True((await s.Service.ExtendAsync(adminId, Guid.NewGuid(), "x")).NotFound);

        var result = await s.Service.ExtendAsync(adminId, s.Request.Id, "  Cần xác minh thêm người đại diện  ");

        Assert.True(result.Success);
        Assert.Equal(originalDue.AddDays(CenterDeletionDeadlines.MaxExtensionDays), s.Request.DueAt);
        Assert.NotNull(s.Request.ExtendedAt);
        Assert.Equal("Cần xác minh thêm người đại diện", s.Request.ExtensionNote);
        var audit = Assert.Single(s.Audit.Entries);
        Assert.Equal(("center.deletion_extend", adminId), (audit.Action, audit.ActorUserId));
        Assert.Equal("Cần xác minh thêm người đại diện", Detail(audit, "reason"));
        // Người yêu cầu và email liên hệ đều được báo lý do và hạn mới
        Assert.Equal(new[] { "info@aau.vn", "owner@center.vn" }, s.Email.CenterDeletionEmails.Select(e => e.To).OrderBy(x => x));
        Assert.All(s.Email.CenterDeletionEmails, e =>
        {
            Assert.Equal("extended", e.Outcome);
            Assert.Equal("Cần xác minh thêm người đại diện", e.Note);
            Assert.Equal(s.Request.DueAt, e.PurgeAt);
        });

        // Chỉ một lần
        var again = await s.Service.ExtendAsync(adminId, s.Request.Id, "Thêm lần nữa");
        Assert.False(again.Success);
        Assert.Equal(originalDue.AddDays(20), s.Request.DueAt);
    }

    [Fact]
    public async Task Extend_AfterApproval_PushesScheduledPurge_ButNotAfterCompletionOrRejection()
    {
        var s = new Setup();
        await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "ok");
        var purgeBefore = s.Request.ScheduledPurgeAt!.Value;

        Assert.True((await s.Service.ExtendAsync(Guid.NewGuid(), s.Request.Id, "Chờ cơ quan chức năng phản hồi")).Success);
        Assert.Equal(purgeBefore.AddDays(20), s.Request.ScheduledPurgeAt);
        Assert.Equal(0, (await s.Service.PurgeDueAsync(DateTime.UtcNow.AddDays(30))).Purged);

        var done = new Setup();
        await done.Service.ApproveAsync(Guid.NewGuid(), done.Request.Id, "ok");
        await done.Service.PurgeDueAsync(DateTime.UtcNow.AddDays(21));
        Assert.False((await done.Service.ExtendAsync(Guid.NewGuid(), done.Request.Id, "Muộn")).Success);

        var rejected = new Setup();
        await rejected.Service.RejectAsync(Guid.NewGuid(), rejected.Request.Id, "Không xác minh được");
        Assert.False((await rejected.Service.ExtendAsync(Guid.NewGuid(), rejected.Request.Id, "x")).Success);
    }

    [Fact]
    public async Task SetLegalHold_WithOpenDeletionRequest_SendsGenericNoticeWithoutInvestigationDetails()
    {
        var s = new Setup();

        await s.Service.SetLegalHoldAsync(Guid.NewGuid(), s.Center.Id, "Công văn số 12/CSĐT, nghi lừa đảo");

        // Luật 91/2025 Điều 14 khoản 5: không xóa được vì lý do chính đáng thì phải thông báo cho người yêu cầu
        Assert.Equal(new[] { "info@aau.vn", "owner@center.vn" }, s.Email.CenterDeletionEmails.Select(e => e.To).OrderBy(x => x));
        Assert.All(s.Email.CenterDeletionEmails, e =>
        {
            Assert.Equal("held", e.Outcome);
            Assert.Null(e.Note);   // không lộ lý do giữ hay việc điều tra
        });
    }

    [Fact]
    public async Task SetLegalHold_WithoutOpenDeletionRequest_SendsNoNotice()
    {
        var s = new Setup();
        await s.Service.RejectAsync(Guid.NewGuid(), s.Request.Id, "Không xác minh được");
        s.Email.CenterDeletionEmails.Clear();

        await s.Service.SetLegalHoldAsync(Guid.NewGuid(), s.Center.Id, "Khiếu nại lừa đảo");

        Assert.Empty(s.Email.CenterDeletionEmails);
    }

    [Fact]
    public async Task SetLegalHold_RefusedAfterDataWasPurged()
    {
        var s = new Setup();
        await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "ok");
        await s.Service.PurgeDueAsync(DateTime.UtcNow.AddDays(35));

        var result = await s.Service.SetLegalHoldAsync(Guid.NewGuid(), s.Center.Id, "Muộn");

        Assert.False(result.Success);
        Assert.False(s.Center.IsLegalHold);
    }

    [Fact]
    public async Task Search_ValidatesFilterAndTreatsAllAsNoFilter()
    {
        var s = new Setup();

        await s.Service.SearchAsync("ALL", 1, 20);
        Assert.Null(s.Work.LastSearchStatus);
        await s.Service.SearchAsync("Approved", 1, 20);
        Assert.Equal("approved", s.Work.LastSearchStatus);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.SearchAsync("unknown", 1, 20));
    }

    [Fact]
    public void Controllers_AreAdminOnly()
    {
        foreach (var controller in new[] { typeof(AdminCenterDeletionRequestsController), typeof(AdminCenterLegalHoldController) })
        {
            var attr = controller
                .GetCustomAttributes(typeof(AuthorizeAttribute), false)
                .Cast<AuthorizeAttribute>().Single();
            Assert.Equal(UserRoles.Admin, attr.Roles);
        }
    }
}
