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

public record KnowledgeImportErrorDto(int RowNumber, string Message);

public record KnowledgeImportPreviewDto(int TotalRows, IReadOnlyList<KnowledgeImportRow> Rows, IReadOnlyList<KnowledgeImportErrorDto> Errors);

public record KnowledgeImportResultDto(
    int TotalRows, int Created, int Updated, int Unchanged, IReadOnlyList<KnowledgeImportErrorDto> Errors);
