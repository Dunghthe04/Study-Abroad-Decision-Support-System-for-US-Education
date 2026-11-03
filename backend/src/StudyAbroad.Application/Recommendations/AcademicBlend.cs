namespace StudyAbroad.Application.Recommendations;

// Gộp vị trí theo GPA và theo điểm thi thành p, rồi hiệu chỉnh theo ngoại khóa thành p'.
public static class AcademicBlend
{
    // Bậc ngoại khóa trung bình: không hiệu chỉnh.
    public const int NeutralEcTier = 3;

    // p = trung bình có trọng số của các chỉ số đang có. Trọng số theo mức trường coi trọng (CDS C7).
    // Thiếu một chỉ số thì p bằng chỉ số còn lại; thiếu cả hai thì null.
    public static double? Combine(
        double? pg, double? ps, string? gpaImportance, string? testImportance,
        decimal? satSubmitPct = null, GroupingOptions? options = null)
    {
        var o = options ?? new GroupingOptions();
        var wg = Weight(gpaImportance);
        var ws = Weight(testImportance);
        // Ít sinh viên nộp SAT thì P25, P75 chỉ đại diện nhóm có nộp (thường điểm cao): giảm trọng số.
        if (satSubmitPct is decimal share && (double)share < o.LowSubmitPct) ws *= o.LowSubmitFactor;

        double sum = 0, weights = 0;
        if (pg is double g) { sum += wg * g; weights += wg; }
        if (ps is double s) { sum += ws * s; weights += ws; }
        return weights > 0 ? sum / weights : null;
    }

    // p' = p + hiệu chỉnh ngoại khóa, kẹp trong [0, 1]. Bậc 3 là trung bình nên không đổi.
    public static double AdjustForActivities(
        double p, int ecTier, string? ecImportance, GroupingOptions? options = null)
    {
        var o = options ?? new GroupingOptions();
        var shift = o.EcStep * (ecTier - NeutralEcTier) * EcWeight(ecImportance);
        return Math.Clamp(p + Math.Clamp(shift, -o.EcCap, o.EcCap), 0, 1);
    }

    // Mức trường coi trọng một tiêu chí (CDS C7); không rõ thì lấy mức giữa.
    public static double Weight(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        "very_important" => 1.0,
        "important" => 0.75,
        "considered" => 0.5,
        "not_considered" => 0.25,
        _ => 0.5
    };

    // Mức trường coi trọng ngoại khóa; không rõ hoặc không xét thì không hiệu chỉnh.
    public static double EcWeight(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        "very_important" => 1.0,
        "important" => 0.7,
        "considered" => 0.4,
        _ => 0.0
    };
}
