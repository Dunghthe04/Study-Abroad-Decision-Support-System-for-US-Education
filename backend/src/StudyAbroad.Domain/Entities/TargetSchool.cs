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
}
