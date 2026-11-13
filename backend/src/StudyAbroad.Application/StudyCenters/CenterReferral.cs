using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

/// <summary>
/// Luật chọn trung tâm để chat gợi ý (B6, skill_chuyen_trung_tam). Code chọn, AI không chọn.
/// Chỉ lấy trung tâm đang hoạt động, đã xác minh, có đúng dịch vụ và nhận bậc học của học sinh;
/// học sinh nêu thành phố thì chỉ lấy trung tâm ở thành phố đó (có bao nhiêu hiện bấy nhiêu), sắp theo tên (không xếp hạng chất lượng); trả hết trung tâm khớp, tối đa 20.
/// </summary>
/// <summary>Một thành phố và số trung tâm gợi ý được ở đó cho một dịch vụ.</summary>
public record CityCount(string City, int Count);

public static class CenterReferral
{
    public const int MaxCenters = 20;  // đủ cho một thành phố; chat hiện từng trang (3 trung tâm đầu, còn lại bấm Xem thêm)
    public const string Verified = "verified";

    // Học sinh nêu thành phố: chỉ lấy trung tâm ở đúng thành phố đó, có bao nhiêu hiện bấy nhiêu (không lấp cho đủ 3 bằng nơi khác).
    // Thành phố không có trung tâm nào cho dịch vụ này thì trả rỗng; chat dùng CityCounts để cho học sinh chọn nơi đang có.
    // Không nêu thành phố: sắp theo tên (không xếp hạng chất lượng). Trả hết trung tâm khớp, tối đa MaxCenters.
    public static IReadOnlyList<StudyCenter> Pick(
        IEnumerable<StudyCenter> centers, string service, string? studyLevel, string? city)
    {
        var matching = Eligible(centers, service, studyLevel);
        if (!string.IsNullOrWhiteSpace(city))
            matching = matching.Where(c => SameCity(c.City, city));
        return matching.OrderBy(c => c.Name, StringComparer.Ordinal).Take(MaxCenters).ToList();
    }

    /// <summary>Thành phố có trung tâm đúng dịch vụ (và bậc học), kèm số trung tâm; nhiều trung tâm xếp trước, bằng nhau thì theo tên thành phố.</summary>
    public static IReadOnlyList<CityCount> CityCounts(IEnumerable<StudyCenter> centers, string service, string? studyLevel) =>
        Eligible(centers, service, studyLevel)
            .Where(c => !string.IsNullOrWhiteSpace(c.City))
            .GroupBy(c => c.City!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CityCount(g.First().City!, g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.City, StringComparer.Ordinal)
            .ToList();

    private static IEnumerable<StudyCenter> Eligible(IEnumerable<StudyCenter> centers, string service, string? studyLevel) =>
        centers.Where(c => IsReferable(c)
                           && c.Services.Contains(service)
                           && (string.IsNullOrWhiteSpace(studyLevel) || c.StudyLevels.Contains(studyLevel)));

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
