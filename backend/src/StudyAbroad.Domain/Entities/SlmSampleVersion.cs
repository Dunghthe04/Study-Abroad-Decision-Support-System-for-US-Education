using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Phiên bản bất biến của một mẫu huấn luyện SLM (UC-90): mỗi lần thêm, sửa, nhập hoặc khôi phục tạo một dòng mới.</summary>
public class SlmSampleVersion : BaseEntity
{
    public Guid SampleId { get; set; }

    public int Version { get; set; }

    public Guid? ActorUserId { get; set; }

    /// <summary>create | edit | restore | import.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Số phiên bản nguồn khi Action = restore; null với thao tác khác.</summary>
    public int? RestoredFromVersion { get; set; }

    public string SampleType { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;

    public string? Profile { get; set; }

    public string? PendingQuestion { get; set; }

    public string Intents { get; set; } = "[]";

    public string? Context { get; set; }

    public string Split { get; set; } = "train";

    public string? SourceReview { get; set; }

    public string Source { get; set; } = "manual";
}
