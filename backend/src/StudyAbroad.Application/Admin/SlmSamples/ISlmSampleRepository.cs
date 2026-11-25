using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.SlmSamples;

/// <summary>Truy vấn và lưu mẫu huấn luyện SLM cùng các phiên bản bất biến.</summary>
public interface ISlmSampleRepository
{
    /// <summary>Một trang danh sách (mới nhất trước) kèm tên người tạo, và tổng số dòng thỏa bộ lọc (lọc Đang dùng hoặc Đã xóa qua filter.Deleted).</summary>
    Task<(IReadOnlyList<SlmSampleListRow> Rows, int Total)> PageAsync(
        SlmSampleFilter filter, int skip, int take, CancellationToken ct = default);

    /// <summary>Một dòng để đọc, không theo dõi thay đổi. includeDeleted = true đọc được cả mẫu đã xóa (chỉ đọc ở UC-90).</summary>
    Task<SlmTrainingSample?> FindAsync(Guid id, bool includeDeleted = false, CancellationToken ct = default);

    Task<IReadOnlyList<SlmSampleVersionRow>> VersionsAsync(Guid sampleId, CancellationToken ct = default);

    Task<SlmSampleVersion?> FindVersionAsync(Guid sampleId, int version, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);

    Task AddAsync(SlmTrainingSample sample, SlmSampleVersion initialVersion, CancellationToken ct = default);

    /// <summary>Thêm nhiều mẫu kèm phiên bản đầu tiên trong một lần lưu: hoặc thêm hết, hoặc không thêm gì.</summary>
    Task AddRangeAsync(
        IReadOnlyList<SlmTrainingSample> samples, IReadOnlyList<SlmSampleVersion> initialVersions, CancellationToken ct = default);

    /// <summary>
    /// Nạp mẫu theo id, gọi apply rồi lưu. apply trả (Changed, NewVersion): nếu Changed = true thì cập nhật UpdatedAt,
    /// nếu NewVersion khác null thì chèn thêm bản ghi phiên bản bất biến trong cùng lần lưu. Trả true nếu tìm thấy mẫu.
    /// </summary>
    Task<bool> UpdateAsync(
        Guid id, Func<SlmTrainingSample, (bool Changed, SlmSampleVersion? NewVersion)> apply,
        bool includeDeleted = false, CancellationToken ct = default);

    Task<IReadOnlyList<SlmSampleCountRow>> CountsAsync(CancellationToken ct = default);

    /// <summary>Khóa để nhận ra dòng trùng khi import: CHỈ lấy mẫu chưa xóa (UC-94).</summary>
    Task<IReadOnlyList<SlmSampleKeyRow>> ExistingKeysAsync(CancellationToken ct = default);

    /// <summary>Khóa của các mẫu chưa xóa cùng loại mẫu để kiểm tra trùng (UC-92, UC-93, UC-94).</summary>
    Task<IReadOnlyList<SlmSampleKeyRow>> LiveKeysByTypeAsync(string sampleType, CancellationToken ct = default);

    Task<IReadOnlyList<SlmSampleCreatorDto>> CreatorsAsync(CancellationToken ct = default);

    /// <summary>Danh sách giá trị nguồn khác nhau hiện có trên các mẫu chưa xóa (UC-89).</summary>
    Task<IReadOnlyList<string>> SourcesAsync(CancellationToken ct = default);

    /// <summary>Mọi mẫu chưa xóa, cũ trước (UC-96).</summary>
    Task<IReadOnlyList<SlmTrainingSample>> ExportableAsync(CancellationToken ct = default);

    Task MarkExportedAsync(IReadOnlyList<Guid> ids, DateTime exportedAt, CancellationToken ct = default);
}

public record SlmSampleKeyRow(
    Guid Id, string SampleType, string Question, string? PendingQuestion, string? Profile, string? Context, string Intents);
