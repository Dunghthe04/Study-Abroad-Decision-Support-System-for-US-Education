using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

/// <summary>[USAS-2134 / UC-110] Xem thông báo: chỉ thấy thông báo của mình, đánh dấu đã đọc, đường dẫn nội bộ.</summary>
public class NotificationServiceTests
{
    private sealed class FakeRepo : INotificationRepository
    {
        public List<Notification> Items { get; } = [];

        public Task<PagedResult<Notification>> GetPageAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default)
        {
            var query = Items.Where(n => n.UserId == userId && (!unreadOnly || n.ReadAt is null))
                .OrderByDescending(n => n.CreatedAt).ToList();
            return Task.FromResult(new PagedResult<Notification>(
                query.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, query.Count));
        }

        public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Items.Count(n => n.UserId == userId && n.ReadAt is null));

        public Task<Notification?> GetForUpdateAsync(Guid userId, Guid notificationId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(n => n.Id == notificationId && n.UserId == userId));

        public Task SaveAsync(Notification notification, CancellationToken ct = default) => Task.CompletedTask;

        public Task<int> MarkAllReadAsync(Guid userId, DateTime readAtUtc, CancellationToken ct = default)
        {
            var unread = Items.Where(n => n.UserId == userId && n.ReadAt is null).ToList();
            foreach (var n in unread) n.ReadAt = readAtUtc;
            return Task.FromResult(unread.Count);
        }

        public Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken ct = default)
        {
            Items.AddRange(notifications);
            return Task.CompletedTask;
        }
    }

    private static Notification Add(FakeRepo repo, Guid userId, string title, int minutesAgo = 0, bool read = false, string? link = null)
    {
        var n = new Notification
        {
            UserId = userId, Type = "test", Title = title, LinkUrl = link,
            CreatedAt = DateTime.UtcNow.AddMinutes(-minutesAgo),
            ReadAt = read ? DateTime.UtcNow : null,
        };
        repo.Items.Add(n);
        return n;
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyOwnNotificationsNewestFirstWithUnreadCount()
    {
        var repo = new FakeRepo();
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        Add(repo, me, "cũ", minutesAgo: 10, read: true);
        Add(repo, me, "mới", minutesAgo: 1);
        Add(repo, other, "của người khác");
        var service = new NotificationService(repo);

        var result = await service.GetMineAsync(me, unreadOnly: false, page: 1, pageSize: 20);

        Assert.Equal(["mới", "cũ"], result.Items.Select(i => i.Title).ToArray());
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.UnreadCount);
        Assert.DoesNotContain(result.Items, i => i.Title == "của người khác");
    }

    [Fact]
    public async Task GetMine_UnreadOnly_FiltersAndClampsPaging()
    {
        var repo = new FakeRepo();
        var me = Guid.NewGuid();
        Add(repo, me, "đã đọc", read: true);
        Add(repo, me, "chưa đọc");
        var service = new NotificationService(repo);

        var result = await service.GetMineAsync(me, unreadOnly: true, page: 0, pageSize: 999);

        Assert.Equal("chưa đọc", Assert.Single(result.Items).Title);
        Assert.Equal(1, result.Page);
        Assert.Equal(NotificationService.MaxPageSize, result.PageSize);
    }

    [Fact]
    public async Task MarkRead_SetsReadAtOnce_AndKeepsFirstReadTime()
    {
        var repo = new FakeRepo();
        var me = Guid.NewGuid();
        var n = Add(repo, me, "tin");
        var service = new NotificationService(repo);

        var first = await service.MarkReadAsync(me, n.Id);
        var firstReadAt = n.ReadAt;
        var second = await service.MarkReadAsync(me, n.Id);

        Assert.True(first.IsRead);
        Assert.True(second.IsRead);
        Assert.NotNull(firstReadAt);
        Assert.Equal(firstReadAt, n.ReadAt);
    }

    [Fact]
    public async Task MarkRead_NotificationOfAnotherUser_IsNotFoundAndUnchanged()
    {
        var repo = new FakeRepo();
        var owner = Guid.NewGuid();
        var n = Add(repo, owner, "riêng tư");
        var service = new NotificationService(repo);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.MarkReadAsync(Guid.NewGuid(), n.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => service.MarkReadAsync(owner, Guid.Empty));
        Assert.Null(n.ReadAt);
    }

    [Fact]
    public async Task MarkAllRead_OnlyTouchesOwnUnreadNotifications()
    {
        var repo = new FakeRepo();
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        Add(repo, me, "a");
        Add(repo, me, "b");
        Add(repo, me, "c", read: true);
        var theirs = Add(repo, other, "d");
        var service = new NotificationService(repo);

        var updated = await service.MarkAllReadAsync(me);

        Assert.Equal(2, updated);
        Assert.Equal(0, await service.CountUnreadAsync(me));
        Assert.Null(theirs.ReadAt);
    }

    [Fact]
    public async Task Notify_TrimsTruncatesAndKeepsOnlyInternalLinks()
    {
        var repo = new FakeRepo();
        var service = new NotificationService(repo);
        var user = Guid.NewGuid();

        await service.NotifyAsync(user, "  center_verification  ", new string('T', 400), "  nội dung  ", "/center/verification");
        await service.NotifyAsync(user, "x", "ngoài", null, "https://evil.example/login");

        Assert.Equal("center_verification", repo.Items[0].Type);
        Assert.Equal(NotificationService.MaxTitleLength, repo.Items[0].Title.Length);
        Assert.Equal("nội dung", repo.Items[0].Body);
        Assert.Equal("/center/verification", repo.Items[0].LinkUrl);
        Assert.Null(repo.Items[1].LinkUrl);
        await Assert.ThrowsAsync<ArgumentException>(() => service.NotifyAsync(Guid.Empty, "x", "y"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.NotifyAsync(user, " ", "y"));
    }

    [Theory]
    [InlineData("/center/leads", "/center/leads")]
    [InlineData("  /account#parent-links ", "/account#parent-links")]
    [InlineData("//evil.example", null)]
    [InlineData("https://evil.example", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("/\\evil.example", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void SafeLink_AllowsOnlyInternalPaths(string? input, string? expected) =>
        Assert.Equal(expected, NotificationService.SafeLink(input));

    [Theory]
    [InlineData("verified", true)]
    [InlineData("reject", true)]
    [InlineData("changes_requested", true)]
    [InlineData("revoked", true)]
    [InlineData("submitted", false)]
    public void VerificationOutcomeTitle_OnlyReviewOutcomesProduceANotification(string outcome, bool expected) =>
        Assert.Equal(expected, StudyCenterService.VerificationOutcomeTitle(outcome) is not null);

    // ---- controller

    private static NotificationsController ControllerFor(INotificationService service, Guid? userId)
    {
        var identity = userId is { } id
            ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test")
            : new ClaimsIdentity();
        return new NotificationsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } },
        };
    }

    [Fact]
    public async Task Controller_UsesUserIdFromToken_AndMaps404()
    {
        var repo = new FakeRepo();
        var me = Guid.NewGuid();
        Add(repo, me, "của tôi");
        var foreign = Add(repo, Guid.NewGuid(), "của người khác");
        var controller = ControllerFor(new NotificationService(repo), me);

        var list = Assert.IsType<OkObjectResult>(await controller.GetMine());
        Assert.Equal("của tôi", Assert.Single(Assert.IsType<NotificationListDto>(list.Value).Items).Title);
        Assert.IsType<NotFoundObjectResult>(await controller.MarkRead(foreign.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Controller_WithoutUser_ReturnsUnauthorized()
    {
        var controller = ControllerFor(new NotificationService(new FakeRepo()), null);

        Assert.IsType<UnauthorizedObjectResult>(await controller.GetMine());
        Assert.IsType<UnauthorizedObjectResult>(await controller.MarkAllRead(CancellationToken.None));
    }

    [Fact]
    public void Controller_RequiresAuthenticationButNoSpecificRole()
    {
        var attr = typeof(NotificationsController).GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single();

        Assert.Null(attr.Roles);
    }
}
