using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-110] Xem thông báo: chỉ thấy và thao tác trên thông báo của chính mình.</summary>
public class NotificationTests
{
    /// <summary>Repository giả, lọc theo UserId giống bản thật.</summary>
    internal sealed class FakeNotificationRepo : INotificationRepository
    {
        public List<Notification> Items { get; } = [];
        public bool FailOnAdd { get; set; }

        public Task<PagedResult<Notification>> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default)
        {
            var mine = Items.Where(n => n.UserId == userId && (!unreadOnly || n.ReadAt == null))
                .OrderByDescending(n => n.CreatedAt).ToList();
            var items = mine.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedResult<Notification>(items, page, pageSize, mine.Count));
        }

        public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Items.Count(n => n.UserId == userId && n.ReadAt == null));

        public Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
        {
            var n = Items.FirstOrDefault(x => x.Id == notificationId && x.UserId == userId);
            if (n is null) return Task.FromResult(false);
            n.ReadAt ??= DateTime.UtcNow;
            return Task.FromResult(true);
        }

        public Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
        {
            var unread = Items.Where(n => n.UserId == userId && n.ReadAt == null).ToList();
            foreach (var n in unread) n.ReadAt = DateTime.UtcNow;
            return Task.FromResult(unread.Count);
        }

        public Task AddAsync(Notification notification, CancellationToken ct = default)
        {
            if (FailOnAdd) throw new InvalidOperationException("db down");
            Items.Add(notification);
            return Task.CompletedTask;
        }
    }

    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private static (FakeNotificationRepo Repo, NotificationService Service) Setup()
    {
        var repo = new FakeNotificationRepo();
        repo.Items.AddRange(
        [
            new Notification { UserId = Me, Type = "lead_new", Title = "Cũ", CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new Notification { UserId = Me, Type = "lead_new", Title = "Mới", CreatedAt = DateTime.UtcNow, LinkUrl = "/center/leads" },
            new Notification { UserId = Me, Type = "lead_new", Title = "Đã đọc", ReadAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow.AddHours(-1) },
            new Notification { UserId = Other, Type = "lead_new", Title = "Của người khác" },
        ]);
        return (repo, new NotificationService(repo));
    }

    private static NotificationsController Controller(NotificationService service, Guid userId) =>
        new(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "TestAuth")),
                },
            },
        };

    [Fact]
    public async Task GetMine_ReturnsOnlyOwnNotificationsNewestFirst_AndUnreadFilter()
    {
        var (_, service) = Setup();

        var all = await service.GetMineAsync(Me, unreadOnly: false, 1, 20);
        var unread = await service.GetMineAsync(Me, unreadOnly: true, 1, 20);

        Assert.Equal(["Mới", "Đã đọc", "Cũ"], all.Items.Select(n => n.Title));
        Assert.DoesNotContain(all.Items, n => n.Title == "Của người khác");
        Assert.Equal(2, unread.TotalCount);
        Assert.All(unread.Items, n => Assert.False(n.IsRead));
        Assert.Equal(2, await service.GetUnreadCountAsync(Me));
    }

    [Fact]
    public async Task GetMine_ClampsPaging()
    {
        var (_, service) = Setup();

        var page = await service.GetMineAsync(Me, false, 0, 1000);

        Assert.Equal(1, page.Page);
        Assert.Equal(NotificationService.MaxPageSize, page.PageSize);
    }

    [Fact]
    public async Task MarkRead_OthersNotificationReturnsFalse_AndOwnIsMarked()
    {
        var (repo, service) = Setup();
        var others = repo.Items.Single(n => n.UserId == Other);
        var mine = repo.Items.First(n => n.UserId == Me && n.ReadAt == null);

        Assert.False(await service.MarkReadAsync(Me, others.Id));
        Assert.Null(others.ReadAt);
        Assert.False(await service.MarkReadAsync(Me, Guid.Empty));
        Assert.True(await service.MarkReadAsync(Me, mine.Id));
        Assert.NotNull(mine.ReadAt);
    }

    [Fact]
    public async Task MarkAllRead_OnlyTouchesOwnNotifications()
    {
        var (repo, service) = Setup();

        Assert.Equal(2, await service.MarkAllReadAsync(Me));
        Assert.Equal(0, await service.GetUnreadCountAsync(Me));
        Assert.Null(repo.Items.Single(n => n.UserId == Other).ReadAt);
    }

    [Fact]
    public async Task Notify_KeepsOnlyInternalLinks_TruncatesAndSkipsInvalid()
    {
        var repo = new FakeNotificationRepo();
        var service = new NotificationService(repo);

        await service.NotifyAsync(Me, "lead_new", "Tiêu đề", "Nội dung", "/center/leads");
        await service.NotifyAsync(Me, "lead_new", "Ngoài", null, "https://evil.example.com");
        await service.NotifyAsync(Me, "lead_new", "Giao thức", null, "//evil.example.com");
        await service.NotifyAsync(Me, "lead_new", new string('a', 400), null, null);
        await service.NotifyAsync(Guid.Empty, "lead_new", "Không người nhận", null, null);
        await service.NotifyAsync(Me, " ", "Không loại", null, null);

        Assert.Equal(4, repo.Items.Count);
        Assert.Equal("/center/leads", repo.Items[0].LinkUrl);
        Assert.Null(repo.Items[1].LinkUrl);
        Assert.Null(repo.Items[2].LinkUrl);
        Assert.Equal(NotificationService.MaxTitleLength, repo.Items[3].Title.Length);
    }

    [Fact]
    public async Task Notify_RepositoryFailure_DoesNotThrow()
    {
        var repo = new FakeNotificationRepo { FailOnAdd = true };

        await new NotificationService(repo).NotifyAsync(Me, "lead_new", "x", null, null);

        Assert.Empty(repo.Items);
    }

    [Fact]
    public void Mapper_MapsReadState()
    {
        var read = new Notification { Type = "t", Title = "a", ReadAt = DateTime.UtcNow };
        var unread = new Notification { Type = "t", Title = "b" };

        Assert.True(NotificationMapper.ToDto(read).IsRead);
        Assert.False(NotificationMapper.ToDto(unread).IsRead);
        Assert.Equal(unread.Id, NotificationMapper.ToDto(unread).Id);
    }

    [Fact]
    public async Task Controller_MarkReadOfAnotherUsersNotification_Returns404()
    {
        var (repo, service) = Setup();
        var others = repo.Items.Single(n => n.UserId == Other);

        var result = await Controller(service, Me).MarkRead(others.Id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Null(others.ReadAt);
    }

    [Fact]
    public async Task Controller_ListAndUnreadCount_UseTheCallerId()
    {
        var (_, service) = Setup();
        var controller = Controller(service, Other);

        var list = Assert.IsType<PagedResult<NotificationDto>>(Assert.IsType<OkObjectResult>(await controller.List(1, 20, false, CancellationToken.None)).Value);
        var count = Assert.IsType<UnreadCountDto>(Assert.IsType<OkObjectResult>(await controller.UnreadCount(CancellationToken.None)).Value);

        Assert.Equal("Của người khác", Assert.Single(list.Items).Title);
        Assert.Equal(1, count.Count);
        Assert.IsType<NoContentResult>(await controller.MarkRead(list.Items[0].Id, CancellationToken.None));
    }

    [Fact]
    public async Task Controller_WithoutUser_ReturnsUnauthorized_AndRequiresAuthentication()
    {
        var (_, service) = Setup();
        var controller = new NotificationsController(service) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        Assert.IsType<UnauthorizedResult>(await controller.List(1, 20, false, CancellationToken.None));
        Assert.NotEmpty(typeof(NotificationsController).GetCustomAttributes(typeof(AuthorizeAttribute), false));
    }
}
