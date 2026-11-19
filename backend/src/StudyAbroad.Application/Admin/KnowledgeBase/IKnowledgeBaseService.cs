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

    /// <summary>Đọc file xlsx, kiểm tra mọi dòng, trả 100 dòng đầu và mọi lỗi. Không ghi gì. File hỏng thì InvalidDataException.</summary>
    Task<KnowledgeImportPreviewDto> PreviewAsync(Stream workbook, CancellationToken ct = default);

    /// <summary>Thêm các dòng hợp lệ, chưa trùng, ở trạng thái pending trong một transaction; dòng lỗi được báo và bỏ qua.</summary>
    Task<KnowledgeImportResultDto> ImportAsync(Stream workbook, Guid actorUserId, CancellationToken ct = default);

    /// <summary>
    /// UC-53: lưu câu chat chưa trả lời được thành mục nháp, chỉ có câu hỏi. Bỏ qua nếu trống, quá dài,
    /// có thông tin liên hệ hoặc link, hoặc trùng câu đã có (kể cả đã xóa). Trả true nếu đã thêm.
    /// </summary>
    Task<bool> CaptureUnansweredAsync(string? question, CancellationToken ct = default);
}

/// <summary>Đọc sheet knowledge_base của file xlsx thành các dòng thô, chưa kiểm tra nội dung.</summary>
public interface IKnowledgeWorkbookReader
{
    /// <summary>Thiếu sheet knowledge_base, thiếu cột bắt buộc, hoặc file hỏng thì ném InvalidDataException.</summary>
    IReadOnlyList<KnowledgeImportRow> Read(Stream workbook, CancellationToken ct = default);
}

public interface IKnowledgeTemplateWriter
{
    byte[] Build();
}
