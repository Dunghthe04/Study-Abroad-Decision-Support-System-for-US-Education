using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Câu hỏi chuẩn trong bộ đánh giá AI (backlog #30).</summary>
public class EvalCase : BaseEntity
{
    public string Question { get; set; } = string.Empty;
    public string? ExpectedAnswer { get; set; }
    public string? StudyLevel { get; set; }
    public string? Category { get; set; }
    public string? ExpectedSourcesJson { get; set; }
    public bool IsActive { get; set; } = true;
}
