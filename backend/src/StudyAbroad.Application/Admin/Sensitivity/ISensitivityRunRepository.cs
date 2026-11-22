using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.Sensitivity;

// [UC-85] Một lần chạy đã lưu kèm tên người chạy (null nếu tài khoản đã xóa).
public sealed record SensitivityRunRecord(SensitivityRun Run, string? CreatedByName);

public interface ISensitivityRunRepository
{
    /// <summary>Lưu lần chạy và dòng nhật ký trong MỘT lần ghi: hoặc có cả hai, hoặc không có gì.</summary>
    Task AddAsync(SensitivityRun run, AuditLog audit, CancellationToken ct = default);

    /// <summary>Các lần chạy mới nhất trước, tối đa limit dòng. Không tải cột kết quả (ResultJson, ParametersJson) cho nhẹ.</summary>
    Task<IReadOnlyList<SensitivityRunRecord>> ListAsync(int limit, CancellationToken ct = default);

    /// <summary>Một lần chạy đầy đủ; null nếu không có.</summary>
    Task<SensitivityRunRecord?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Mã lần chạy ngay TRƯỚC lần chạy này (theo thời điểm chạy); null nếu không có lần nào trước nó.</summary>
    Task<Guid?> FindPreviousIdAsync(Guid id, CancellationToken ct = default);
}
