using System.Text.Json.Nodes;
using StudyAbroad.Application.Advisor;

namespace StudyAbroad.Application.Recommendations;

/// <summary>Trạng thái của một trường trong bảng so sánh theo độ phù hợp (UC-154).</summary>
public static class FitCompareStatus
{
    public const string InList = "in_list";       // trong ngân sách, xếp được nhóm
    public const string Aid = "aid";              // vượt ngân sách nhưng đi được nếu có học bổng lớn (UC-22 4a)
    public const string NoData = "no_data";       // chưa đủ dữ liệu để xếp nhóm (UC-22 5a)
    public const string Excluded = "excluded";    // bị loại bởi điều kiện cứng (UC-22 bước 3, 4)
}

/// <summary>
/// [UC-154] Một trường trong bảng so sánh. Group: R | M | S | NA (null khi bị loại). ExcludeReason: MAJOR | STATE | CONTROL | ENGLISH | ENGLISH_NO_CERT | BUDGET.
/// Total và Parts theo thang 0 đến 1 như UC-24 (Parts đủ năm thành phần theo thứ tự đặc tả, kèm trọng số đã dùng). Why: mã lý do như thẻ giải thích.
/// </summary>
public record FitCompareSchoolDto(
    string Code,
    string Name,
    string? State,
    string Status,
    string? Group,
    string? ExcludeReason,
    double? Total,
    IReadOnlyList<FitPartDto> Parts,
    IReadOnlyList<string> Why);

/// <summary>Kết quả so sánh 2 đến 4 trường theo công thức gợi ý. PriorityAdjusted: trọng số đã đổi theo nút ưu tiên của lần gợi ý gần nhất (UC-25).</summary>
public record FitCompareResult(IReadOnlyList<FitCompareSchoolDto> Schools, bool PriorityAdjusted);

public partial class RecommendationService
{
    /// <summary>
    /// [UC-154] "BU hay NYU hợp với em hơn": tính nhóm và điểm phù hợp của từng trường được nêu bằng ĐÚNG công thức và tham số của UC-22
    /// (lọc cứng, xếp nhóm, giới hạn theo độ chọn lọc, fit score), trên hồ sơ thật. Trọng số theo nút ưu tiên của lần gợi ý gần nhất.
    /// Không lưu, không gọi advisor; trường nào cũng được hỏi, kể cả trường không nằm trong danh sách gợi ý (khi đó cho biết bị loại vì sao).
    /// Hồ sơ thiếu dữ liệu như UC-22 E1 hoặc không có trường trong dữ liệu thì ném InvalidOperationException.
    /// </summary>
    public async Task<FitCompareResult> CompareFitAsync(Guid userId, IReadOnlyList<string> codes, CancellationToken ct)
    {
        var profile = await LoadScoredProfileAsync(userId, ct)
            ?? throw new InvalidOperationException("Chưa có hồ sơ.");
        if (string.IsNullOrWhiteSpace(profile.TargetLevel))
            throw new InvalidOperationException("Hồ sơ chưa có bậc học muốn theo.");
        EnsureRunnable(profile);

        var schools = await recommendations.ListSchoolsAsync(profile.TargetLevel, ct);
        var latest = await recommendations.GetLatestAsync(userId, ct);
        var pref = latest is null ? null : ReadPref(JsonNode.Parse(latest.CriteriaJson)?["pref"]);
        var run = Evaluate(profile, pref, schools, EcTierOf(profile));

        var baseline = RecommendationTuning.Current.Fit;
        var weights = SchoolRanker.WeightsFromPriorities(NormalizePref(pref), baseline);

        var result = new List<FitCompareSchoolDto>();
        foreach (var code in codes)
        {
            var school = schools.FirstOrDefault(s => string.Equals(s.University.Code, code, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Chưa có trường {code} trong dữ liệu.");
            var university = school.University;
            var scored = run.Scored.FirstOrDefault(s => string.Equals(s.School.University.Code, university.Code, StringComparison.OrdinalIgnoreCase));
            if (scored is null)
            {
                // Bị loại ở bước lọc cứng: nói rõ lý do, không có nhóm và điểm
                var filter = run.Filters.First(f => string.Equals(f.School.Code, university.Code, StringComparison.OrdinalIgnoreCase));
                var reason = filter.ExcludeReason is "BUDGET" or "BUDGET_NO_AID" ? "BUDGET" : filter.ExcludeReason;
                result.Add(new FitCompareSchoolDto(
                    university.Code, university.Name, university.State, FitCompareStatus.Excluded, null, reason, null, [], []));
                continue;
            }

            var status = scored.Filter.Outcome == FilterOutcome.AidBlock ? FitCompareStatus.Aid
                : scored.Grade.Group == SchoolGroup.NoData ? FitCompareStatus.NoData
                : FitCompareStatus.InList;
            var fit = scored.Fit;
            result.Add(new FitCompareSchoolDto(
                university.Code, university.Name, university.State, status, GroupCode(scored.Grade.Group), null, fit?.Value,
                [
                    new FitPartDto("position", fit?.Position, weights.Position),
                    new FitPartDto("budget", fit?.Budget, weights.Budget),
                    new FitPartDto("quality", fit?.Quality, weights.Quality),
                    new FitPartDto("aid", fit?.Aid, weights.Aid),
                    new FitPartDto("english", fit?.English, weights.English),
                ],
                WhyCodes(scored)));
        }
        return new FitCompareResult(result, weights != baseline);
    }
}
