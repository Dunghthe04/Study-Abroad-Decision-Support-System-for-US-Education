using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyAbroad.Application.BirthDateChanges;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-107 / UC-150] Yêu cầu sửa ngày sinh.</summary>
public class BirthDateChangeRepository(AppDbContext db) : IBirthDateChangeRepository
{
    public Task<BirthDateChangeRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.BirthDateChangeRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<BirthDateChangeRequest?> GetPendingByUserAsync(Guid userId, CancellationToken ct = default) =>
        db.BirthDateChangeRequests.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Status == BirthDateChangeStatuses.Pending, ct);

    public Task<BirthDateChangeRequest?> GetLatestByUserAsync(Guid userId, CancellationToken ct = default) =>
        db.BirthDateChangeRequests.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> TryAddAsync(BirthDateChangeRequest request, CancellationToken ct = default)
    {
        db.BirthDateChangeRequests.Add(request);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(request).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> GetEvidencePathsAsync(CancellationToken ct = default) =>
        await db.BirthDateChangeRequests.AsNoTracking()
            .Where(x => x.EvidencePath != null)
            .Select(x => x.EvidencePath!)
            .ToListAsync(ct);

    public async Task<bool> TryCloseAsync(Guid id, string newStatus, Guid? reviewedByUserId, string? rejectReason, DateTime at, CancellationToken ct = default)
    {
        DateTime? reviewedAt = reviewedByUserId == null ? null : at;   // người dùng tự hủy thì không có người duyệt
        // UPDATE có điều kiện status = pending: Admin duyệt và người dùng hủy cùng lúc thì chỉ một bên ghi được
        var rows = await db.BirthDateChangeRequests
            .Where(x => x.Id == id && x.Status == BirthDateChangeStatuses.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, newStatus)
                .SetProperty(x => x.ReviewedByUserId, reviewedByUserId)
                .SetProperty(x => x.ReviewedAt, reviewedAt)
                .SetProperty(x => x.RejectReason, rejectReason)
                .SetProperty(x => x.EvidencePath, (string?)null)
                // Đặc tả UC-107/150: đóng yêu cầu thì chỉ còn loại giấy tờ, không giữ tên, kiểu, dung lượng tệp
                .SetProperty(x => x.EvidenceFileName, (string?)null)
                .SetProperty(x => x.EvidenceContentType, (string?)null)
                .SetProperty(x => x.EvidenceSize, (long?)null)
                .SetProperty(x => x.UpdatedAt, at), ct);
        return rows == 1;
    }

    public async Task<PagedResult<BirthDateRequestListRow>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safeSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = from r in db.BirthDateChangeRequests.AsNoTracking()
                    join u in db.Users.AsNoTracking() on r.UserId equals u.Id
                    select new { r, u.FullName, u.Email, u.Role };
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.r.Status == status);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(x => x.r.Status == BirthDateChangeStatuses.Pending ? 0 : 1)
            .ThenBy(x => x.r.Status == BirthDateChangeStatuses.Pending ? x.r.CreatedAt : DateTime.MinValue)
            .ThenByDescending(x => x.r.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(ct);

        var items = rows.Select(x => new BirthDateRequestListRow(x.r, x.FullName, x.Email, x.Role)).ToList();
        return new PagedResult<BirthDateRequestListRow>(items, safePage, safeSize, total);
    }
}
