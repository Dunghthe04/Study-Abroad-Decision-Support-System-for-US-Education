using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Kết quả AI phân tích hồ sơ và chiến lược cải thiện (backlog #4, #7, #15).</summary>
public class AnalysisResult : BaseEntity
{
    public Guid StudentProfileId { get; set; }
    /// <summary>gpa | strengths_weaknesses | strategy</summary>
    public string Kind { get; set; } = string.Empty;
    public string ResultJson { get; set; } = "{}";
    public string? ModelVersion { get; set; }
}
