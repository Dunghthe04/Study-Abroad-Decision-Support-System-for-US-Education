using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Admin.KnowledgeBase;

public interface IKnowledgeBaseService
{
    /// <summary>Danh sách một trang. page từ 1; pageSize bị giới hạn 1 đến 100. Bộ lọc lạ thì ném ArgumentException.</summary>
    Task<PagedResult<KnowledgeEntryDto>> GetPageAsync(KnowledgeEntryFilter filter, int page, int pageSize, CancellationToken ct = default);

    Task<KnowledgeEntryDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Chi tiết kèm lịch sử. Mục đã xóa vẫn xem được (để khôi phục).</summary>
    Task<KnowledgeEntryDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<KnowledgeCreatorDto>> GetCreatorsAsync(CancellationToken ct = default);

    /// <summary>Tạo ở trạng thái draft, hoặc pending nếu Submit. Dữ liệu sai thì ArgumentException; trùng câu hỏi thì KnowledgeDuplicateEntryException.</summary>
    Task<KnowledgeEntryDto> CreateAsync(SaveKnowledgeEntryRequest request, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Sửa và ghi lịch sử. Mục approved đổi bất kỳ trường nào thì về pending. Null nếu không có. Lỗi như CreateAsync.</summary>
    Task<KnowledgeEntryDto?> UpdateAsync(Guid id, SaveKnowledgeEntryRequest request, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Xóa mềm. False nếu không có hoặc đã xóa.</summary>
    Task<bool> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Khôi phục mục đã xóa. Null nếu không có mục đã xóa đó; câu hỏi đã trùng một mục còn sống thì KnowledgeDuplicateEntryException.</summary>
    Task<KnowledgeEntryDto?> RestoreAsync(Guid id, Guid actorUserId, CancellationToken ct = default);

    /// <summary>
    /// Duyệt theo lô (tối đa 500) sang approved, rejected hoặc needs_fix; từ chối và yêu cầu sửa phải có ghi chú.
    /// Bỏ qua mục do chính người duyệt tạo, sửa hoặc gửi duyệt, mục không ở pending, và mục chưa đủ nội dung khi duyệt.
    /// </summary>
    Task<ReviewKnowledgeEntriesResultDto> ReviewAsync(ReviewKnowledgeEntriesRequest request, Guid actorUserId, CancellationToken ct = default);
}