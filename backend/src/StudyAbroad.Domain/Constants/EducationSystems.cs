namespace StudyAbroad.Domain.Constants;

/// <summary>
/// Các hệ chương trình học tập hỗ trợ bởi hệ thống (Story #2: B1-01).
/// </summary>
public static class EducationSystems
{
    public const string Standard = "standard";         // Công lập thường
    public const string Specialized = "specialized";   // Trường Chuyên / Năng khiếu
    public const string DualDegree = "dual_degree";     // Song bằng (Việt Nam + Quốc tế)
    public const string International = "international"; // Quốc tế (IB, AP, A-Level, Cambridge)
    public const string Private = "private";           // Tư thục chất lượng cao
    public const string Other = "other";               // Khác

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Standard,
        Specialized,
        DualDegree,
        International,
        Private,
        Other
    };

    public static bool IsValid(string? system) => system is not null && All.Contains(system);
}
