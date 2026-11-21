using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[USAS-2138 / UC-114] Moderator kiểm tra thay đổi website / fanpage do trung tâm tự sửa.</summary>
public class CenterProfileChangeReviewTests
{
    private sealed class FakeChangeRepo : ICenterProfileChangeRepository
    {
        public List<CenterProfileChange> Items { get; } = [];
        public List<CenterChangeCenterInfo> Centers { get; } = [];
        public List<Notification> Notifications { get; } = [];
        public int Updates { get; private set; }
        public string? LastSearchStatus { get; private set; } = "unset";

        public Task<CenterProfileChange?> GetPendingAsync(Guid studyCenterId, string field, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x =>
                x.StudyCenterId == studyCenterId && x.Field == field && x.Status == CenterProfileChangeStatuses.Pending));

        public Task<CenterProfileChange?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

        public Task AddAsync(CenterProfileChange change, CancellationToken ct = default)
        {
            Items.Add(change);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CenterProfileChange change, CancellationToken ct = default)
        {
            Updates++;
            return Task.CompletedTask;
        }

        public Task<PagedResult<CenterProfileChange>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default)
        {
            LastSearchStatus = status;
            var rows = Items.Where(x => status == null || x.Status == status).ToList();
            return Task.FromResult(new PagedResult<CenterProfileChange>(rows, page, pageSize, rows.Count));
        }

        public Task<IReadOnlyList<CenterChangeCenterInfo>> GetCenterInfosAsync(IReadOnlyCollection<Guid> centerIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CenterChangeCenterInfo>>(Centers.Where(c => centerIds.Contains(c.Id)).ToList());

        public Task AddNotificationAsync(Notification notification, CancellationToken ct = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAudit : IAuditLogRepository
    {
        public List<AuditLog> Logs { get; } = [];
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
        {
            Logs.Add(entry);
            return Task.CompletedTask;
        }
        public Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private static (CenterProfileChangeReviewService Service, FakeChangeRepo Repo, FakeAudit Audit, CenterProfileChange Change, Guid OwnerId) Setup(
        string status = CenterProfileChangeStatuses.Pending)
    {
        var repo = new FakeChangeRepo();
        var audit = new FakeAudit();
        var ownerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        repo.Centers.Add(new CenterChangeCenterInfo(centerId, "Trung Tâm A", ownerId, CenterVerificationStatuses.Verified, true));
        var change = new CenterProfileChange
        {
            StudyCenterId = centerId,
            Field = CenterProfileChangeFields.Website,
            OldValue = "https://cu.example.com",
            NewValue = "https://moi.example.com",
            Status = status,
        };
        repo.Items.Add(change);
        return (new CenterProfileChangeReviewService(repo, audit), repo, audit, change, ownerId);
    }

    [Fact]
    public async Task Search_DefaultsToPendingAndIncludesCenterInfo()
    {
        var (service, repo, _, change, _) = Setup();
        repo.Items.Add(new CenterProfileChange { StudyCenterId = change.StudyCenterId, Field = "fanpage", Status = CenterProfileChangeStatuses.Passed });

        var page = await service.SearchAsync(null, 1, 20);

        Assert.Equal(CenterProfileChangeStatuses.Pending, repo.LastSearchStatus);
        var item = Assert.Single(page.Items);
        Assert.Equal("Trung Tâm A", item.StudyCenterName);
        Assert.Equal(CenterVerificationStatuses.Verified, item.CenterVerificationStatus);
        Assert.True(item.CenterIsActive);
        Assert.Equal("https://cu.example.com", item.OldValue);
        Assert.Equal("https://moi.example.com", item.NewValue);
    }

    [Fact]
    public async Task Search_AllPassesNullAndUnknownFilterThrows()
    {
        var (service, repo, _, _, _) = Setup();

        await service.SearchAsync("ALL", 1, 20);
        Assert.Null(repo.LastSearchStatus);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync("approved", 1, 20));
    }

    [Fact]
    public async Task Pass_MarksPassedSetsReviewerAndLogsAudit_WithoutNotification()
    {
        var (service, repo, audit, change, _) = Setup();
        var moderatorId = Guid.NewGuid();

        var result = await service.PassAsync(change.Id, change.NewValue, moderatorId);

        Assert.True(result.Success);
        Assert.Equal(CenterProfileChangeStatuses.Passed, change.Status);
        Assert.Equal(moderatorId, change.ReviewedByUserId);
        Assert.NotNull(change.ReviewedAt);
        Assert.Null(change.ReviewReason);
        Assert.Equal(1, repo.Updates);
        var log = Assert.Single(audit.Logs);
        Assert.Equal("center_change.pass", log.Action);
        Assert.Equal(moderatorId, log.ActorUserId);
        Assert.Empty(repo.Notifications);
    }

    [Fact]
    public async Task Fail_RequiresReason_AndRejectsTooLongReason()
    {
        var (service, repo, audit, change, _) = Setup();

        var empty = await service.FailAsync(change.Id, "   ", change.NewValue, Guid.NewGuid());
        var tooLong = await service.FailAsync(change.Id, new string('a', CenterProfileChangeReviewService.MaxReasonLength + 1), change.NewValue, Guid.NewGuid());

        Assert.False(empty.Success);
        Assert.False(tooLong.Success);
        Assert.Equal(CenterProfileChangeStatuses.Pending, change.Status);
        Assert.Equal(0, repo.Updates);
        Assert.Empty(audit.Logs);
    }

    [Fact]
    public async Task Fail_MarksFailedNotifiesCenterOwnerAndLogsOldNewValues()
    {
        var (service, repo, audit, change, ownerId) = Setup();
        var moderatorId = Guid.NewGuid();

        var result = await service.FailAsync(change.Id, "  Website dẫn tới trang cá cược  ", change.NewValue, moderatorId);

        Assert.True(result.Success);
        Assert.Equal(CenterProfileChangeStatuses.Failed, change.Status);
        Assert.Equal("Website dẫn tới trang cá cược", change.ReviewReason);
        Assert.Equal(moderatorId, change.ReviewedByUserId);

        var notification = Assert.Single(repo.Notifications);
        Assert.Equal(ownerId, notification.UserId);
        Assert.Equal("center_change_failed", notification.Type);
        Assert.Equal("/center/profile", notification.LinkUrl);
        Assert.Contains("Website dẫn tới trang cá cược", notification.Body);

        var log = Assert.Single(audit.Logs);
        Assert.Equal("center_change.fail", log.Action);
        using var detail = JsonDocument.Parse(log.DetailJson!);
        Assert.Equal("https://cu.example.com", detail.RootElement.GetProperty("oldValue").GetString());
        Assert.Equal("https://moi.example.com", detail.RootElement.GetProperty("newValue").GetString());
    }

    [Theory]
    [InlineData(CenterProfileChangeStatuses.Passed)]
    [InlineData(CenterProfileChangeStatuses.Failed)]
    [InlineData(CenterProfileChangeStatuses.Withdrawn)]
    public async Task AlreadyReviewed_IsRejected_AndNothingChanges(string status)
    {
        var (service, repo, audit, change, _) = Setup(status);

        var pass = await service.PassAsync(change.Id, change.NewValue, Guid.NewGuid());
        var fail = await service.FailAsync(change.Id, "Lý do", change.NewValue, Guid.NewGuid());

        Assert.False(pass.Success);
        Assert.False(pass.NotFound);
        Assert.False(fail.Success);
        Assert.Equal(status, change.Status);
        Assert.Equal(0, repo.Updates);
        Assert.Empty(audit.Logs);
        Assert.Empty(repo.Notifications);
    }

    [Fact]
    public async Task UnknownChange_ReturnsNotFound()
    {
        var (service, _, _, _, _) = Setup();

        Assert.True((await service.PassAsync(Guid.NewGuid(), "x", Guid.NewGuid())).NotFound);
        Assert.True((await service.FailAsync(Guid.NewGuid(), "Lý do", "x", Guid.NewGuid())).NotFound);
    }

    [Fact]
    public async Task PassAndFail_WhenCenterEditedAgainAfterModeratorOpenedList_AreRejected()
    {
        var (service, repo, audit, change, _) = Setup();
        var seen = change.NewValue;                       // giá trị Moderator đang xem
        change.NewValue = "https://khac.example.com";     // trung tâm sửa tiếp trong lúc đó

        var pass = await service.PassAsync(change.Id, seen, Guid.NewGuid());
        var fail = await service.FailAsync(change.Id, "Lý do", seen, Guid.NewGuid());

        Assert.False(pass.Success);
        Assert.False(fail.Success);
        Assert.Contains("tải lại", pass.Error);
        Assert.Equal(CenterProfileChangeStatuses.Pending, change.Status);
        Assert.Equal(0, repo.Updates);
        Assert.Empty(audit.Logs);
        Assert.Empty(repo.Notifications);
    }

    [Fact]
    public async Task Pass_EmptyAndNullValueAreTheSame_WhenCenterClearedTheField()
    {
        var (service, _, _, change, _) = Setup();
        change.NewValue = null;   // trung tâm xóa website

        var result = await service.PassAsync(change.Id, "", Guid.NewGuid());

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Search_WithdrawnIsAValidFilter()
    {
        var (service, repo, _, _, _) = Setup();

        await service.SearchAsync("withdrawn", 1, 20);

        Assert.Equal(CenterProfileChangeStatuses.Withdrawn, repo.LastSearchStatus);
    }

    [Fact]
    public async Task Fail_CenterWithoutOwner_StillSavesWithoutNotification()
    {
        var (service, repo, _, change, _) = Setup();
        repo.Centers[0] = repo.Centers[0] with { OwnerUserId = null };

        var result = await service.FailAsync(change.Id, "Lý do", change.NewValue, Guid.NewGuid());

        Assert.True(result.Success);
        Assert.Empty(repo.Notifications);
    }

    // ---- Controller ----

    private static (ModeratorCenterProfileChangesController Controller, FakeChangeRepo Repo, CenterProfileChange Change) CreateController(Guid userId)
    {
        var (service, repo, _, change, _) = Setup();
        var controller = new ModeratorCenterProfileChangesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, UserRoles.Moderator)], "test")),
                },
            },
        };
        return (controller, repo, change);
    }

    [Fact]
    public void Controller_IsModeratorOnly()
    {
        var attr = typeof(ModeratorCenterProfileChangesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single();
        Assert.Equal(UserRoles.Moderator, attr.Roles);
    }

    [Fact]
    public async Task Controller_PassAndFail_MapResults()
    {
        var (controller, _, change) = CreateController(Guid.NewGuid());

        var seen = new PassCenterProfileChangeRequest(change.NewValue);
        Assert.IsType<NotFoundObjectResult>(await controller.Pass(Guid.NewGuid(), seen, CancellationToken.None));
        Assert.IsType<ObjectResult>(await controller.Fail(change.Id, new FailCenterProfileChangeRequest(null, change.NewValue), CancellationToken.None));

        // thiếu body: không cho duyệt khi không biết Moderator đã xem giá trị nào
        Assert.IsType<ObjectResult>(await controller.Pass(change.Id, null, CancellationToken.None));
        Assert.IsType<ObjectResult>(await controller.Fail(change.Id, null, CancellationToken.None));

        var ok = Assert.IsType<OkObjectResult>(await controller.Pass(change.Id, seen, CancellationToken.None));
        Assert.Equal(CenterProfileChangeStatuses.Passed, Assert.IsType<CenterProfileChangeDto>(ok.Value).Status);

        // lần hai: đã kiểm tra -> 400
        Assert.IsType<ObjectResult>(await controller.Pass(change.Id, seen, CancellationToken.None));
    }

    [Fact]
    public async Task Controller_List_InvalidFilter_ReturnsValidationProblem()
    {
        var (controller, _, _) = CreateController(Guid.NewGuid());

        Assert.IsType<OkObjectResult>(await controller.List(null, 1, 20, CancellationToken.None));
        Assert.IsType<ObjectResult>(await controller.List("bogus", 1, 20, CancellationToken.None));
    }
}
