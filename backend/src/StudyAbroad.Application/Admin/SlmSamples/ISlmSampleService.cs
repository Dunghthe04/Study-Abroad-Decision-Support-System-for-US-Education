using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Admin.SlmSamples;

public interface ISlmSampleService
{
    /// <summary>Danh sách một trang, mới nhất trước. page từ 1; pageSize bị giới hạn 1 đến 100. Bộ lọc lạ thì ném ArgumentException.</summary>
    Task<PagedResult<SlmSampleDto>> GetPageAsync(SlmSampleFilter filter, int page, int pageSize, CancellationToken ct = default);

    Task<SlmSampleSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    Task<SlmSampleDto?> GetAsync(Guid id, CancellationToken ct = default);

    Task<SlmSampleDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<SlmSampleCreatorDto>> GetCreatorsAsync(CancellationToken ct = default);

    /// <summary>
    /// Tạo một dòng ở trạng thái draft, hoặc pending nếu Submit. Dữ liệu sai hoặc có số điện thoại / e-mail thì ném ArgumentException;
    /// trùng mẫu đã có thì ném SlmDuplicateSampleException.
    /// </summary>
    Task<SlmSampleDto> CreateAsync(SaveSlmSampleRequest request, Guid actorUserId, CancellationToken ct = default);

    /// <summary>
    /// Sửa một dòng và ghi lịch sử. Dòng approved đổi nội dung thì về pending; Submit đưa draft, rejected, needs_fix sang pending.
    /// Null nếu không có dòng. Lỗi như CreateAsync.
    /// </summary>
    Task<SlmSampleDto?> UpdateAsync(Guid id, SaveSlmSampleRequest request, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Xóa mềm. False nếu không có hoặc đã xóa.</summary>
    Task<bool> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);

    /// <summary>
    /// Duyệt một hoặc nhiều dòng sang approved, rejected hoặc needs_fix. Từ chối và yêu cầu sửa phải có ghi chú.
    /// Bỏ qua dòng do chính người duyệt tạo, sửa hoặc gửi duyệt, và dòng approved chưa có câu trả lời lẫn intents.
    /// </summary>
    Task<ReviewSlmSamplesResultDto> ReviewAsync(ReviewSlmSamplesRequest request, Guid actorUserId, CancellationToken ct = default);

    /// <summary>
    /// Nhập file xlsx. Dòng hợp lệ và chưa trùng được thêm ở trạng thái draft trong một lần lưu; dòng lỗi được báo kèm lý do và không thêm.
    /// File không đọc được thì ném InvalidDataException.
    /// </summary>
    Task<SlmImportResultDto> ImportAsync(Stream workbook, Guid actorUserId, CancellationToken ct = default);

    Task<SlmImportPreviewDto> PreviewAsync(Stream workbook, CancellationToken ct = default);

    /// <summary>UC-102: xuất mẫu approved chưa xóa ra jsonl hoặc xlsx, ghi nhật ký và đánh dấu đã xuất. Định dạng lạ thì ném ArgumentException.</summary>
    Task<SlmExportFile> ExportAsync(
        string? format, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default);
}

public interface ISlmTemplateWriter
{
    byte[] Build();

    /// <summary>File xlsx các mẫu đã duyệt, mỗi loại mẫu một sheet.</summary>
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
