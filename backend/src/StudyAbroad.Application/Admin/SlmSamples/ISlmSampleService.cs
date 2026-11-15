using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Admin.SlmSamples;

public interface ISlmSampleService
{
    /// <summary>Danh sách một trang, mới nhất trước. page từ 1; pageSize bị giới hạn 1 đến 100. Bộ lọc lạ thì ném ArgumentException.</summary>
    Task<PagedResult<SlmSampleDto>> GetPageAsync(SlmSampleFilter filter, int page, int pageSize, CancellationToken ct = default);

    Task<SlmSampleSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    Task<SlmSampleDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Tạo một dòng ở trạng thái draft. Dữ liệu sai (loại mẫu, tập, JSON, câu hỏi trống) thì ném ArgumentException.</summary>
    Task<SlmSampleDto> CreateAsync(SaveSlmSampleRequest request, Guid actorUserId, CancellationToken ct = default);

    /// <summary>
    /// Sửa một dòng. Nếu Question, Intents, Profile, PendingQuestion hoặc Context đổi thì dòng quay về draft và xóa thông tin duyệt cũ.
    /// Null nếu không có dòng. Dữ liệu sai thì ném ArgumentException.
    /// </summary>
    Task<SlmSampleDto?> UpdateAsync(Guid id, SaveSlmSampleRequest request, Guid actorUserId, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Duyệt một hoặc nhiều dòng cùng lúc. Trạng thái lạ, danh sách mã trống hoặc quá 500 mã thì ném ArgumentException.</summary>
    Task<ReviewSlmSamplesResultDto> ReviewAsync(ReviewSlmSamplesRequest request, Guid actorUserId, CancellationToken ct = default);

    /// <summary>
    /// Nhập file xlsx. Dòng hợp lệ và chưa trùng được thêm ở trạng thái draft trong một lần lưu; dòng lỗi được báo kèm lý do và không thêm.
    /// File không đọc được thì ném InvalidDataException.
    /// </summary>
    Task<SlmImportResultDto> ImportAsync(Stream workbook, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Xuất các dòng approved thành nội dung JSONL, mỗi dòng là một SlmExportLine. split null là mọi tập.</summary>
    Task<string> ExportJsonlAsync(string? split, CancellationToken ct = default);
}

/// <summary>Đọc file xlsx dữ liệu huấn luyện thành các dòng thô, chưa kiểm tra nội dung.</summary>
public interface ISlmWorkbookReader
{
    /// <summary>
    /// Mỗi sheet có tên trùng một loại mẫu được đọc; sheet khác (README, summary) bị bỏ qua. Cột được nhận theo tên ở dòng tiêu đề
    /// (thứ tự cột có thể khác giữa các sheet). File hỏng hoặc không phải xlsx thì ném InvalidDataException.
    /// </summary>
    IReadOnlyList<SlmImportRow> Read(Stream workbook);
}
