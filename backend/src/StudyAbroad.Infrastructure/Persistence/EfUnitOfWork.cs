using StudyAbroad.Application.Common;

namespace StudyAbroad.Infrastructure.Persistence;

/// <summary>
/// Transaction trên AppDbContext dùng chung trong request: các repository gọi SaveChangesAsync bên trong
/// đều nằm trong transaction này. Không commit (lỗi hoặc hủy) thì dispose sẽ rollback.
/// </summary>
public class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await work(ct);
        await tx.CommitAsync(ct);
    }
}
