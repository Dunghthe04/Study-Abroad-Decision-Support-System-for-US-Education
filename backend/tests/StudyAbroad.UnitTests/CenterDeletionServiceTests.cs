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
    }

    private sealed class FakeAccountPurge : IAccountPurgeRepository
    {
        public List<Guid> DeletedUsers { get; } = [];
        public Task<List<Guid>> GetDueUserIdsAsync(DateTime deletedBefore, IReadOnlyCollection<string> roles, CancellationToken ct = default) => Task.FromResult(new List<Guid>());
        public Task<bool> LockIfStillDueAsync(Guid userId, DateTime deletedBefore, IReadOnlyCollection<string> roles, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> ClearPaymentPayloadsAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<ForumPurgeResult> PurgeForumPostsAsync(Guid userId, DateTime now, CancellationToken ct = default) => Task.FromResult(new ForumPurgeResult(0, 0));
        public Task DeleteUserAsync(Guid userId, CancellationToken ct = default) { DeletedUsers.Add(userId); return Task.CompletedTask; }
    }

    private sealed class Setup
    {
        public FakeCenterRepo Centers { get; } = new();
        public FakeRequestRepo Requests { get; } = new();
        public FakeWorkRepo Work { get; }
        public FakeUserRepository Users { get; } = new();
        public FakeAccountPurge Purge { get; } = new();
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
            Service = new CenterDeletionService(Requests, Work, Centers, Users, Purge, Audit, UnitOfWork, Email,
                new AccountDeletionOptions { RetentionPeriod = TimeSpan.FromDays(30) });
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
        Assert.InRange(s.Request.ScheduledPurgeAt!.Value - s.Request.HandledAt!.Value, TimeSpan.FromDays(30), TimeSpan.FromDays(30));
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
    public async Task PurgeDue_OnlyAfterDeadline_DeletesDataAndOwnerThenCompletes()
    {
        var s = new Setup();
        await s.Service.ApproveAsync(Guid.NewGuid(), s.Request.Id, "ok");
        s.Email.CenterDeletionEmails.Clear();

        var early = await s.Service.PurgeDueAsync(DateTime.UtcNow.AddDays(29));
        Assert.Equal(0, early.Purged);
        Assert.Empty(s.Work.PurgedCenters);

        var due = await s.Service.PurgeDueAsync(DateTime.UtcNow.AddDays(31));

        Assert.Equal(1, due.Purged);
        Assert.Empty(due.Failures);
        Assert.Equal([s.Center.Id], s.Work.PurgedCenters);
        Assert.Equal([s.Owner.Id], s.Purge.DeletedUsers);
        Assert.Equal([s.Center.Id], s.Work.DeletedFileFolders);
        Assert.Equal(CenterDeletionRequestStatuses.Completed, s.Request.Status);
        Assert.NotNull(s.Request.CompletedAt);
        Assert.Contains(s.Audit.Entries, e => e.Action == "center.purge");
        Assert.All(s.Email.CenterDeletionEmails, e => Assert.Equal("completed", e.Outcome));

        // Chạy lại không xóa lần hai
        var rerun = await s.Service.PurgeDueAsync(DateTime.UtcNow.AddDays(40));
        Assert.Equal(0, rerun.Purged);
        Assert.Single(s.Purge.DeletedUsers);
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
        Assert.Empty(s.Purge.DeletedUsers);
        Assert.Equal(CenterDeletionRequestStatuses.Approved, s.Request.Status);
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
    public void Controller_IsAdminOnly()
    {
        var attr = typeof(AdminCenterDeletionRequestsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single();
        Assert.Equal(UserRoles.Admin, attr.Roles);
    }
}
