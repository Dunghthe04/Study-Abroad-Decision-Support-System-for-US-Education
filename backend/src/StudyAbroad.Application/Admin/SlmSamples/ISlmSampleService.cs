using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Admin.SlmSamples;

public interface ISlmSampleService
{
    /// <summary>Danh sách một trang, mới nhất trước (UC-89). page từ 1; pageSize bị giới hạn 1 đến 100. Bộ lọc lạ thì ném ArgumentException.</summary>
    Task<PagedResult<SlmSampleDto>> GetPageAsync(SlmSampleFilter filter, int page, int pageSize, CancellationToken ct = default);

    Task<SlmSampleSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    Task<SlmSampleDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Chi tiết một mẫu (kể cả đã xóa) kèm lịch sử phiên bản (UC-90).</summary>
    Task<SlmSampleDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<SlmSampleCreatorDto>> GetCreatorsAsync(CancellationToken ct = default);

    /// <summary>Danh sách các nguồn (gồm manual, excel và các nguồn tùy chỉnh đang dùng) cho bộ lọc UC-89.</summary>
    Task<IReadOnlyList<string>> GetSourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// Tạo một mẫu mới ở phiên bản 1 (UC-92). Dữ liệu sai, thiếu intents hoặc có số điện thoại / e-mail thì ném ArgumentException;
    /// trùng mẫu chưa xóa thì ném SlmDuplicateSampleException.
    /// </summary>
    Task<SlmSampleDto> CreateAsync(
        SaveSlmSampleRequest request, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    /// <summary>
    /// Sửa một mẫu đang dùng và tạo phiên bản mới nếu có thay đổi (UC-93).
    /// Null nếu không có dòng; mẫu đã xóa thì ném SlmSampleDeletedException. Lỗi như CreateAsync.
    /// </summary>
    Task<SlmSampleDto?> UpdateAsync(
        Guid id, SaveSlmSampleRequest request, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    /// <summary>Xóa mềm mẫu (UC-94). False nếu không có hoặc đã xóa.</summary>
    Task<bool> DeleteAsync(
        Guid id, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    /// <summary>Khôi phục mẫu đã xóa mềm (UC-94 3a). Null nếu không tồn tại; trùng mẫu đang dùng thì ném SlmDuplicateSampleException.</summary>
    Task<SlmSampleDto?> RestoreAsync(
        Guid id, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    /// <summary>Khôi phục một phiên bản cũ bằng cách tạo phiên bản mới có nội dung đó (UC-90 2a). Null nếu mẫu hoặc phiên bản không tồn tại.</summary>
    Task<SlmSampleDto?> RestoreVersionAsync(
        Guid id, int version, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    /// <summary>
    /// Nhập file xlsx (UC-95). Dòng hợp lệ và chưa trùng với mẫu chưa xóa được thêm ở phiên bản 1; dòng lỗi được báo kèm lý do.
    /// File không đọc được thì ném InvalidDataException.
    /// </summary>
    Task<SlmImportResultDto> ImportAsync(
        Stream workbook, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    Task<SlmImportPreviewDto> PreviewAsync(Stream workbook, CancellationToken ct = default);

    /// <summary>UC-96: xuất toàn bộ mẫu chưa xóa ra jsonl hoặc xlsx, ghi nhật ký truy cập (UC-91) và đánh dấu thời điểm xuất. Định dạng lạ thì ném ArgumentException.</summary>
    Task<SlmExportFile> ExportAsync(
        string? format, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default);
}

public interface ISlmTemplateWriter
{
    byte[] Build();

    /// <summary>File xlsx các mẫu chưa xóa, mỗi loại mẫu một sheet (UC-96).</summary>
    byte[] BuildExport(IReadOnlyList<StudyAbroad.Domain.Entities.SlmTrainingSample> samples);
}

/// <summary>Đọc file xlsx dữ liệu huấn luyện thành các dòng thô, chưa kiểm tra nội dung.</summary>
public interface ISlmWorkbookReader
{
    /// <summary>
    /// Mỗi sheet có tên trùng một loại mẫu được đọc; sheet khác (README, summary) bị bỏ qua. Cột được nhận theo tên ở dòng tiêu đề
    /// (thứ tự cột có thể khác giữa các sheet). File hỏng hoặc không phải xlsx thì ném InvalidDataException.
    /// </summary>
    IReadOnlyList<SlmImportRow> Read(Stream workbook, CancellationToken ct = default);
}
