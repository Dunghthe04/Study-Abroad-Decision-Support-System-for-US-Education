using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>Lọc, sắp xếp, phân trang ở database; truy vấn đọc không theo dõi thay đổi.</summary>
public class SlmSampleRepository(AppDbContext db) : ISlmSampleRepository
{
    private IQueryable<SlmTrainingSample> Live => db.SlmTrainingSamples.Where(x => x.DeletedAt == null);

    public async Task<(IReadOnlyList<SlmSampleListRow> Rows, int Total)> PageAsync(
        SlmSampleFilter filter, int skip, int take, CancellationToken ct = default)
    {
        var query = Filtered(Live.AsNoTracking(), filter);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .GroupJoin(db.Users, s => s.CreatedByUserId, u => u.Id, (s, users) => new { s, users })
            .SelectMany(x => x.users.DefaultIfEmpty(), (x, u) => new { x.s, Name = u == null ? null : u.FullName })
            .OrderByDescending(x => x.s.CreatedAt)
            .ThenBy(x => x.s.Id)
            .ToListAsync(ct);
        return (rows.Select(x => new SlmSampleListRow(x.s, x.Name)).ToList(), total);
    }

    public static IQueryable<SlmTrainingSample> Filtered(IQueryable<SlmTrainingSample> query, SlmSampleFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.SampleType))
            query = query.Where(x => x.SampleType == filter.SampleType);
        if (!string.IsNullOrWhiteSpace(filter.Split))
            query = query.Where(x => x.Split == filter.Split);
        if (!string.IsNullOrWhiteSpace(filter.ReviewStatus))
            query = query.Where(x => x.ReviewStatus == filter.ReviewStatus);
        if (!string.IsNullOrWhiteSpace(filter.Source))
            query = query.Where(x => x.Source == filter.Source);
        if (filter.CreatedByUserId is { } creator)
            query = query.Where(x => x.CreatedByUserId == creator);
        if (filter.From is { } from)
            query = query.Where(x => x.CreatedAt >= from);
        if (filter.To is { } to)
            query = query.Where(x => x.CreatedAt < to);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var escaped = filter.Search.Trim()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            var pattern = $"%{escaped}%";
            query = query.Where(x => EF.Functions.ILike(x.Question, pattern));
        }
        return query;
    }

    public Task<SlmTrainingSample?> FindAsync(Guid id, CancellationToken ct = default) =>
        Live.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

    public async Task AddAsync(SlmTrainingSample sample, CancellationToken ct = default)
    {
        db.SlmTrainingSamples.Add(sample);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IReadOnlyList<SlmTrainingSample> samples, CancellationToken ct = default)
    {
        db.SlmTrainingSamples.AddRange(samples);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> UpdateManyAsync(
        IReadOnlyList<Guid> ids, Func<SlmTrainingSample, bool> apply, CancellationToken ct = default)
    {
        var distinctIds = ids.Distinct().ToList();
        var samples = await Live.Where(x => distinctIds.Contains(x.Id)).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var sample in samples)
        {
            if (!apply(sample))
                continue;
            sample.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
        return samples.Count;
    }

    public async Task<IReadOnlyList<SlmSampleCountRow>> CountsAsync(CancellationToken ct = default) =>
        await Live.AsNoTracking()
            .GroupBy(x => new { x.SampleType, x.ReviewStatus })
            .Select(g => new SlmSampleCountRow(g.Key.SampleType, g.Key.ReviewStatus, g.Count()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SlmSampleKeyRow>> ExistingKeysAsync(CancellationToken ct = default) =>
        await db.SlmTrainingSamples.AsNoTracking()
            .Select(x => new SlmSampleKeyRow(x.SampleType, x.Question, x.PendingQuestion, x.Profile, x.Context, x.Intents))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SlmTrainingSample>> SameQuestionAsync(
        string question, bool includeDeleted, CancellationToken ct = default)
    {
        var source = includeDeleted ? db.SlmTrainingSamples : Live;
        return await SameQuestion(source.AsNoTracking(), question).ToListAsync(ct);
    }

    public static IQueryable<SlmTrainingSample> SameQuestion(IQueryable<SlmTrainingSample> query, string question)
    {
        var lowered = question.Trim().ToLower();
        return query.Where(x => x.Question.Trim().ToLower() == lowered);
    }

    public async Task<IReadOnlyList<SlmSampleCreatorDto>> CreatorsAsync(CancellationToken ct = default) =>
        await Creators(Live.AsNoTracking(), db.Users).ToListAsync(ct);

    public static IQueryable<SlmSampleCreatorDto> Creators(IQueryable<SlmTrainingSample> samples, IQueryable<User> users) =>
        samples
            .Where(x => x.CreatedByUserId != null)
            .Select(x => x.CreatedByUserId!.Value)
            .Distinct()
            .Join(users, id => id, u => u.Id, (_, u) => u)
            .OrderBy(u => u.FullName)
            .Select(u => new SlmSampleCreatorDto(u.Id, u.FullName));

    public async Task<IReadOnlyList<SlmTrainingSample>> ApprovedAsync(CancellationToken ct = default) =>
        await Live.AsNoTracking()
            .Where(x => x.ReviewStatus == SlmReviewStatuses.Approved)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

    public async Task MarkExportedAsync(IReadOnlyList<Guid> ids, DateTime exportedAt, CancellationToken ct = default)
    {
        foreach (var chunk in ids.Chunk(1000))
        {
            await db.SlmTrainingSamples
                .Where(x => chunk.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExportedAt, (DateTime?)exportedAt), ct);
        }
    }
}
