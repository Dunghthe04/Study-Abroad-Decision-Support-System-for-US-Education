using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.KnowledgeBase;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>Lọc, sắp xếp, phân trang ở database; truy vấn đọc không theo dõi thay đổi.</summary>
public class KnowledgeBaseRepository(AppDbContext db) : IKnowledgeBaseRepository
{
    public async Task<(IReadOnlyList<KnowledgeDocument> Rows, int Total)> PageAsync(
        KnowledgeEntryFilter filter, int skip, int take, CancellationToken ct = default)
    {
        var query = Sorted(Filtered(db.KnowledgeDocuments.AsNoTracking(), filter), filter.Sort);
        var total = await query.CountAsync(ct);
        var rows = await query.Skip(skip).Take(take).ToListAsync(ct);
        return (rows, total);
    }

    public static IQueryable<KnowledgeDocument> Filtered(IQueryable<KnowledgeDocument> query, KnowledgeEntryFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.DocType))
            query = query.Where(x => x.DocType == filter.DocType);
        if (!string.IsNullOrWhiteSpace(filter.Label))
            query = query.Where(x => x.Label == filter.Label);
        if (!string.IsNullOrWhiteSpace(filter.TopicCode))
            query = query.Where(x => x.TopicCode == filter.TopicCode);
        // ponytail: ILIKE quét tuần tự, ~1000 dòng chỉ vài ms. Cần tìm không dấu / nhanh hơn thì thêm pg_trgm + unaccent bằng migration
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = LikePattern(filter.Search);
            query = query.Where(x => EF.Functions.ILike(x.Question, pattern) || EF.Functions.ILike(x.Answer, pattern));
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

    public static IQueryable<KnowledgeDocument> Sorted(IQueryable<KnowledgeDocument> query, string? sort) => sort switch
    {
        "oldest" => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.EntryNo),
        "entryNo" => query.OrderBy(x => x.EntryNo),
        "updated" => query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenByDescending(x => x.EntryNo),
        _ => query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.EntryNo),
    };

    public Task<KnowledgeDocument?> FindAsync(Guid id, CancellationToken ct = default) =>
        db.KnowledgeDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<int> MaxEntryNoAsync(CancellationToken ct = default) =>
        await db.KnowledgeDocuments.MaxAsync(x => (int?)x.EntryNo, ct) ?? 0;

    public Task<Guid?> FindIdByEntryNoAsync(int entryNo, CancellationToken ct = default) =>
        db.KnowledgeDocuments.AsNoTracking()
            .Where(x => x.EntryNo == entryNo)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(KnowledgeDocument document, CancellationToken ct = default)
    {
        db.KnowledgeDocuments.Add(document);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdateAsync(Guid id, Action<KnowledgeDocument> apply, CancellationToken ct = default)
    {
        var document = await db.KnowledgeDocuments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (document is null)
            return false;
        apply(document);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) =>
        await db.KnowledgeDocuments.Where(x => x.Id == id).ExecuteDeleteAsync(ct) > 0;

    public async Task<IReadOnlyDictionary<int, KnowledgeDocument>> TrackedByEntryNosAsync(
        IReadOnlyCollection<int> entryNos, CancellationToken ct = default) =>
        await db.KnowledgeDocuments.Where(x => entryNos.Contains(x.EntryNo)).ToDictionaryAsync(x => x.EntryNo, ct);

    public async Task SaveImportAsync(IReadOnlyList<KnowledgeDocument> added, CancellationToken ct = default)
    {
        db.KnowledgeDocuments.AddRange(added);
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearEmbeddingsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        var array = ids.ToArray();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE app.knowledge_documents SET embedding = NULL WHERE id = ANY({array})", ct);
    }
}
