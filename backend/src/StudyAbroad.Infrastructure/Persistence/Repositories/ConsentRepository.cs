using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Policies;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-104] Bằng chứng đồng ý điều khoản của tài khoản. Chỉ thêm dòng mới; dòng mới nhất của (người dùng, mục đích) là tình trạng hiện tại.</summary>
public class ConsentRepository(AppDbContext db) : IConsentRepository
{
    public Task<Consent?> GetLatestAsync(Guid userId, string purpose, CancellationToken ct = default) =>
        db.Consents.AsNoTracking()
            .Where(c => c.UserId == userId && c.Purpose == purpose)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(Consent consent, CancellationToken ct = default)
    {
        db.Consents.Add(consent);
        await db.SaveChangesAsync(ct);
    }
}
