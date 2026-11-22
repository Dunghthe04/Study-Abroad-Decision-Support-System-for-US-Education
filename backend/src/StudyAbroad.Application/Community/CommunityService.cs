using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Community;

public sealed class CommunityService(ICommunityRepository repo, ICommunityChatNotifier notifier) : ICommunityService
{
    public const int MaxContentLength = 2000;
    public const int DefaultTake = 50;
    public const int MaxTake = 100;

    public async Task<IReadOnlyList<CommunityMessageDto>> ListMessagesAsync(
        Guid viewerId,
        DateTime? before, Guid? beforeId,
        DateTime? after, Guid? afterId,
        int take, CancellationToken ct)
    {
        take = take <= 0 ? DefaultTake : Math.Min(take, MaxTake);
        var posts = await repo.ListVisibleChatAsync(before, beforeId, after, afterId, take, ct);

        var replyToIds = posts.Select(p => p.ReplyToPostId).OfType<Guid>().Distinct().ToList();
        var replyPosts = replyToIds.Count > 0
            ? (await repo.GetChatPostsAsync(replyToIds, ct)).ToDictionary(p => p.Id)
            : new Dictionary<Guid, ForumPost>();

        var userIds = posts.Select(p => p.UserId)
            .Concat(replyPosts.Values.Select(p => p.UserId))
            .OfType<Guid>()
            .Distinct();

        var users = await repo.GetUsersAsync(userIds, ct);
        return posts.Select(p => ToDto(p, users, replyPosts, viewerId)).ToList();
    }

    public async Task<CommunityMessageDto> SendAsync(Guid userId, SendCommunityMessageRequest request, CancellationToken ct)
    {
        var user = await RequireActiveUserAsync(userId, ct);
        EnsureNotRestricted(user);

        var content = Sanitize(request.Content);
        if (content.Length == 0)
            throw new CommunityRuleException("empty_content", 400, "Nhập nội dung trước khi gửi.");
        if (content.Length > MaxContentLength)
            throw new CommunityRuleException("too_long", 400, $"Tin nhắn tối đa {MaxContentLength} ký tự.");

        ForumPost? replyPost = null;
        if (request.ReplyToId is { } replyId)
        {
            replyPost = await repo.GetChatPostAsync(replyId, ct);
        }

        var post = new ForumPost
        {
            UserId = user.Id,
            Category = ForumCategories.CommunityChat,
            Content = content,
            ReplyToPostId = replyPost?.Id,
            Status = ForumPostStatuses.Visible,
        };
        await repo.AddPostAsync(post, ct);

        var users = new Dictionary<Guid, User> { [user.Id] = user };
        if (replyPost?.UserId is { } replyUserId && !users.ContainsKey(replyUserId))
        {
            var replyUser = await repo.GetUserAsync(replyUserId, ct);
            if (replyUser != null) users[replyUserId] = replyUser;
        }

        var replyPosts = replyPost != null
            ? new Dictionary<Guid, ForumPost> { [replyPost.Id] = replyPost }
            : new Dictionary<Guid, ForumPost>();

        var dto = ToDto(post, users, replyPosts, userId);
        await notifier.NewMessageAsync(dto with { IsMine = false }, ct);
        return dto;
    }

    public async Task<CommunityMessageDto> SendImageAsync(
        Guid userId,
        string? content,
        Guid? replyToId,
        Func<CancellationToken, Task<CommunityAttachment>> attachmentFactory,
        CancellationToken ct)
    {
        var user = await RequireActiveUserAsync(userId, ct);
        EnsureNotRestricted(user);

        var sanitizedContent = Sanitize(content);
        if (sanitizedContent.Length > MaxContentLength)
            throw new CommunityRuleException("too_long", 400, $"Nội dung tối đa {MaxContentLength} ký tự.");

        var attachment = await attachmentFactory(ct);
        if (string.IsNullOrWhiteSpace(attachment?.Url))
            throw new CommunityRuleException("missing_image", 400, "Không có ảnh đính kèm.");

        ForumPost? replyPost = null;
        if (replyToId is { } replyId)
        {
            replyPost = await repo.GetChatPostAsync(replyId, ct);
        }

        var post = new ForumPost
        {
            UserId = user.Id,
            Category = ForumCategories.CommunityChat,
            Content = sanitizedContent,
            AttachmentUrl = attachment.Url,
            AttachmentPublicId = attachment.PublicId,
            ReplyToPostId = replyPost?.Id,
            Status = ForumPostStatuses.Visible,
        };
        await repo.AddPostAsync(post, ct);

        var users = new Dictionary<Guid, User> { [user.Id] = user };
        if (replyPost?.UserId is { } replyUserId && !users.ContainsKey(replyUserId))
        {
            var replyUser = await repo.GetUserAsync(replyUserId, ct);
            if (replyUser != null) users[replyUserId] = replyUser;
        }

        var replyPosts = replyPost != null
            ? new Dictionary<Guid, ForumPost> { [replyPost.Id] = replyPost }
            : new Dictionary<Guid, ForumPost>();

        var dto = ToDto(post, users, replyPosts, userId);
        await notifier.NewMessageAsync(dto with { IsMine = false }, ct);
        return dto;
    }

    public async Task ReportAsync(Guid reporterId, Guid messageId, ReportCommunityMessageRequest request, CancellationToken ct)
    {
        await RequireActiveUserAsync(reporterId, ct);
        var post = await repo.GetChatPostAsync(messageId, ct)
            ?? throw new CommunityRuleException("not_found", 404, "Không tìm thấy tin nhắn.");
        if (post.UserId == reporterId)
            throw new CommunityRuleException("own_message", 400, "Không báo cáo tin của chính mình.");
        if (post.Status != ForumPostStatuses.Visible)
            throw new CommunityRuleException("not_found", 404, "Không tìm thấy tin nhắn.");

        var reason = (request.Reason ?? string.Empty).Trim();
        if (reason.Length is < 3 or > 64)
            throw new CommunityRuleException("invalid_reason", 400, "Lý do báo cáo từ 3 đến 64 ký tự.");
        var detail = string.IsNullOrWhiteSpace(request.Detail) ? null : request.Detail.Trim();
        if (detail is { Length: > 1000 })
            throw new CommunityRuleException("invalid_detail", 400, "Mô tả tối đa 1000 ký tự.");

        if (await repo.HasOpenReportAsync(messageId, reporterId, ct))
            throw new CommunityRuleException("already_reported", 409, "Bạn đã báo cáo tin này rồi.");

        await repo.AddReportAsync(new ForumReport
        {
            ForumPostId = messageId,
            ReporterUserId = reporterId,
            Reason = reason,
            Detail = detail,
            Status = ForumReportStatuses.Open,
        }, ct);
    }

    public async Task<IReadOnlyList<CommunityPresenceDto>> DescribeUsersAsync(
        IEnumerable<Guid> userIds,
        IReadOnlyDictionary<Guid, DateTime>? userLastSeen = null,
        CancellationToken ct = default)
    {
        var users = await repo.GetUsersAsync(userIds, ct);
        return users.Values
            .Where(u => u.DeletedAt is null)
            .OrderBy(u => u.FullName)
            .Select(u => new CommunityPresenceDto(
                u.Id,
                u.FullName,
                u.Role,
                userLastSeen != null && userLastSeen.TryGetValue(u.Id, out var seen) ? seen : null))
            .ToList();
    }

    public async Task<PagedResult<CommunityReportDto>> ListReportsAsync(string? status, int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 50 ? 20 : pageSize;
        var result = await repo.ListReportsAsync(status, page, pageSize, ct);
        var posts = (await repo.GetChatPostsAsync(result.Items.Select(r => r.ForumPostId), ct))
            .ToDictionary(p => p.Id);
        var users = await repo.GetUsersAsync(
            result.Items.Select(r => r.ReporterUserId).OfType<Guid>()
                .Concat(posts.Values.Select(p => p.UserId).OfType<Guid>()),
            ct);

        var dtos = result.Items.Select(r =>
        {
            posts.TryGetValue(r.ForumPostId, out var post);
            var authorId = post?.UserId;
            var author = authorId is { } id && users.TryGetValue(id, out var a) ? a : null;
            var reporter = r.ReporterUserId is { } rid && users.TryGetValue(rid, out var rp) ? rp : null;
            var isRestricted = author?.ForumRestrictedUntil is { } u && u > DateTime.UtcNow;
            return new CommunityReportDto(
                r.Id,
                r.ForumPostId,
                post?.Content ?? "",
                post?.CreatedAt ?? r.CreatedAt,
                authorId,
                author?.FullName ?? "Tài khoản đã xóa",
                r.ReporterUserId,
                reporter?.FullName ?? "Tài khoản đã xóa",
                r.Reason,
                r.Detail,
                r.Status,
                r.CreatedAt,
                isRestricted,
                author?.ForumRestrictedUntil);
        }).ToList();

        return new PagedResult<CommunityReportDto>(dtos, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task HideFromReportAsync(Guid handlerId, Guid reportId, CancellationToken ct)
    {
        var report = await RequireOpenReportAsync(reportId, ct);
        await HideMessageAsync(handlerId, report.ForumPostId, ct);
    }

    public async Task HideMessageAsync(Guid handlerId, Guid messageId, CancellationToken ct)
    {
        var post = await repo.GetChatPostAsync(messageId, ct)
            ?? throw new CommunityRuleException("not_found", 404, "Không tìm thấy tin nhắn.");
        post.Status = ForumPostStatuses.Hidden;
        post.UpdatedAt = DateTime.UtcNow;
        await repo.CloseOpenReportsForPostAsync(post.Id, handlerId, ct);
        await repo.SaveChangesAsync(ct);
        await notifier.MessageHiddenAsync(post.Id, ct);
    }

    public async Task DismissReportAsync(Guid handlerId, Guid reportId, CancellationToken ct)
    {
        var report = await RequireOpenReportAsync(reportId, ct);
        report.Status = ForumReportStatuses.Dismissed;
        report.HandledByUserId = handlerId;
        report.HandledAt = DateTime.UtcNow;
        await repo.SaveChangesAsync(ct);
    }

    public async Task RestrictUserAsync(Guid handlerId, Guid userId, RestrictCommunityUserRequest request, CancellationToken ct)
    {
        var user = await repo.GetUserAsync(userId, ct)
            ?? throw new CommunityRuleException("not_found", 404, "Không tìm thấy tài khoản.");
        if (user.Role is UserRoles.Admin or UserRoles.Moderator)
            throw new CommunityRuleException("cannot_restrict", 400, "Không hạn chế tài khoản quản trị.");
        if (request.Days is < 1 or > 90)
            throw new CommunityRuleException("invalid_days", 400, "Số ngày hạn chế từ 1 đến 90.");
        var reason = (request.Reason ?? string.Empty).Trim();
        if (reason.Length is < 3 or > 500)
            throw new CommunityRuleException("invalid_reason", 400, "Lý do hạn chế từ 3 đến 500 ký tự.");

        var until = DateTime.UtcNow.AddDays(request.Days);
        user.ForumRestrictedUntil = until;
        user.ForumRestrictionReason = reason;
        user.ForumRestrictedByUserId = handlerId;
        user.UpdatedAt = DateTime.UtcNow;
        await repo.SaveChangesAsync(ct);

        await notifier.UserRestrictionChangedAsync(user.Id, isRestricted: true, until, reason, ct);
    }

    public async Task UnrestrictUserAsync(Guid handlerId, Guid userId, CancellationToken ct)
    {
        _ = handlerId;
        var user = await repo.GetUserAsync(userId, ct)
            ?? throw new CommunityRuleException("not_found", 404, "Không tìm thấy tài khoản.");
        user.ForumRestrictedUntil = null;
        user.ForumRestrictionReason = null;
        user.UpdatedAt = DateTime.UtcNow;
        await repo.SaveChangesAsync(ct);

        await notifier.UserRestrictionChangedAsync(user.Id, isRestricted: false, null, null, ct);
    }

    public async Task<CommunityRestrictionDto> RestrictionStatusAsync(Guid userId, CancellationToken ct)
    {
        var user = await repo.GetUserAsync(userId, ct);
        if (user is null)
            return new CommunityRestrictionDto(false, null, null, null);

        if (user.ForumRestrictedUntil is { } until && until > DateTime.UtcNow)
        {
            var vnTime = until.AddHours(7);
            var timeStr = vnTime.ToString("HH:mm dd/MM/yyyy");
            var reason = user.ForumRestrictionReason;
            var message = !string.IsNullOrWhiteSpace(reason)
                ? $"Bạn đang bị hạn chế chat đến {timeStr}. Lý do: {reason}"
                : $"Bạn đang bị hạn chế chat đến {timeStr}.";
            return new CommunityRestrictionDto(true, until, reason, message);
        }

        return new CommunityRestrictionDto(false, null, null, null);
    }

    private async Task<ForumReport> RequireOpenReportAsync(Guid reportId, CancellationToken ct)
    {
        var report = await repo.GetReportAsync(reportId, ct)
            ?? throw new CommunityRuleException("not_found", 404, "Không tìm thấy báo cáo.");
        if (report.Status != ForumReportStatuses.Open)
            throw new CommunityRuleException("already_handled", 409, "Báo cáo này đã được xử lý.");
        return report;
    }

    private async Task<User> RequireActiveUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await repo.GetUserAsync(userId, ct)
            ?? throw new CommunityRuleException("not_found", 404, "Không tìm thấy tài khoản.");
        if (user.DeletedAt is not null || user.Status is UserStatuses.Deleted or UserStatuses.Locked)
            throw new CommunityRuleException("account_blocked", 403, "Tài khoản không còn quyền chat.");
        return user;
    }

    private static void EnsureNotRestricted(User user)
    {
        if (user.ForumRestrictedUntil is { } until && until > DateTime.UtcNow)
        {
            var vnTime = until.AddHours(7);
            var timeStr = vnTime.ToString("HH:mm dd/MM/yyyy");
            throw new CommunityRuleException(
                "forum_restricted",
                403,
                user.ForumRestrictionReason is { Length: > 0 } reason
                    ? $"Bạn đang bị hạn chế chat đến {timeStr}. Lý do: {reason}"
                    : $"Bạn đang bị hạn chế chat đến {timeStr}.");
        }
    }

    private static string Sanitize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return string.Concat(raw.Where(c => c is not '\0' and not '\u200B')).Trim();
    }

    private static CommunityMessageDto ToDto(
        ForumPost post,
        IReadOnlyDictionary<Guid, User> users,
        IReadOnlyDictionary<Guid, ForumPost> replyPosts,
        Guid viewerId)
    {
        User? author = post.UserId is { } id && users.TryGetValue(id, out var u) ? u : null;

        CommunityReplyToDto? replyDto = null;
        if (post.ReplyToPostId is { } replyId && replyPosts.TryGetValue(replyId, out var parentPost))
        {
            User? replyAuthor = parentPost.UserId is { } parentUid && users.TryGetValue(parentUid, out var pu) ? pu : null;
            var snippet = parentPost.Status == ForumPostStatuses.Visible
                ? (string.IsNullOrWhiteSpace(parentPost.Content) && !string.IsNullOrWhiteSpace(parentPost.AttachmentUrl)
                    ? "[Hình ảnh]"
                    : (parentPost.Content.Length > 100 ? parentPost.Content[..100] + "..." : parentPost.Content))
                : "[Tin nhắn đã bị ẩn]";

            replyDto = new CommunityReplyToDto(
                parentPost.Id,
                parentPost.UserId,
                replyAuthor?.FullName ?? "Tài khoản đã xóa",
                snippet);
        }

        return new CommunityMessageDto(
            post.Id,
            post.UserId,
            author?.FullName ?? "Tài khoản đã xóa",
            author?.Role ?? "",
            post.Content,
            post.CreatedAt,
            post.UserId == viewerId,
            post.AttachmentUrl,
            replyDto);
    }
}
