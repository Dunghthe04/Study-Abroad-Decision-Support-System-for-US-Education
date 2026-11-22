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
    public async Task HideFromReport_HidesPostAndClosesAllOpenReportsAndNotifies()
    {
        var env = Env();
        var handlerId = Guid.NewGuid();
        var post = new ForumPost
        {
            UserId = env.User.Id,
            Category = ForumCategories.CommunityChat,
            Content = "xấu",
            Status = ForumPostStatuses.Visible,
        };
        env.Repo.Posts.Add(post);
        var report1 = new ForumReport
        {
            ForumPostId = post.Id,
            ReporterUserId = Guid.NewGuid(),
            Reason = "abuse 1",
            Status = ForumReportStatuses.Open,
        };
        var report2 = new ForumReport
        {
            ForumPostId = post.Id,
            ReporterUserId = Guid.NewGuid(),
            Reason = "abuse 2",
            Status = ForumReportStatuses.Open,
        };
        env.Repo.Reports.Add(report1);
        env.Repo.Reports.Add(report2);

        await env.Service.HideFromReportAsync(handlerId, report1.Id, CancellationToken.None);

        Assert.Equal(ForumPostStatuses.Hidden, post.Status);
        Assert.Equal(ForumReportStatuses.Actioned, report1.Status);
        Assert.Equal(ForumReportStatuses.Actioned, report2.Status);
        Assert.Equal(handlerId, report1.HandledByUserId);
        Assert.Equal(handlerId, report2.HandledByUserId);
        Assert.Equal(post.Id, Assert.Single(env.Notifier.Hidden));
    }

    [Fact]
    public async Task RestrictAndUnrestrictUser_SavesHandlerIdAndNotifies()
    {
        var env = Env();
        var handlerId = Guid.NewGuid();

        await env.Service.RestrictUserAsync(handlerId, env.User.Id, new RestrictCommunityUserRequest(7, "Spam liên tục"), CancellationToken.None);

        Assert.NotNull(env.User.ForumRestrictedUntil);
        Assert.Equal("Spam liên tục", env.User.ForumRestrictionReason);
        Assert.Equal(handlerId, env.User.ForumRestrictedByUserId);
        Assert.True(env.Notifier.RestrictionChanges.Last().IsRestricted);

        var status = await env.Service.RestrictionStatusAsync(env.User.Id, CancellationToken.None);
        Assert.True(status.IsRestricted);
        Assert.Equal("Spam liên tục", status.Reason);
        Assert.NotNull(status.RestrictedUntil);

        await env.Service.UnrestrictUserAsync(Guid.NewGuid(), env.User.Id, CancellationToken.None);

        Assert.Null(env.User.ForumRestrictedUntil);
        Assert.Null(env.User.ForumRestrictionReason);
        Assert.Equal(handlerId, env.User.ForumRestrictedByUserId);
        Assert.False(env.Notifier.RestrictionChanges.Last().IsRestricted);

        var clearedStatus = await env.Service.RestrictionStatusAsync(env.User.Id, CancellationToken.None);
        Assert.False(clearedStatus.IsRestricted);
    }

    [Fact]
    public async Task DirectHideMessage_ByModerator_HidesAndNotifies()
    {
        var env = Env();
        var handlerId = Guid.NewGuid();
        var post = new ForumPost
        {
            UserId = env.User.Id,
            Category = ForumCategories.CommunityChat,
            Content = "quảng cáo",
            Status = ForumPostStatuses.Visible,
        };
        env.Repo.Posts.Add(post);

        await env.Service.HideMessageAsync(handlerId, post.Id, CancellationToken.None);

        Assert.Equal(ForumPostStatuses.Hidden, post.Status);
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
        public List<(Guid UserId, bool IsRestricted, DateTime? Until, string? Reason)> RestrictionChanges { get; } = [];

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

        public Task UserRestrictionChangedAsync(Guid userId, bool isRestricted, DateTime? restrictedUntil, string? reason, CancellationToken ct = default)
        {
            RestrictionChanges.Add((userId, isRestricted, restrictedUntil, reason));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRepo : ICommunityRepository
    {
        public List<ForumPost> Posts { get; } = [];
        public List<ForumReport> Reports { get; } = [];
        public Dictionary<Guid, User> Users { get; } = [];

        public Task<IReadOnlyList<ForumPost>> ListVisibleChatAsync(
            DateTime? before, Guid? beforeId,
            DateTime? after, Guid? afterId,
            int take, CancellationToken ct)
        {
            var query = Posts
                .Where(p => p.Status == ForumPostStatuses.Visible && p.Category == ForumCategories.CommunityChat);

            if (after is { } afterAt)
            {
                if (afterId is { } aId)
                    query = query.Where(p => p.CreatedAt > afterAt || (p.CreatedAt == afterAt && p.Id > aId));
                else
                    query = query.Where(p => p.CreatedAt > afterAt);

                return Task.FromResult<IReadOnlyList<ForumPost>>(query
                    .OrderBy(p => p.CreatedAt)
                    .ThenBy(p => p.Id)
                    .Take(take)
                    .ToList());
            }

            if (before is { } beforeAt)
            {
                if (beforeId is { } bId)
                    query = query.Where(p => p.CreatedAt < beforeAt || (p.CreatedAt == beforeAt && p.Id < bId));
                else
                    query = query.Where(p => p.CreatedAt < beforeAt);
            }

            return Task.FromResult<IReadOnlyList<ForumPost>>(query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Take(take)
                .ToList());
        }

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

        public Task CloseOpenReportsForPostAsync(Guid postId, Guid handlerId, CancellationToken ct)
        {
            foreach (var r in Reports.Where(r => r.ForumPostId == postId && r.Status == ForumReportStatuses.Open))
            {
                r.Status = ForumReportStatuses.Actioned;
                r.HandledByUserId = handlerId;
                r.HandledAt = DateTime.UtcNow;
            }
            return Task.CompletedTask;
        }

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
