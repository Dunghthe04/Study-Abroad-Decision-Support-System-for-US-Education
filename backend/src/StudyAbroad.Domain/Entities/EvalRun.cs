using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Một lần chạy bộ đánh giá AI; kết quả từng câu lưu trong ResultsJson (backlog #30).</summary>
public class EvalRun : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ModelVersion { get; set; }
    public Guid? SkillId { get; set; }
    public int TotalCases { get; set; }
    public int PassedCases { get; set; }
    /// <summary>mỗi câu: caseId, passed, answer, latencyMs</summary>
    public string ResultsJson { get; set; } = "[]";
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? Notes { get; set; }
}
