using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.SlmSamples;

/// <summary>Một dòng dữ liệu huấn luyện trả cho giao diện admin.</summary>
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
    string ReviewStatus,
    string? ReviewNote,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string Source = "manual",
    Guid? CreatedByUserId = null,
    string? CreatedByName = null,
    DateTime? ExportedAt = null);

/// <summary>Chi tiết một mẫu kèm tên người duyệt (UC-96).</summary>
public record SlmSampleDetailDto(SlmSampleDto Sample, string? ReviewedByName);

public record SlmSampleCreatorDto(Guid UserId, string Name);

public record SlmSampleListRow(SlmTrainingSample Sample, string? CreatorName);

public record SlmExportFile(byte[] Content, string ContentType, string FileName);

/// <summary>Mẫu trùng với một mẫu đã có (409 kèm mã mẫu đó).</summary>
public class SlmDuplicateSampleException(Guid existingId)
    : Exception("Mẫu này trùng với một mẫu đã có.")
{
    public Guid ExistingId { get; } = existingId;
}

/// <summary>
/// Nội dung tạo hoặc sửa một dòng. Profile, Intents, Context là chuỗi JSON hợp lệ (Intents là một mảng, bắt buộc).
/// Split bỏ trống thì mặc định train. Submit = true thì gửi duyệt (pending), cần có intents.
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
    string? Source = null,
    bool Submit = false);

/// <summary>Bộ lọc danh sách. Các trường bỏ trống là không lọc. Search tìm trong câu hỏi. From gồm, To không gồm (UTC).</summary>
public record SlmSampleFilter(
    string? SampleType, string? Split, string? ReviewStatus, string? Search,
    Guid? CreatedByUserId = null, DateTime? From = null, DateTime? To = null, string? Source = null);

/// <summary>Duyệt một hoặc nhiều dòng cùng lúc: đổi sang cùng một trạng thái và cùng một ghi chú.</summary>
public record ReviewSlmSamplesRequest(IReadOnlyList<Guid>? Ids, string? Status, string? Note);

/// <summary>
/// Kết quả duyệt theo lô: số dòng được đổi, số mã không tồn tại, số dòng bỏ qua vì người duyệt đã tạo hoặc sửa nó,
/// số dòng bỏ qua vì chưa có câu trả lời lẫn intents.
/// </summary>
public record ReviewSlmSamplesResultDto(int Updated, int NotFound, int SkippedOwn = 0, int SkippedNotReady = 0, int SkippedNotPending = 0);

public record SlmSampleCountRow(string SampleType, string ReviewStatus, int Count);

/// <summary>Số dòng theo trạng thái duyệt và theo loại, cho các tab của trang admin.</summary>
public record SlmSampleSummaryDto(
    int Total,
    IReadOnlyDictionary<string, int> ByStatus,
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
