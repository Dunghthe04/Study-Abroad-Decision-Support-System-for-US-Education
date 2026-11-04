using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Recommendations;

// Công thức xếp nhóm trường Khó vào / Vừa sức / An toàn (CONG_THUC_GOI_Y_TRUONG.docx).
// Gồm: kiểu dữ liệu, vị trí học thuật p, gộp và hiệu chỉnh ngoại khóa, xếp nhóm, chấm một trường.

// ============================== KIỂU DỮ LIỆU ==============================

// Tham số của công thức xếp nhóm trường. Mọi con số đều chỉnh được, không ghi cứng trong hàm tính.
public record GroupingOptions(
    double LowSubmitPct = 0.5,       // tỉ lệ sinh viên nộp SAT dưới mốc này thì SAT ít đại diện
    double LowSubmitFactor = 0.5,    // khi đó trọng số điểm thi nhân hệ số này
    double EcStep = 0.05,            // p đổi bao nhiêu cho mỗi bậc ngoại khóa lệch khỏi mức trung bình
    double EcCap = 0.10,             // trần tổng hiệu chỉnh ngoại khóa (cộng hoặc trừ)
    double LowerCut = 0.25,          // p' dưới mốc này: Khó vào
    double UpperCut = 0.75,          // p' từ mốc này: An toàn
    double AlwaysReachRate = 0.15,   // tỉ lệ nhận dưới mốc này: luôn Khó vào
    double SafetyMinRate = 0.50,     // tỉ lệ nhận dưới mốc này: không được là An toàn
    double IntlRatioCut = 0.5);      // tỉ lệ nhận du học sinh / tỉ lệ chung dưới mốc này: hạ một nhóm

// Điểm học thuật của học sinh. EcTier = bậc ngoại khóa 1..5 (3 là trung bình, không hiệu chỉnh).
public record StudentAcademics(decimal? Gpa4, int? Sat, int? Act, int EcTier = AcademicBlend.NeutralEcTier);

// Số liệu học thuật của trường. Các tỉ lệ ở dạng 0–1.
// GpaDist: 7 tỉ lệ từ thấp lên cao: dưới 2,5 · 2,5–2,99 · 3,0–3,24 · 3,25–3,49 · 3,5–3,74 · 3,75–3,99 · đúng 4,0.
// Importance: very_important | important | considered | not_considered (CDS C7).
public record SchoolAcademics(
    decimal? AvgGpa4,
    IReadOnlyList<decimal>? GpaDist,
    int? Sat25, int? Sat75,
    int? Act25, int? Act75,
    string? SatPolicy, decimal? SatSubmitPct,
    string? GpaImportance, string? TestImportance, string? EcImportance,
    decimal? AcceptanceRate, decimal? IntlAcceptanceRate);

// Kết quả chấm một trường: nhóm, các vị trí đã tính (null nếu thiếu dữ liệu) và lý do.
public record SchoolGrade(
    SchoolGroup Group, double? Pg, double? Ps, double? P, double? PAdjusted, IReadOnlyList<string> Reasons);

// Nhóm của một trường đối với một học sinh. NoData = chưa đủ dữ liệu để xếp.
// Thứ tự từ thấp lên cao theo mức dễ vào, để "hạ một nhóm" chỉ là trừ một.
public enum SchoolGroup
{
    NoData,
    Reach,   // Khó vào
    Match,   // Vừa sức
    Safety   // An toàn
}

// Nhóm và lý do (mã), để bảng trường nói được vì sao trường nằm ở nhóm đó.
public record GroupDecision(SchoolGroup Group, IReadOnlyList<string> Reasons);

public static class GroupReasons
{
    public const string LowAcceptRate = "LOW_ACCEPT_RATE";        // tỉ lệ nhận dưới 15%: luôn Khó vào
    public const string BelowRange = "P_BELOW_RANGE";             // p' dưới 0,25
    public const string InRange = "P_IN_RANGE";                   // p' từ 0,25 đến dưới 0,75
    public const string AboveRange = "P_ABOVE_RANGE";             // p' từ 0,75 trở lên
    public const string CappedSelective = "CAPPED_SELECTIVE";     // lẽ ra An toàn nhưng tỉ lệ nhận dưới 50%
    public const string CappedWeakMetric = "CAPPED_WEAK_METRIC";  // lẽ ra An toàn nhưng một chỉ số dưới 0,25
    public const string IntlDowngrade = "INTL_DOWNGRADE";         // tỉ lệ nhận du học sinh thấp hơn hẳn: hạ một nhóm
    public const string NoAcceptRate = "NO_ACCEPT_RATE";          // thiếu tỉ lệ nhận nên bỏ luật theo tỉ lệ nhận
    public const string NeedTest = "NEED_TEST";                   // trường bắt buộc SAT mà học sinh chưa có điểm thi
    public const string NoAcademicData = "NO_ACADEMIC_DATA";      // thiếu cả GPA lẫn điểm thi để so sánh
}
// ---- Bước 5: fit score và chọn trường ----

// Thông tin của học sinh dùng cho fit score.
public record FitStudent(decimal? BudgetUsd, decimal? Ielts);

// Hạng của trường trong một bảng xếp hạng: hạng Rank trong tổng số Total trường của bảng.
public record RankPosition(int Rank, int Total);

// Thông tin của trường dùng cho fit score. TotalCostUsd = học phí + ăn ở + phí + chi khác (một năm).
// Quality: chất lượng của trường, từ 0 đến 1 (tính từ xếp hạng bằng SchoolRanker.QualityFromRanks, hoặc số thay thế).
public record FitSchool(decimal? TotalCostUsd, decimal? IntlAidAvgUsd, decimal? MinIelts, double? Quality);

// Trọng số của năm thành phần. Ba thanh ưu tiên của học sinh (chi phí, học bổng, chất lượng) chỉ đổi các trọng số này.
public record FitWeights(
    double Position = 0.30, double Budget = 0.25, double Aid = 0.15, double Quality = 0.20, double English = 0.10);

// Điểm phù hợp: tổng và từng thành phần (null nếu thiếu dữ liệu nên bị bỏ khỏi tổng).
public record FitScore(double Value, double? Position, double? Budget, double? Aid, double? Quality, double? English);

// Một trường đã xếp nhóm, kèm dữ liệu cho fit score.
public record GradedSchool(string Code, SchoolGrade Grade, FitSchool Fit);

// Một trường đã chấm fit score. Fit là null khi trường chưa đủ dữ liệu để xếp nhóm.
public record RankedSchool(string Code, SchoolGrade Grade, FitScore? Fit);

// Số trường mỗi nhóm trước khi cắt còn tối đa N trường.
public record GroupCounts(int Reach, int Match, int Safety, int NoData);

// Kết quả gợi ý: mỗi nhóm giữ tối đa N trường theo fit score giảm dần; Available là số trường thực có mỗi nhóm.
public record Shortlist(
    IReadOnlyList<RankedSchool> Reach, IReadOnlyList<RankedSchool> Match,
    IReadOnlyList<RankedSchool> Safety, IReadOnlyList<RankedSchool> NoData, GroupCounts Available);

// ============================== TÍNH TOÁN ==============================

// Vị trí học thuật của học sinh so với sinh viên đã nhập học của trường: số từ 0 đến 1,
// bằng tỉ lệ sinh viên có điểm thấp hơn học sinh.
public static class AcademicPosition
{
    // Trong phân phối chuẩn, khoảng từ P25 đến P75 rộng 2 · Φ⁻¹(0,75) ≈ 1,349 độ lệch chuẩn.
    private const double IqrInSigma = 1.3489795003921634;

    // Trường chỉ công bố P25 và P75. Giả định điểm phân bố chuẩn:
    //   μ = (P25 + P75) / 2,   σ = (P75 − P25) / 1,349,   p = Φ((điểm − μ) / σ)
    public static double FromQuartiles(double score, double p25, double p75)
    {
        if (p75 <= p25) throw new ArgumentException("P75 phải lớn hơn P25", nameof(p75));
        var mu = (p25 + p75) / 2;
        var sigma = (p75 - p25) / IqrInSigma;
        return Phi((score - mu) / sigma);
    }

    // Mốc GPA của 6 khoảng đầu (dưới 2,5 · 2,5–2,99 · 3,0–3,24 · 3,25–3,49 · 3,5–3,74 · 3,75–3,99);
    // khoảng thứ 7 là đúng 4,0.
    private static readonly double[] GpaEdges = [2.0, 2.5, 3.0, 3.25, 3.5, 3.75, 4.0];

    // Độ lệch chuẩn GPA giả định khi trường chỉ công bố GPA trung bình.
    public const double DefaultGpaSigma = 0.3;

    // Vị trí theo GPA. Ưu tiên phân bố GPA của trường (7 tỉ lệ từ thấp lên cao);
    // không có thì giả định hình chuông quanh GPA trung bình; không có cả hai thì trả null.
    public static double? FromGpa(
        double gpa, IReadOnlyList<decimal>? distribution, decimal? average, double sigma = DefaultGpaSigma)
    {
        if (distribution is { Count: 7 })
        {
            var total = distribution.Sum(x => (double)x);
            if (total > 0) return FromDistribution(gpa, distribution, total);
        }
        if (average is decimal avg) return Phi((gpa - (double)avg) / sigma);
        return null;
    }

    // Cộng dồn tỉ lệ các khoảng nằm dưới gpa, rồi lấy một phần của khoảng chứa gpa (nội suy tuyến tính).
    private static double FromDistribution(double gpa, IReadOnlyList<decimal> dist, double total)
    {
        double below = 0;
        for (var i = 0; i < 6; i++)
        {
            var share = (double)dist[i] / total;
            if (gpa < GpaEdges[i + 1])
                return below + share * Math.Clamp((gpa - GpaEdges[i]) / (GpaEdges[i + 1] - GpaEdges[i]), 0, 1);
            below += share;
        }
        // GPA từ 4,0 trở lên: thấp hơn mọi khoảng trước, ngang một nửa nhóm đúng 4,0.
        return below + (double)dist[6] / total * 0.5;
    }

    // Hàm phân phối chuẩn tích lũy: tỉ lệ nằm bên trái z.
    public static double Phi(double z) => 0.5 * (1 + Erf(z / Math.Sqrt(2)));

    // Abramowitz–Stegun 7.1.26, sai số dưới 1,5e-7 (.NET không có sẵn erf).
    private static double Erf(double x)
    {
        var sign = Math.Sign(x);
        x = Math.Abs(x);
        var t = 1 / (1 + 0.3275911 * x);
        var y = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return sign * y;
    }
}

// Gộp vị trí theo GPA và theo điểm thi thành p, rồi hiệu chỉnh theo ngoại khóa thành p'.
public static class AcademicBlend
{
    // Bậc ngoại khóa trung bình: không hiệu chỉnh.
    public const int NeutralEcTier = 3;

    // p = trung bình có trọng số của các chỉ số đang có. Trọng số theo mức trường coi trọng (CDS C7).
    // Thiếu một chỉ số thì p bằng chỉ số còn lại; thiếu cả hai thì null.
    // Mọi chỉ số đang có đều có trọng số 0 (trường "không xét" cả hai): coi như bằng nhau, không chia cho 0.
    public static double? Combine(
        double? pg, double? ps, string? gpaImportance, string? testImportance,
        decimal? satSubmitPct = null, GroupingOptions? options = null)
    {
        var o = options ?? new GroupingOptions();
        var wg = Weight(gpaImportance);
        var ws = Weight(testImportance);
        // Ít sinh viên nộp SAT thì P25, P75 chỉ đại diện nhóm có nộp (thường điểm cao): giảm trọng số.
        if (satSubmitPct is decimal share && (double)share < o.LowSubmitPct) ws *= o.LowSubmitFactor;

        var parts = new List<(double Value, double Weight)>();
        if (pg is double g) parts.Add((g, wg));
        if (ps is double s) parts.Add((s, ws));
        if (parts.Count == 0) return null;

        var total = parts.Sum(x => x.Weight);
        if (total <= 0) return parts.Average(x => x.Value);
        return parts.Sum(x => x.Value * x.Weight) / total;
    }

    // p' = p + hiệu chỉnh ngoại khóa, kẹp trong [0, 1]. Bậc 3 là trung bình nên không đổi.
    public static double AdjustForActivities(
        double p, int ecTier, string? ecImportance, GroupingOptions? options = null)
    {
        var o = options ?? new GroupingOptions();
        var shift = o.EcStep * (ecTier - NeutralEcTier) * EcWeight(ecImportance);
        return Math.Clamp(p + Math.Clamp(shift, -o.EcCap, o.EcCap), 0, 1);
    }

    // Mức trường coi trọng một tiêu chí (CDS C7); "không xét" là 0 (thầy chốt); không rõ thì lấy mức giữa.
    public static double Weight(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        "very_important" => 1.0,
        "important" => 0.75,
        "considered" => 0.5,
        "not_considered" => 0.0,
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

public static class SchoolGrouper
{
    // Xếp nhóm từ p' (đã hiệu chỉnh ngoại khóa), hai chỉ số gốc pg, ps và tỉ lệ nhận (dạng 0–1).
    // pg, ps, acceptRate, intlAcceptRate có thể thiếu (null).
    public static GroupDecision Classify(
        double pAdjusted, double? pg, double? ps, double? acceptRate, double? intlAcceptRate,
        GroupingOptions? options = null)
    {
        var o = options ?? new GroupingOptions();
        var reasons = new List<string>();
        if (acceptRate is null) reasons.Add(GroupReasons.NoAcceptRate);

        SchoolGroup group;
        if (acceptRate < o.AlwaysReachRate)
        {
            // Trường chọn lọc rất cao: điểm tốt đến đâu vẫn không chắc, không bao giờ coi là Vừa sức hay An toàn.
            group = SchoolGroup.Reach;
            reasons.Add(GroupReasons.LowAcceptRate);
        }
        else if (pAdjusted < o.LowerCut)
        {
            group = SchoolGroup.Reach;
            reasons.Add(GroupReasons.BelowRange);
        }
        else if (pAdjusted < o.UpperCut)
        {
            group = SchoolGroup.Match;
            reasons.Add(GroupReasons.InRange);
        }
        else
        {
            reasons.Add(GroupReasons.AboveRange);
            // p' đủ cao cho An toàn, nhưng trường nhận ít hơn một nửa hồ sơ, hoặc có một chỉ số dưới mốc
            // (chỉ số mạnh không được che chỉ số yếu) thì chỉ Vừa sức.
            if (acceptRate < o.SafetyMinRate)
            {
                group = SchoolGroup.Match;
                reasons.Add(GroupReasons.CappedSelective);
            }
            else if (pg < o.LowerCut || ps < o.LowerCut)
            {
                group = SchoolGroup.Match;
                reasons.Add(GroupReasons.CappedWeakMetric);
            }
            else
            {
                group = SchoolGroup.Safety;
            }
        }

        // Trường nhận du học sinh ít hơn hẳn tỉ lệ chung thì khó hơn con số trung bình: hạ một nhóm.
        if (acceptRate is double rate && rate > 0 && intlAcceptRate is double intl
            && intl / rate < o.IntlRatioCut && group > SchoolGroup.Reach)
        {
            group--;
            reasons.Add(GroupReasons.IntlDowngrade);
        }
        return new GroupDecision(group, reasons);
    }

    // Chấm một trường cho một học sinh: ghép bước 1 đến 4.
    // Thiếu dữ liệu thì không đoán: trả NoData kèm lý do.
    public static SchoolGrade Grade(StudentAcademics student, SchoolAcademics school, GroupingOptions? options = null)
    {
        double? pg = student.Gpa4 is decimal gpa
            ? AcademicPosition.FromGpa((double)gpa, school.GpaDist, school.AvgGpa4)
            : null;
        double? ps = TestPosition(student, school);

        // Trường bắt buộc SAT mà học sinh chưa có điểm thi nào: không xếp được.
        var studentHasTest = student.Sat is not null || student.Act is not null;
        if (!studentHasTest && string.Equals(school.SatPolicy, "required", StringComparison.OrdinalIgnoreCase))
            return new SchoolGrade(SchoolGroup.NoData, pg, null, null, null, [GroupReasons.NeedTest]);

        var p = AcademicBlend.Combine(
            pg, ps, school.GpaImportance, school.TestImportance, school.SatSubmitPct, options);
        if (p is null)
            return new SchoolGrade(SchoolGroup.NoData, pg, ps, null, null, [GroupReasons.NoAcademicData]);

        var pAdjusted = AcademicBlend.AdjustForActivities(p.Value, student.EcTier, school.EcImportance, options);
        var decision = Classify(
            pAdjusted, pg, ps, ToDouble(school.AcceptanceRate), ToDouble(school.IntlAcceptanceRate), options);
        return new SchoolGrade(decision.Group, pg, ps, p, pAdjusted, decision.Reasons);
    }

    // Vị trí theo điểm thi: SAT nếu cả hai bên có, không thì ACT. Hai bài không quy đổi qua nhau.
    // Mốc P25, P75 không hợp lệ (P75 không lớn hơn P25) coi như thiếu.
    private static double? TestPosition(StudentAcademics student, SchoolAcademics school)
    {
        if (student.Sat is int sat && school.Sat25 is int s25 && school.Sat75 is int s75 && s75 > s25)
            return AcademicPosition.FromQuartiles(sat, s25, s75);
        if (student.Act is int act && school.Act25 is int a25 && school.Act75 is int a75 && a75 > a25)
            return AcademicPosition.FromQuartiles(act, a25, a75);
        return null;
    }

    private static double? ToDouble(decimal? value) => value is decimal v ? (double)v : null;
}
// Fit score để chọn và sắp xếp trường trong cùng một nhóm. Không đổi nhóm.
public static class SchoolRanker
{
    public const int DefaultMaxPerGroup = 4;

    // Fit = 0,30·A + 0,25·C + 0,15·H + 0,20·Q + 0,10·E, mỗi thành phần trong 0..1.
    // Thành phần nào thiếu dữ liệu thì bỏ khỏi tổng và chia lại trọng số cho các thành phần còn lại.
    public static FitScore Score(double pAdjusted, FitStudent student, FitSchool school, FitWeights? weights = null)
    {
        var w = weights ?? new FitWeights();

        // A: độ vừa tầm. Cao nhất khi học sinh ở giữa nhóm sinh viên của trường; quá khó hoặc quá dễ đều thấp.
        double a = Clamp01(1 - 2 * Math.Abs(pAdjusted - 0.5));

        // C: ngân sách. Càng rẻ hơn ngân sách càng cao; vượt ngân sách thì 0.
        // H: học bổng bù được bao nhiêu phần vượt ngân sách (không vượt thì đủ, bằng 1).
        double? c = null, h = null;
        if (student.BudgetUsd is decimal budget && budget > 0 && school.TotalCostUsd is decimal cost)
        {
            c = Clamp01((double)((budget - cost) / budget));
            if (cost <= budget) h = 1;
            else if (school.IntlAidAvgUsd is decimal aid) h = Clamp01((double)(aid / (cost - budget)));
        }

        // E: tiếng Anh. Đạt mức tối thiểu thì 1; mỗi band thiếu mất 1 điểm.
        double? e = student.Ielts is decimal ielts && school.MinIelts is decimal min
            ? Clamp01(1 - (double)(min - ielts))
            : null;

        double? q = school.Quality is double quality ? Clamp01(quality) : null;

        double sum = w.Position * a, weightSum = w.Position;
        Add(c, w.Budget, ref sum, ref weightSum);
        Add(h, w.Aid, ref sum, ref weightSum);
        Add(q, w.Quality, ref sum, ref weightSum);
        Add(e, w.English, ref sum, ref weightSum);
        return new FitScore(weightSum > 0 ? sum / weightSum : 0, a, c, h, q, e);
    }

    // Ba thanh ưu tiên của học sinh [chi phí, học bổng, chất lượng], mỗi số 0–4 (2 là cân bằng).
    // Nhân trọng số mặc định của thành phần tương ứng với mức / 2: 0 là bỏ hẳn, 2 giữ nguyên, 4 gấp đôi.
    // Chỉ đổi thứ tự trong nhóm; ngoài khoảng 0–4 hoặc không đủ 3 số thì dùng trọng số mặc định.
    public static FitWeights WeightsFromPriorities(IReadOnlyList<int>? priorities)
    {
        var defaults = new FitWeights();
        if (priorities is not { Count: 3 } || priorities.Any(n => n is < 0 or > 4)) return defaults;
        return defaults with
        {
            Budget = defaults.Budget * priorities[0] / 2,
            Aid = defaults.Aid * priorities[1] / 2,
            Quality = defaults.Quality * priorities[2] / 2
        };
    }

    // Chọn tối đa maxPerGroup trường mỗi nhóm theo fit score giảm dần. Cùng điểm thì xếp theo mã trường
    // để kết quả luôn giống nhau với cùng dữ liệu.
    public static Shortlist Pick(
        IEnumerable<GradedSchool> schools, FitStudent student, FitWeights? weights = null,
        int maxPerGroup = DefaultMaxPerGroup)
    {
        var ranked = schools
            .Select(s => new RankedSchool(
                s.Code, s.Grade,
                s.Grade.PAdjusted is double p ? Score(p, student, s.Fit, weights) : null))
            .ToList();

        IReadOnlyList<RankedSchool> Top(SchoolGroup group) => ranked
            .Where(r => r.Grade.Group == group)
            .OrderByDescending(r => r.Fit?.Value ?? 0)
            .ThenBy(r => r.Code, StringComparer.Ordinal)
            .Take(maxPerGroup)
            .ToList();

        int Count(SchoolGroup group) => ranked.Count(r => r.Grade.Group == group);

        return new Shortlist(
            Top(SchoolGroup.Reach), Top(SchoolGroup.Match), Top(SchoolGroup.Safety), Top(SchoolGroup.NoData),
            new GroupCounts(
                Count(SchoolGroup.Reach), Count(SchoolGroup.Match), Count(SchoolGroup.Safety), Count(SchoolGroup.NoData)));
    }

    // Chất lượng từ xếp hạng: trung bình của (1 − (hạng − 1) / tổng số trường) qua các bảng xếp hạng hợp lệ.
    public static double? QualityFromRanks(IReadOnlyList<RankPosition>? rankings)
    {
        var scores = (rankings ?? [])
            .Where(r => r.Total > 0 && r.Rank >= 1 && r.Rank <= r.Total)
            .Select(r => 1 - (double)(r.Rank - 1) / r.Total)
            .ToList();
        return scores.Count == 0 ? null : scores.Average();
    }

    private static void Add(double? value, double weight, ref double sum, ref double weightSum)
    {
        if (value is not double v) return;
        sum += weight * v;
        weightSum += weight;
    }

    private static double Clamp01(double x) => Math.Clamp(x, 0, 1);
}

// ---- Bậc ngoại khóa k ----

// Một hoạt động đã có vai trò và phạm vi (học sinh chọn trong form, hoặc đã xác nhận). Role: 1,0 · 0,8 · 0,6 · 0,3.
public record EcActivityEntry(decimal Role, int Scope, int Months);

// Kết quả tính bậc ngoại khóa: TongHd = tổng điểm hoạt động, Bonus = điểm giải thưởng, M = điểm gộp 0–1, Tier = bậc 1–5.
public record EcScore(decimal TongHd, decimal Bonus, decimal M, int Tier);

// Tính bậc ngoại khóa k (1 đến 5) bằng code từ vai trò, phạm vi, số tháng của hoạt động và cấp giải thưởng.
// Cùng danh sách hoạt động thì luôn ra cùng k: không có AI, không có yếu tố ngẫu nhiên.
public static class ActivityTier
{
    public const int MaxActivities = 10;   // chỉ xét tối đa chừng này hoạt động và giải thưởng (theo thứ tự nhập)
    public const int TopActivities = 4;    // tổng điểm lấy 4 hoạt động cao nhất
    public const int TopAwards = 3;        // thưởng lấy 3 giải cao nhất
    private const decimal AwardPoint = 0.05m;   // mỗi cấp giải thưởng được 0,05
    private const decimal MaxBonus = 0.5m;
    private const decimal DefaultRole = 0.3m;   // vai trò không rõ coi là thành viên
    // m dưới 0,10 là bậc 1; dưới 0,25 là bậc 2; dưới 0,45 là bậc 3; dưới 0,65 là bậc 4; còn lại bậc 5
    private static readonly decimal[] TierCuts = [0.10m, 0.25m, 0.45m, 0.65m];

    // Mã vai trò trong hồ sơ → hệ số: sáng lập 1,0 · trưởng 0,8 · phó 0,6 · thành viên 0,3; mã lạ thì 0,3.
    public static decimal RoleWeight(string? role) => role switch
    {
        "founder" => 1.0m,
        "leader" => 0.8m,
        "vice_leader" => 0.6m,
        _ => DefaultRole
    };

    // q = 0,4 × (phạm vi / 5) + 0,35 × vai trò + 0,25 × min(số tháng / 24, 1), làm tròn 2 chữ số.
    public static decimal ActivityPoints(decimal role, int scope, int months) =>
        Round2(0.4m * Math.Clamp(scope, 1, 5) / 5 + 0.35m * role + 0.25m * Math.Min(Math.Max(months, 0) / 24m, 1m));

    // tong_hd = tổng q của 4 hoạt động cao nhất; bonus = min(0,5, tổng 0,05 × cấp của 3 giải cao nhất);
    // m = min(1, (tong_hd + bonus) / 4); k theo m. Chưa có hoạt động nào thì m = 0 và k = 1.
    public static EcScore Compute(IEnumerable<EcActivityEntry> activities, IEnumerable<int> awardLevels)
    {
        var tongHd = activities
            .Take(MaxActivities)
            .Select(a => ActivityPoints(a.Role, a.Scope, a.Months))
            .OrderByDescending(q => q)
            .Take(TopActivities)
            .Sum();
        var awardPoints = awardLevels
            .Take(MaxActivities)
            .OrderByDescending(level => level)
            .Take(TopAwards)
            .Sum(level => level * AwardPoint);
        var bonus = Round2(Math.Min(MaxBonus, awardPoints));
        var m = Round2(Math.Min(1m, (tongHd + bonus) / 4));
        return new EcScore(Round2(tongHd), bonus, m, TierOf(m));
    }

    public static int TierOf(decimal m) => 1 + TierCuts.Count(cut => m >= cut);

    // Từ danh sách hoạt động trong hồ sơ (ProfileActivity): giữ hoạt động có tên và phạm vi 1–5, tách giải thưởng ra riêng.
    public static EcScore FromProfile(IEnumerable<ProfileActivity> items)
    {
        var valid = items.Where(a => a.ImpactLevel is >= 1 and <= 5 && !string.IsNullOrWhiteSpace(a.Title)).ToList();
        var activities = valid
            .Where(a => !IsAward(a))
            .Take(MaxActivities)
            .Select(a => new EcActivityEntry(RoleWeight(a.Role), a.ImpactLevel!.Value, Months(a)));
        var awards = valid.Where(IsAward).Take(MaxActivities).Select(a => a.ImpactLevel!.Value);
        return Compute(activities, awards);
    }

    private static bool IsAward(ProfileActivity a) => string.Equals(a.Kind, "award", StringComparison.OrdinalIgnoreCase);

    // Số tháng học sinh khai; không có thì tính từ ngày bắt đầu đến ngày kết thúc (hoặc hôm nay); không có gì thì 0.
    private static int Months(ProfileActivity activity)
    {
        if (activity.DurationMonths is int months) return Math.Clamp(months, 0, 120);
        if (activity.StartDate is not DateOnly start) return 0;
        var end = activity.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var count = (end.Year - start.Year) * 12 + end.Month - start.Month;
        return Math.Clamp(count, 0, 120);
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
