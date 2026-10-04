using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Kiến thức đã kiểm chứng cho AI: mỗi dòng Đã kiểm chứng ở sheet 05 là một bản ghi, script nạp sẽ embed sang advisor.documents (backlog #11, #12).</summary>
public class KnowledgeDocument : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    /// <summary>visa | ho_so | chi_phi | hoc_bong ...</summary>
    public string DocType { get; set; } = string.Empty;
    public string StudyLevel { get; set; } = "general";
    public string Content { get; set; } = string.Empty;
    public DateOnly? RetrievedAt { get; set; }
    /// <summary>draft | verified | rejected</summary>
    public string Status { get; set; } = "draft";
    public Guid? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAt { get; set; }
    /// <summary>lần nạp gần nhất vào advisor.documents</summary>
    public DateTime? IngestedAt { get; set; }
}
