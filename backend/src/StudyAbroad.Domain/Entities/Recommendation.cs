using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Một lần AI gợi ý danh sách trường theo 3 tiêu chí (backlog #6, #8).</summary>
public class Recommendation : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? StudentProfileId { get; set; }
    public string StudyLevel { get; set; } = string.Empty;
    /// <summary>3 tiêu chí đầu vào: ngành, bang, ngân sách</summary>
    public string CriteriaJson { get; set; } = "{}";
    /// <summary>danh sách trường: universityId, rank, category (reach/match/safety), score, reason</summary>
    public string ItemsJson { get; set; } = "[]";
    public string? AlgorithmVersion { get; set; }
}
