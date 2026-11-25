using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyAbroad.Application.Admin.KnowledgeBase;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>Lọc, sắp xếp, phân trang ở database; truy vấn đọc không theo dõi thay đổi.</summary>
public class KnowledgeBaseRepository(AppDbContext db) : IKnowledgeBaseRepository
{
    private IQueryable<KnowledgeDocument> Live => db.KnowledgeDocuments.Where(x => x.DeletedAt == null);

    public async Task<(IReadOnlyList<KnowledgeDocument> Rows, int Total)> PageAsync(
        KnowledgeEntryFilter filter, int skip, int take, CancellationToken ct = default)
    {
        var source = filter.Deleted ? db.KnowledgeDocuments.Where(x => x.DeletedAt != null) : Live;
        var query = Sorted(Filtered(source.AsNoTracking(), filter), filter.Sort);
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

    public Task<KnowledgeDocument?> FindAsync(Guid id, bool includeDeleted = true, CancellationToken ct = default) =>
        (includeDeleted ? db.KnowledgeDocuments : Live).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<KnowledgeEntryVersionRow>> VersionsAsync(Guid documentId, CancellationToken ct = default)
    {
        var rows = await db.KnowledgeDocumentVersions.AsNoTracking()
            .Where(v => v.DocumentId == documentId)
            .GroupJoin(db.Users, v => v.ActorUserId, u => u.Id, (v, users) => new { v, users })
            .SelectMany(x => x.users.DefaultIfEmpty(), (x, u) => new { x.v, Name = u == null ? null : u.FullName })
            .OrderByDescending(x => x.v.Version)
            .ToListAsync(ct);
        return rows.Select(x => new KnowledgeEntryVersionRow(x.v, x.Name)).ToList();
    }

    public Task<KnowledgeDocumentVersion?> FindVersionAsync(Guid documentId, int version, CancellationToken ct = default) =>
        db.KnowledgeDocumentVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.DocumentId == documentId && v.Version == version, ct);

    public async Task<int> MaxEntryNoAsync(CancellationToken ct = default) =>
        await db.KnowledgeDocuments.MaxAsync(x => (int?)x.EntryNo, ct) ?? 0;

    public Task<Guid?> FindIdByEntryNoAsync(int entryNo, CancellationToken ct = default) =>
        db.KnowledgeDocuments.AsNoTracking()
            .Where(x => x.EntryNo == entryNo)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(KnowledgeDocument document, KnowledgeDocumentVersion initialVersion, CancellationToken ct = default)
    {
        db.KnowledgeDocuments.Add(document);
        initialVersion.DocumentId = document.Id;
        db.KnowledgeDocumentVersions.Add(initialVersion);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(document).State = EntityState.Detached;
            db.Entry(initialVersion).State = EntityState.Detached;
            var existing = await FindIdByEntryNoAsync(document.EntryNo, ct);
            throw new KnowledgeDuplicateEntryException(existing ?? Guid.Empty);
        }
    }

    public async Task<bool> UpdateAsync(
        Guid id, Func<KnowledgeDocument, (bool Changed, KnowledgeDocumentVersion? NewVersion)> apply,
        bool includeDeleted = true, CancellationToken ct = default)
    {
        var source = includeDeleted ? db.KnowledgeDocuments : Live;
        var document = await source.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (document is null)
            return false;

        var (changed, newVersion) = apply(document);
        if (!changed)
            return true;

        if (newVersion is not null)
        {
            newVersion.DocumentId = document.Id;
            db.KnowledgeDocumentVersions.Add(newVersion);
        }
        await SaveVersionedAsync(ct);
        return true;
    }

    public async Task<IReadOnlyDictionary<int, KnowledgeDocument>> TrackedByEntryNosAsync(
        IReadOnlyCollection<int> entryNos, CancellationToken ct = default) =>
        await db.KnowledgeDocuments.Where(x => entryNos.Contains(x.EntryNo)).ToDictionaryAsync(x => x.EntryNo, ct);

    public async Task SaveImportAsync(
        IReadOnlyList<KnowledgeDocument> added, IReadOnlyList<KnowledgeDocumentVersion> versions,
        CancellationToken ct = default)
    {
        db.KnowledgeDocuments.AddRange(added);
        db.KnowledgeDocumentVersions.AddRange(versions);
        await SaveVersionedAsync(ct);
    }

    // Hai người cùng Version++ thì unique (document_id, version) / entry_no chặn bên chậm: trả 409 thay vì 500
    private async Task SaveVersionedAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConcurrencyConflictException("Mục tri thức vừa được người khác thay đổi. Hãy tải lại rồi thử lại.", ex);
        }
    }

    public async Task ClearEmbeddingsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        var array = ids.ToArray();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE app.knowledge_documents SET embedding = NULL WHERE id = ANY({array})", ct);
    }
}
