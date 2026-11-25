using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Admin.KnowledgeBase;

public interface IKnowledgeBaseService
{
    /// <summary>UC-112: danh sách một trang. page từ 1; pageSize bị giới hạn 1 đến 100. Bộ lọc lạ thì ném ArgumentException.</summary>
    Task<PagedResult<KnowledgeEntryDto>> GetPageAsync(KnowledgeEntryFilter filter, int page, int pageSize, CancellationToken ct = default);

    /// <summary>UC-113: xem chi tiết mục tri thức (kể cả mục đã xóa - chỉ đọc).</summary>
    Task<KnowledgeEntryDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>UC-113: danh sách phiên bản bất biến của một mục tri thức, mới nhất trước. Null nếu không tồn tại mục.</summary>
    Task<IReadOnlyList<KnowledgeEntryVersionDto>?> GetVersionsAsync(Guid id, CancellationToken ct = default);

    /// <summary>UC-114: tạo mục mới ở phiên bản 1, ghi nhật ký thao tác. Dữ liệu sai thì ArgumentException; entry_no đã dùng thì KnowledgeDuplicateEntryException.</summary>
    Task<KnowledgeEntryDto> CreateAsync(
        SaveKnowledgeEntryRequest request, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    /// <summary>UC-115: sửa mọi trường trừ entry_no, tạo phiên bản mới nếu đổi và ghi nhật ký. Null nếu không có dòng; mục đã xóa thì KnowledgeEntryDeletedException.</summary>
    Task<KnowledgeEntryDto?> UpdateAsync(
        Guid id, SaveKnowledgeEntryRequest request, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    /// <summary>UC-116: xóa mềm mục tri thức, xóa vector để chat ngừng dùng và ghi nhật ký. False nếu không có hoặc đã xóa.</summary>
    Task<bool> DeleteAsync(
        Guid id, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    /// <summary>UC-116 3a: khôi phục mục đã xóa mềm, tạo lại vector và ghi nhật ký. Null nếu không tồn tại.</summary>
    Task<KnowledgeEntryDto?> RestoreAsync(
        Guid id, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    /// <summary>UC-113 3a: khôi phục phiên bản cũ bằng cách tạo phiên bản mới có nội dung đó, tạo lại vector nếu câu hỏi đổi và ghi nhật ký.</summary>
    Task<KnowledgeEntryDto?> RestoreVersionAsync(
        Guid id, int version, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    /// <summary>UC-117: đọc file xlsx, kiểm tra mọi dòng, trả 100 dòng đầu và mọi lỗi. Không ghi gì. File hỏng thì InvalidDataException.</summary>
    Task<KnowledgeImportPreviewDto> PreviewAsync(Stream workbook, CancellationToken ct = default);

    /// <summary>UC-117: thêm hoặc cập nhật theo entry_no trong một transaction, tạo phiên bản mới, ghi nhật ký; dòng lỗi được báo và bỏ qua.</summary>
    Task<KnowledgeImportResultDto> ImportAsync(
        Stream workbook, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    /// <summary>Tiến độ tạo vector. Không hỏi được advisor thì trả Available = false chứ không ném lỗi.</summary>
    Task<KnowledgeEmbedStatusDto> GetEmbedStatusAsync(CancellationToken ct = default);

    /// <summary>Nhờ advisor tạo vector cho dòng còn thiếu (nút "Thử lại"). Không nhờ được thì trả Available = false.</summary>
    Task<KnowledgeEmbedStatusDto> StartEmbedAsync(CancellationToken ct = default);
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
