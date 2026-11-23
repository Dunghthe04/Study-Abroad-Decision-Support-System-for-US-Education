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
    string? ImageUrl,
    string? Note,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Số entry_no đã dùng cho dòng khác (409 kèm mã dòng đó).</summary>
public class KnowledgeDuplicateEntryException(Guid existingId)
    : Exception("Mã số này đã dùng cho một dòng khác.")
{
    public Guid ExistingId { get; } = existingId;
}

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
    string? ImageUrl,
    string? Note);

/// <summary>Bộ lọc danh sách; trường trống là không lọc. Sort: newest | oldest | entryNo | updated.</summary>
public record KnowledgeEntryFilter(
    string? Search = null,
    string? DocType = null,
    string? Label = null,
    string? TopicCode = null,
    string? Sort = null);

/// <summary>Một dòng đọc từ sheet knowledge_base, chưa kiểm tra. RowNumber là số dòng trong sheet.</summary>
public record KnowledgeImportRow(
    int RowNumber, string? Id, string? DocType, string? Label, string? Question, string? Answer, string? Source,
    string? SourceUrl, string? RetrievedAt, string? TopicCode, string? ImageUrl, string? Note);

/// <summary>
/// Tiến độ tạo vector cho trang admin. Available: false khi không hỏi được advisor (tắt, chưa có API, sai khóa); khi đó các số còn lại là 0.
/// Pending: số dòng chưa có vector, tức chat chưa thấy. Started: chỉ có nghĩa với lần bấm tạo (false nếu không có gì để làm hoặc đang chạy).
/// </summary>
public record KnowledgeEmbedStatusDto(bool Available, int Pending, bool Running, int Done, string? Error, bool Started)
{
    public static KnowledgeEmbedStatusDto Unavailable { get; } = new(false, 0, false, 0, null, false);
}

public record KnowledgeImportErrorDto(int RowNumber, string Message);

/// <summary>Pending: số dòng có note bắt đầu bằng "CHỜ DUYỆT", import bỏ qua, không kiểm tra, không lưu.</summary>
public record KnowledgeImportPreviewDto(
    int TotalRows, IReadOnlyList<KnowledgeImportRow> Rows, IReadOnlyList<KnowledgeImportErrorDto> Errors, int Pending = 0);

/// <summary>
/// VectorsStarted: advisor đã bắt đầu tạo vector cho dòng thiếu vector. VectorWarning: không nhờ được advisor (tắt, lỗi),
/// các dòng đã lưu nhưng chat chưa thấy cho tới khi vector được tạo.
/// </summary>
public record KnowledgeImportResultDto(
    int TotalRows, int Created, int Updated, int Unchanged, IReadOnlyList<KnowledgeImportErrorDto> Errors, int Pending = 0,
    bool VectorsStarted = false, string? VectorWarning = null);
