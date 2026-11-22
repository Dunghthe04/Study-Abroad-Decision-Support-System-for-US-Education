using StudyAbroad.Application.Recommendations.Sensitivity;

namespace StudyAbroad.Application.Admin.Sensitivity;

// [UC-85] Lý do hai lần chạy không cùng điều kiện. Có cảnh báo thì con số vẫn hiện, nhưng chênh lệch có thể không do tham số.
public static class SensitivityComparisonWarnings
{
    public const string AlgorithmVersionDiffers = "algorithm_version_differs";   // thuật toán đã đổi giữa hai lần
    public const string ProfileSetDiffers = "profile_set_differs";               // khác bộ hồ sơ giả lập
    public const string SchoolDataDiffers = "school_data_differs";               // dữ liệu trường đã đổi
    public const string ThresholdsDiffer = "thresholds_differ";                  // khác ngưỡng nhãn: nhãn không so được, con số vẫn so được
}

// So một mức nhích giữa hai lần chạy, theo đúng cách đo của loại tham số (% đổi nhóm hoặc % đổi top 4).
// Rate null khi lần chạy đó không có mức này.
public sealed record SensitivityStepComparison(double Step, double? BaseRate, double? OtherRate)
{
    public double? Delta => BaseRate is double a && OtherRate is double b ? b - a : null;
}

// So một tham số. Baseline: giá trị gốc của tham số trong mỗi lần chạy (khác nhau nghĩa là nhóm đã đổi tham số đó giữa hai lần).
// Các cột Base* / Other* null khi tham số không được chọn ở lần chạy đó.
public sealed record SensitivityParameterComparison(
    string Key, string Name, SensitivityKind Kind,
    double? BaseBaseline, double? OtherBaseline,
    double? BaseRate, double? OtherRate,
    string? BaseLabel, string? OtherLabel,
    IReadOnlyList<SensitivityStepComparison> Steps)
{
    public double? Delta => BaseRate is double a && OtherRate is double b ? b - a : null;
    public bool BaselineChanged => BaseBaseline is double a && OtherBaseline is double b && Math.Abs(a - b) > 1e-12;
}

// Kết quả so sánh. Base là lần chạy TRƯỚC, Other là lần SAU (đổi chỗ nếu gọi ngược) để Delta luôn là "sau trừ trước".
// Other null khi không có lần chạy trước để so (AF-5): danh sách tham số rỗng.
public sealed record SensitivityComparison(
    SensitivityRunListRow Base, SensitivityRunListRow? Other,
    IReadOnlyList<string> Warnings, IReadOnlyList<SensitivityParameterComparison> Parameters)
{
    public bool HasEarlierRun => Other is not null;
}

public static class SensitivityComparer
{
    // Chỉ có một lần chạy (chưa có lần nào trước nó): trả so sánh rỗng, không phải lỗi
    public static SensitivityComparison Alone(SensitivityRunDetail run) => new(run.Summary, null, [], []);

    public static SensitivityComparison Compare(SensitivityRunDetail first, SensitivityRunDetail second)
    {
        // Luôn so "trước → sau" theo thời điểm chạy
        var (older, newer) = first.Summary.CreatedAt <= second.Summary.CreatedAt ? (first, second) : (second, first);

        var warnings = new List<string>();
        if (older.Report.AlgorithmVersion != newer.Report.AlgorithmVersion)
            warnings.Add(SensitivityComparisonWarnings.AlgorithmVersionDiffers);
        if (older.Report.ProfileSetVersion != newer.Report.ProfileSetVersion || older.Report.ProfileSetHash != newer.Report.ProfileSetHash)
            warnings.Add(SensitivityComparisonWarnings.ProfileSetDiffers);
        if (older.Report.SchoolDataHash != newer.Report.SchoolDataHash)
            warnings.Add(SensitivityComparisonWarnings.SchoolDataDiffers);
        if (!Same(older.Request.StableBelow, newer.Request.StableBelow) || !Same(older.Request.WatchUpTo, newer.Request.WatchUpTo))
            warnings.Add(SensitivityComparisonWarnings.ThresholdsDiffer);

        var before = older.Report.Parameters.ToDictionary(p => p.Key, StringComparer.Ordinal);
        var after = newer.Report.Parameters.ToDictionary(p => p.Key, StringComparer.Ordinal);
        // Thứ tự theo danh mục tham số để bảng so sánh luôn cùng thứ tự
        var keys = SensitivityParameters.All.Select(p => p.Key).Where(k => before.ContainsKey(k) || after.ContainsKey(k)).ToList();

        var rows = keys.Select(key =>
        {
            before.TryGetValue(key, out var a);
            after.TryGetValue(key, out var b);
            var any = (a ?? b)!;
            var steps = (a?.Points ?? []).Select(p => p.Step).Concat((b?.Points ?? []).Select(p => p.Step))
                .Distinct().OrderBy(Math.Abs).ThenBy(s => s)
                .Select(step => new SensitivityStepComparison(step, RateAt(a, step), RateAt(b, step)))
                .ToList();
            return new SensitivityParameterComparison(
                key, any.Name, any.Kind, a?.Baseline, b?.Baseline, a?.Rate, b?.Rate, a?.Label, b?.Label, steps);
        }).ToList();

        return new SensitivityComparison(older.Summary, newer.Summary, warnings, rows);
    }

    private static double? RateAt(SensitivityParameterResult? result, double step)
    {
        var point = result?.Points.FirstOrDefault(p => Same(p.Step, step));
        if (point is null) return null;
        return result!.Kind == SensitivityKind.Grouping ? point.GroupChangeRate : point.TopChangeRate;
    }

    private static bool Same(double a, double b) => Math.Abs(a - b) < 1e-9;
}
