using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyAbroad.Application.Admin.Policies;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class PolicyDocumentRepository(AppDbContext db) : IPolicyDocumentRepository, IPolicyVersionLookup
{
    public Task<string?> GetPublishedVersionAsync(string kind, CancellationToken ct = default) =>
        db.PolicyDocuments.AsNoTracking()
            .Where(d => d.Kind == kind && d.Status == PolicyStatuses.Published)
            .Select(d => d.Version)
            .FirstOrDefaultAsync(ct);

    public Task<PolicyDocument?> FindAsync(Guid id, CancellationToken ct = default) =>
        db.PolicyDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<PolicyDocument?> FindPublishedAsync(string kind, CancellationToken ct = default) =>
        db.PolicyDocuments.FirstOrDefaultAsync(d => d.Kind == kind && d.Status == PolicyStatuses.Published, ct);

    public Task<PolicyDocument?> FindDraftAsync(string kind, CancellationToken ct = default) =>
        db.PolicyDocuments.FirstOrDefaultAsync(d => d.Kind == kind && d.Status == PolicyStatuses.Draft, ct);

    public async Task<IReadOnlyList<PolicyDocument>> ListAsync(string? kind, CancellationToken ct = default) =>
        await db.PolicyDocuments.AsNoTracking()
            .Where(d => kind == null || d.Kind == kind)
            // Bản nháp (chưa có ngày công bố) lên đầu, rồi các bản đã công bố từ mới đến cũ
            .OrderByDescending(d => d.PublishedAt == null)
            .ThenByDescending(d => d.PublishedAt)
            .ThenByDescending(d => d.CreatedAt)
            .ThenBy(d => d.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ListVersionsAsync(string kind, string prefix, CancellationToken ct = default) =>
        await db.PolicyDocuments.AsNoTracking()
            .Where(d => d.Kind == kind && d.Version != null && d.Version.StartsWith(prefix))
            .Select(d => d.Version!)
            .ToListAsync(ct);

    public async Task AddAsync(PolicyDocument document, CancellationToken ct = default)
    {
        db.PolicyDocuments.Add(document);
        await SaveOrConflictAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct = default) => SaveOrConflictAsync(ct);

    public async Task RemoveAsync(PolicyDocument document, CancellationToken ct = default)
    {
        db.PolicyDocuments.Remove(document);
        await SaveOrConflictAsync(ct);
    }

    // Hai admin bấm cùng lúc: chỉ mục duy nhất trong DB chặn bản nháp thứ hai, bản hiệu lực thứ hai, số phiên bản trùng
    private async Task SaveOrConflictAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new PolicyRuleException("policy_conflict", "Có người vừa thay đổi tài liệu này. Tải lại trang rồi thử lại.");
        }
    }
}
