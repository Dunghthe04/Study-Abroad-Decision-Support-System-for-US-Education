namespace StudyAbroad.Domain.Constants;

/// <summary>
/// Thang điểm học tập hỗ trợ bởi hệ thống (Story #2: B1-02).
/// </summary>
public static class GradeScales
{
    public const string Scale10 = "10";       // Thang 10 (0.0 - 10.0, phổ biến THPT Việt Nam)
    public const string Scale100 = "100";     // Thang 100 (0 - 100)
    public const string Scale4 = "4";         // Thang 4 (0.0 - 4.0, chuẩn Mỹ/Đại học)
    public const string ScaleLetter = "letter"; // Thang chữ (A+, A, A-, B+, B, B-, C+, C, C-, D, F)

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Scale10,
        Scale100,
        Scale4,
        ScaleLetter
    };

    public static bool IsValid(string? scale) => scale is not null && All.Contains(scale);
}
