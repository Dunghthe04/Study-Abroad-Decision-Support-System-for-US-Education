using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>Lọc, sắp xếp, phân trang ở database; truy vấn đọc không theo dõi thay đổi.</summary>
public class SlmSampleRepository(AppDbContext db) : ISlmSampleRepository
{
    public async Task<(IReadOnlyList<SlmTrainingSample> Rows, int Total)> PageAsync(
        SlmSampleFilter filter, int skip, int take, CancellationToken ct = default)
    {
        var query = db.SlmTrainingSamples.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.SampleType))
            query = query.Where(x => x.SampleType == filter.SampleType);
        if (!string.IsNullOrWhiteSpace(filter.Split))
            query = query.Where(x => x.Split == filter.Split);
        if (!string.IsNullOrWhiteSpace(filter.ReviewStatus))
            query = query.Where(x => x.ReviewStatus == filter.ReviewStatus);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var escaped = filter.Search.Trim()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            var pattern = $"%{escaped}%";
            query = query.Where(x => EF.Functions.ILike(x.Question, pattern));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        return (rows, total);
    }

    public Task<SlmTrainingSample?> FindAsync(Guid id, CancellationToken ct = default) =>
        db.SlmTrainingSamples.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

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

    public async Task<bool> UpdateAsync(Guid id, Action<SlmTrainingSample> apply, CancellationToken ct = default)
    {
        var sample = await db.SlmTrainingSamples.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (sample is null)
            return false;

        apply(sample);
        sample.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var deleted = await db.SlmTrainingSamples.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        return deleted > 0;
    }

    public async Task<int> ReviewAsync(
        IReadOnlyList<Guid> ids, string status, string? note, Guid reviewerId, DateTime reviewedAt, CancellationToken ct = default)
    {
        var distinctIds = ids.Distinct().ToList();
        var now = DateTime.UtcNow;
        return await db.SlmTrainingSamples
            .Where(x => distinctIds.Contains(x.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.ReviewStatus, status)
                .SetProperty(x => x.ReviewNote, note)
                .SetProperty(x => x.ReviewedByUserId, (Guid?)reviewerId)
                .SetProperty(x => x.ReviewedAt, (DateTime?)reviewedAt)
                .SetProperty(x => x.UpdatedAt, (DateTime?)now), ct);
    }

    public async Task<IReadOnlyList<SlmSampleCountRow>> CountsAsync(CancellationToken ct = default) =>
        await db.SlmTrainingSamples.AsNoTracking()
            .GroupBy(x => new { x.SampleType, x.ReviewStatus })
            .Select(g => new SlmSampleCountRow(g.Key.SampleType, g.Key.ReviewStatus, g.Count()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SlmSampleKeyRow>> ExistingKeysAsync(CancellationToken ct = default) =>
        await db.SlmTrainingSamples.AsNoTracking()
            .Select(x => new SlmSampleKeyRow(x.SampleType, x.Question, x.PendingQuestion, x.Profile, x.Context, x.Intents))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SlmTrainingSample>> ApprovedAsync(string? split, CancellationToken ct = default)
    {
        var query = db.SlmTrainingSamples.AsNoTracking()
            .Where(x => x.ReviewStatus == SlmReviewStatuses.Approved);
        if (!string.IsNullOrWhiteSpace(split))
            query = query.Where(x => x.Split == split);

        return await query
            .OrderBy(x => x.SampleType)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);
    }
}
