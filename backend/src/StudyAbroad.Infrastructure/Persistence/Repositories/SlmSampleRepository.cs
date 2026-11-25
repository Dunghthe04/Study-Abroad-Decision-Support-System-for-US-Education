using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>Lọc, sắp xếp, phân trang ở database; truy vấn đọc không theo dõi thay đổi.</summary>
public class SlmSampleRepository(AppDbContext db) : ISlmSampleRepository
{
    private IQueryable<SlmTrainingSample> Live => db.SlmTrainingSamples.Where(x => x.DeletedAt == null);

    public async Task<(IReadOnlyList<SlmSampleListRow> Rows, int Total)> PageAsync(
        SlmSampleFilter filter, int skip, int take, CancellationToken ct = default)
    {
        var source = filter.Deleted ? db.SlmTrainingSamples.Where(x => x.DeletedAt != null) : Live;
        var query = Filtered(source.AsNoTracking(), filter);

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

    public Task<SlmTrainingSample?> FindAsync(Guid id, bool includeDeleted = false, CancellationToken ct = default) =>
        (includeDeleted ? db.SlmTrainingSamples : Live).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<SlmSampleVersionRow>> VersionsAsync(Guid sampleId, CancellationToken ct = default)
    {
        var rows = await db.SlmSampleVersions.AsNoTracking()
            .Where(v => v.SampleId == sampleId)
            .GroupJoin(db.Users, v => v.ActorUserId, u => u.Id, (v, users) => new { v, users })
            .SelectMany(x => x.users.DefaultIfEmpty(), (x, u) => new { x.v, Name = u == null ? null : u.FullName })
            .OrderByDescending(x => x.v.Version)
            .ToListAsync(ct);
        return rows.Select(x => new SlmSampleVersionRow(x.v, x.Name)).ToList();
    }

    public Task<SlmSampleVersion?> FindVersionAsync(Guid sampleId, int version, CancellationToken ct = default) =>
        db.SlmSampleVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.SampleId == sampleId && v.Version == version, ct);

    public async Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

    public async Task AddAsync(SlmTrainingSample sample, SlmSampleVersion initialVersion, CancellationToken ct = default)
    {
        db.SlmTrainingSamples.Add(sample);
        initialVersion.SampleId = sample.Id;
        db.SlmSampleVersions.Add(initialVersion);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(
        IReadOnlyList<SlmTrainingSample> samples, IReadOnlyList<SlmSampleVersion> initialVersions, CancellationToken ct = default)
    {
        db.SlmTrainingSamples.AddRange(samples);
        db.SlmSampleVersions.AddRange(initialVersions);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdateAsync(
        Guid id, Func<SlmTrainingSample, (bool Changed, SlmSampleVersion? NewVersion)> apply,
        bool includeDeleted = false, CancellationToken ct = default)
    {
        var source = includeDeleted ? db.SlmTrainingSamples : Live;
        var sample = await source.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (sample is null)
            return false;

        var (changed, newVersion) = apply(sample);
        if (!changed)
            return true;

        sample.UpdatedAt = DateTime.UtcNow;
        if (newVersion is not null)
        {
            newVersion.SampleId = sample.Id;
            db.SlmSampleVersions.Add(newVersion);
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        // Hai người cùng Version++ thì unique (sample_id, version) chặn bên chậm: trả 409 thay vì 500
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConcurrencyConflictException("Mẫu vừa được người khác thay đổi. Hãy tải lại rồi thử lại.", ex);
        }
        return true;
    }

    public async Task<IReadOnlyList<SlmSampleCountRow>> CountsAsync(CancellationToken ct = default) =>
        await db.SlmTrainingSamples.AsNoTracking()
            .GroupBy(x => new { x.SampleType, Deleted = x.DeletedAt != null })
            .Select(g => new SlmSampleCountRow(g.Key.SampleType, g.Key.Deleted, g.Count()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SlmSampleKeyRow>> ExistingKeysAsync(CancellationToken ct = default) =>
        await Live.AsNoTracking()
            .Select(x => new SlmSampleKeyRow(x.Id, x.SampleType, x.Question, x.PendingQuestion, x.Profile, x.Context, x.Intents))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SlmSampleKeyRow>> LiveKeysByTypeAsync(string sampleType, CancellationToken ct = default) =>
        await Live.AsNoTracking()
            .Where(x => x.SampleType == sampleType)
            .Select(x => new SlmSampleKeyRow(x.Id, x.SampleType, x.Question, x.PendingQuestion, x.Profile, x.Context, x.Intents))
            .ToListAsync(ct);

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

    public async Task<IReadOnlyList<string>> SourcesAsync(CancellationToken ct = default) =>
        await Live.AsNoTracking()
            .Select(x => x.Source)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SlmTrainingSample>> ExportableAsync(CancellationToken ct = default) =>
        await Live.AsNoTracking()
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
