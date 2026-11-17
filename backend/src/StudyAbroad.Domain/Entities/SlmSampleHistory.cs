using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Lịch sử sửa và duyệt của một mẫu SLM (UC-96).</summary>
public class SlmSampleHistory : BaseEntity
{
    public Guid SampleId { get; set; }

    public Guid? ActorUserId { get; set; }

    /// <summary>create | edit | submit | review | delete | capture.</summary>
    public string Action { get; set; } = string.Empty;

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public string? Note { get; set; }

    /// <summary>Tên các trường đã đổi, cách nhau bởi dấu phẩy.</summary>
    public string? ChangedFields { get; set; }
}
