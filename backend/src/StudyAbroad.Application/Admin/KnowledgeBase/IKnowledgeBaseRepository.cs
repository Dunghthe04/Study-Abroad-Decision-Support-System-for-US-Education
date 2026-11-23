using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.KnowledgeBase;

public interface IKnowledgeBaseRepository
{
    Task<(IReadOnlyList<KnowledgeDocument> Rows, int Total)> PageAsync(
        KnowledgeEntryFilter filter, int skip, int take, CancellationToken ct = default);

    /// <summary>Một dòng để đọc, không theo dõi thay đổi. Null nếu không có.</summary>
    Task<KnowledgeDocument?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Số entry_no lớn nhất, 0 nếu bảng trống.</summary>
    Task<int> MaxEntryNoAsync(CancellationToken ct = default);

    /// <summary>Mã dòng đang dùng entry_no này, null nếu chưa có.</summary>
    Task<Guid?> FindIdByEntryNoAsync(int entryNo, CancellationToken ct = default);

    Task AddAsync(KnowledgeDocument document, CancellationToken ct = default);

    /// <summary>Nạp dòng, gọi apply rồi lưu. False nếu không có dòng.</summary>
    Task<bool> UpdateAsync(Guid id, Action<KnowledgeDocument> apply, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Các dòng có entry_no trong danh sách, có theo dõi thay đổi để lưu cùng SaveImportAsync.</summary>
    Task<IReadOnlyDictionary<int, KnowledgeDocument>> TrackedByEntryNosAsync(
        IReadOnlyCollection<int> entryNos, CancellationToken ct = default);

    /// <summary>Thêm dòng mới và lưu mọi thay đổi của các dòng đang theo dõi trong một transaction.</summary>
    Task SaveImportAsync(IReadOnlyList<KnowledgeDocument> added, CancellationToken ct = default);

    /// <summary>Xóa vector để dòng được tạo lại khi nạp.</summary>
    Task ClearEmbeddingsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
