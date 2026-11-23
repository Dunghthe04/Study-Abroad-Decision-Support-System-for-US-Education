using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyAbroad.Infrastructure.Persistence.Repositories;

namespace StudyAbroad.UnitTests;

// IOrderRepository.IsConflict quyết định ApplyPaymentAsync có chạy lại một lần hay trả 500 (L1: deadlock 40P01 giữa purge và thanh toán).
public class OrderRepositoryConflictTests
{
    // IsConflict không đụng DbContext
    private static readonly OrderRepository Repo = new(null!);

    private static PostgresException Pg(string sqlState) => new("x", "ERROR", "ERROR", sqlState);

    [Fact]
    public void L1_DeadlockFromARawLockQuery_IsAConflict() => Assert.True(Repo.IsConflict(Pg(PostgresErrorCodes.DeadlockDetected)));

    [Fact]
    public void L1_DeadlockWrappedByDbUpdateException_IsAConflict() =>
        Assert.True(Repo.IsConflict(new DbUpdateException("lưu lỗi", Pg(PostgresErrorCodes.DeadlockDetected))));

    [Fact]
    public void UniqueViolationAndConcurrencyStayConflicts()
    {
        Assert.True(Repo.IsConflict(new DbUpdateException("trùng", Pg(PostgresErrorCodes.UniqueViolation))));
        Assert.True(Repo.IsConflict(new DbUpdateConcurrencyException("xmin")));
    }

    [Fact]
    public void OtherDatabaseErrors_AreNotConflicts()
    {
        Assert.False(Repo.IsConflict(Pg(PostgresErrorCodes.ForeignKeyViolation)));
        Assert.False(Repo.IsConflict(new DbUpdateException("lưu lỗi", Pg(PostgresErrorCodes.ForeignKeyViolation))));
        Assert.False(Repo.IsConflict(new InvalidOperationException("khác")));
    }
}
