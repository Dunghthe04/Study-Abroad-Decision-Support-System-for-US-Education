namespace StudyAbroad.Application.Recommendations.Sensitivity;

// [UC-85] Yêu cầu một lần phân tích.
// Steps: độ lớn các mức nhích (0,10 và/hoặc 0,20); mỗi mức chạy cả chiều cộng lẫn chiều trừ.
// StableBelow, WatchUpTo: hai ngưỡng gắn nhãn (BA: dưới 2% Ổn định, 2–10% Cần theo dõi, trên 10% Nhạy), chỉnh được.
public sealed record SensitivityRequest(
    IReadOnlyList<string> ParameterKeys,
    IReadOnlyList<double> Steps,
    double StableBelow = SensitivityAnalyzer.DefaultStableBelow,
    double WatchUpTo = SensitivityAnalyzer.DefaultWatchUpTo);

// Kết quả một mức nhích của một tham số.
// Step: +0,10, −0,20...; Value: giá trị thật đã dùng (sau khi kẹp vào khoảng hợp lệ); Clamped: có bị kẹp không (AF-2).
// Pairs / ChangedPairs: số cặp (hồ sơ, trường) và số cặp đổi nhóm. MovedSafer: số cặp chuyển sang nhóm DỄ hơn
// (ví dụ Vừa sức lên An toàn), là chiều nguy hiểm vì học sinh có thể chủ quan; MovedHarder: chiều ngược lại.
// Slots / ChangedSlots: số chỗ trong top 4 của các khối (Khó vào, Vừa sức, An toàn, Cần học bổng) và số chỗ đổi trường.
// RankedSchools / RankShiftTotal (số phụ, không dùng gắn nhãn): xét TOÀN BỘ thứ tự trường trong ba nhóm chính, đúng thứ tự
// học sinh thấy khi bấm "Xem thêm" (điểm phù hợp giảm dần). RankedSchools là số trường có mặt trong cùng nhóm ở cả hai lần,
// RankShiftTotal là tổng số bậc các trường đó dịch đi. Null ở các lần chạy cũ, trước khi có số này.
public sealed record SensitivityPoint(
    double Step, double Value, bool Clamped,
    int Pairs, int ChangedPairs, int MovedSafer, int MovedHarder,
    int Slots, int ChangedSlots,
    int? RankedSchools = null, long? RankShiftTotal = null)
{
    public double GroupChangeRate => Pairs == 0 ? 0 : (double)ChangedPairs / Pairs;
    public double TopChangeRate => Slots == 0 ? 0 : (double)ChangedSlots / Slots;
    // Tỉ lệ trường top 4 ban đầu còn giữ lại trong top 4 (BA: độ trùng top 4)
    public double TopOverlap => 1 - TopChangeRate;
    // Trung bình mỗi trường dịch bao nhiêu bậc trong nhóm. Ví dụ 0,4: thứ tự gần như giữ nguyên
    public double? AvgRankShift => RankedSchools is int n ? (n == 0 ? 0 : (double)(RankShiftTotal ?? 0) / n) : null;
}

// Kết quả một tham số. Rate: số dùng để gắn nhãn (lớn nhất của +10% và −10%, theo đúng cách đo của loại tham số);
// Rate và Label null khi không chạy mức ±10% (BA: chỉ chọn ±20% thì không phân loại).
// Label: stable | watch | sensitive.
public sealed record SensitivityParameterResult(
    string Key, string Name, SensitivityKind Kind, double Baseline,
    IReadOnlyList<SensitivityPoint> Points, double? Rate, string? Label);

// Kết quả cả lần phân tích, kèm đủ thông tin để biết lần chạy này đo trên điều kiện nào (so sánh hai lần chạy cần cùng điều kiện).
// SchoolDataHash: dấu nhận biết dữ liệu trường (RecommendationService.SchoolDataFingerprint).
public sealed record SensitivityReport(
    string AlgorithmVersion, string ProfileSetVersion, string ProfileSetHash, int ProfileCount, int SchoolCount,
    string SchoolDataHash, double StableBelow, double WatchUpTo, IReadOnlyList<SensitivityParameterResult> Parameters);

public static class SensitivityLabels
{
    public const string Stable = "stable";
    public const string Watch = "watch";
    public const string Sensitive = "sensitive";
}

// [UC-85] Phân tích độ nhạy: hàm thuần, không đọc hay ghi DB, không gọi AI.
// Với mỗi tham số và mỗi mức nhích: tạo bộ tham số đã nhích, chạy lại toàn bộ hồ sơ qua đúng hàm gợi ý thật
// (RecommendationService.EvaluateWith), rồi so với lần chạy gốc.
public static class SensitivityAnalyzer
{
    public const double DefaultStableBelow = 0.02;
    public const double DefaultWatchUpTo = 0.10;
    // Mức ±10% là mức dùng để gắn nhãn
    public const double ClassifyStep = 0.10;
    public static readonly IReadOnlyList<double> AllowedSteps = [0.10, 0.20];

    // Bốn khối có top 4. Khối "chưa đủ dữ liệu" không có thứ tự theo fit nên không tính.
    private static readonly Func<PickedBlocks, IReadOnlyList<RankedSchool>>[] Blocks =
        [b => b.Reach, b => b.Match, b => b.Safety, b => b.Aid];

    public static SensitivityReport Run(
        SensitivityProfileSet profileSet, IReadOnlyList<RecommendSchoolInput> schools, SensitivityRequest request,
        RecommendationTuning? baseline = null)
    {
        var parameters = Validate(request);
        var b = baseline ?? RecommendationTuning.Current;

        var (students, baseRuns) = Prepare(profileSet, schools, b);

        var results = new List<SensitivityParameterResult>();
        foreach (var parameter in parameters)
        {
            var original = parameter.Get(b);
            var points = new List<SensitivityPoint>();
            foreach (var step in request.Steps.Distinct().OrderBy(x => x))
                foreach (var signed in new[] { -step, step })
                    points.Add(Measure(parameter, b, original, signed, students, schools, baseRuns));

            var rate = ClassifyRate(parameter.Kind, points);
            results.Add(new SensitivityParameterResult(
                parameter.Key, parameter.Name, parameter.Kind, original, points, rate,
                rate is double r ? Label(r, request.StableBelow, request.WatchUpTo) : null));
        }

        return new SensitivityReport(
            RecommendationService.AlgorithmVersion, profileSet.Version, profileSet.Hash, profileSet.Profiles.Count, schools.Count,
            RecommendationService.SchoolDataFingerprint(schools), request.StableBelow, request.WatchUpTo, results);
    }

    // Nhãn theo tỉ lệ thay đổi: dưới stableBelow là Ổn định, đến watchUpTo (gồm cả mốc) là Cần theo dõi, trên nữa là Nhạy
    public static string Label(double rate, double stableBelow = DefaultStableBelow, double watchUpTo = DefaultWatchUpTo) =>
        rate < stableBelow ? SensitivityLabels.Stable
        : rate <= watchUpTo ? SensitivityLabels.Watch
        : SensitivityLabels.Sensitive;

    // Giá trị sau khi nhích tương đối (0,75 nhích +10% là 0,825), kẹp vào [0, 1] vì mọi tham số trong danh mục
    // là ngưỡng, tỉ lệ, hệ số hoặc trọng số nằm trong khoảng đó (AF-2: kẹp và ghi lại giá trị thật đã dùng).
    public static (double Value, bool Clamped) Nudge(double original, double signedStep)
    {
        var raw = original * (1 + signedStep);
        var value = Math.Clamp(raw, 0, 1);
        return (value, value != raw);
    }

    // Đo một mức nhích bất kỳ của một tham số, không qua kiểm tra mức 10%/20%. Dùng cho test bất biến
    // (nhích 0% phải ra 0 thay đổi) và để xem nhanh một mức lẻ khi cần.
    public static SensitivityPoint MeasureOne(
        SensitivityProfileSet profileSet, IReadOnlyList<RecommendSchoolInput> schools, string parameterKey, double signedStep,
        RecommendationTuning? baseline = null)
    {
        var parameter = SensitivityParameters.Find(parameterKey) ?? throw new ArgumentException($"Không có tham số '{parameterKey}'.");
        var b = baseline ?? RecommendationTuning.Current;
        var (students, baseRuns) = Prepare(profileSet, schools, b);
        return Measure(parameter, b, parameter.Get(b), signedStep, students, schools, baseRuns);
    }

    // Hồ sơ giả lập đổi sang hồ sơ học sinh một lần, rồi chạy gốc một lần để làm mốc so sánh
    private static (List<(Domain.Entities.StudentProfile Student, int EcTier)> Students, List<Snapshot> BaseRuns) Prepare(
        SensitivityProfileSet profileSet, IReadOnlyList<RecommendSchoolInput> schools, RecommendationTuning baseline)
    {
        var students = profileSet.Profiles.Select(p => (Student: p.ToStudentProfile(profileSet.Major), p.EcTier)).ToList();
        var baseRuns = students.AsParallel().AsOrdered()
            .Select(s => Snapshot.Of(RecommendationService.EvaluateWith(s.Student, schools, s.EcTier, baseline)))
            .ToList();
        return (students, baseRuns);
    }

    private static SensitivityPoint Measure(
        SensitivityParameter parameter, RecommendationTuning baseline, double original, double signedStep,
        List<(Domain.Entities.StudentProfile Student, int EcTier)> students, IReadOnlyList<RecommendSchoolInput> schools,
        List<Snapshot> baseRuns)
    {
        var (value, clamped) = Nudge(original, signedStep);
        var tuning = parameter.With(baseline, value);

        // Mỗi hồ sơ độc lập nên chạy song song; cộng các số nguyên nên kết quả không phụ thuộc thứ tự chạy
        var diffs = Enumerable.Range(0, students.Count).AsParallel()
            .Select(i => Snapshot.Of(RecommendationService.EvaluateWith(students[i].Student, schools, students[i].EcTier, tuning))
                .Compare(baseRuns[i]))
            .ToList();

        return new SensitivityPoint(
            signedStep, value, clamped,
            diffs.Sum(d => d.Pairs), diffs.Sum(d => d.Changed), diffs.Sum(d => d.Safer), diffs.Sum(d => d.Harder),
            diffs.Sum(d => d.Slots), diffs.Sum(d => d.ChangedSlots),
            diffs.Sum(d => d.Ranked), diffs.Sum(d => d.RankShift));
    }

    private static double? ClassifyRate(SensitivityKind kind, List<SensitivityPoint> points)
    {
        var at10 = points.Where(p => Math.Abs(Math.Abs(p.Step) - ClassifyStep) < 1e-9).ToList();
        if (at10.Count == 0) return null;
        return at10.Max(p => kind == SensitivityKind.Grouping ? p.GroupChangeRate : p.TopChangeRate);
    }

    // AF-1: phải chọn ít nhất một tham số; tham số và mức nhích phải nằm trong danh mục; ngưỡng nhãn hợp lệ
    private static List<SensitivityParameter> Validate(SensitivityRequest request)
    {
        if (request.ParameterKeys is not { Count: > 0 })
            throw new ArgumentException("Cần chọn ít nhất một tham số để phân tích.");
        if (request.Steps is not { Count: > 0 } || request.Steps.Any(s => !AllowedSteps.Any(a => Math.Abs(a - s) < 1e-9)))
            throw new ArgumentException("Mức nhích chỉ được là 10% hoặc 20%.");
        if (!(request.StableBelow > 0 && request.StableBelow < request.WatchUpTo && request.WatchUpTo < 1))
            throw new ArgumentException("Ngưỡng nhãn không hợp lệ: cần 0 < ngưỡng Ổn định < ngưỡng Cần theo dõi < 100%.");

        var list = new List<SensitivityParameter>();
        foreach (var key in request.ParameterKeys.Distinct(StringComparer.Ordinal))
            list.Add(SensitivityParameters.Find(key) ?? throw new ArgumentException($"Không có tham số '{key}'."));
        return list;
    }

    // Ba nhóm chính có nút "Xem thêm" (khối cần học bổng và khối thiếu dữ liệu thì không)
    private static readonly SchoolGroup[] MainGroups = [SchoolGroup.Reach, SchoolGroup.Match, SchoolGroup.Safety];

    // Phần cần so của một lượt gợi ý: nhóm của từng trường (sau lọc cứng), danh sách trường trong top 4 từng khối,
    // và thứ tự đầy đủ của từng nhóm chính (như danh sách "Xem thêm": trường trong ngân sách, điểm phù hợp giảm dần, cùng điểm theo mã)
    private sealed record Snapshot(Dictionary<string, SchoolGroup> Groups, List<HashSet<string>> Tops, List<List<string>> Orders)
    {
        public static Snapshot Of(RecommendationRun run) => new(
            run.Scored.ToDictionary(s => s.School.University.Code, s => s.Grade.Group, StringComparer.OrdinalIgnoreCase),
            run.Picked is { } picked
                ? Blocks.Select(block => block(picked).Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase)).ToList()
                : Blocks.Select(_ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)).ToList(),
            MainGroups.Select(group => run.Scored
                    .Where(s => s.Filter.Outcome == FilterOutcome.Keep && s.Grade.Group == group)
                    .OrderByDescending(s => s.Fit?.Value ?? 0)
                    .ThenBy(s => s.School.University.Code, StringComparer.Ordinal)
                    .Select(s => s.School.University.Code)
                    .ToList())
                .ToList());

        public Diff Compare(Snapshot before)
        {
            int pairs = 0, changed = 0, safer = 0, harder = 0;
            foreach (var (code, oldGroup) in before.Groups)
            {
                // Lọc cứng không phụ thuộc tham số nên hai lần chạy có cùng các trường; vẫn kiểm tra cho chắc
                if (!Groups.TryGetValue(code, out var newGroup)) continue;
                pairs++;
                if (newGroup == oldGroup) continue;
                changed++;
                // SchoolGroup xếp từ khó đến dễ (Reach < Match < Safety); NoData không có chiều
                if (oldGroup != SchoolGroup.NoData && newGroup != SchoolGroup.NoData)
                {
                    if (newGroup > oldGroup) safer++;
                    else harder++;
                }
            }

            int slots = 0, changedSlots = 0;
            for (var i = 0; i < before.Tops.Count; i++)
            {
                slots += before.Tops[i].Count;
                changedSlots += before.Tops[i].Count(code => !Tops[i].Contains(code));
            }
            // Thứ tự đầy đủ: trường nào còn ở cùng nhóm thì cộng số bậc nó dịch đi (trường rời nhóm đã được đếm ở trên)
            int ranked = 0;
            long rankShift = 0;
            for (var i = 0; i < before.Orders.Count; i++)
            {
                var now = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (var r = 0; r < Orders[i].Count; r++) now[Orders[i][r]] = r;
                for (var r = 0; r < before.Orders[i].Count; r++)
                {
                    if (!now.TryGetValue(before.Orders[i][r], out var newRank)) continue;
                    ranked++;
                    rankShift += Math.Abs(newRank - r);
                }
            }
            return new Diff(pairs, changed, safer, harder, slots, changedSlots, ranked, rankShift);
        }
    }

    private readonly record struct Diff(
        int Pairs, int Changed, int Safer, int Harder, int Slots, int ChangedSlots, int Ranked, long RankShift);
}
