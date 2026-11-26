using System.Text.Json.Nodes;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Dashboard.Roadmap;

public class RoadmapService(
    IProfileFlowRepository profileFlowRepository,
    ITargetSchoolService targetSchoolService,
    IDashboardRepository dashboardRepository,
    IRoadmapRepository roadmapRepository,
    IChatBenchmarkRepository benchmarkRepository,
    IStudyCenterRepository studyCenterRepository) : IRoadmapService
{
    public async Task<RoadmapDto?> GetRoadmapAsync(Guid studentId, CancellationToken ct = default)
    {
        var profile = await profileFlowRepository.GetProfileByUserIdAsync(studentId, ct);
        if (profile is null)
            return null;

        var level = string.IsNullOrWhiteSpace(profile.TargetLevel) ? StudyLevels.Undergraduate : profile.TargetLevel;
        var certifications = await profileFlowRepository.GetProfileCertificationsAsync(profile.Id, ct);

        // GetTargetSchoolsAsync đã bỏ trường phụ huynh mới gợi ý; ưu tiên Priority rồi trường thêm trước
        var summary = await targetSchoolService.GetTargetSchoolsAsync(studentId, ct: ct);
        var targets = summary.Items
            .OrderBy(t => t.Priority).ThenBy(t => t.CreatedAt)
            .Take(RoadmapBuilder.MaxSchools)
            .ToList();
        var ids = targets.Select(t => t.UniversityId).Distinct().ToList();

        var offerings = await roadmapRepository.GetOfferingsAsync(ids, level, ct);
        var costs = ids.Count > 0
            ? await dashboardRepository.GetUniversityCostsAsync(ids, level, ct)
            : new Dictionary<Guid, UniversityCostRow>();
        var scholarships = await roadmapRepository.GetScholarshipsAsync(ids, level, ct);

        RoadmapAverageMarks? average = null;
        if (targets.Count == 0)
            average = ToAverage(ChatBenchmarks.From(await benchmarkRepository.ListAsync(level, ct)));

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var input = new RoadmapInputs(profile, certifications.ToList(), targets, offerings, costs, scholarships, average);
        var roadmap = RoadmapBuilder.Build(input, today);

        // chỉ đọc danh sách trung tâm của Đức, lọc và xếp trong bộ nhớ
        var centers = await studyCenterRepository.ListReferableAsync(ct);
        return RoadmapCenters.Attach(roadmap, centers, profile.Province, level, studentId, today);
    }

    public async Task<SetProvinceResult> SetProvinceAsync(Guid studentId, string? province, CancellationToken ct = default)
    {
        string? canonical = null;
        if (!string.IsNullOrWhiteSpace(province))
        {
            canonical = VietnamProvinces.Resolve(province);
            if (canonical is null)
                return SetProvinceResult.InvalidProvince;
        }

        // không tạo hồ sơ mới chỉ để lưu tỉnh: phụ huynh không có hồ sơ riêng, học sinh chưa có hồ sơ thì chưa có lộ trình
        var profile = await profileFlowRepository.GetProfileByUserIdAsync(studentId, ct);
        if (profile is null)
            return SetProvinceResult.NoProfile;

        profile.Province = canonical;
        await profileFlowRepository.UpdateProfileAsync(profile, ct);
        return SetProvinceResult.Saved;
    }

    // Dùng lại đúng mốc chat đang dùng (ChatBenchmarks) để Tổng quan và chat khớp nhau ở chế độ average
    public static RoadmapAverageMarks? ToAverage(JsonObject? benchmarks)
    {
        if (benchmarks is null) return null;
        var gpa = benchmarks["gpa4"]?.GetValue<decimal>();
        var english = new Dictionary<string, decimal>();
        if (benchmarks["english"] is JsonObject eng)
            foreach (var (key, value) in eng)
                if (value is not null) english[key] = value.GetValue<decimal>();
        return new RoadmapAverageMarks(gpa, english);
    }
}
