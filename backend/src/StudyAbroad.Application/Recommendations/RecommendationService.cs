using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StudyAbroad.Application.Recommendations;

//Một trường đã đưa vào gợi ý: hồ sơ trường + số liệu + hạng trong các bảng xếp hạng chung (null nếu chưa nạp)
public record RecommendSchoolInput(
    University University, UniversityOffering Offering, IReadOnlyList<string> Majors,
    IReadOnlyList<RankPosition>? Rankings = null);

// Một trường đã chấm nhóm và fit score. Fit null khi trường chưa đủ dữ liệu để xếp nhóm.
public record ScoredSchool(RecommendSchoolInput School, FilterResult Filter, SchoolGrade Grade, FitScore? Fit);

// Các khối hiển thị: ba nhóm chính (trong ngân sách), khối cần học bổng lớn, khối chưa đủ dữ liệu.
public record PickedBlocks(
    IReadOnlyList<RankedSchool> Reach,
    IReadOnlyList<RankedSchool> Match,
    IReadOnlyList<RankedSchool> Safety,
    IReadOnlyList<RankedSchool> Aid,
    IReadOnlyList<RankedSchool> Other);

// Kết quả mỗi lượt. Picked null khi không còn trường nào sau lọc cứng.
public record RecommendationRun(
    int Tier,
    IReadOnlyList<FilterResult> Filters,
    IReadOnlyList<ScoredSchool> Scored,
    PickedBlocks? Picked
);

// Lời tư vấn một trường, trả cho frontend.
public record SchoolExplanation(
    string Code,
    string Name,
    string Status,
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Improvements);

// Giá trị giả định của trình mô phỏng hồ sơ. Trường nào null thì giữ nguyên hồ sơ thật.
public record SimulationOverrides(
    decimal? Gpa, decimal? Sat, decimal? Act, decimal? Ielts,
    decimal? BudgetUsd, string? ScholarshipNeed, int? EcTier);

// Số trường mỗi nhóm của một lượt tính (đếm trên mọi trường còn sau lọc cứng).
public record GroupSummary(int Kept, int Reach, int Match, int Safety, int AidBlock, int NoData);

// Một trường đổi trạng thái giữa hồ sơ thật và hồ sơ giả định. Trạng thái: R, M, S, NA, AID (cần học bổng lớn), OUT (bị loại).
public record SchoolChange(string Code, string Name, string From, string To);

// Giá trị của hồ sơ thật, để thanh kéo hiện "hồ sơ" bên cạnh "giả định". EcTier là bậc tính từ các hoạt động trong hồ sơ.
public record SimulationProfile(
    decimal? Gpa, int? Sat, int? Act, decimal? Ielts, decimal? BudgetUsd, string? ScholarshipNeed, int EcTier);

// Kết quả mô phỏng: không lưu gì, không gọi advisor. Criteria và Items cùng khuôn với lượt gợi ý đã lưu (của hồ sơ giả định).
public record SimulationResult(
    SimulationProfile Profile, GroupSummary Baseline, GroupSummary Simulated, IReadOnlyList<SchoolChange> Changes,
    string CriteriaJson, string ItemsJson);

public partial class RecommendationService(IAdvisorClient advisor, IRecommendationRepository recommendations, IProfileFlowRepository profiles)
    : IRecommendationRunner

{
    public const decimal BudgetTolerance = 0.10m;
    private static readonly int[] BalancedPref = [2, 2, 2];
    // Mỗi khối giữ tối đa chừng này trường (thầy chốt)
    public const int MaxPerBlock = 4;
    public const string AlgorithmVersion = "percentile-fit-1";
    // Bắt đầu bằng "Hồ sơ" để controller trả 400 kèm câu này
    public const string MissingScholarshipMessage = "Hồ sơ chưa chọn mức cần học bổng, cần chọn trước khi gợi ý trường.";
    public const string MissingMajorMessage = "Hồ sơ chưa chọn ngành, cần chọn ngành trước khi gợi ý trường.";

    public Task<RecommendationRun> RecommendAsync(
        StudentProfile profile,
        IReadOnlyList<ProfileActivity> activities,
        IReadOnlyList<RecommendSchoolInput> schools, // dánh sách các trường active và đúng bậc học
        IReadOnlyList<int>? pref = null,
        CancellationToken ct = default
    ) => RunAsync(profile, schools, pref, ActivityTier.FromProfile(activities).Tier, ct);

    // Lọc cứng, chấm nhóm và fit score bằng code, chọn các khối, lưu. Bậc ngoại khóa tính từ hoạt động đã lưu trong hồ sơ.
    private async Task<RecommendationRun> RunAsync(
        StudentProfile profile,
        IReadOnlyList<RecommendSchoolInput> schools,
        IReadOnlyList<int>? pref,
        int tier,
        CancellationToken ct)
    {
        EnsureRunnable(profile);
        var (filtered, kept) = FilterSchools(profile, schools);

        if (kept.Count == 0)
            return await SaveAsync(profile, pref, schools, new RecommendationRun(tier, filtered, [], null), ct);

        var run = Compute(profile, pref, schools, filtered, kept, tier);
        return await SaveAsync(profile, pref, schools, run, ct);
    }

    // Hồ sơ phải có đủ hai thứ trước khi gợi ý (bắt đầu bằng "Hồ sơ" để controller trả 400).
    private static void EnsureRunnable(StudentProfile profile)
    {
        // C7: học bổng trống không có nghĩa là "không cần học bổng" (ToAidNeed sẽ coi là None, loại sai trường).
        // Chat sẽ hỏi F2 rồi gợi ý lại
        if (string.IsNullOrWhiteSpace(profile.ScholarshipNeed))
            throw new InvalidOperationException(MissingScholarshipMessage);
        // E: ngành "chưa rõ" (null) qua được bước đánh giá, nhưng không có ngành thì không lọc được trường
        if (string.IsNullOrWhiteSpace(profile.IntendedMajor))
            throw new InvalidOperationException(MissingMajorMessage);
    }

    // Các tiêu chí lọc cứng của học sinh
    private static FilterCriteria CriteriaOf(StudentProfile profile) => new(
        profile.IntendedMajor,
        profile.PreferredStates,
        SchoolControl(profile.TargetSchoolType),
        profile.BudgetMaxUsd,
        ToAidNeed(profile.ScholarshipNeed),
        profile.Ielts,
        profile.Toefl,
        profile.Duolingo);

    // Lọc cứng. filtered = mọi trường kèm kết quả; kept = các trường còn lại (trong ngân sách hoặc cần học bổng lớn).
    private static (IReadOnlyList<FilterResult> Filtered, IReadOnlyList<FilterResult> Kept) FilterSchools(
        StudentProfile profile, IReadOnlyList<RecommendSchoolInput> schools)
    {
        // filtered = danh sách các trường đã lọc cứng (tiêu chí, danh sách trường, dung sai)
        var filtered = HardFilter.Apply(CriteriaOf(profile), schools.Select(ToFilterSchool), BudgetTolerance);
        // Chỉ giữ các trường kept, aid
        var kept = filtered.Where(r => r.Outcome != FilterOutcome.Excluded).ToList();
        return (filtered, kept);
    }

    // Phần thuần của lượt gợi ý (không gọi mạng, không ghi DB): cùng hồ sơ và dữ liệu thì cùng kết quả.
    private static RecommendationRun Compute(
        StudentProfile profile,
        IReadOnlyList<int>? pref,
        IReadOnlyList<RecommendSchoolInput> schools,
        IReadOnlyList<FilterResult> filtered,
        IReadOnlyList<FilterResult> kept,
        int tier)
    {
        var byCode = kept.ToDictionary(r => r.School.Code, StringComparer.OrdinalIgnoreCase);
        var student = new StudentAcademics(profile.OverallGpa, SatScore(profile.Sat), ActScore(profile.Act), tier);
        var fitStudent = new FitStudent(profile.BudgetMaxUsd, profile.Ielts);
        var weights = SchoolRanker.WeightsFromPriorities(NormalizePref(pref));

        // Chấm nhóm cho từng trường còn lại sau lọc cứng
        var graded = new List<(RecommendSchoolInput School, FilterResult Filter, GradedSchool Graded)>();
        // Duyệt theo mã trường để kết quả không phụ thuộc thứ tự đọc từ DB
        foreach (var school in schools
                     .Where(s => byCode.ContainsKey(s.University.Code))
                     .OrderBy(s => s.University.Code, StringComparer.Ordinal))
        {
            var grade = SchoolGrouper.Grade(student, ToSchoolAcademics(school));
            graded.Add((school, byCode[school.University.Code],
                new GradedSchool(school.University.Code, grade, ToFitSchool(school))));
        }

        var scored = graded
            .Select(g => new ScoredSchool(
                g.School, g.Filter, g.Graded.Grade,
                g.Graded.Grade.PAdjusted is double p ? SchoolRanker.Score(p, fitStudent, g.Graded.Fit, weights) : null))
            .ToList();

        // Ba nhóm chính chỉ gồm trường trong ngân sách; trường cần học bổng lớn đi vào khối riêng
        var main = SchoolRanker.Pick(
            graded.Where(g => g.Filter.Outcome == FilterOutcome.Keep).Select(g => g.Graded),
            fitStudent, weights, MaxPerBlock);
        var aidPick = SchoolRanker.Pick(
            graded.Where(g => g.Filter.Outcome == FilterOutcome.AidBlock).Select(g => g.Graded),
            fitStudent, weights, MaxPerBlock);
        var aid = aidPick.Reach.Concat(aidPick.Match).Concat(aidPick.Safety)
            .OrderByDescending(r => r.Fit?.Value ?? 0)
            .ThenBy(r => r.Code, StringComparer.Ordinal)
            .Take(MaxPerBlock)
            .ToList();

        return new RecommendationRun(
            tier, filtered, scored,
            new PickedBlocks(main.Reach, main.Match, main.Safety, aid, main.NoData));
    }


    public async Task<Recommendation> CreateAsync(Guid userId, IReadOnlyList<int>? pref, CancellationToken ct)
    {
        //Lấy hồ sơ sinh viên
        var profile = await profiles.GetProfileByUserIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Chưa có hồ sơ.");
        // Đã submit hồ sơ bước 6 chưa?
        if (profile.FlowSubmittedAt is null)
            throw new InvalidOperationException("Hồ sơ chưa được gửi ở bước 6.");
        if (string.IsNullOrWhiteSpace(profile.TargetLevel))
            throw new InvalidOperationException("Hồ sơ chưa có bậc học muốn theo.");

        //Lấy ra các activity, giải thưởng
        var activities = await profiles.GetProfileActivitiesAsync(profile.Id, ct);
        //Lấy ra danh sách trường active và đúng bậc học
        var schools = await recommendations.ListSchoolsAsync(profile.TargetLevel, ct);
        //Gợi ý trường
        await RecommendAsync(profile, activities, schools, pref, ct);

        return await recommendations.GetLatestAsync(userId, ct)
            ?? throw new InvalidOperationException("Không lưu được kết quả gợi ý.");
    }

    // Đổi nút ưu tiên: tính lại bằng code với trọng số mới. Không gọi advisor, giữ bậc ngoại khóa của lần gần nhất.
    public async Task<Recommendation> RepickAsync(Guid userId, IReadOnlyList<int> pref, CancellationToken ct)
    {
        // Lấy lượt gợi ý gần nhất
        var latest = await recommendations.GetLatestAsync(userId, ct)
            ?? throw new InvalidOperationException("Chưa có lượt gợi ý nào.");
        var profile = await profiles.GetProfileByUserIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Chưa có hồ sơ.");

        var tier = ActivityTier.FromProfile(await profiles.GetProfileActivitiesAsync(profile.Id, ct)).Tier;
        var schools = await recommendations.ListSchoolsAsync(latest.StudyLevel, ct);
        await RunAsync(profile, schools, pref, tier, ct);

        return await recommendations.GetLatestAsync(userId, ct)
            ?? throw new InvalidOperationException("Không lưu được kết quả gợi ý.");
    }

    // Trình mô phỏng hồ sơ (thanh kéo GPA, SAT, ACT, IELTS, ngân sách, học bổng, ngoại khóa).
    // Tính lại bằng code trên bản sao hồ sơ với các giá trị giả định, so với hồ sơ thật. Không lưu, không gọi advisor.
    public async Task<SimulationResult> SimulateAsync(
        Guid userId, SimulationOverrides overrides, IReadOnlyList<int>? pref, CancellationToken ct)
    {
        var profile = await profiles.GetProfileByUserIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Chưa có hồ sơ.");
        if (string.IsNullOrWhiteSpace(profile.TargetLevel))
            throw new InvalidOperationException("Hồ sơ chưa có bậc học muốn theo.");

        var simulated = Overlay(profile, overrides);
        EnsureRunnable(profile);
        EnsureRunnable(simulated);

        // Bậc ngoại khóa của hồ sơ thật tính từ các hoạt động đã lưu
        var tier = ActivityTier.FromProfile(await profiles.GetProfileActivitiesAsync(profile.Id, ct)).Tier;

        var schools = await recommendations.ListSchoolsAsync(profile.TargetLevel, ct);
        var baselineRun = Evaluate(profile, pref, schools, tier);
        var simulatedRun = Evaluate(simulated, pref, schools, overrides.EcTier ?? tier);

        return new SimulationResult(
            new SimulationProfile(
                profile.OverallGpa, SatScore(profile.Sat), ActScore(profile.Act), profile.Ielts,
                profile.BudgetMaxUsd, profile.ScholarshipNeed, tier),
            Summarize(baselineRun), Summarize(simulatedRun), Diff(baselineRun, simulatedRun),
            JsonSerializer.Serialize(BuildCriteria(simulated, pref, simulatedRun), StoredJson),
            JsonSerializer.Serialize(BuildItems(simulated.BudgetMaxUsd, simulatedRun), StoredJson));
    }

    // Bản sao hồ sơ với giá trị giả định (chỉ các trường mà công thức đọc). Giá trị ngoài khoảng cho phép thì ném ArgumentException.
    private static StudentProfile Overlay(StudentProfile profile, SimulationOverrides o)
    {
        if (o.Gpa is < 0 or > 4) throw new ArgumentException("GPA giả định phải từ 0 đến 4.");
        if (o.Sat is < 400 or > 1600) throw new ArgumentException("SAT giả định phải từ 400 đến 1600.");
        if (o.Act is < 1 or > 36) throw new ArgumentException("ACT giả định phải từ 1 đến 36.");
        if (o.Ielts is < 0 or > 9) throw new ArgumentException("IELTS giả định phải từ 0 đến 9.");
        if (o.BudgetUsd is < 0) throw new ArgumentException("Ngân sách giả định không được âm.");
        if (o.ScholarshipNeed is not null && !ProfileConstants.ScholarshipNeeds.All.Contains(o.ScholarshipNeed))
            throw new ArgumentException("Mức cần học bổng giả định không hợp lệ.");
        if (o.EcTier is < 1 or > 5) throw new ArgumentException("Bậc ngoại khóa giả định phải từ 1 đến 5.");

        return new StudentProfile
        {
            Id = profile.Id,
            UserId = profile.UserId,
            TargetLevel = profile.TargetLevel,
            IntendedMajor = profile.IntendedMajor,
            PreferredStates = profile.PreferredStates,
            TargetSchoolType = profile.TargetSchoolType,
            OverallGpa = o.Gpa ?? profile.OverallGpa,
            Sat = o.Sat ?? profile.Sat,
            Act = o.Act ?? profile.Act,
            Ielts = o.Ielts ?? profile.Ielts,
            Toefl = profile.Toefl,
            Duolingo = profile.Duolingo,
            BudgetMaxUsd = o.BudgetUsd ?? profile.BudgetMaxUsd,
            ScholarshipNeed = o.ScholarshipNeed ?? profile.ScholarshipNeed
        };
    }

    // Lọc cứng rồi chấm: phần thuần dùng chung cho mô phỏng (không đọc ngoại khóa, không lưu).
    private static RecommendationRun Evaluate(
        StudentProfile profile, IReadOnlyList<int>? pref, IReadOnlyList<RecommendSchoolInput> schools, int tier)
    {
        var (filtered, kept) = FilterSchools(profile, schools);
        return kept.Count == 0
            ? new RecommendationRun(tier, filtered, [], null)
            : Compute(profile, pref, schools, filtered, kept, tier);
    }

    private static GroupSummary Summarize(RecommendationRun run)
    {
        int Count(SchoolGroup group) => run.Scored.Count(s => s.Filter.Outcome == FilterOutcome.Keep && s.Grade.Group == group);
        return new GroupSummary(
            run.Scored.Count,
            Count(SchoolGroup.Reach), Count(SchoolGroup.Match), Count(SchoolGroup.Safety),
            run.Scored.Count(s => s.Filter.Outcome == FilterOutcome.AidBlock),
            Count(SchoolGroup.NoData));
    }

    // Trạng thái của một trường: nhóm (R, M, S, NA), AID nếu cần học bổng lớn, OUT nếu bị loại.
    private static string Status(ScoredSchool? school) =>
        school is null ? "OUT"
        : school.Filter.Outcome == FilterOutcome.AidBlock ? "AID"
        : GroupCode(school.Grade.Group);

    private static IReadOnlyList<SchoolChange> Diff(RecommendationRun baseline, RecommendationRun simulated)
    {
        var before = baseline.Scored.ToDictionary(s => s.School.University.Code, StringComparer.OrdinalIgnoreCase);
        var after = simulated.Scored.ToDictionary(s => s.School.University.Code, StringComparer.OrdinalIgnoreCase);
        return before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.Ordinal)
            .Select(code =>
            {
                before.TryGetValue(code, out var b);
                after.TryGetValue(code, out var a);
                var name = (a ?? b)!.School.University.Name;
                return new SchoolChange(code, name, Status(b), Status(a));
            })
            .Where(change => change.From != change.To)
            .ToList();
    }

    private static readonly string[] BlockNames = ["reach", "match", "safety", "aid", "other"];
    // Mở chi tiết một trường: Qwen viết lời tư vấn từ số đã lưu. Không lọc, không chấm lại.
    public async Task<SchoolExplanation> ExplainAsync(Guid userId, string code, CancellationToken ct)
    {
        var last = await recommendations.GetLatestAsync(userId, ct)
            ?? throw new InvalidOperationException("Chưa có lần gợi ý nào.");
        var profile = await profiles.GetProfileByUserIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Chưa có hồ sơ.");

        var criteria = JsonNode.Parse(last.CriteriaJson)!.AsObject();
        var items = JsonNode.Parse(last.ItemsJson)!.AsObject();

        var scored = items["scored"]!.AsArray()
            .FirstOrDefault(row => string.Equals(row!["code"]?.GetValue<string>(), code, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Chưa có trường {code} trong lần gợi ý gần nhất.");
        var school = await recommendations.FindSchoolAsync(scored["code"]!.GetValue<string>(), last.StudyLevel, ct)
            ?? throw new InvalidOperationException($"Chưa tìm thấy thông tin trường {code}.");

        var university = school.University;
        var offering = school.Offering;
        var student = new ExplainStudentDto(
            ToDouble(profile.OverallGpa),
            SatScore(profile.Sat),
            ToDouble(profile.BudgetMaxUsd),
            criteria["tier"]?.GetValue<int>(),
            profile.IntendedMajor);
        var target = new ExplainSchoolDto(
            university.Name,
            university.State,
            university.Control,
            scored["g2"]!.GetValue<string>(),
            scored["outcome"]!.GetValue<string>() == nameof(FilterOutcome.AidBlock) ? "AID" : "MAIN",
            StoredWhy(items, university.Code),
            ToDouble(university.AcceptanceRate),
            offering.Sat25,
            offering.Sat75,
            Num(scored, "pg"),
            Num(scored, "ps"),
            ToDouble(TotalCost(offering)),
            Num(scored, "gap"));

        var result = await advisor.ExplainAsync(new ExplainRequestDto(student, target), ct);
        return new SchoolExplanation(university.Code, university.Name, result.Status,
            result.Summary, result.Strengths, result.Improvements);
    }

    // Mã lý do của một trường: nằm ở khối nào thì lấy `why` ở khối đó.
    private static List<string> StoredWhy(JsonObject items, string code)
    {
        foreach (var name in BlockNames)
        {
            if (items[name] is not JsonArray block) continue;
            foreach (var entry in block)
            {
                if (!string.Equals(entry?["code"]?.GetValue<string>(), code, StringComparison.OrdinalIgnoreCase)) continue;
                return entry!["why"]?.AsArray().Select(w => w!.GetValue<string>()).ToList() ?? [];
            }
        }
        return [];
    }


    public Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct) =>
        recommendations.GetLatestAsync(userId, ct);

    private static readonly JsonSerializerOptions StoredJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Chuyển kết quả thành JSON và lưu bảng. Giữ nguyên khuôn JSON mà frontend và chat đang đọc.
    private async Task<RecommendationRun> SaveAsync(
        StudentProfile profile,
        IReadOnlyList<int>? pref,
        IReadOnlyList<RecommendSchoolInput> schools,
        RecommendationRun run,
        CancellationToken ct)
    {
        var criteria = BuildCriteria(profile, pref, run);
        var items = BuildItems(profile.BudgetMaxUsd, run);

        await recommendations.AddAsync(new Recommendation
        {
            UserId = profile.UserId,
            StudentProfileId = profile.Id,
            StudyLevel = string.IsNullOrWhiteSpace(profile.TargetLevel) ? "undergraduate" : profile.TargetLevel,
            CriteriaJson = JsonSerializer.Serialize(criteria, StoredJson),
            ItemsJson = JsonSerializer.Serialize(items, StoredJson),
            AlgorithmVersion = AlgorithmVersion
        }, ct);

        return run;
    }

    // Tiêu chí và kết quả của một lượt, theo khuôn JSON mà frontend và chat đọc.
    private static object BuildCriteria(StudentProfile profile, IReadOnlyList<int>? pref, RecommendationRun run) => new
    {
        major = profile.IntendedMajor,
        states = profile.PreferredStates,
        budget = profile.BudgetMaxUsd,
        scholarshipNeed = profile.ScholarshipNeed,
        pref = NormalizePref(pref),
        tier = run.Tier,
        algorithm = AlgorithmVersion,
        status = run.Picked is null ? null : "OK"
    };

    private static object BuildItems(decimal? budget, RecommendationRun run) => new
    {
        scored = run.Scored.Select(item => ScoredJson(item, budget)).ToList(),
        failed = Array.Empty<string>(),
        reach = Block(run.Picked?.Reach, run.Scored),
        match = Block(run.Picked?.Match, run.Scored),
        safety = Block(run.Picked?.Safety, run.Scored),
        aid = Block(run.Picked?.Aid, run.Scored),
        other = Block(run.Picked?.Other, run.Scored)
    };

    // Một trường đã chấm, theo khuôn cũ của frontend: pg, ps, s, adj theo thang 0–100; f, h, q theo thang 0–1.
    private static object ScoredJson(ScoredSchool item, decimal? budget)
    {
        var school = item.School;
        var grade = item.Grade;
        var fit = item.Fit;
        var cost = TotalCost(school.Offering);
        return new
        {
            universityId = school.University.Id,
            code = school.University.Code,
            name = school.University.Name,
            state = school.University.State,
            control = school.University.Control,
            website = school.University.Website,
            cost,
            outcome = item.Filter.Outcome.ToString(),
            flags = item.Filter.Flags,
            g2 = GroupCode(grade.Group),
            s = Scale100(grade.PAdjusted),
            f = Round(fit?.Budget),
            h = Round(fit?.Aid),
            q = Round(fit?.Quality),
            gap = budget is decimal b && cost is decimal c && c > b ? c - b : (decimal?)null,
            pg = Scale100(grade.Pg),
            ps = Scale100(grade.Ps),
            adj = grade.P is double p && grade.PAdjusted is double pa ? Math.Round((pa - p) * 100, 1) : (double?)null,
            r = IntlRatio(school),
            acceptRate = Round(ToDouble(AcceptRate(school))),
            a = Round(fit?.Position),
            e = Round(fit?.English),
            fit = Round(fit?.Value),
            reasons = grade.Reasons
        };
    }

    // Một khối: mã trường, điểm xếp hạng (fit) và mã lý do để frontend giải thích.
    private static IReadOnlyList<object> Block(IReadOnlyList<RankedSchool>? entries, IReadOnlyList<ScoredSchool> scored) =>
        entries?.Select(e => (object)new
        {
            code = e.Code,
            x = Round(e.Fit?.Value),
            why = WhyCodes(scored.First(s => string.Equals(s.School.University.Code, e.Code, StringComparison.OrdinalIgnoreCase)))
        }).ToList()
        ?? [];

    // Mã lý do hiển thị cho từng trường, suy từ số đã tính (cùng bộ mã frontend đang dùng).
    private static List<string> WhyCodes(ScoredSchool item)
    {
        var grade = item.Grade;
        var why = new List<string>();
        // Luật theo tỷ lệ nhận quyết định nhóm nên nói trước các lý do về điểm
        if (grade.Reasons.Contains(GroupReasons.LowAcceptRate)) why.Add("LOW_RATE");
        if (grade.Reasons.Contains(GroupReasons.CappedSelective)) why.Add("RATE_CAP");
        if (grade.Reasons.Contains(GroupReasons.CappedWeakMetric)) why.Add("WEAK_CAP");
        if (grade.Pg is double pg)
            why.Add(pg >= 0.75 ? "GPA_HIGH" : pg >= 0.25 ? "GPA_MID" : "GPA_LOW");
        if (grade.Ps is double ps)
            why.Add(ps >= 0.75 ? "SAT_ABOVE" : ps >= 0.25 ? "SAT_IN_RANGE" : "SAT_BELOW");
        if (item.Filter.Outcome == FilterOutcome.AidBlock) why.Add("NEED_AID");
        else if (TotalCost(item.School.Offering) is not null) why.Add("BUDGET_OK");
        if (grade.Reasons.Contains(GroupReasons.IntlDowngrade)) why.Add("INTL_LOW");
        if (grade.P is double p && grade.PAdjusted is double pa)
        {
            if (pa - p > 0.005) why.Add("EC_PLUS");
            else if (pa - p < -0.005) why.Add("EC_MINUS");
        }
        return why;
    }

    // Nhóm theo mã cũ: R khó vào · M vừa sức · S an toàn · NA thiếu dữ liệu.
    private static string GroupCode(SchoolGroup group) => group switch
    {
        SchoolGroup.Reach => "R",
        SchoolGroup.Match => "M",
        SchoolGroup.Safety => "S",
        _ => "NA"
    };

    private static double? Scale100(double? value) => value is double v ? Math.Round(v * 100, 1) : null;
    private static double? Round(double? value) => value is double v ? Math.Round(v, 4) : null;

    // Tỉ lệ nhận du học sinh trên tỉ lệ nhận chung; dưới 0,5 thì trường bị hạ một nhóm.
    private static double? IntlRatio(RecommendSchoolInput school)
    {
        var rate = AcceptRate(school);
        var intl = IntlAcceptRate(school);
        return rate is decimal r && r > 0 && intl is decimal i ? Math.Round((double)(i / r), 3) : null;
    }

    private static FilterSchool ToFilterSchool(RecommendSchoolInput input)
    {
        var offering = input.Offering;
        return new FilterSchool(
            input.University.Code,
            input.Majors,
            input.University.State,
            input.University.Control,
            TotalCost(offering),
            HasIntlAid(offering.IntlAidPolicy),
            offering.MinIelts,
            offering.MinToefl,
            offering.MinDuolingo);
    }

    // Số liệu học thuật của trường cho công thức xếp nhóm. Tỉ lệ nhận thiếu thì tính từ số được nhận / số nộp.
    private static SchoolAcademics ToSchoolAcademics(RecommendSchoolInput input)
    {
        var offering = input.Offering;
        return new SchoolAcademics(
            offering.AvgGpa4,
            GpaDist(offering),
            offering.Sat25, offering.Sat75,
            offering.Act25, offering.Act75,
            offering.SatPolicy, offering.SatSubmitPct,
            offering.C7GpaImportance, offering.C7TestImportance, offering.C7EcImportance,
            AcceptRate(input), IntlAcceptRate(input));
    }

    // Số liệu của trường cho fit score. Chất lượng tính từ hạng trong các bảng xếp hạng chung; chưa có hạng thì bỏ thành phần Q.
    private static FitSchool ToFitSchool(RecommendSchoolInput input)
    {
        var offering = input.Offering;
        return new FitSchool(
            TotalCost(offering), offering.IntlAidAvgUsd, offering.MinIelts, SchoolRanker.QualityFromRanks(input.Rankings));
    }

    // Đọc một số từ JSON đã lưu (dùng khi mở chi tiết một trường).
    private static double? Num(JsonNode row, string key) =>
        row[key] is JsonValue value && value.TryGetValue<double>(out var number) ? number : null;

    private static decimal? AcceptRate(RecommendSchoolInput input) =>
        input.University.AcceptanceRate ?? Rate(input.Offering.AdmittedTotal, input.Offering.ApplicantsTotal);

    private static decimal? IntlAcceptRate(RecommendSchoolInput input) =>
        input.Offering.IntlAcceptanceRate ?? Rate(input.Offering.IntlAdmitted, input.Offering.IntlApplicants);

    private static decimal? Rate(int? admitted, int? applicants) =>
        admitted is int a && applicants is int n && n > 0 ? (decimal)a / n : null;

    // Học phí null thì tổng null. Phần còn lại thiếu thì cộng 0.
    private static decimal? TotalCost(UniversityOffering offering)
    {
        if (offering.TuitionUsd is not decimal tuition) return null;
        return tuition
            + (offering.LivingUsd ?? 0)
            + (offering.FeesUsd ?? 0)
            + (offering.OtherExpensesUsd ?? 0);
    }
    // "none" là không có. Chưa khai thì null để lọc cứng gắn cờ, không loại.
    private static bool? HasIntlAid(string? policy) => policy switch
    {
        null or "" => null,
        "none" => false,
        _ => true
    };
    // Đủ 7 khoảng mới dùng. Thứ tự: dưới 2.50 → đúng 4.0.
    private static IReadOnlyList<decimal>? GpaDist(UniversityOffering offering)
    {
        decimal?[] bins =
        [
            offering.GpaPctBelow250,
            offering.GpaPct250To299,
            offering.GpaPct300To324,
            offering.GpaPct325To349,
            offering.GpaPct350To374,
            offering.GpaPct375To399,
            offering.GpaPct40
        ];
        return bins.All(b => b is not null) ? bins.Select(b => b!.Value).ToList() : null;
    }
    private static string? SchoolControl(string? targetType) =>
        string.IsNullOrWhiteSpace(targetType) || targetType.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? null
            : targetType;
    private static AidNeed ToAidNeed(string? scholarshipNeed) => scholarshipNeed switch
    {
        ProfileConstants.ScholarshipNeeds.Mandatory => AidNeed.Required,
        ProfileConstants.ScholarshipNeeds.Preferred => AidNeed.Preferred,
        _ => AidNeed.None
    };
    private static IReadOnlyList<int> NormalizePref(IReadOnlyList<int>? pref) =>
        pref is { Count: 3 } && pref.All(n => n is >= 0 and <= 4) ? pref : BalancedPref;
    private static int? SatScore(decimal? sat) =>
        sat is decimal score && score is >= 400 and <= 1600 ? (int)score : null;
    private static int? ActScore(decimal? act) =>
        act is decimal score && score is >= 1 and <= 36 ? (int)score : null;
    private static double? ToDouble(decimal? value) => value is decimal number ? (double)number : null;
}
