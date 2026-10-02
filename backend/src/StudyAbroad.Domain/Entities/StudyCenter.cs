using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// A study-abroad consulting center listed on the platform (data comes from the team's market survey).
/// </summary>
public class StudyCenter : BaseEntity
{
    /// <summary>Survey code, e.g. CTY_012.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }

    /// <summary>Study levels the center advises on. Values from <see cref="Constants.StudyLevels"/>.</summary>
    public List<string> StudyLevels { get; set; } = [];

    /// <summary>Service keys from the survey catalog, e.g. "CHON_TRUONG", "LUYEN_PHONG_VAN".</summary>
    public List<string> Services { get; set; } = [];

    public DateOnly? SurveyedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
