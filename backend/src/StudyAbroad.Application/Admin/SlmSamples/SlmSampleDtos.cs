using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.SlmSamples;

/// <summary>Một dòng dữ liệu huấn luyện trả cho giao diện admin (UC-89, UC-90).</summary>
public record SlmSampleDto(
    Guid Id,
    string SampleType,
    string Question,
    string? Profile,
    string? PendingQuestion,
    string Intents,
    string? Context,
    string Split,
    string? SourceReview,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string Source = "manual",
    Guid? CreatedByUserId = null,
    string? CreatedByName = null,
    DateTime? ExportedAt = null,
    int Version = 1,
    DateTime? DeletedAt = null);

/// <summary>Một phiên bản bất biến của mẫu huấn luyện SLM (UC-90).</summary>
public record SlmSampleVersionDto(
    Guid Id,
    int Version,
    string Action,
    int? RestoredFromVersion,
    Guid? ActorUserId,
    string? ActorName,
    DateTime CreatedAt,
    string SampleType,
    string Question,
    string? Profile,
    string? PendingQuestion,
    string Intents,
    string? Context,
    string Split,
    string? SourceReview,
    string Source);

/// <summary>Chi tiết một mẫu kèm danh sách phiên bản (UC-90).</summary>
public record SlmSampleDetailDto(SlmSampleDto Sample, IReadOnlyList<SlmSampleVersionDto> Versions);

public record SlmSampleCreatorDto(Guid UserId, string Name);

public record SlmSampleListRow(SlmTrainingSample Sample, string? CreatorName);

public record SlmSampleVersionRow(SlmSampleVersion Version, string? ActorName);

public record SlmExportFile(byte[] Content, string ContentType, string FileName);

/// <summary>Mẫu trùng với một mẫu chưa xóa đã có (409 kèm mã mẫu đó).</summary>
public class SlmDuplicateSampleException(Guid existingId)
    : Exception("Mẫu này trùng với một mẫu chưa xóa đã có.")
{
    public Guid ExistingId { get; } = existingId;
}

/// <summary>Thao tác sửa hoặc khôi phục phiên bản trên mẫu đã bị xóa mềm (409).</summary>
public class SlmSampleDeletedException(string message = "Mẫu đã bị xóa. Hãy khôi phục mẫu trước khi thực hiện thao tác này.")
    : Exception(message);

/// <summary>
/// Nội dung tạo hoặc sửa một dòng (UC-92, UC-93). Intents là mảng JSON không rỗng (bắt buộc).
/// Profile, Context là chuỗi JSON hợp lệ (không bắt buộc). Split bỏ trống thì mặc định train.
/// </summary>
public record SaveSlmSampleRequest(
    string? SampleType,
    string? Question,
    string? Profile,
    string? PendingQuestion,
    string? Intents,
    string? Context,
    string? Split,
    string? SourceReview,
    string? Source = null);

/// <summary>Bộ lọc danh sách (UC-89). Các trường bỏ trống là không lọc. Search tìm trong câu hỏi. From gồm, To không gồm (UTC). Deleted = true lọc mẫu đã xóa.</summary>
public record SlmSampleFilter(
    string? SampleType, string? Split, string? Search,
    Guid? CreatedByUserId = null, DateTime? From = null, DateTime? To = null, string? Source = null,
    bool Deleted = false);

public record SlmSampleCountRow(string SampleType, bool Deleted, int Count);

/// <summary>Số dòng đang dùng, đã xóa và theo loại mẫu, cho trang admin (UC-89).</summary>
public record SlmSampleSummaryDto(
    int Total,
    int Deleted,
    IReadOnlyDictionary<string, int> ByType);

/// <summary>Một dòng đọc từ file Excel, chưa kiểm tra. RowNumber là số thứ tự dòng trong sheet để báo lỗi.</summary>
public record SlmImportRow(
    string SheetName,
    int RowNumber,
    string? Question,
    string? Profile,
    string? PendingQuestion,
    string? Intents,
    string? Context,
    string? Split,
    string? SourceReview);

public record SlmImportErrorDto(string SheetName, int RowNumber, string Message);

public record SlmImportPreviewDto(
    int TotalRows,
    IReadOnlyList<SlmImportRow> Rows,
    IReadOnlyList<SlmImportErrorDto> Errors);

/// <summary>Kết quả import: dòng mới thêm, dòng trùng đã bỏ qua, dòng lỗi (không thêm) kèm lý do.</summary>
public record SlmImportResultDto(
    int TotalRows,
    int Inserted,
    int Duplicates,
    IReadOnlyList<SlmImportErrorDto> Errors);
