using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Lịch sử sửa và duyệt của một mục kho tri thức (cùng dạng với SlmSampleHistory).</summary>
public class KnowledgeBaseEntryHistory : BaseEntity
{
    public Guid EntryId { get; set; }

    public Guid? ActorUserId { get; set; }

    /// <summary>create | edit | submit | review | delete | restore | capture | migrate.</summary>
    public string Action { get; set; } = string.Empty;

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public string? Note { get; set; }

    /// <summary>Tên các trường đã đổi, cách nhau bởi dấu phẩy.</summary>
    public string? ChangedFields { get; set; }
}