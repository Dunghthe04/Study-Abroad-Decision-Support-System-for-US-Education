using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.KnowledgeBase;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>Lọc, sắp xếp, phân trang ở database; truy vấn đọc không theo dõi thay đổi.</summary>
public class KnowledgeBaseRepository(AppDbContext db) : IKnowledgeBaseRepository
{
    private IQueryable<KnowledgeBaseEntry> Live => db.KnowledgeBaseEntries.Where(x => x.DeletedAt == null);

    public async Task<(IReadOnlyList<KnowledgeEntryListRow> Rows, int Total)> PageAsync(
        KnowledgeEntryFilter filter, int skip, int take, CancellationToken ct = default)
    {
        var source = filter.Deleted ? db.KnowledgeBaseEntries.Where(x => x.DeletedAt != null) : Live;
        var query = Sorted(Filtered(source.AsNoTracking(), filter), filter.Sort);

        var total = await query.CountAsync(ct);
        var rows = await query
            .Skip(skip)
            .Take(take)
            .GroupJoin(db.Users, e => e.CreatedByUserId, u => u.Id, (e, users) => new { e, users })
            .SelectMany(x => x.users.DefaultIfEmpty(), (x, u) => new { x.e, Name = u == null ? null : u.FullName })
            .ToListAsync(ct);
        // Sau GroupJoin, Postgres không giữ thứ tự của subquery: sắp lại trong bộ nhớ (tối đa 100 dòng)
        var ordered = Sorted(rows.Select(x => x.e).AsQueryable(), filter.Sort).ToList();
        var names = rows.ToDictionary(x => x.e.Id, x => x.Name);
        return (ordered.Select(e => new KnowledgeEntryListRow(e, names[e.Id])).ToList(), total);
    }

    public static IQueryable<KnowledgeBaseEntry> Filtered(IQueryable<KnowledgeBaseEntry> query, KnowledgeEntryFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Label))
            query = query.Where(x => x.Label == filter.Label);
        if (!string.IsNullOrWhiteSpace(filter.Level))
            query = query.Where(x => x.Levels.Contains(filter.Level));
        if (!string.IsNullOrWhiteSpace(filter.ReviewStatus))
            query = query.Where(x => x.ReviewStatus == filter.ReviewStatus);
        if (!string.IsNullOrWhiteSpace(filter.Origin))
            query = query.Where(x => x.Origin == filter.Origin);
        if (filter.CreatedByUserId is { } creator)
            query = query.Where(x => x.CreatedByUserId == creator);
        // ponytail: ILIKE quét tuần tự, ~1000 dòng chỉ vài ms. Cần tìm không dấu / nhanh hơn thì thêm pg_trgm + unaccent bằng migration
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = LikePattern(filter.Search);
            query = query.Where(x => EF.Functions.ILike(x.Question, pattern)
                                     || (x.Answer != null && EF.Functions.ILike(x.Answer, pattern)));
        }
        if (!string.IsNullOrWhiteSpace(filter.Source))
        {
            var pattern = LikePattern(filter.Source);
            query = query.Where(x => x.Source != null && EF.Functions.ILike(x.Source, pattern));
        }
        return query;
    }

    private static string LikePattern(string text)
    {
        var escaped = text.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        return $"%{escaped}%";
    }

    public static IQueryable<KnowledgeBaseEntry> Sorted(IQueryable<KnowledgeBaseEntry> query, string? sort) => sort switch
    {
        "oldest" => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.EntryNo),
        "entryNo" => query.OrderBy(x => x.EntryNo),
        "updated" => query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenByDescending(x => x.EntryNo),
        _ => query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.EntryNo),
    };

    public Task<KnowledgeBaseEntry?> FindAsync(Guid id, bool includeDeleted = false, CancellationToken ct = default) =>
        (includeDeleted ? db.KnowledgeBaseEntries : Live).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<KnowledgeEntryHistoryRow>> HistoryAsync(Guid entryId, CancellationToken ct = default)
    {
        var rows = await db.KnowledgeBaseEntryHistory.AsNoTracking()
            .Where(h => h.EntryId == entryId)
            .GroupJoin(db.Users, h => h.ActorUserId, u => u.Id, (h, users) => new { h, users })
            .SelectMany(x => x.users.DefaultIfEmpty(), (x, u) => new { x.h, Name = u == null ? null : u.FullName })
            .OrderBy(x => x.h.CreatedAt)
            .ThenBy(x => x.h.Id)
            .ToListAsync(ct);
        return rows.Select(x => new KnowledgeEntryHistoryRow(x.h, x.Name)).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

    public async Task AddAsync(KnowledgeBaseEntry entry, KnowledgeBaseEntryHistory history, CancellationToken ct = default)
    {
        db.KnowledgeBaseEntries.Add(entry);
        db.KnowledgeBaseEntryHistory.Add(history);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(
        IReadOnlyList<KnowledgeBaseEntry> entries, IReadOnlyList<KnowledgeBaseEntryHistory> history, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.KnowledgeBaseEntries.AddRange(entries);
        db.KnowledgeBaseEntryHistory.AddRange(history);
        await db.SaveChangesAsync(ct);
        // File tự đặt entry_no (cột identity BY DEFAULT) nên bộ đếm không tự tăng: đẩy nó lên số lớn nhất,
        // nếu không thì lần "Thêm tri thức" tiếp theo xin số 1 và đụng unique index
        await db.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('app.knowledge_base_entries', 'entry_no'), " +
            "(SELECT coalesce(max(entry_no), 1) FROM app.knowledge_base_entries))", ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<int> UpdateManyAsync(
        IReadOnlyList<Guid> ids, Func<KnowledgeBaseEntry, KnowledgeBaseEntryHistory?> apply,
        bool includeDeleted = false, CancellationToken ct = default)
    {
        var distinctIds = ids.Distinct().ToList();
        var source = includeDeleted ? db.KnowledgeBaseEntries : Live;
        var entries = await source.Where(x => distinctIds.Contains(x.Id)).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var entry in entries)
        {
            if (apply(entry) is not { } history)
                continue;
            entry.UpdatedAt = now;
            history.EntryId = entry.Id;
            db.KnowledgeBaseEntryHistory.Add(history);
        }
        await db.SaveChangesAsync(ct);
        return entries.Count;
    }

    public async Task<IReadOnlyList<(Guid EntryId, Guid ActorUserId)>> ContributorsAsync(
        IReadOnlyList<Guid> entryIds, CancellationToken ct = default)
    {
        var rows = await db.KnowledgeBaseEntryHistory.AsNoTracking()
            .Where(h => entryIds.Contains(h.EntryId) && h.ActorUserId != null
                        && (h.Action == KnowledgeHistoryActions.Create || h.Action == KnowledgeHistoryActions.Edit
                            || h.Action == KnowledgeHistoryActions.Submit))
            .Select(h => new { h.EntryId, ActorUserId = h.ActorUserId!.Value })
            .Distinct()
            .ToListAsync(ct);
        return rows.Select(x => (x.EntryId, x.ActorUserId)).ToList();
    }

    public async Task<IReadOnlyList<KnowledgeKeyRow>> ExistingKeysAsync(CancellationToken ct = default) =>
        await db.KnowledgeBaseEntries.AsNoTracking()
            .Select(x => new KnowledgeKeyRow(x.EntryNo, x.Question))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<KnowledgeBaseEntry>> SameQuestionAsync(
        string question, bool includeDeleted, CancellationToken ct = default)
    {
        var source = includeDeleted ? db.KnowledgeBaseEntries : Live;
        return await SameQuestion(source.AsNoTracking(), question).ToListAsync(ct);
    }

    /// <summary>Câu hỏi đã được service chuẩn hóa (NFC, gộp khoảng trắng) trước khi lưu, nên ở đây chỉ cần so chữ thường.</summary>
    public static IQueryable<KnowledgeBaseEntry> SameQuestion(IQueryable<KnowledgeBaseEntry> query, string question)
    {
        var lowered = question.Trim().ToLower();
        return query.Where(x => x.Question.ToLower() == lowered);
    }

    public async Task<IReadOnlyList<KnowledgeCreatorDto>> CreatorsAsync(CancellationToken ct = default) =>
        await db.KnowledgeBaseEntries.AsNoTracking()
            .Where(x => x.CreatedByUserId != null)
            .Select(x => x.CreatedByUserId!.Value)
            .Distinct()
            .Join(db.Users, id => id, u => u.Id, (_, u) => u)
            .OrderBy(u => u.FullName)
            .Select(u => new KnowledgeCreatorDto(u.Id, u.FullName))
            .ToListAsync(ct);
}