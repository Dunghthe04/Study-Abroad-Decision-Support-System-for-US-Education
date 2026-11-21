using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class OrderRepository(AppDbContext db) : IOrderRepository
{
    private const string OnePendingIndex = "ux_payment_transactions_one_pending_per_user";

    public Task<PaymentTransaction?> FindByCodeAsync(long orderCode, CancellationToken ct = default) =>
        db.PaymentTransactions.AsNoTracking().FirstOrDefaultAsync(o => o.OrderCode == orderCode, ct);

    public async Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)> ListByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.PaymentTransactions.AsNoTracking().Where(o => o.UserId == userId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.OrderCode)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyDictionary<Guid, UserSubscription>> GetCyclesAsync(IReadOnlyCollection<Guid> cycleIds, CancellationToken ct = default) =>
        cycleIds.Count == 0
            ? new Dictionary<Guid, UserSubscription>()
            : await db.UserSubscriptions.AsNoTracking().Where(c => cycleIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

    public async Task<OrderInsertResult> TryAddAsync(PaymentTransaction order, CancellationToken ct = default)
    {
        db.PaymentTransactions.Add(order);
        try
        {
            await db.SaveChangesAsync(ct);
            return OrderInsertResult.Added;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            db.Entry(order).State = EntityState.Detached;
            return pg.ConstraintName == OnePendingIndex ? OrderInsertResult.PendingExists : OrderInsertResult.CodeTaken;
        }
    }

    // Khóa dòng đơn như AccountPurgeRepository.LockIfStillDueAsync. Bản đang theo dõi trong bộ nhớ có thể cũ nên bỏ đi và nạp lại sau khi khóa
    public async Task<PaymentTransaction?> LockByCodeAsync(long orderCode, CancellationToken ct = default)
    {
        foreach (var stale in db.PaymentTransactions.Local.Where(o => o.OrderCode == orderCode).ToList())
            db.Entry(stale).State = EntityState.Detached;

        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM app.payment_transactions
            WHERE order_code = {orderCode}
            FOR UPDATE
            """).ToListAsync(ct);
        return ids.Count == 0 ? null : await db.PaymentTransactions.FirstOrDefaultAsync(o => o.Id == ids[0], ct);
    }

    public void AddCycle(UserSubscription cycle) => db.UserSubscriptions.Add(cycle);

    public void AddGroup(FamilyGroup group) => db.FamilyGroups.Add(group);

    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public void ClearTracking() => db.ChangeTracker.Clear();

    // Deadlock (40P01) là vòng chờ khóa với tiến trình khác (purge tài khoản...): Postgres hủy một bên, bên đó chạy lại được. Ném thẳng từ câu SELECT ... FOR UPDATE
    // hoặc bọc trong DbUpdateException khi tới từ SaveChanges
    public bool IsConflict(Exception ex) =>
        ex is DbUpdateConcurrencyException
        || ex is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected }
        || ex is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.DeadlockDetected } };
}
