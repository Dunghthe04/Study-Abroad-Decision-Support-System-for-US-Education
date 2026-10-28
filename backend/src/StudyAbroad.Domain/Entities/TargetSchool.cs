using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Trường mục tiêu người dùng lưu lại (backlog #20).</summary>
public class TargetSchool : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid UniversityId { get; set; }
    /// <summary>reach | match | safety</summary>
    public string? Category { get; set; }
    /// <summary>considering | applying | submitted | admitted | rejected</summary>
    public string Status { get; set; } = "considering";
    public int Priority { get; set; }
    public string? Notes { get; set; }

    /// <summary>Vòng nộp: Early Action, Early Decision, Regular (UC-32).</summary>
    public string? ApplicationRound { get; set; }

    /// <summary>Hạn nộp hồ sơ.</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>Cache kết quả phân tích khoảng cách thiếu hụt đến trường mục tiêu (UC-81 - Q2).</summary>
    public string? GapAnalysisJson { get; set; }
}
