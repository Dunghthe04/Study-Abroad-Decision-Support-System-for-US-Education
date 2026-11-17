namespace StudyAbroad.Application.Common;

/// <summary>Chạy nhiều bước ghi DB trong một transaction: lỗi ở bất kỳ bước nào thì không bước nào được lưu.</summary>
public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default);
}
