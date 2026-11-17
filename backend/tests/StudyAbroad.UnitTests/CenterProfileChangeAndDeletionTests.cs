using Microsoft.AspNetCore.Authorization;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence.Repositories;

namespace StudyAbroad.UnitTests;

/// <summary>
/// [UC-92] đánh dấu thay đổi website / fanpage, [UC-114] Moderator kiểm tra thay đổi, [UC-93] xử lý yêu cầu xóa tài khoản trung tâm.
/// </summary>
public class CenterProfileChangeAndDeletionTests
{
    private sealed class FakeAudit : IAuditLogRepository
    {
        public List<AuditLog> Logs { get; } = [];
        public Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
        {
            Logs.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotifications : INotificationService
    {
        public List<(Guid UserId, string Type, string Title, string? Body, string? Link)> Sent { get; } = [];

        public Task NotifyAsync(Guid userId, string type, string title, string? body = null, string? linkUrl = null, CancellationToken ct = default)
        {
            Sent.Add((userId, type, title, body, linkUrl));
            return Task.CompletedTask;
        }

        public Task<NotificationListDto> GetMineAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<NotificationDto> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    // ---------------- UC-92 / UC-114: thay đổi website, fanpage

    private sealed class FakeChangeRepo : ICenterProfileChangeRepository
    {
        public List<CenterProfileChange> Changes { get; } = [];
        public Guid? OwnerUserId { get; set; } = Guid.NewGuid();

        public Task<CenterProfileChange?> GetPendingAsync(Guid centerId, string field, CancellationToken ct = default) =>
            Task.FromResult(Changes.LastOrDefault(c => c.StudyCenterId == centerId && c.Field == field && c.Status == "pending"));

        public Task<CenterProfileChange?> GetForUpdateAsync(Guid changeId, CancellationToken ct = default) =>
            Task.FromResult(Changes.FirstOrDefault(c => c.Id == changeId));

        public Task AddAsync(CenterProfileChange change, CancellationToken ct = default)
        {
            Changes.Add(change);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CenterProfileChange change, CancellationToken ct = default) => Task.CompletedTask;

        public Task<PagedResult<CenterProfileChangeDto>> SearchAsync(string status, int page, int pageSize, CancellationToken ct = default)
        {
            var items = Changes.Where(c => status == "all" || c.Status == status)
                .Select(c => new CenterProfileChangeDto(c.Id, c.StudyCenterId, "Trung tâm A", c.Field, c.OldValue, c.NewValue, c.Status,
                    c.CreatedAt, c.ReviewedByUserId, c.ReviewedAt, c.ReviewReason)).ToList();
            return Task.FromResult(new PagedResult<CenterProfileChangeDto>(items, page, pageSize, items.Count));
        }

        public Task<(string Name, Guid? OwnerUserId)?> GetCenterInfoAsync(Guid centerId, CancellationToken ct = default) =>
            Task.FromResult<(string Name, Guid? OwnerUserId)?>(("Trung tâm A", OwnerUserId));
    }

    private static (CenterProfileChangeService Service, FakeChangeRepo Repo, FakeAudit Audit, FakeNotifications Notifications) ChangeSetup()
    {
        var repo = new FakeChangeRepo();
        var audit = new FakeAudit();
        var notifications = new FakeNotifications();
        return (new CenterProfileChangeService(repo, audit, notifications), repo, audit, notifications);
    }

    [Fact]
    public async Task Record_ChangedWebsite_CreatesPendingChangeWithOldAndNewValue()
    {
        var (service, repo, _, _) = ChangeSetup();
        var centerId = Guid.NewGuid();

        await service.RecordAsync(centerId, Guid.NewGuid(), CenterProfileChangeService.FieldWebsite, "https://cu.vn", " https://moi.vn ");

        var change = Assert.Single(repo.Changes);
        Assert.Equal("pending", change.Status);
        Assert.Equal("https://cu.vn", change.OldValue);
        Assert.Equal("https://moi.vn", change.NewValue);
    }

    [Theory]
    [InlineData("https://a.vn", "https://a.vn")]
    [InlineData("https://a.vn", " HTTPS://A.VN ")]
    [InlineData(null, "")]
    [InlineData("", "   ")]
    public async Task Record_SameValue_CreatesNothing(string? oldValue, string? newValue)
    {
        var (service, repo, _, _) = ChangeSetup();

        await service.RecordAsync(Guid.NewGuid(), Guid.NewGuid(), CenterProfileChangeService.FieldFanpage, oldValue, newValue);

        Assert.Empty(repo.Changes);
    }

    [Fact]
    public async Task Record_ChangedAgainBeforeReview_KeepsOriginalOldValue_AndRevertWithdraws()
    {
        var (service, repo, _, _) = ChangeSetup();
        var centerId = Guid.NewGuid();
        var user = Guid.NewGuid();

        await service.RecordAsync(centerId, user, "website", "https://goc.vn", "https://lan1.vn");
        await service.RecordAsync(centerId, user, "website", "https://lan1.vn", "https://lan2.vn");

        var change = Assert.Single(repo.Changes);
        Assert.Equal("https://goc.vn", change.OldValue);
        Assert.Equal("https://lan2.vn", change.NewValue);

        await service.RecordAsync(centerId, user, "website", "https://lan2.vn", "https://goc.vn");
        Assert.Equal("withdrawn", change.Status);
    }

    [Fact]
    public async Task Review_Failed_RequiresReason_ThenNotifiesCenterAndLogs()
    {
        var (service, repo, audit, notifications) = ChangeSetup();
        var centerId = Guid.NewGuid();
        await service.RecordAsync(centerId, Guid.NewGuid(), "fanpage", null, "https://facebook.com/x");
        var change = repo.Changes[0];
        var moderator = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewAsync(moderator, change.Id, new ReviewCenterProfileChangeRequest(false, "  ")));
        Assert.Equal("pending", change.Status);

        var dto = await service.ReviewAsync(moderator, change.Id, new ReviewCenterProfileChangeRequest(false, " Trang không thuộc trung tâm "));

        Assert.Equal("failed", dto.Status);
        Assert.Equal("Trang không thuộc trung tâm", change.ReviewReason);
        Assert.Equal(moderator, change.ReviewedByUserId);
        Assert.Equal("center.profile_change_failed", Assert.Single(audit.Logs).Action);
        var sent = Assert.Single(notifications.Sent);
        Assert.Equal(repo.OwnerUserId, sent.UserId);
        Assert.Contains("không đạt", sent.Title);
        Assert.Equal("/center/profile", sent.Link);
    }

    [Fact]
    public async Task Review_Passed_NeedsNoReason_AndCannotBeReviewedTwice()
    {
        var (service, repo, audit, _) = ChangeSetup();
        await service.RecordAsync(Guid.NewGuid(), Guid.NewGuid(), "website", null, "https://a.vn");
        var change = repo.Changes[0];

        var dto = await service.ReviewAsync(Guid.NewGuid(), change.Id, new ReviewCenterProfileChangeRequest(true));

        Assert.Equal("passed", dto.Status);
        Assert.Equal("center.profile_change_passed", Assert.Single(audit.Logs).Action);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewAsync(Guid.NewGuid(), change.Id, new ReviewCenterProfileChangeRequest(false, "đổi ý")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ReviewAsync(Guid.NewGuid(), Guid.NewGuid(), new ReviewCenterProfileChangeRequest(true)));
    }

    [Fact]
    public async Task Get_DefaultsToPending_AndRejectsUnknownFilter()
    {
        var (service, repo, _, _) = ChangeSetup();
        repo.Changes.Add(new CenterProfileChange { Field = "website", Status = "pending" });
        repo.Changes.Add(new CenterProfileChange { Field = "website", Status = "passed" });

        Assert.Single((await service.GetAsync(null, 1, 20)).Items);
        Assert.Equal(2, (await service.GetAsync("ALL", 1, 20)).Items.Count);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync("xyz", 1, 20));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("https://trungtam.vn", true)]
    [InlineData("http://trungtam.vn/gioi-thieu", true)]
    [InlineData("trungtam.vn", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("ftp://trungtam.vn", false)]
    public void IsValidPublicUrlOrEmpty_AcceptsOnlyHttpUrls(string? value, bool expected) =>
        Assert.Equal(expected, StudyCenterService.IsValidPublicUrlOrEmpty(value));

    [Fact]
    public void ChangeReviewController_IsModeratorOnly()
    {
        var attr = typeof(AdminCenterProfileChangesController).GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single();
        Assert.Equal(UserRoles.Moderator, attr.Roles);
    }

    // ---------------- UC-93: yêu cầu xóa tài khoản và dữ liệu trung tâm

    private sealed class FakeDeletionRepo : ICenterDeletionRepository
    {
        public CenterDeletionTarget? Target { get; set; } = new(Guid.NewGuid(), "Trung tâm A", "lienhe@a.vn");
        public List<CenterDeletionRequest> Requests { get; } = [];
        public List<Guid> AdminIds { get; } = [Guid.NewGuid()];
        public CenterDeletionOutcome Outcome { get; set; } = new(0, []);
        public List<(Guid? CenterId, Guid? OwnerId)> Executed { get; } = [];

        public Task<CenterDeletionTarget?> GetTargetByOwnerAsync(Guid ownerUserId, CancellationToken ct = default) => Task.FromResult(Target);

        public Task<CenterDeletionRequest?> GetLatestForUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Requests.Where(r => r.RequestedByUserId == userId).OrderByDescending(r => r.CreatedAt).FirstOrDefault());

        public Task<CenterDeletionRequest?> GetPendingForCenterAsync(Guid centerId, CancellationToken ct = default) =>
            Task.FromResult(Requests.FirstOrDefault(r => r.StudyCenterId == centerId && r.Status == "pending"));

        public Task<CenterDeletionRequest?> GetForUpdateAsync(Guid requestId, CancellationToken ct = default) =>
            Task.FromResult(Requests.FirstOrDefault(r => r.Id == requestId));

        public Task AddAsync(CenterDeletionRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CenterDeletionRequest request, CancellationToken ct = default) => Task.CompletedTask;

        public Task<PagedResult<CenterDeletionRequest>> SearchAsync(string status, int page, int pageSize, CancellationToken ct = default)
        {
            var items = Requests.Where(r => status == "all" || r.Status == status).ToList();
            return Task.FromResult(new PagedResult<CenterDeletionRequest>(items, page, pageSize, items.Count));
        }

        public Task<IReadOnlyList<Guid>> GetActiveAdminUserIdsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(AdminIds);

        public Task<CenterDeletionOutcome> ExecuteDeletionAsync(Guid? centerId, Guid? ownerUserId, CancellationToken ct = default)
        {
            Executed.Add((centerId, ownerUserId));
            return Task.FromResult(Outcome);
        }
    }

    private static (CenterDeletionService Service, FakeDeletionRepo Repo, FakeAudit Audit, FakeNotifications Notifications, FakeEmailSender Email) DeletionSetup()
    {
        var repo = new FakeDeletionRepo();
        var audit = new FakeAudit();
        var notifications = new FakeNotifications();
        var email = new FakeEmailSender();
        return (new CenterDeletionService(repo, audit, notifications, email), repo, audit, notifications, email);
    }

    [Fact]
    public async Task Request_CreatesPendingRequestWithDeadlineSnapshotsAndNotifiesAdmins()
    {
        var (service, repo, audit, notifications, _) = DeletionSetup();
        var owner = Guid.NewGuid();

        var dto = await service.RequestAsync(owner, new CreateCenterDeletionRequest("  Ngừng kinh doanh  "));

        var entity = Assert.Single(repo.Requests);
        Assert.Equal("pending", dto.Status);
        Assert.Equal("Trung tâm A", entity.CenterName);
        Assert.Equal("lienhe@a.vn", entity.ContactEmail);
        Assert.Equal("Ngừng kinh doanh", entity.Reason);
        Assert.Equal(CenterDeletionService.ProcessingDeadline, entity.DueAt - entity.CreatedAt);
        Assert.Equal("center.deletion_requested", Assert.Single(audit.Logs).Action);
        Assert.Equal(repo.AdminIds[0], Assert.Single(notifications.Sent).UserId);
    }

    [Fact]
    public async Task Request_SecondWhilePending_IsRejected_AndAccountWithoutCenter_IsNotFound()
    {
        var (service, repo, _, _, _) = DeletionSetup();
        var owner = Guid.NewGuid();
        await service.RequestAsync(owner, new CreateCenterDeletionRequest());

        await Assert.ThrowsAsync<ArgumentException>(() => service.RequestAsync(owner, new CreateCenterDeletionRequest()));
        Assert.Single(repo.Requests);

        repo.Target = null;
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RequestAsync(Guid.NewGuid(), new CreateCenterDeletionRequest()));
    }

    [Fact]
    public async Task Cancel_WithdrawsPendingRequest_AndAllowsANewOne()
    {
        var (service, repo, _, _, _) = DeletionSetup();
        var owner = Guid.NewGuid();
        await service.RequestAsync(owner, new CreateCenterDeletionRequest());

        var cancelled = await service.CancelAsync(owner);

        Assert.Equal("cancelled", cancelled.Status);
        await service.RequestAsync(owner, new CreateCenterDeletionRequest());
        Assert.Equal(2, repo.Requests.Count);
        Assert.Equal("pending", (await service.GetMineAsync(owner))!.Status);
    }

    [Fact]
    public async Task Complete_RequiresIdentityCheck_ThenDeletesNotifiesLeadUsersLogsAndEmails()
    {
        var (service, repo, audit, notifications, email) = DeletionSetup();
        var owner = Guid.NewGuid();
        var studentA = Guid.NewGuid();
        var parentB = Guid.NewGuid();
        repo.Outcome = new CenterDeletionOutcome(2, [studentA, parentB]);
        var created = await service.RequestAsync(owner, new CreateCenterDeletionRequest());
        audit.Logs.Clear();
        notifications.Sent.Clear();
        var admin = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CompleteAsync(admin, created.Id, new CompleteCenterDeletionRequest(IdentityVerified: false)));
        Assert.Empty(repo.Executed);

        var dto = await service.CompleteAsync(admin, created.Id, new CompleteCenterDeletionRequest(true, "Đã gọi xác nhận"));

        Assert.Equal("completed", dto.Status);
        Assert.True(dto.IdentityVerified);
        Assert.Equal((repo.Target!.StudyCenterId, owner), Assert.Single(repo.Executed));
        Assert.Equal("center.deleted", Assert.Single(audit.Logs).Action);
        Assert.Equal([studentA, parentB], notifications.Sent.Select(s => s.UserId).ToArray());
        Assert.All(notifications.Sent, s => Assert.Equal("lead_closed", s.Type));
        Assert.Equal(("lienhe@a.vn", "Trung tâm A", true, "Đã gọi xác nhận"), Assert.Single(email.DeletionResults));

        // Yêu cầu đã xử lý thì không xử lý lại
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CompleteAsync(admin, created.Id, new CompleteCenterDeletionRequest(true)));
        Assert.Single(repo.Executed);
    }

    [Fact]
    public async Task Reject_RequiresReason_KeepsDataNotifiesOwnerAndEmails()
    {
        var (service, repo, audit, notifications, email) = DeletionSetup();
        var owner = Guid.NewGuid();
        var created = await service.RequestAsync(owner, new CreateCenterDeletionRequest());
        audit.Logs.Clear();
        notifications.Sent.Clear();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RejectAsync(Guid.NewGuid(), created.Id, new RejectCenterDeletionRequest(" ")));

        var dto = await service.RejectAsync(Guid.NewGuid(), created.Id, new RejectCenterDeletionRequest("Không xác minh được danh tính"));

        Assert.Equal("rejected", dto.Status);
        Assert.Empty(repo.Executed);
        Assert.Equal("center.deletion_rejected", Assert.Single(audit.Logs).Action);
        Assert.Equal(owner, Assert.Single(notifications.Sent).UserId);
        Assert.False(Assert.Single(email.DeletionResults).Completed);
    }

    [Fact]
    public void Scrub_RemovesCenterAndAccountDataButKeepsRows()
    {
        var center = new StudyCenter
        {
            Code = "TT-A", Name = "Trung tâm A", Website = "https://a.vn", FanpageUrl = "https://fb.com/a", Phone = "0900000000",
            Email = "a@a.vn", TaxCode = "0123456789", LegalRepresentative = "Nguyễn Văn A", OwnerUserId = Guid.NewGuid(),
            VerificationStatus = CenterVerificationStatuses.Verified, IsActive = true, Services = ["X"], Description = "mô tả",
        };
        var user = new User { Email = "chu@a.vn", FullName = "Chủ trung tâm", Phone = "0900000000", Role = UserRoles.Center, Status = UserStatuses.Active, PasswordHash = "hash" };

        CenterDeletionRepository.ScrubCenter(center);
        CenterDeletionRepository.ScrubUser(user);

        Assert.Equal("TT-A", center.Code);
        Assert.Equal(CenterDeletionRepository.DeletedCenterName, center.Name);
        Assert.False(center.IsActive);
        Assert.Equal(CenterVerificationStatuses.Unverified, center.VerificationStatus);
        Assert.Null(center.OwnerUserId);
        Assert.All(new[] { center.Website, center.FanpageUrl, center.Phone, center.Email, center.TaxCode, center.LegalRepresentative, center.Description }, Assert.Null);
        Assert.Empty(center.Services);

        Assert.Equal(UserStatuses.Deleted, user.Status);
        Assert.EndsWith("@deleted.invalid", user.Email);
        Assert.DoesNotContain("chu@a.vn", user.Email);
        Assert.Null(user.Phone);
        Assert.NotEqual("hash", user.PasswordHash);
    }

    [Fact]
    public void DeletionControllers_AreRestrictedToCenterAndAdmin()
    {
        static string? Roles(Type t) => t.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single().Roles;

        Assert.Equal(UserRoles.Center, Roles(typeof(CenterDeletionRequestsController)));
        Assert.Equal(UserRoles.Admin, Roles(typeof(AdminCenterDeletionRequestsController)));
    }
}
