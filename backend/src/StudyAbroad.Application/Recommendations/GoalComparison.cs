namespace StudyAbroad.Application.Recommendations;

// Một dòng bảng "bạn so với trường" trong goal-seeking, mỗi dòng là một tiêu chí bộ chấm nhóm dùng.
// Factor: sat | act | gpa | ec | accept.
// Status:
//   sat, act, gpa: below | within | above | unknown (trường chưa công bố) | no_profile (hồ sơ chưa có)
//   ec: below | within | above so với bậc trung bình (3); unknown khi trường không xét ngoại khóa
//   accept: always_reach (tỉ lệ nhận dưới mốc luôn Khó vào) | downgrade (nhận du học sinh ít, bị hạ một nhóm) |
//           capped (nhận dưới 50% hồ sơ nên tối đa là Vừa sức, không bao giờ An toàn) | ok | unknown
// Student: số của học sinh (ec: bậc 1 đến 5). Low, High: khoảng của trường (GPA chỉ có trung bình nên Low = High);
//   riêng accept: Low = tỉ lệ nhận chung, High = tỉ lệ nhận du học sinh (0..1).
// Gap: chênh so với mốc gần nhất ngoài khoảng, âm là thiếu, dương là vượt, 0 là trong khoảng; ec: bậc trừ 3; accept: du học sinh / chung.
// Position: vị trí của học sinh trong sinh viên của trường, 0..1 (sat, act, gpa), đúng số bộ chấm dùng.
// Importance: mức trường coi trọng tiêu chí (very_important | important | considered | not_considered), null nếu không rõ.
public record GoalCompareRow(
    string Factor, string Status, decimal? Student, decimal? Low, decimal? High, decimal? Gap,
    double? Position = null, string? Importance = null);

// Bảng so sánh học sinh với trường, tính hoàn toàn bằng code từ đúng số liệu và đúng luật bộ chấm nhóm đang dùng.
// Tài chính và tiếng Anh đã có sẵn trong kết quả goal-seeking (Budget, English) nên không lặp ở đây.
public static class GoalComparison
{
    // GPA chỉ có trung bình của trường: lệch trong khoảng này coi là ngang trung bình
    public const decimal GpaTolerance = 0.05m;

    // grade: kết quả chấm hiện tại, để biết luật hạ nhóm theo tỉ lệ nhận có thật sự áp dụng cho học sinh này không
    public static IReadOnlyList<GoalCompareRow> Rows(StudentAcademics student, SchoolAcademics school, SchoolGrade grade)
    {
        var rows = new List<GoalCompareRow>();
        // Điểm thi: SAT nếu học sinh có; chỉ có ACT mà trường công bố khoảng ACT thì so ACT
        if (student.Sat is null && student.Act is not null && school.Act25 is not null && school.Act75 is not null)
            rows.Add(Range("act", student.Act, school.Act25, school.Act75, school.TestImportance));
        else
            rows.Add(Range("sat", student.Sat, school.Sat25, school.Sat75, school.TestImportance));
        rows.Add(Gpa(student.Gpa4, school));
        rows.Add(Activities(student.EcTier, school.EcImportance));
        rows.Add(Acceptance(school, grade));
        return rows;
    }

    private static double? Position(int? student, int? low, int? high) =>
        student is int s && low is int a && high is int b && b > a
            ? Math.Round(AcademicPosition.FromQuartiles(s, a, b), 3)
            : null;

    private static GoalCompareRow Range(string factor, int? student, int? low, int? high, string? importance)
    {
        if (low is null || high is null)
            return new GoalCompareRow(factor, "unknown", student, null, null, null, null, importance);
        if (student is null)
            return new GoalCompareRow(factor, "no_profile", null, low, high, null, null, importance);
        var position = Position(student, low, high);
        if (student < low) return new GoalCompareRow(factor, "below", student, low, high, student - low, position, importance);
        if (student > high) return new GoalCompareRow(factor, "above", student, low, high, student - high, position, importance);
        return new GoalCompareRow(factor, "within", student, low, high, 0, position, importance);
    }

    private static GoalCompareRow Gpa(decimal? student, SchoolAcademics school)
    {
        var average = school.AvgGpa4;
        // Cùng cách bộ chấm: ưu tiên phân phối GPA của trường, không có thì giả định hình chuông quanh trung bình
        double? position = null;
        if (student is decimal g && AcademicPosition.FromGpa((double)g, school.GpaDist, average) is double p)
            position = Math.Round(p, 3);
        if (average is null)
            return new GoalCompareRow("gpa", "unknown", student, null, null, null, position, school.GpaImportance);
        if (student is null)
            return new GoalCompareRow("gpa", "no_profile", null, average, average, null, null, school.GpaImportance);
        var diff = Math.Round(student.Value - average.Value, 2);
        var status = diff < -GpaTolerance ? "below" : diff > GpaTolerance ? "above" : "within";
        return new GoalCompareRow(
            "gpa", status, student, average, average, status == "within" ? 0 : diff, position, school.GpaImportance);
    }

    // Ngoại khóa: bậc 3 là trung bình, không hiệu chỉnh. Trường không xét ngoại khóa (hoặc không rõ) thì bậc không đổi kết quả
    private static GoalCompareRow Activities(int tier, string? importance)
    {
        var effective = AcademicBlend.EcWeight(importance) > 0;
        var status = !effective ? "unknown" : tier < AcademicBlend.NeutralEcTier ? "below" : tier > AcademicBlend.NeutralEcTier ? "above" : "within";
        return new GoalCompareRow(
            "ec", status, tier, AcademicBlend.NeutralEcTier, AcademicBlend.NeutralEcTier, tier - AcademicBlend.NeutralEcTier, null, importance);
    }

    // Tỉ lệ nhận: luật "dưới 15% luôn Khó vào" và luật "du học sinh nhận ít hơn một nửa mức chung thì hạ một nhóm"
    private static GoalCompareRow Acceptance(SchoolAcademics school, SchoolGrade grade)
    {
        var rate = school.AcceptanceRate;
        var intl = school.IntlAcceptanceRate;
        decimal? ratio = rate is > 0 && intl is not null ? Math.Round(intl.Value / rate.Value, 3) : null;
        string status;
        if (grade.Reasons.Contains(GroupReasons.LowAcceptRate)) status = "always_reach";
        else if (grade.Reasons.Contains(GroupReasons.IntlDowngrade)) status = "downgrade";
        else if (rate < (decimal)new GroupingOptions().SafetyMinRate) status = "capped";
        else if (rate is null) status = "unknown";
        else status = "ok";
        return new GoalCompareRow("accept", status, null, rate, intl, ratio);
    }
}
