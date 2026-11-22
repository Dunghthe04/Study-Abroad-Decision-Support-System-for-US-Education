using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Community;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public sealed class CommunityRepository(AppDbContext db) : ICommunityRepository
{
    public async Task<IReadOnlyList<ForumPost>> ListVisibleChatAsync(DateTime? before, int take, CancellationToken ct)
    {
        var query = db.ForumPosts.AsNoTracking()
            .Where(p => p.Category == ForumCategories.CommunityChat
                        && p.ParentPostId == null
                        && p.Status == ForumPostStatuses.Visible);
        if (before is { } at)
            query = query.Where(p => p.CreatedAt < at);

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<ForumPost?> GetChatPostAsync(Guid id, CancellationToken ct) =>
        db.ForumPosts.FirstOrDefaultAsync(
            p => p.Id == id && p.Category == ForumCategories.CommunityChat && p.ParentPostId == null, ct);

    public async Task<IReadOnlyList<ForumPost>> GetChatPostsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return [];
        return await db.ForumPosts.AsNoTracking()
            .Where(p => idList.Contains(p.Id) && p.Category == ForumCategories.CommunityChat)
            .ToListAsync(ct);
    }

    public async Task AddPostAsync(ForumPost post, CancellationToken ct)
    {
        db.ForumPosts.Add(post);
        await db.SaveChangesAsync(ct);
    }

    public Task<bool> HasOpenReportAsync(Guid postId, Guid reporterUserId, CancellationToken ct) =>
        db.ForumReports.AnyAsync(
            r => r.ForumPostId == postId && r.ReporterUserId == reporterUserId && r.Status == ForumReportStatuses.Open, ct);

    public async Task AddReportAsync(ForumReport report, CancellationToken ct)
    {
        db.ForumReports.Add(report);
        await db.SaveChangesAsync(ct);
    }

    public Task<ForumReport?> GetReportAsync(Guid id, CancellationToken ct) =>
        db.ForumReports.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<PagedResult<ForumReport>> ListReportsAsync(string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = db.ForumReports.AsNoTracking()
            .Where(r => db.ForumPosts.Any(p => p.Id == r.ForumPostId && p.Category == ForumCategories.CommunityChat));
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
            query = query.Where(r => r.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new PagedResult<ForumReport>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyDictionary<Guid, User>> GetUsersAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<Guid, User>();
        return await db.Users.AsNoTracking()
            .Where(u => idList.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);
    }

    public Task<User?> GetUserAsync(Guid id, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
