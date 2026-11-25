using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.KnowledgeBase;

public interface IKnowledgeBaseRepository
{
    Task<(IReadOnlyList<KnowledgeDocument> Rows, int Total)> PageAsync(
        KnowledgeEntryFilter filter, int skip, int take, CancellationToken ct = default);

    /// <summary>Một dòng để đọc, không theo dõi thay đổi. Mặc định đọc được cả mục đã xóa (UC-113 chỉ đọc).</summary>
    Task<KnowledgeDocument?> FindAsync(Guid id, bool includeDeleted = true, CancellationToken ct = default);

    Task<IReadOnlyList<KnowledgeEntryVersionRow>> VersionsAsync(Guid documentId, CancellationToken ct = default);

    Task<KnowledgeDocumentVersion?> FindVersionAsync(Guid documentId, int version, CancellationToken ct = default);

    /// <summary>Số entry_no lớn nhất từng được dùng (kể cả mục đã xóa - UC-114 2a, UC-116), 0 nếu bảng trống.</summary>
    Task<int> MaxEntryNoAsync(CancellationToken ct = default);

    /// <summary>Mã dòng đang dùng entry_no này (kể cả mục đã xóa - UC-114 E1), null nếu chưa có.</summary>
    Task<Guid?> FindIdByEntryNoAsync(int entryNo, CancellationToken ct = default);

    /// <summary>Lưu mục mới kèm phiên bản đầu tiên. Nếu đụng unique index entry_no (23505) thì ném KnowledgeDuplicateEntryException.</summary>
    Task AddAsync(KnowledgeDocument document, KnowledgeDocumentVersion initialVersion, CancellationToken ct = default);

    /// <summary>
    /// Nạp dòng, gọi apply rồi lưu. apply trả (Changed, NewVersion): nếu Changed = true thì lưu dòng và chèn NewVersion (nếu có).
    /// False nếu không có dòng.
    /// </summary>
    Task<bool> UpdateAsync(
        Guid id, Func<KnowledgeDocument, (bool Changed, KnowledgeDocumentVersion? NewVersion)> apply,
        bool includeDeleted = true, CancellationToken ct = default);

    /// <summary>Các dòng có entry_no trong danh sách (kể cả mục đã xóa), có theo dõi thay đổi để lưu cùng SaveImportAsync.</summary>
    Task<IReadOnlyDictionary<int, KnowledgeDocument>> TrackedByEntryNosAsync(
        IReadOnlyCollection<int> entryNos, CancellationToken ct = default);

    /// <summary>Thêm dòng mới, lưu thay đổi của các dòng đang theo dõi và chèn các phiên bản mới trong một transaction.</summary>
    Task SaveImportAsync(
        IReadOnlyList<KnowledgeDocument> added, IReadOnlyList<KnowledgeDocumentVersion> versions,
        CancellationToken ct = default);

    /// <summary>Xóa vector để dòng không còn được chat dùng hoặc được tạo lại vector.</summary>
    Task ClearEmbeddingsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
