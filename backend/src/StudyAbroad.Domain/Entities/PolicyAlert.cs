using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Cảnh báo thay đổi chính sách visa, học phí, tuyển sinh (FE-18: UC-56, UC-84).</summary>
public class PolicyAlert : BaseEntity
{
    public string AlertType { get; set; } = "visa";
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public string? SourceUrl { get; set; }
    /// <summary>info | warning | critical</summary>
    public string Severity { get; set; } = "info";
    public bool IsActive { get; set; } = true;
}
