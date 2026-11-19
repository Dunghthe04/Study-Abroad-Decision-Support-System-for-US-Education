using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.KnowledgeBase;

/// <summary>Một mục kho tri thức trả cho giao diện admin. Levels là mã (chung, dai_hoc...).</summary>
public record KnowledgeEntryDto(
    Guid Id,
    int EntryNo,
    string? Label,
    IReadOnlyList<string> Levels,
    string Question,
    string? Answer,
    string? Source,
    string Origin,
    string ReviewStatus,
    string? ReviewNote,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    Guid? CreatedByUserId,
    string? CreatedByName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? DeletedAt);

public record KnowledgeEntryHistoryDto(
    Guid Id,
    string Action,
    string? FromStatus,
    string? ToStatus,
    string? Note,
    IReadOnlyList<string> ChangedFields,
    Guid? ActorUserId,
    string? ActorName,
    DateTime CreatedAt);

/// <summary>Chi tiết một mục kèm lịch sử, cũ trước.</summary>
public record KnowledgeEntryDetailDto(KnowledgeEntryDto Entry, string? ReviewedByName, IReadOnlyList<KnowledgeEntryHistoryDto> History);

public record KnowledgeCreatorDto(Guid UserId, string Name);

public record KnowledgeEntryListRow(KnowledgeBaseEntry Entry, string? CreatorName);

public record KnowledgeEntryHistoryRow(KnowledgeBaseEntryHistory History, string? ActorName);

/// <summary>Câu hỏi trùng một mục còn sống (409 kèm mã mục đó).</summary>
public class KnowledgeDuplicateEntryException(Guid existingId)
    : Exception("Câu hỏi này đã có trong kho tri thức.")
{
    public Guid ExistingId { get; } = existingId;
}

/// <summary>
/// Nội dung tạo hoặc sửa. Chỉ câu hỏi bắt buộc khi lưu nháp; Submit = true (gửi duyệt) thì cần đủ nhãn, bậc học, câu trả lời, nguồn.
/// Levels là mã bậc học.
/// </summary>
public record SaveKnowledgeEntryRequest(
    string? Label,
    IReadOnlyList<string>? Levels,
    string? Question,
    string? Answer,
    string? Source,
    bool Submit = false);

/// <summary>Bộ lọc danh sách; trường trống là không lọc. Deleted = true thì chỉ xem mục đã xóa. Sort: newest | oldest | entryNo | updated.</summary>
public record KnowledgeEntryFilter(
    string? Search = null,
    string? Label = null,
    string? Level = null,
    string? Source = null,
    string? ReviewStatus = null,
    string? Origin = null,
    Guid? CreatedByUserId = null,
    bool Deleted = false,
    string? Sort = null);

/// <summary>Duyệt theo lô: cùng một trạng thái, cùng một ghi chú.</summary>
public record ReviewKnowledgeEntriesRequest(IReadOnlyList<Guid>? Ids, string? Status, string? Note);

public record ReviewKnowledgeEntriesResultDto(int Updated, int NotFound, int SkippedOwn, int SkippedNotReady, int SkippedNotPending);

/// <summary>Một dòng đọc từ sheet knowledge_base, chưa kiểm tra. RowNumber là số dòng trong sheet.</summary>
public record KnowledgeImportRow(int RowNumber, string? Id, string? Label, string? Level, string? Question, string? Answer, string? Source);

public record KnowledgeImportErrorDto(int RowNumber, string Message);

public record KnowledgeImportPreviewDto(int TotalRows, IReadOnlyList<KnowledgeImportRow> Rows, IReadOnlyList<KnowledgeImportErrorDto> Errors);

public record KnowledgeImportResultDto(int TotalRows, int Inserted, int Duplicates, IReadOnlyList<KnowledgeImportErrorDto> Errors);