using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Recommendations;

// ============================== KẾT QUẢ TRẢ CHO FRONTEND ==============================

// Một thay đổi. Factor: sat | gpa | ecTier. From null nếu hồ sơ chưa có.
public record GoalChangeDto(string Factor, decimal? From, decimal To);

// Difficulty: easy | hard | veryHard
public record GoalPlanDto(IReadOnlyList<GoalChangeDto> Changes, double Effort, string Difficulty);

// Tài chính của trường với hồ sơ thật. Status:
//   ok            chi phí nằm trong ngân sách (kể cả dung sai 10%)
//   need_aid      vượt ngân sách nhưng trường có học bổng quốc tế: nằm ở khối "cần học bổng lớn"
//   over_budget   vượt ngân sách và không có học bổng hoặc học sinh không cần: trường bị loại khỏi danh sách
//   unknown_cost  trường chưa có số liệu chi phí
//   no_budget     hồ sơ chưa có ngân sách
// MinBudgetUsd: ngân sách mỗi năm tối thiểu để trường không bị chặn (đã tính dung sai). ShortfallUsd: chi phí còn thiếu so với mức đó.
public record GoalBudgetDto(
    string Status, decimal? CostUsd, decimal? BudgetUsd, decimal? MinBudgetUsd, decimal? ShortfallUsd, decimal? IntlAidAvgUsd);

// Trường tương tự đang ở đúng nhóm đích, gợi ý khi trường mục tiêu không đạt được.
public record GoalAlternativeDto(string Code, string Name, string? State, string? Control, decimal? CostUsd, string Group);

// Kết quả goal-seeking cho một trường. Current, Target, Best: R | M | S | NA (chưa đủ dữ liệu).
// Outcome: already_reached | reachable | blocked | no_data.
// Cheapest: mức tối thiểu ít công sức nhất. Recommended: mức nên đặt mục tiêu (có đệm, an toàn hơn khi số liệu có sai số); null nếu không có đệm.
// FilterReason: trường không nằm trong danh sách vì bộ lọc ngoài ngân sách (MAJOR | STATE | CONTROL), null nếu không bị loại.
public record SchoolGoalResult(
    string Code,
    string Name,
    string Current,
    string Target,
    string Outcome,
    string Best,
    IReadOnlyList<string> Reasons,
    GoalPlanDto? Cheapest,
    GoalPlanDto? Recommended,
    IReadOnlyList<GoalPlanDto> Alone,
    IReadOnlyList<string> NoEffect,
    GoalBudgetDto Budget,
    string? FilterReason,
    IReadOnlyList<GoalAlternativeDto> Alternatives);

// ============================== GOAL-SEEKING ==============================

// Một trường trong ô chọn trường của chat: mã, tên, bang
public record SchoolOption(string Code, string Name, string? State);

public partial class RecommendationService
{
    // Đệm cho "mức nên đặt mục tiêu": p' phải vượt ngưỡng nhóm thêm chừng này. Số liệu của trường và hồ sơ tự khai đều có sai số.
    public const double GoalMargin = 0.05;
    public const int MaxAlternatives = 3;
    private const decimal MoneyStep = 100m;

    // Danh sách trường (theo tên) cho ô chọn có gợi ý trong chat: giao diện tải một lần rồi lọc ngay trên máy khi học sinh gõ
    public async Task<IReadOnlyList<SchoolOption>> ListSchoolOptionsAsync(Guid userId, CancellationToken ct)
    {
        var profile = await profiles.GetProfileByUserIdAsync(userId, ct);
        var level = string.IsNullOrWhiteSpace(profile?.TargetLevel) ? "undergraduate" : profile!.TargetLevel!;
        var schools = await recommendations.ListSchoolsAsync(level, ct);
        return schools
            .Select(s => new SchoolOption(s.University.Code, s.University.Name, s.University.State))
            .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Khớp tên trường học sinh gõ trong chat với danh sách trường của bậc học họ theo. Không bao giờ đoán bừa: xem SchoolNameMatcher.
    public async Task<NameMatch> FindSchoolsAsync(Guid userId, string text, CancellationToken ct)
    {
        var profile = await profiles.GetProfileByUserIdAsync(userId, ct);
        var level = string.IsNullOrWhiteSpace(profile?.TargetLevel) ? "undergraduate" : profile!.TargetLevel!;
        var schools = await recommendations.ListSchoolsAsync(level, ct);
        return SchoolNameMatcher.Match(text, schools.Select(s => new SchoolRef(s.University.Code, s.University.Name)).ToList());
    }

    // Học sinh hỏi "cần gì để vào trường này". target: reach | match | safety; bỏ trống thì nhóm cao hơn một bậc so với hiện tại.
    // Tính từ hồ sơ thật, giữ các yếu tố khác nguyên. Không lưu, không gọi advisor.
    public async Task<SchoolGoalResult> GoalSeekAsync(Guid userId, string code, string? target, CancellationToken ct)
    {
        var profile = await profiles.GetProfileByUserIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Chưa có hồ sơ.");
        if (string.IsNullOrWhiteSpace(profile.TargetLevel))
            throw new InvalidOperationException("Hồ sơ chưa có bậc học muốn theo.");
        EnsureRunnable(profile);

        var tier = EcTierOf(profile);
        var schools = await recommendations.ListSchoolsAsync(profile.TargetLevel, ct);
        var school = schools.FirstOrDefault(s => string.Equals(s.University.Code, code, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Chưa có trường {code} trong dữ liệu.");

        var student = new StudentAcademics(profile.OverallGpa, SatScore(profile.Sat), ActScore(profile.Act), tier);
        var academics = ToSchoolAcademics(school);
        var current = SchoolGrouper.Grade(student, academics).Group;
        var goal = ParseTarget(target, current);

        var result = GoalSeeker.Seek(student, academics, goal);

        // Mức nên đặt mục tiêu: cùng tìm kiếm nhưng ngưỡng nhóm cao hơn một khoảng đệm
        GoalPlanDto? recommended = null;
        if (result.Outcome == GoalOutcome.Reachable)
        {
            var d = new GroupingOptions();
            var padded = GoalSeeker.Seek(
                student, academics, goal, d with { LowerCut = d.LowerCut + GoalMargin, UpperCut = d.UpperCut + GoalMargin });
            recommended = padded.Outcome == GoalOutcome.Reachable ? ToDto(padded.Cheapest) : null;
        }

        var criteria = CriteriaOf(profile);
        var filter = HardFilter.Check(criteria, ToFilterSchool(school), BudgetTolerance);
        var budget = BudgetOf(profile, school, filter);

        var alternatives = result.Outcome is GoalOutcome.Blocked or GoalOutcome.NoData
            ? Alternatives(profile, schools, school, goal, tier)
            : [];

        return new SchoolGoalResult(
            school.University.Code, school.University.Name,
            GroupCode(result.Current), GroupCode(goal), OutcomeCode(result.Outcome), GroupCode(result.Best),
            result.Reasons,
            ToDto(result.Cheapest), recommended,
            result.Alone.Select(p => ToDto(p)!).ToList(),
            result.NoEffect.Select(FactorCode).ToList(),
            budget, NonBudgetFilter(filter), alternatives);
    }

    // Nhóm đích học sinh chọn. Bỏ trống: Khó vào hoặc chưa đủ dữ liệu thì Vừa sức, Vừa sức thì An toàn.
    private static SchoolGroup ParseTarget(string? target, SchoolGroup current) => target?.Trim().ToLowerInvariant() switch
    {
        null or "" => current switch { SchoolGroup.Match or SchoolGroup.Safety => SchoolGroup.Safety, _ => SchoolGroup.Match },
        "reach" => SchoolGroup.Reach,
        "match" => SchoolGroup.Match,
        "safety" => SchoolGroup.Safety,
        _ => throw new ArgumentException("Nhóm đích phải là reach, match hoặc safety."),
    };

    // Chi phí của trường so với ngân sách. Dùng đúng bộ lọc cứng của lượt gợi ý nên kết luận khớp với trang gợi ý.
    private static GoalBudgetDto BudgetOf(StudentProfile profile, RecommendSchoolInput school, FilterResult filter)
    {
        var cost = TotalCost(school.Offering);
        var aid = school.Offering.IntlAidAvgUsd;
        var budget = profile.BudgetMaxUsd;
        if (cost is not decimal c)
            return new GoalBudgetDto("unknown_cost", null, budget, null, null, aid);
        if (budget is not decimal b)
            return new GoalBudgetDto("no_budget", c, null, MinBudget(c), null, aid);

        var ceiling = b * (1 + BudgetTolerance);
        var shortfall = c > ceiling ? Math.Round(c - ceiling, 0) : (decimal?)null;
        var status = filter.Outcome switch
        {
            FilterOutcome.Keep => "ok",
            FilterOutcome.AidBlock => "need_aid",
            _ when c <= ceiling => "ok",   // bị loại vì lý do khác, chi phí vẫn trong ngân sách
            _ => "over_budget",
        };
        return new GoalBudgetDto(status, c, b, MinBudget(c), shortfall, aid);
    }

    // Ngân sách tối thiểu để trường không bị chặn: chi phí chia (1 + dung sai), làm tròn lên trăm USD.
    private static decimal MinBudget(decimal cost) => Math.Ceiling(cost / (1 + BudgetTolerance) / MoneyStep) * MoneyStep;

    // Lý do trường không có trong danh sách mà không phải ngân sách (ngành, bang, loại trường)
    private static string? NonBudgetFilter(FilterResult filter) =>
        filter.Outcome == FilterOutcome.Excluded && filter.ExcludeReason is "MAJOR" or "STATE" or "CONTROL" ? filter.ExcludeReason : null;

    // Trường tương tự đang ở đúng nhóm đích với hồ sơ thật: cùng loại (công/tư) trước, rồi chi phí gần nhất, rồi fit score cao nhất.
    private static IReadOnlyList<GoalAlternativeDto> Alternatives(
        StudentProfile profile, IReadOnlyList<RecommendSchoolInput> schools, RecommendSchoolInput school, SchoolGroup goal, int tier)
    {
        var run = Evaluate(profile, null, schools, tier);
        var cost = TotalCost(school.Offering);
        var control = school.University.Control;

        double Gap(ScoredSchool s) =>
            cost is decimal mine && TotalCost(s.School.Offering) is decimal other ? (double)Math.Abs(other - mine) : double.MaxValue;

        return run.Scored
            .Where(s => s.Filter.Outcome == FilterOutcome.Keep && s.Grade.Group == goal
                        && !string.Equals(s.School.University.Code, school.University.Code, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => string.Equals(s.School.University.Control, control, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(Gap)
            .ThenByDescending(s => s.Fit?.Value ?? 0)
            .ThenBy(s => s.School.University.Code, StringComparer.Ordinal)
            .Take(MaxAlternatives)
            .Select(s => new GoalAlternativeDto(
                s.School.University.Code, s.School.University.Name, s.School.University.State, s.School.University.Control,
                TotalCost(s.School.Offering), GroupCode(s.Grade.Group)))
            .ToList();
    }

    private static GoalPlanDto? ToDto(GoalPlan? plan) => plan is null ? null : new GoalPlanDto(
        plan.Changes.Select(c => new GoalChangeDto(FactorCode(c.Factor), c.From, c.To)).ToList(),
        plan.Effort,
        plan.Difficulty switch { GoalDifficulty.Easy => "easy", GoalDifficulty.Hard => "hard", _ => "veryHard" });

    private static string FactorCode(GoalFactor factor) => factor switch
    {
        GoalFactor.Sat => "sat",
        GoalFactor.Gpa => "gpa",
        _ => "ecTier",
    };

    private static string OutcomeCode(GoalOutcome outcome) => outcome switch
    {
        GoalOutcome.AlreadyReached => "already_reached",
        GoalOutcome.Reachable => "reachable",
        GoalOutcome.Blocked => "blocked",
        _ => "no_data",
    };
}
