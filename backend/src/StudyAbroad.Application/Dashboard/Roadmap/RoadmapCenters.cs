using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Dashboard.Roadmap;

/// <summary>
/// Gợi ý tối đa 3 trung tâm cho mỗi nhiệm vụ chưa xong (kế hoạch mục 6.4). Gợi ý theo dịch vụ và khu vực, không phải quảng cáo.
/// Nguồn là ListReferableAsync của Đức (đang hoạt động + đã xác minh), loại thêm trung tâm bị tạm giữ pháp lý.
/// Xếp: cùng tỉnh -> điểm (chưa có đánh giá = 0) -> ngẫu nhiên ổn định theo học sinh + ngày.
/// Không đủ trung tâm cùng tỉnh thì bù trung tâm toàn quốc; SameProvince = false để giao diện hiện câu báo.
/// </summary>
public static class RoadmapCenters
{
    public const int MaxPerTask = 3;

    public static RoadmapDto Attach(RoadmapDto roadmap, IEnumerable<StudyCenter> centers, string? province,
        string? studyLevel, Guid studentId, DateOnly today)
    {
        var pool = centers.Where(c => c.IsActive && !c.IsLegalHold).ToList();
        if (pool.Count == 0) return roadmap;

        var tasks = roadmap.Tasks
            .Select(t => t.Status == "done" || t.Services.Count == 0
                ? t
                : t with { Centers = Suggest(pool, t.Services, province, studyLevel, studentId, today) })
            .ToList();
        return roadmap with { Tasks = tasks };
    }

    public static IReadOnlyList<RoadmapCenterDto> Suggest(IReadOnlyList<StudyCenter> pool, IReadOnlyList<string> services,
        string? province, string? studyLevel, Guid studentId, DateOnly today)
    {
        var hasProvince = !string.IsNullOrWhiteSpace(province);
        return pool
            .Where(c => c.Services.Any(services.Contains)
                        // trung tâm luyện thi thường không ghi bậc học: không ghi thì coi như nhận mọi bậc
                        && (string.IsNullOrWhiteSpace(studyLevel) || c.StudyLevels.Count == 0 || c.StudyLevels.Contains(studyLevel)))
            .Select(c => (Center: c, Same: hasProvince && VietnamProvinces.SameProvince(c.City, province)))
            .OrderByDescending(x => x.Same)
            .ThenByDescending(x => Score(x.Center))
            .ThenBy(x => TieBreak(studentId, today, x.Center.Id), StringComparer.Ordinal)
            .Take(MaxPerTask)
            .Select(x => ToDto(x.Center, x.Same))
            .ToList();
    }

    // Trung tâm chưa có đánh giá thật thì điểm 0, không dùng mặc định 5.0 (quyết định 2)
    public static decimal Score(StudyCenter c) => c.ReviewCount > 0 ? c.AverageRating : 0m;

    // Cùng học sinh, cùng ngày thì thứ tự giống nhau, tải lại trang không nhảy; sang ngày khác thì đổi
    private static string TieBreak(Guid studentId, DateOnly today, Guid centerId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{studentId:N}:{today:yyyyMMdd}:{centerId:N}")));

    private static RoadmapCenterDto ToDto(StudyCenter c, bool sameProvince) =>
        new(c.Id, c.Name, c.City, Score(c), c.ReviewCount, true, FeeText(c), sameProvince);

    // Học phí chỉ hiện để tự xem, không so với ngân sách du học (quyết định 3)
    public static string? FeeText(StudyCenter c)
    {
        if (!string.IsNullOrWhiteSpace(c.FeeDescription)) return c.FeeDescription.Trim();
        var vi = CultureInfo.GetCultureInfo("vi-VN");
        return (c.FeeMin, c.FeeMax) switch
        {
            ({ } min, { } max) when min == max => $"{min.ToString("N0", vi)} đ",
            ({ } min, { } max) => $"{min.ToString("N0", vi)} – {max.ToString("N0", vi)} đ",
            ({ } min, _) => $"từ {min.ToString("N0", vi)} đ",
            (null, { } max) => $"đến {max.ToString("N0", vi)} đ",
            _ => null,
        };
    }
}
