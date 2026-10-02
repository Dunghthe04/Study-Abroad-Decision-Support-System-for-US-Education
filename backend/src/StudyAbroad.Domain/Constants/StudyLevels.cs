namespace StudyAbroad.Domain.Constants;

/// <summary>
/// Study levels supported by the platform. Keep in sync with advisor/app/core/study_levels.py
/// and frontend/src/lib/study-levels.ts.
/// </summary>
public static class StudyLevels
{
    public const string Secondary = "secondary";              // THCS/THPT
    public const string CommunityCollege = "community_college"; // Cao đẳng cộng đồng (2+2)
    public const string Undergraduate = "undergraduate";      // Đại học
    public const string Master = "master";                    // Thạc sĩ
    public const string Phd = "phd";                          // Tiến sĩ

    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { Secondary, CommunityCollege, Undergraduate, Master, Phd };

    public static bool IsValid(string? level) => level is not null && All.Contains(level);
}
