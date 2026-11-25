using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.KnowledgeBase;

public record KnowledgeEntryDto(
    Guid Id,
    int EntryNo,
    string DocType,
    string Label,
    string Question,
    string Answer,
    string? Source,
    string? SourceUrl,
    DateOnly RetrievedAt,
    string? TopicCode,
    string? Note,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int Version = 1,
    DateTime? DeletedAt = null,
    string? VectorWarning = null);

/// <summary>Một phiên bản bất biến của mục kho tri thức (UC-113).</summary>
public record KnowledgeEntryVersionDto(
    Guid Id,
    int Version,
    string Action,
    int? RestoredFromVersion,
    Guid? ActorUserId,
    string? ActorName,
    DateTime CreatedAt,
    string DocType,
    string Label,
    string Question,
    string Answer,
    string? Source,
    string? SourceUrl,
    DateOnly RetrievedAt,
    string? TopicCode,
    string? Note);

public record KnowledgeEntryVersionRow(KnowledgeDocumentVersion Version, string? ActorName);

/// <summary>Số entry_no đã dùng cho dòng khác, kể cả mục đã xóa (409 kèm mã dòng đó - UC-114 E1).</summary>
public class KnowledgeDuplicateEntryException(Guid existingId)
    : Exception("Mã số (id) này đã được dùng cho một mục tri thức khác.")
{
    public Guid ExistingId { get; } = existingId;
}

/// <summary>Mục tri thức đã bị xóa mềm nên không thể sửa hoặc khôi phục phiên bản trước khi khôi phục mục (UC-115 E1).</summary>
public class KnowledgeEntryDeletedException(string message = "Mục tri thức đã bị xóa, không thể sửa; hãy khôi phục trước.")
    : Exception(message);

public record SaveKnowledgeEntryRequest(
    int? EntryNo,
    string? DocType,
    string? Label,
    string? Question,
    string? Answer,
    string? Source,
    string? SourceUrl,
    DateOnly? RetrievedAt,
    string? TopicCode,
    string? Note);

/// <summary>Bộ lọc danh sách (UC-112); trường trống là không lọc. Sort: newest | oldest | entryNo | updated. Deleted = true lọc mục đã xóa.</summary>
public record KnowledgeEntryFilter(
    string? Search = null,
    string? DocType = null,
    string? Label = null,
    string? TopicCode = null,
    string? Sort = null,
    bool Deleted = false);

/// <summary>Một dòng đọc từ sheet knowledge_base (10 cột - UC-117), chưa kiểm tra. RowNumber là số dòng trong sheet.</summary>
public record KnowledgeImportRow(
    int RowNumber, string? Id, string? DocType, string? Label, string? Question, string? Answer, string? Source,
    string? SourceUrl, string? RetrievedAt, string? TopicCode, string? Note);

/// <summary>
/// Tiến độ tạo vector cho trang admin. Available: false khi không hỏi được advisor (tắt, chưa có API, sai khóa); khi đó các số còn lại là 0.
/// Pending: số dòng chưa có vector, tức chat chưa thấy. Started: chỉ có nghĩa với lần bấm tạo (false nếu không có gì để làm hoặc đang chạy).
/// </summary>
public record KnowledgeEmbedStatusDto(bool Available, int Pending, bool Running, int Done, string? Error, bool Started)
{
    public static KnowledgeEmbedStatusDto Unavailable { get; } = new(false, 0, false, 0, null, false);
}

public record KnowledgeImportErrorDto(int RowNumber, string Message);

public record KnowledgeImportPreviewDto(
    int TotalRows, IReadOnlyList<KnowledgeImportRow> Rows, IReadOnlyList<KnowledgeImportErrorDto> Errors);

/// <summary>
/// VectorsStarted: advisor đã bắt đầu tạo vector cho dòng thiếu vector. VectorWarning: không nhờ được advisor (tắt, lỗi),
/// các dòng đã lưu nhưng chat chưa thấy cho tới khi vector được tạo.
/// </summary>
public record KnowledgeImportResultDto(
    int TotalRows, int Created, int Updated, int Unchanged, IReadOnlyList<KnowledgeImportErrorDto> Errors,
    bool VectorsStarted = false, string? VectorWarning = null);
