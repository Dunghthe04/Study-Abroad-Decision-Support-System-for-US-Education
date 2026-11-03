using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

/// <summary>
/// Luật chọn trung tâm để chat gợi ý (B6, skill_chuyen_trung_tam). Code chọn, AI không chọn.
/// Chỉ lấy trung tâm đang hoạt động, đã xác minh, có đúng dịch vụ và nhận bậc học của học sinh;
/// cùng thành phố với học sinh xếp trước, còn lại sắp theo tên (không xếp hạng chất lượng); tối đa 3.
/// </summary>
public static class CenterReferral
{
    public const int MaxCenters = 3;
    public const string Verified = "verified";

    public static IReadOnlyList<StudyCenter> Pick(
        IEnumerable<StudyCenter> centers, string service, string? studyLevel, string? city) =>
        centers
            .Where(c => IsReferable(c)
                        && c.Services.Contains(service)
                        && (string.IsNullOrWhiteSpace(studyLevel) || c.StudyLevels.Contains(studyLevel)))
            .OrderByDescending(c => SameCity(c.City, city))
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .Take(MaxCenters)
            .ToList();

    /// <summary>Các thành phố đang có trung tâm gợi ý được: AI chọn thành phố của học sinh trong danh sách này.</summary>
    public static IReadOnlyList<string> Cities(IEnumerable<StudyCenter> centers) =>
        centers
            .Where(c => IsReferable(c) && !string.IsNullOrWhiteSpace(c.City))
            .Select(c => c.City!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

    private static bool IsReferable(StudyCenter c) => c.IsActive && c.VerificationStatus == Verified;

    private static bool SameCity(string? centerCity, string? studentCity) =>
        !string.IsNullOrWhiteSpace(studentCity)
        && string.Equals(centerCity, studentCity, StringComparison.OrdinalIgnoreCase);
}
