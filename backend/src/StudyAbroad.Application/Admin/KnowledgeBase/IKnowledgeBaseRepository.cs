using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.KnowledgeBase;

/// <summary>Mọi truy vấn bỏ qua mục đã xóa mềm, trừ khi tham số includeDeleted / filter.Deleted nói khác, và ExistingKeysAsync (luôn gồm cả mục đã xóa).</summary>
public interface IKnowledgeBaseRepository
{
    /// <summary>Một trang danh sách kèm tên người tạo, và tổng số dòng thỏa bộ lọc. filter.Deleted = true thì chỉ lấy mục đã xóa.</summary>
    Task<(IReadOnlyList<KnowledgeEntryListRow> Rows, int Total)> PageAsync(
        KnowledgeEntryFilter filter, int skip, int take, CancellationToken ct = default);

    /// <summary>Một mục để đọc, không theo dõi thay đổi. Null nếu không có (hoặc đã xóa mà includeDeleted = false).</summary>
    Task<KnowledgeBaseEntry?> FindAsync(Guid id, bool includeDeleted = false, CancellationToken ct = default);

    /// <summary>Lịch sử của một mục, cũ trước, kèm tên người thao tác.</summary>
    Task<IReadOnlyList<KnowledgeEntryHistoryRow>> HistoryAsync(Guid entryId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);

    /// <summary>Thêm mục và dòng lịch sử trong một lần lưu.</summary>
    Task AddAsync(KnowledgeBaseEntry entry, KnowledgeBaseEntryHistory history, CancellationToken ct = default);

    /// <summary>Thêm nhiều mục (EntryNo do file đặt) và lịch sử trong một transaction, rồi đẩy bộ đếm entry_no lên số lớn nhất.</summary>
    Task AddRangeAsync(
        IReadOnlyList<KnowledgeBaseEntry> entries, IReadOnlyList<KnowledgeBaseEntryHistory> history, CancellationToken ct = default);

    /// <summary>
    /// Gọi apply cho từng mục tìm thấy trong ids rồi lưu một lần. apply trả dòng lịch sử nếu mục đổi, null nếu không đổi.
    /// includeDeleted = true để khôi phục mục đã xóa. Trả số mục tìm thấy.
    /// </summary>
    Task<int> UpdateManyAsync(
        IReadOnlyList<Guid> ids, Func<KnowledgeBaseEntry, KnowledgeBaseEntryHistory?> apply,
        bool includeDeleted = false, CancellationToken ct = default);

    /// <summary>Người đã tạo, sửa hoặc gửi duyệt từng mục (theo lịch sử), để chặn tự duyệt.</summary>
    Task<IReadOnlyList<(Guid EntryId, Guid ActorUserId)>> ContributorsAsync(
        IReadOnlyList<Guid> entryIds, CancellationToken ct = default);

    /// <summary>Mã số và câu hỏi của mọi mục, gồm cả mục đã xóa: để nhận ra dòng trùng khi nhập file.</summary>
    Task<IReadOnlyList<KnowledgeKeyRow>> ExistingKeysAsync(CancellationToken ct = default);

    /// <summary>Các mục có câu hỏi giống hệt (không phân biệt hoa thường).</summary>
    Task<IReadOnlyList<KnowledgeBaseEntry>> SameQuestionAsync(string question, bool includeDeleted, CancellationToken ct = default);

    Task<IReadOnlyList<KnowledgeCreatorDto>> CreatorsAsync(CancellationToken ct = default);
}

public record KnowledgeKeyRow(int EntryNo, string Question);