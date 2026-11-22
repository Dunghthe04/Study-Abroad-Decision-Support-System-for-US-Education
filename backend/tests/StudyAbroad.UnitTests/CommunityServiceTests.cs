using StudyAbroad.Application.Common;
using StudyAbroad.Application.Community;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class CommunityServiceTests
{
    [Fact]
    public async Task Send_Empty_Throws()
    {
        var env = Env();
        var ex = await Assert.ThrowsAsync<CommunityRuleException>(() =>
            env.Service.SendAsync(env.User.Id, new SendCommunityMessageRequest("  "), CancellationToken.None));
        Assert.Equal("empty_content", ex.Code);
        Assert.Empty(env.Repo.Posts);
    }

    [Fact]
    public async Task Send_TooLong_Throws()
    {
        var env = Env();
        var ex = await Assert.ThrowsAsync<CommunityRuleException>(() =>
            env.Service.SendAsync(env.User.Id, new SendCommunityMessageRequest(new string('a', 2001)), CancellationToken.None));
        Assert.Equal("too_long", ex.Code);
    }

    [Fact]
    public async Task Send_Restricted_Throws()
    {
        var env = Env();
        env.User.ForumRestrictedUntil = DateTime.UtcNow.AddDays(1);
        env.User.ForumRestrictionReason = "spam";
        var ex = await Assert.ThrowsAsync<CommunityRuleException>(() =>
            env.Service.SendAsync(env.User.Id, new SendCommunityMessageRequest("xin chào"), CancellationToken.None));
        Assert.Equal("forum_restricted", ex.Code);
    }

    [Fact]
    public async Task Send_PersistsVisibleCommunityChat()
    {
        var env = Env();
        var dto = await env.Service.SendAsync(env.User.Id, new SendCommunityMessageRequest("  xin chào  "), CancellationToken.None);
        var post = Assert.Single(env.Repo.Posts);
        Assert.Equal(ForumCategories.CommunityChat, post.Category);
        Assert.Equal(ForumPostStatuses.Visible, post.Status);
        Assert.Equal("xin chào", post.Content);
        Assert.Equal("xin chào", dto.Content);
        Assert.True(dto.IsMine);
        Assert.Single(env.Notifier.Messages);
    }

    [Fact]
    public async Task HideFromReport_HidesPostAndNotifies()
    {
        var env = Env();
        var post = new ForumPost
        {
            UserId = env.User.Id,
            Category = ForumCategories.CommunityChat,
            Content = "xấu",
            Status = ForumPostStatuses.Visible,
        };
        env.Repo.Posts.Add(post);
        var report = new ForumReport
        {
            ForumPostId = post.Id,
            ReporterUserId = Guid.NewGuid(),
            Reason = "abuse",
            Status = ForumReportStatuses.Open,
        };
        env.Repo.Reports.Add(report);

        await env.Service.HideFromReportAsync(Guid.NewGuid(), report.Id, CancellationToken.None);

        Assert.Equal(ForumPostStatuses.Hidden, post.Status);
        Assert.Equal(ForumReportStatuses.Actioned, report.Status);
        Assert.Equal(post.Id, Assert.Single(env.Notifier.Hidden));
    }

    private static (CommunityService Service, FakeRepo Repo, FakeNotifier Notifier, User User) Env()
    {
        var user = new User
        {
            Email = "a@x.vn",
            FullName = "An",
            Role = UserRoles.Student,
            Status = UserStatuses.Active,
        };
        var repo = new FakeRepo { Users = { [user.Id] = user } };
        var notifier = new FakeNotifier();
        return (new CommunityService(repo, notifier), repo, notifier, user);
    }

    private sealed class FakeNotifier : ICommunityChatNotifier
    {
        public List<CommunityMessageDto> Messages { get; } = [];
        public List<Guid> Hidden { get; } = [];
        public Task NewMessageAsync(CommunityMessageDto message, CancellationToken ct = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
        public Task MessageHiddenAsync(Guid messageId, CancellationToken ct = default)
        {
            Hidden.Add(messageId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRepo : ICommunityRepository
    {
        public List<ForumPost> Posts { get; } = [];
        public List<ForumReport> Reports { get; } = [];
        public Dictionary<Guid, User> Users { get; } = [];

        public Task<IReadOnlyList<ForumPost>> ListVisibleChatAsync(DateTime? before, int take, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ForumPost>>(Posts
                .Where(p => p.Status == ForumPostStatuses.Visible && p.Category == ForumCategories.CommunityChat)
                .Where(p => before is null || p.CreatedAt < before)
                .OrderByDescending(p => p.CreatedAt)
                .Take(take)
                .ToList());

        public Task<ForumPost?> GetChatPostAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Posts.FirstOrDefault(p => p.Id == id));

        public Task<IReadOnlyList<ForumPost>> GetChatPostsAsync(IEnumerable<Guid> ids, CancellationToken ct)
        {
            var set = ids.ToHashSet();
            return Task.FromResult<IReadOnlyList<ForumPost>>(Posts.Where(p => set.Contains(p.Id)).ToList());
        }

        public Task AddPostAsync(ForumPost post, CancellationToken ct)
        {
            Posts.Add(post);
            return Task.CompletedTask;
        }

        public Task<bool> HasOpenReportAsync(Guid postId, Guid reporterUserId, CancellationToken ct) =>
            Task.FromResult(Reports.Any(r => r.ForumPostId == postId && r.ReporterUserId == reporterUserId && r.Status == ForumReportStatuses.Open));

        public Task AddReportAsync(ForumReport report, CancellationToken ct)
        {
            Reports.Add(report);
            return Task.CompletedTask;
        }

        public Task<ForumReport?> GetReportAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Reports.FirstOrDefault(r => r.Id == id));

        public Task<PagedResult<ForumReport>> ListReportsAsync(string? status, int page, int pageSize, CancellationToken ct)
        {
            var items = Reports.Where(r => status is null or "all" || r.Status == status).ToList();
            return Task.FromResult(new PagedResult<ForumReport>(items, page, pageSize, items.Count));
        }

        public Task<IReadOnlyDictionary<Guid, User>> GetUsersAsync(IEnumerable<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, User>>(
                Users.Where(kv => ids.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value));

        public Task<User?> GetUserAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Users.GetValueOrDefault(id));

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
