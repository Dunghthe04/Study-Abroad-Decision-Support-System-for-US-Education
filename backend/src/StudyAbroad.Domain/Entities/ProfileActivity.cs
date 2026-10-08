using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Ngoại khóa, kinh nghiệm hoặc giải thưởng trong hồ sơ (backlog #3).</summary>
public class ProfileActivity : BaseEntity
{
    public Guid StudentProfileId { get; set; }
    /// <summary>extracurricular | experience | award</summary>
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Organization { get; set; }
    /// <summary>vai trò, hoặc cấp giải</summary>
    public string? Role { get; set; }
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    /// <summary>[USAS-364] Mức độ ảnh hưởng: 1 (Trường/CLB) đến 5 (Quốc tế)</summary>
    public int? ImpactLevel { get; set; }
}
