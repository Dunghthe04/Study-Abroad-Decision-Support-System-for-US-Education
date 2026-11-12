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
    DateTime? UpdatedAt);

/// <summary>
/// Nội dung tạo hoặc sửa một dòng. Profile, Intents, Context là chuỗi JSON hợp lệ (Intents là một mảng, bắt buộc).
/// Split bỏ trống thì mặc định train.
/// </summary>
public record SaveSlmSampleRequest(
    string? SampleType,
    string? Question,
    string? Profile,
    string? PendingQuestion,
    string? Intents,
    string? Context,
    string? Split,
    string? SourceReview);

/// <summary>Bộ lọc danh sách. Các trường bỏ trống là không lọc. Search tìm trong câu hỏi.</summary>
public record SlmSampleFilter(string? SampleType, string? Split, string? ReviewStatus, string? Search);

/// <summary>Duyệt một hoặc nhiều dòng cùng lúc: đổi sang cùng một trạng thái và cùng một ghi chú.</summary>
public record ReviewSlmSamplesRequest(IReadOnlyList<Guid>? Ids, string? Status, string? Note);

/// <summary>Kết quả duyệt theo lô: số dòng thực sự được đổi, số mã không tồn tại.</summary>
public record ReviewSlmSamplesResultDto(int Updated, int NotFound);

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

/// <summary>Kết quả import: dòng mới thêm, dòng trùng đã bỏ qua, dòng lỗi (không thêm) kèm lý do.</summary>
public record SlmImportResultDto(
    int TotalRows,
    int Inserted,
    int Duplicates,
    IReadOnlyList<SlmImportErrorDto> Errors);

/// <summary>Một dòng của file JSONL xuất ra: đầu vào và đầu ra cho model.</summary>
public record SlmExportLine(
    string Type,
    string Question,
    string? Profile,
    string? PendingQuestion,
    string? Context,
    string Intents);
