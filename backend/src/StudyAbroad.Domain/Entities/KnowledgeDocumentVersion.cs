using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Phiên bản bất biến của một mục kho tri thức (UC-113): mỗi lần thêm, sửa, nhập cập nhật hoặc khôi phục tạo một dòng mới.</summary>
public class KnowledgeDocumentVersion : BaseEntity
{
    public Guid DocumentId { get; set; }

    public int Version { get; set; }

    public Guid? ActorUserId { get; set; }

    /// <summary>create | edit | restore | import.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Số phiên bản nguồn khi Action = restore; null với thao tác khác.</summary>
    public int? RestoredFromVersion { get; set; }

    public string DocType { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    public string? Source { get; set; }

    public string? SourceUrl { get; set; }

    public DateOnly RetrievedAt { get; set; }

    public string? TopicCode { get; set; }

    public string? Note { get; set; }
}
