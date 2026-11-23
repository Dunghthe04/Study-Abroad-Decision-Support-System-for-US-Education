using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.Policies;

public interface IPolicyDocumentRepository
{
    /// <summary>Một phiên bản theo mã (có theo dõi để sửa rồi gọi SaveAsync); null nếu không có.</summary>
    Task<PolicyDocument?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Bản đang hiệu lực của loại này (có theo dõi); null nếu chưa có.</summary>
    Task<PolicyDocument?> FindPublishedAsync(string kind, CancellationToken ct = default);

    /// <summary>Bản nháp của loại này (có theo dõi); null nếu không có.</summary>
    Task<PolicyDocument?> FindDraftAsync(string kind, CancellationToken ct = default);

    /// <summary>Lịch sử các phiên bản, mới nhất trước. kind null là mọi loại.</summary>
    Task<IReadOnlyList<PolicyDocument>> ListAsync(string? kind, CancellationToken ct = default);

    /// <summary>Các số phiên bản đã dùng của loại này bắt đầu bằng prefix (ví dụ "2026-11"), mọi trạng thái.</summary>
    Task<IReadOnlyList<string>> ListVersionsAsync(string kind, string prefix, CancellationToken ct = default);

    Task AddAsync(PolicyDocument document, CancellationToken ct = default);

    /// <summary>Lưu các thay đổi đã làm trên bản đang theo dõi.</summary>
    Task SaveAsync(CancellationToken ct = default);

    Task RemoveAsync(PolicyDocument document, CancellationToken ct = default);
}
