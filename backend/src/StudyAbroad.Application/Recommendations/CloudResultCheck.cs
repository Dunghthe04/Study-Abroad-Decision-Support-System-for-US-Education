using StudyAbroad.Application.Advisor;

namespace StudyAbroad.Application.Recommendations;

// Kiểm tra khuôn cloud trả về. Sai thì ném lỗi, không sửa số.
public static class CloudResultCheck
{
    private static readonly HashSet<string> ScoreGroups = new(StringComparer.Ordinal) { "R", "M", "S", "NA", "O" };
    private static readonly HashSet<string> FinalGroups = new(StringComparer.Ordinal) { "R", "RF", "M", "S", "NA", "O" };
    private static readonly HashSet<string> WhyCodes = new(StringComparer.Ordinal)
    {
        "GPA_HIGH", "GPA_MID", "GPA_LOW",
        "SAT_ABOVE", "SAT_IN_RANGE", "SAT_BELOW",
        "BUDGET_OK", "NEED_AID",
        "INTL_LOW", "EC_PLUS", "EC_MINUS"
    };

    public static void EnsureTier(int tier)
    {
        if (tier is < 1 or > 5)
            throw new InvalidOperationException($"Bậc ngoại khóa {tier} nằm ngoài 1–5.");
    }

    public static void EnsureScore(IEnumerable<string> sentCodes, ScoreResponseDto score)
    {
        var sent = new HashSet<string>(sentCodes, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        foreach (var item in score.Items)
        {
            if (string.IsNullOrWhiteSpace(item.C) || !sent.Contains(item.C))
                errors.Add($"{item.C}: không thuộc tập đã lọc");
            else if (!seen.Add(item.C))
                errors.Add($"{item.C}: bị chấm trùng");

            if (item.W is null || item.W.Count != 2)
                errors.Add($"{item.C}: trọng số W phải có đúng 2 số");
            else
            {
                Range(errors, item.C, "W[0]", item.W[0], 0, 1);
                Range(errors, item.C, "W[1]", item.W[1], 0, 1);
            }

            if (item.Conf is not ("H" or "L"))
                errors.Add($"{item.C}: conf phải là H hoặc L");
            if (!ScoreGroups.Contains(item.G))
                errors.Add($"{item.C}: nhóm g={item.G} không hợp lệ");
            if (!FinalGroups.Contains(item.G2))
                errors.Add($"{item.C}: nhóm g2={item.G2} không hợp lệ");

            Range(errors, item.C, "pg", item.Pg, 0, 100);
            Range(errors, item.C, "ps", item.Ps, 0, 100);
            Range(errors, item.C, "A", item.A, 0, 100);
            Range(errors, item.C, "adj", item.Adj, -6, 6);
            Range(errors, item.C, "S", item.S, -6, 106);
            NonNegative(errors, item.C, "r", item.R);
            Range(errors, item.C, "F", item.F, 0, 1);
            Range(errors, item.C, "H", item.H, 0, 1);
            Range(errors, item.C, "Q", item.Q, 0, 1);
            NonNegative(errors, item.C, "gap", item.Gap);
        }

        foreach (var code in score.Failed)
        {
            if (string.IsNullOrWhiteSpace(code) || !sent.Contains(code))
                errors.Add($"{code}: mã lỗi không thuộc tập đã lọc");
            else if (!seen.Add(code))
                errors.Add($"{code}: vừa có điểm vừa nằm trong failed");
        }

        foreach (var code in sent)
        {
            if (!seen.Contains(code))
                errors.Add($"{code}: thiếu trong kết quả chấm");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Kết quả chấm sai khuôn: " + string.Join("; ", errors));
    }

    public static void EnsurePick(IReadOnlyList<PickSchoolDto> sent, string needAid, PickResponseDto pick)
    {
        var byCode = new Dictionary<string, PickSchoolDto>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        foreach (var school in sent)
        {
            if (!byCode.TryAdd(school.C, school))
                errors.Add($"{school.C}: gửi chọn trùng mã");
        }

        if (string.IsNullOrWhiteSpace(pick.Status))
            errors.Add("status trống");
        if (needAid == "no" && pick.Aid.Count > 0)
            errors.Add("needAid = no nhưng vẫn có khối học bổng");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CheckBlock(errors, seen, byCode, "reach", pick.Reach, 3, s => s.Blk == "MAIN" && s.G2 is "R" or "RF");
        CheckBlock(errors, seen, byCode, "match", pick.Match, 5, s => s.Blk == "MAIN" && s.G2 == "M");
        CheckBlock(errors, seen, byCode, "safety", pick.Safety, 4, s => s.Blk == "MAIN" && s.G2 == "S");
        CheckBlock(errors, seen, byCode, "aid", pick.Aid, null, s => s.Blk == "AID");
        CheckBlock(errors, seen, byCode, "other", pick.Other, null, s => s.G2 is "NA" or "O");

        if (errors.Count > 0)
            throw new InvalidOperationException("Kết quả chọn sai khuôn: " + string.Join("; ", errors));
    }

    private static void CheckBlock(
        List<string> errors,
        HashSet<string> seen,
        Dictionary<string, PickSchoolDto> byCode,
        string block,
        IReadOnlyList<PickEntryDto> entries,
        int? max,
        Func<PickSchoolDto, bool> fits)
    {
        if (max is int limit && entries.Count > limit)
            errors.Add($"{block} có {entries.Count} trường, tối đa {limit}");

        foreach (var entry in entries)
        {
            if (!byCode.TryGetValue(entry.C, out var school))
            {
                errors.Add($"{entry.C}: không thuộc tập đã chấm");
                continue;
            }
            if (!seen.Add(entry.C))
                errors.Add($"{entry.C}: xuất hiện ở hơn một khối");
            if (!fits(school))
                errors.Add($"{entry.C}: ở khối {block} nhưng blk={school.Blk}, g2={school.G2}");

            Range(errors, entry.C, "X", entry.X, 0, 1);
            foreach (var why in entry.Why)
            {
                if (!WhyCodes.Contains(why))
                    errors.Add($"{entry.C}: mã lý do {why} không hợp lệ");
            }
        }
    }

    private static void Range(List<string> errors, string code, string name, double? value, double min, double max)
    {
        if (value is null) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value < min || value > max)
            errors.Add($"{code}: {name}={value} ngoài {min}–{max}");
    }

    private static void NonNegative(List<string> errors, string code, string name, double? value)
    {
        if (value is null) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value < 0)
            errors.Add($"{code}: {name}={value} phải ≥ 0");
    }
}
