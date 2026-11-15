using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.SlmSamples;

public interface ISlmSampleRepository
{
    /// <summary>Một trang danh sách (mới nhất trước) và tổng số dòng thỏa bộ lọc.</summary>
    Task<(IReadOnlyList<SlmTrainingSample> Rows, int Total)> PageAsync(
        SlmSampleFilter filter, int skip, int take, CancellationToken ct = default);

    /// <summary>Một dòng để đọc, không theo dõi thay đổi. Null nếu không có.</summary>
    Task<SlmTrainingSample?> FindAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(SlmTrainingSample sample, CancellationToken ct = default);

    /// <summary>Thêm nhiều dòng trong một lần lưu: hoặc thêm hết, hoặc không thêm gì.</summary>
    Task AddRangeAsync(IReadOnlyList<SlmTrainingSample> samples, CancellationToken ct = default);

    /// <summary>Lưu thay đổi của một dòng đã có. Trả false nếu dòng không còn tồn tại.</summary>
    Task<bool> UpdateAsync(Guid id, Action<SlmTrainingSample> apply, CancellationToken ct = default);

    /// <summary>Xóa một dòng. Trả false nếu không có.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Đổi trạng thái duyệt cho các dòng có mã trong danh sách, ghi người duyệt và thời điểm duyệt.
    /// Trả số dòng thực sự được đổi (mã không tồn tại thì bỏ qua).
    /// </summary>
    Task<int> ReviewAsync(
        IReadOnlyList<Guid> ids, string status, string? note, Guid reviewerId, DateTime reviewedAt, CancellationToken ct = default);

    Task<IReadOnlyList<SlmSampleCountRow>> CountsAsync(CancellationToken ct = default);

    /// <summary>Khóa để nhận ra dòng trùng khi import: loại mẫu, câu hỏi, câu hệ thống hỏi, hồ sơ, ngữ cảnh, intents.</summary>
    Task<IReadOnlyList<SlmSampleKeyRow>> ExistingKeysAsync(CancellationToken ct = default);

    /// <summary>Các dòng đã duyệt (approved) để xuất JSONL. split null là mọi tập.</summary>
    Task<IReadOnlyList<SlmTrainingSample>> ApprovedAsync(string? split, CancellationToken ct = default);
}

public record SlmSampleKeyRow(
    string SampleType, string Question, string? PendingQuestion, string? Profile, string? Context, string Intents);
