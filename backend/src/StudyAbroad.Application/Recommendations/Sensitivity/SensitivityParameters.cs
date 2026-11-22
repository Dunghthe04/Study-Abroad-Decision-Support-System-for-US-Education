namespace StudyAbroad.Application.Recommendations.Sensitivity;

// [UC-85] Loại tham số quyết định cách đo:
// Grouping = tham số xếp nhóm (B7 đến B9): đo % cặp (hồ sơ, trường) đổi nhóm.
// FitWeight = trọng số điểm phù hợp (B10): không đổi nhóm, chỉ đổi thứ tự, nên đo % trường ra/vào top 4 mỗi khối.
public enum SensitivityKind { Grouping, FitWeight }

// Một tham số được phép nhích. Get đọc giá trị trong một bộ tham số; With trả bộ tham số MỚI với giá trị đã thay
// (record bất biến nên bộ gốc không đổi).
public sealed record SensitivityParameter(
    string Key, string Name, SensitivityKind Kind,
    Func<RecommendationTuning, double> Get,
    Func<RecommendationTuning, double, RecommendationTuning> With);

// Danh mục tham số UC-85. Key trùng mã ở danh mục UC-89 (AlgorithmParameters) khi có.
public static class SensitivityParameters
{
    public static IReadOnlyList<SensitivityParameter> All { get; } =
    [
        G("lower_cut", "Ngưỡng Khó vào / Vừa sức", t => t.Grouping.LowerCut, (g, v) => g with { LowerCut = v }),
        G("upper_cut", "Ngưỡng Vừa sức / An toàn", t => t.Grouping.UpperCut, (g, v) => g with { UpperCut = v }),
        G("always_reach_rate", "Tỉ lệ nhận luôn Khó vào", t => t.Grouping.AlwaysReachRate, (g, v) => g with { AlwaysReachRate = v }),
        G("safety_min_rate", "Tỉ lệ nhận tối thiểu để An toàn", t => t.Grouping.SafetyMinRate, (g, v) => g with { SafetyMinRate = v }),
        G("intl_ratio_cut", "Ngưỡng tỉ lệ du học sinh / chung", t => t.Grouping.IntlRatioCut, (g, v) => g with { IntlRatioCut = v }),
        G("low_submit_pct", "Mốc trường ít sinh viên nộp SAT", t => t.Grouping.LowSubmitPct, (g, v) => g with { LowSubmitPct = v }),
        G("low_submit_factor", "Hệ số giảm trọng số SAT khi ít nộp", t => t.Grouping.LowSubmitFactor, (g, v) => g with { LowSubmitFactor = v }),
        G("ec_step", "Hiệu chỉnh mỗi bậc ngoại khóa", t => t.Grouping.EcStep, (g, v) => g with { EcStep = v }),
        G("ec_cap", "Trần hiệu chỉnh ngoại khóa", t => t.Grouping.EcCap, (g, v) => g with { EcCap = v }),
        F("fit_position", "Trọng số vừa tầm", f => f.Position, (f, v) => f with { Position = v }),
        F("fit_budget", "Trọng số ngân sách", f => f.Budget, (f, v) => f with { Budget = v }),
        F("fit_aid", "Trọng số học bổng", f => f.Aid, (f, v) => f with { Aid = v }),
        F("fit_quality", "Trọng số chất lượng", f => f.Quality, (f, v) => f with { Quality = v }),
        F("fit_english", "Trọng số tiếng Anh", f => f.English, (f, v) => f with { English = v }),
    ];

    public static SensitivityParameter? Find(string key) =>
        All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal));

    private static SensitivityParameter G(
        string key, string name, Func<RecommendationTuning, double> get, Func<GroupingOptions, double, GroupingOptions> set) =>
        new(key, name, SensitivityKind.Grouping, get, (t, v) => t with { Grouping = set(t.Grouping, v) });

    // Trọng số fit: thay một trọng số rồi chia lại để tổng 5 trọng số vẫn bằng tổng ban đầu (BA: tổng luôn là 1).
    // Ví dụ ngân sách 0,25 nhích +10% thành 0,275: tổng thành 1,025, chia lại thì ngân sách ≈ 0,268, bốn trọng số kia giảm nhẹ.
    private static SensitivityParameter F(
        string key, string name, Func<FitWeights, double> get, Func<FitWeights, double, FitWeights> set) =>
        new(key, name, SensitivityKind.FitWeight, t => get(t.Fit), (t, v) =>
        {
            var before = Sum(t.Fit);
            var changed = set(t.Fit, v);
            var scale = before / Sum(changed);
            return t with
            {
                Fit = new FitWeights(changed.Position * scale, changed.Budget * scale, changed.Aid * scale,
                    changed.Quality * scale, changed.English * scale)
            };
        });

    private static double Sum(FitWeights f) => f.Position + f.Budget + f.Aid + f.Quality + f.English;
}
