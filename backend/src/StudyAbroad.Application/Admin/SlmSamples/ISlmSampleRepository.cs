using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.SlmSamples;

/// <summary>Mọi truy vấn bỏ qua dòng đã xóa mềm, trừ ExistingKeysAsync và SameQuestionAsync(includeDeleted: true).</summary>
public interface ISlmSampleRepository
{
    /// <summary>Một trang danh sách (mới nhất trước) kèm tên người tạo, và tổng số dòng thỏa bộ lọc.</summary>
    Task<(IReadOnlyList<SlmSampleListRow> Rows, int Total)> PageAsync(
        SlmSampleFilter filter, int skip, int take, CancellationToken ct = default);

    /// <summary>Một dòng để đọc, không theo dõi thay đổi. Null nếu không có hoặc đã xóa.</summary>
    Task<SlmTrainingSample?> FindAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);

    Task AddAsync(SlmTrainingSample sample, CancellationToken ct = default);

    /// <summary>Thêm nhiều mẫu trong một lần lưu: hoặc thêm hết, hoặc không thêm gì.</summary>
    Task AddRangeAsync(IReadOnlyList<SlmTrainingSample> samples, CancellationToken ct = default);

    /// <summary>
    /// Gọi apply cho từng dòng còn tồn tại trong ids rồi lưu một lần. apply trả true nếu dòng đổi (khi đó UpdatedAt được ghi), false nếu không đổi.
    /// Trả số dòng tìm thấy.
    /// </summary>
    Task<int> UpdateManyAsync(
        IReadOnlyList<Guid> ids, Func<SlmTrainingSample, bool> apply, CancellationToken ct = default);

    Task<IReadOnlyList<SlmSampleCountRow>> CountsAsync(CancellationToken ct = default);

    /// <summary>Khóa để nhận ra dòng trùng khi import, gồm cả dòng đã xóa: loại mẫu, câu hỏi, câu hệ thống hỏi, hồ sơ, ngữ cảnh, intents.</summary>
    Task<IReadOnlyList<SlmSampleKeyRow>> ExistingKeysAsync(CancellationToken ct = default);

    /// <summary>Các dòng có câu hỏi giống hệt (không phân biệt hoa thường, đã bỏ khoảng trắng hai đầu).</summary>
    Task<IReadOnlyList<SlmTrainingSample>> SameQuestionAsync(string question, bool includeDeleted, CancellationToken ct = default);

    Task<IReadOnlyList<SlmSampleCreatorDto>> CreatorsAsync(CancellationToken ct = default);

    /// <summary>Các mẫu approved chưa xóa, cũ trước.</summary>
    Task<IReadOnlyList<SlmTrainingSample>> ApprovedAsync(CancellationToken ct = default);

    Task MarkExportedAsync(IReadOnlyList<Guid> ids, DateTime exportedAt, CancellationToken ct = default);
}

public record SlmSampleKeyRow(
    string SampleType, string Question, string? PendingQuestion, string? Profile, string? Context, string Intents);
