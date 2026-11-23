using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Admin.KnowledgeBase;

public interface IKnowledgeBaseService
{
    /// <summary>Danh sách một trang. page từ 1; pageSize bị giới hạn 1 đến 100. Bộ lọc lạ thì ném ArgumentException.</summary>
    Task<PagedResult<KnowledgeEntryDto>> GetPageAsync(KnowledgeEntryFilter filter, int page, int pageSize, CancellationToken ct = default);

    Task<KnowledgeEntryDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Dữ liệu sai thì ArgumentException; entry_no đã dùng thì KnowledgeDuplicateEntryException. Bỏ trống entry_no thì lấy số lớn nhất cộng 1.</summary>
    Task<KnowledgeEntryDto> CreateAsync(SaveKnowledgeEntryRequest request, CancellationToken ct = default);

    /// <summary>Sửa mọi trường trừ entry_no. Null nếu không có dòng. Câu hỏi đổi thì vector bị xóa để tạo lại.</summary>
    Task<KnowledgeEntryDto?> UpdateAsync(Guid id, SaveKnowledgeEntryRequest request, CancellationToken ct = default);

    /// <summary>Xóa hẳn. False nếu không có dòng.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Đọc file xlsx, kiểm tra mọi dòng, trả 100 dòng đầu và mọi lỗi. Không ghi gì. File hỏng thì InvalidDataException.</summary>
    Task<KnowledgeImportPreviewDto> PreviewAsync(Stream workbook, CancellationToken ct = default);

    /// <summary>Thêm hoặc cập nhật theo entry_no trong một transaction; dòng lỗi được báo và bỏ qua.</summary>
    Task<KnowledgeImportResultDto> ImportAsync(Stream workbook, CancellationToken ct = default);
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
