using System.Globalization;
using System.Text;
using StudyAbroad.Application.Recommendations.Sensitivity;

namespace StudyAbroad.Application.Admin.Sensitivity;

// [UC-85] File CSV của một lần chạy: CHỈ số tổng hợp (BA), mỗi dòng là một mức nhích của một tham số.
// Không có tên người chạy hay dữ liệu cá nhân nào. Số viết kiểu 0.0329 (dấu chấm), có BOM UTF-8 để Excel đọc đúng tiếng Việt.
public static class SensitivityCsv
{
    public static readonly string[] Header =
    [
        "run_id", "created_at_utc", "algorithm_version", "profile_set", "profile_count", "school_count", "school_data_hash",
        "parameter_key", "parameter_name", "kind", "baseline", "step", "value_used", "clamped",
        "pairs", "changed_pairs", "group_change_rate", "moved_safer", "moved_harder",
        "top_slots", "changed_slots", "top_change_rate", "top_overlap", "ranked_schools", "avg_rank_shift",
        "classified_rate", "label"
    ];

    public static string FileName(SensitivityRunDetail run) =>
        $"uc85-sensitivity-{run.Summary.CreatedAt:yyyyMMdd-HHmmss}-{run.Summary.Id.ToString()[..8]}.csv";

    public static byte[] Build(SensitivityRunDetail run)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', Header)).Append("\r\n");
        var s = run.Summary;
        foreach (var p in run.Report.Parameters)
            foreach (var point in p.Points)
            {
                string[] cells =
                [
                    s.Id.ToString(), s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    Text(s.AlgorithmVersion), Text(s.ProfileSetVersion), N(s.ProfileCount), N(s.SchoolCount), Text(s.SchoolDataHash),
                    Text(p.Key), Text(p.Name), Text(KindCode(p.Kind)), N(p.Baseline), N(point.Step), N(point.Value),
                    point.Clamped ? "true" : "false",
                    N(point.Pairs), N(point.ChangedPairs), N(point.GroupChangeRate), N(point.MovedSafer), N(point.MovedHarder),
                    N(point.Slots), N(point.ChangedSlots), N(point.TopChangeRate), N(point.TopOverlap),
                    point.RankedSchools is int ranked ? N(ranked) : "", point.AvgRankShift is double shift ? N(shift) : "",
                    p.Rate is double r ? N(r) : "", Text(p.Label ?? "")
                ];
                sb.Append(string.Join(',', cells)).Append("\r\n");
            }
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string KindCode(SensitivityKind kind) => kind == SensitivityKind.Grouping ? "grouping" : "fit_weight";

    // Số: tối đa 6 chữ số thập phân, dấu chấm, không có đuôi lẻ kiểu 0.8250000000000001
    private static string N(double v) => Math.Round(v, 6).ToString("0.######", CultureInfo.InvariantCulture);
    private static string N(int v) => v.ToString(CultureInfo.InvariantCulture);

    // Chữ: bọc ngoặc kép khi có dấu phẩy, ngoặc kép hoặc xuống dòng. Ô bắt đầu bằng = + - @ thêm dấu ' phía trước
    // để Excel không coi là công thức (chặn chèn công thức qua file CSV).
    private static string Text(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
