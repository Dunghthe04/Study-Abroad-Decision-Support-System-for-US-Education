using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Trường ở Mỹ: đại học, cao đẳng cộng đồng hoặc THPT (backlog #5, #13, #14).</summary>
public class University : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    /// <summary>mã bang, ví dụ CA</summary>
    public string? State { get; set; }
    public string? Website { get; set; }
    /// <summary>public | private</summary>
    public string? Control { get; set; }
    public decimal? AcceptanceRate { get; set; }
    public int? InternationalStudentCount { get; set; }
    public bool IsActive { get; set; } = true;
}
