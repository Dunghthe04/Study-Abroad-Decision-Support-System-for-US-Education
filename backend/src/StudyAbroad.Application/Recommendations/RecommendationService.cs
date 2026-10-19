using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System.Text.Json;

namespace StudyAbroad.Application.Recommendations;

//Một trường đã đưa vào gợi ý: hồ sơ trường + số liệu 
public record RecommendSchoolInput(University University, UniversityOffering Offering);

//Kết quả mỗi lượt. Score và Pick null khi không còn trường để gửi cloud
public record RecommendationRun(
    int Tier,
    bool EcAiUsed,
    IReadOnlyList<FilterResult> Filters,
    ScoreResponseDto? Score,
    PickResponseDto? Pick
);

public class RecommendationService(IAdvisorClient advisor, IRecommendationRepository recommendations, IProfileFlowRepository profiles)

{
    public const decimal BudgetTolerance = 0.10m;
    private static readonly int[] BalancedPref = [2, 2, 2];

    public async Task<RecommendationRun> RecommendAsync(
        StudentProfile profile,
        IReadOnlyList<ProfileActivity> activities,
        IReadOnlyList<RecommendSchoolInput> schools,
        IReadOnlyList<int>? pref = null,
        CancellationToken ct = default
    )
    {
        var criteria = new FilterCriteria(
         profile.IntendedMajor,
         profile.PreferredStates,
         SchoolControl(profile.TargetSchoolType),
         profile.BudgetMaxUsd,
         ToAidNeed(profile.ScholarshipNeed),
         profile.Ielts,
         profile.Toefl,
         profile.Duolingo);

        
        var filtered = HardFilter.Apply(criteria, schools.Select(ToFilterSchool), BudgetTolerance);
        // Những trường được giữ
        var kept = filtered.Where(r=> r.Outcome != FilterOutcome.Excluded).ToList();
        //
        var ec = await advisor.ReadEcAsync(ToEcRequest(activities), ct);
        CloudResultCheck.EnsureTier(ec.Tier);

        if (kept.Count == 0)
            return await SaveAsync(profile, pref, schools, new RecommendationRun(ec.Tier, ec.AiUsed, filtered, null, null), ct);

        //Chuyển thành dict key : code -> value trường
        var byCode = kept.ToDictionary(r => r.School.Code, StringComparer.OrdinalIgnoreCase);

        var scoreSchools = schools
            .Where(s => byCode.ContainsKey(s.University.Code))
            .Select(ToScoreSchool)
            .ToList();

        var student = new ScoreStudentDto(
            profile.OverallGpa is decimal gpa ? (double)gpa : null,
            SatScore(profile.Sat),
            profile.BudgetMaxUsd is decimal budget ? (double)budget : null,
            ec.Tier);

        //Tính điểm trường
        var score = await advisor.ScoreAsync(new ScoreRequestDto(student, scoreSchools), ct);
        CloudResultCheck.EnsureScore(byCode.Keys, score);

        var pickItems = score.Items
            .Select(item => ToPickSchool(item, byCode[item.C].Outcome))
            .ToList();
        if (pickItems.Count == 0)
            return await SaveAsync(profile, pref, schools, new RecommendationRun(ec.Tier, ec.AiUsed, filtered, score, null), ct);

        //Chọn các trường
        var needAid = ToNeedAid(profile.ScholarshipNeed);
        var pick = await advisor.PickAsync(
           new PickRequestDto(NormalizePref(pref), needAid, pickItems),
           ct);
        CloudResultCheck.EnsurePick(pickItems, needAid, pick);
        return await SaveAsync(profile, pref, schools, new RecommendationRun(ec.Tier, ec.AiUsed, filtered, score, pick), ct);
    }

    // Hồ sơ đã gửi thì lọc, chấm, chọn và lưu. Trả về đúng dòng vừa ghi.
    public async Task<Recommendation> CreateAsync(Guid userId, IReadOnlyList<int>? pref, CancellationToken ct)
    {
        var profile = await profiles.GetProfileByUserIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Chưa có hồ sơ.");
        if (profile.FlowSubmittedAt is null)
            throw new InvalidOperationException("Hồ sơ chưa được gửi ở bước 6.");
        if (string.IsNullOrWhiteSpace(profile.TargetLevel))
            throw new InvalidOperationException("Hồ sơ chưa có bậc học muốn theo.");

        var activities = await profiles.GetProfileActivitiesAsync(profile.Id, ct);
        var schools = await recommendations.ListSchoolsAsync(profile.TargetLevel, ct);
        await RecommendAsync(profile, activities, schools, pref, ct);

        return await recommendations.GetLatestAsync(userId, ct)
            ?? throw new InvalidOperationException("Không lưu được kết quả gợi ý.");
    }

    public Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct) =>
        recommendations.GetLatestAsync(userId, ct);

    private static readonly JsonSerializerOptions StoredJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Ghi đúng số cloud đã trả. Không tính lại.
    private async Task<RecommendationRun> SaveAsync(
        StudentProfile profile,
        IReadOnlyList<int>? pref,
        IReadOnlyList<RecommendSchoolInput> schools,
        RecommendationRun run,
        CancellationToken ct)
    {
        var version = run.Pick?.SkillVersion ?? run.Score?.SkillVersion ?? "ec";
        if (version.Length > 32) version = version[..32];

        var universityIds = schools
            .GroupBy(s => s.University.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().University.Id, StringComparer.OrdinalIgnoreCase);
        var filters = run.Filters.ToDictionary(f => f.School.Code, StringComparer.OrdinalIgnoreCase);

        var criteria = new
        {
            major = profile.IntendedMajor,
            states = profile.PreferredStates,
            budget = profile.BudgetMaxUsd,
            scholarshipNeed = profile.ScholarshipNeed,
            pref = pref is { Count: 3 } ? pref : BalancedPref,
            tier = run.Tier,
            ecAiUsed = run.EcAiUsed,
            scoreSkill = run.Score?.SkillVersion,
            scoreModel = run.Score?.Model,
            pickSkill = run.Pick?.SkillVersion,
            pickModel = run.Pick?.Model,
            status = run.Pick?.Status
        };

        var items = new
        {
            scored = (run.Score?.Items ?? []).Select(item => new
            {
                universityId = universityIds.GetValueOrDefault(item.C),
                code = item.C,
                outcome = filters[item.C].Outcome.ToString(),
                flags = filters[item.C].Flags,
                g2 = item.G2,
                s = item.S,
                f = item.F,
                h = item.H,
                q = item.Q,
                gap = item.Gap,
                pg = item.Pg,
                ps = item.Ps,
                adj = item.Adj,
                r = item.R
            }).ToList(),
            failed = run.Score?.Failed ?? [],
            reach = Block(run.Pick?.Reach),
            match = Block(run.Pick?.Match),
            safety = Block(run.Pick?.Safety),
            aid = Block(run.Pick?.Aid),
            other = Block(run.Pick?.Other)
        };

        await recommendations.AddAsync(new Recommendation
        {
            UserId = profile.UserId,
            StudentProfileId = profile.Id,
            StudyLevel = string.IsNullOrWhiteSpace(profile.TargetLevel) ? "undergraduate" : profile.TargetLevel,
            CriteriaJson = JsonSerializer.Serialize(criteria, StoredJson),
            ItemsJson = JsonSerializer.Serialize(items, StoredJson),
            AlgorithmVersion = version
        }, ct);

        return run;
    }

    private static IReadOnlyList<object> Block(IReadOnlyList<PickEntryDto>? entries) =>
        entries?.Select(e => (object)new { code = e.C, x = e.X, why = e.Why }).ToList()
        ?? [];
    private static FilterSchool ToFilterSchool(RecommendSchoolInput input)
    {
        var offering = input.Offering;
        return new FilterSchool(
            input.University.Code,
            offering.Majors,
            input.University.State,
            input.University.Control,
            TotalCost(offering),
            HasIntlAid(offering.IntlAidPolicy),
            offering.MinIelts,
            offering.MinToefl,
            offering.MinDuolingo);
    }
    private static ScoreSchoolDto ToScoreSchool(RecommendSchoolInput input)
    {
        var university = input.University;
        var offering = input.Offering;
        return new ScoreSchoolDto(
            university.Code,
            ToDouble(university.AcceptanceRate),
            ToDouble(offering.IntlAcceptanceRate),
            SatBand(offering),
            ToDouble(offering.SatSubmitPct),
            SatPolicy(offering.SatPolicy),
            GpaBand(offering),
            ToDouble(offering.AvgGpa4),
            [
                C7Level(offering.C7GpaImportance),
                C7Level(offering.C7TestImportance),
                C7Level(offering.C7EcImportance)
            ],
            ToDouble(TotalCost(offering)),
            ToDouble(offering.IntlAidAvgUsd),
            Quality(offering));
    }
    // Chép số cloud đã tính. Không tính lại F, H, Q, X.
    private static PickSchoolDto ToPickSchool(ScoreItemDto item, FilterOutcome outcome) =>
        new(
            item.C,
            item.G2,
            outcome == FilterOutcome.AidBlock ? "AID" : "MAIN",
            item.F,
            item.H,
            item.Q,
            item.Gap,
            item.Pg,
            item.Ps,
            item.Adj,
            item.R);
    private static EcRequestDto ToEcRequest(IReadOnlyList<ProfileActivity> activities)
    {
        var act = activities
            .Where(a => !string.Equals(a.Kind, "award", StringComparison.OrdinalIgnoreCase))
            .Where(a => a.ImpactLevel is >= 1 and <= 5 && !string.IsNullOrWhiteSpace(a.Title))
            .Take(10)
            .Select(a => new EcActivityDto(Clip(a.Title, a.Description, 500), a.ImpactLevel!.Value, Months(a), Role(a.Role)))
            .ToList();
        var awards = activities
            .Where(a => string.Equals(a.Kind, "award", StringComparison.OrdinalIgnoreCase))
            .Where(a => a.ImpactLevel is >= 1 and <= 5 && !string.IsNullOrWhiteSpace(a.Title))
            .Take(10)
            .Select(a => new EcAwardDto(Clip(a.Title, a.Description, 300), a.ImpactLevel!.Value))
            .ToList();
        return new EcRequestDto(act, awards);
    }
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
    // Skill chỉ chép q. Có cả hai tỷ lệ thì lấy trung bình; có một thì dùng tỷ lệ đó.
    private static double? Quality(UniversityOffering offering)
    {
        var rates = new[] { offering.RetentionRate1Yr, offering.GradRate6Yr }
            .Where(r => r is not null)
            .Select(r => (double)r!.Value)
            .ToList();
        return rates.Count == 0 ? null : rates.Average();
    }
    // Thiếu một mốc thì không gửi mảng, để cloud coi là thiếu dữ liệu.
    private static IReadOnlyList<int>? SatBand(UniversityOffering offering) =>
        offering.Sat25 is int low && offering.Sat75 is int high ? [low, high] : null;
    // Đủ 7 khoảng mới gửi. Thứ tự: dưới 2.50 → 4.0.
    private static IReadOnlyList<double>? GpaBand(UniversityOffering offering)
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
        return bins.All(b => b is not null) ? bins.Select(b => (double)b!.Value).ToList() : null;
    }
    private static int C7Level(string? importance) => importance switch
    {
        "very_important" => 3,
        "important" => 2,
        "considered" => 1,
        _ => 0
    };
    private static string? SatPolicy(string? policy) => policy switch
    {
        "required" => "req",
        "optional" => "opt",
        "not_accepted" => "no",
        _ => null
    };
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
    private static string ToNeedAid(string? scholarshipNeed) => scholarshipNeed switch
    {
        ProfileConstants.ScholarshipNeeds.Mandatory => "required",
        ProfileConstants.ScholarshipNeeds.Preferred => "preferred",
        _ => "no"
    };
    private static IReadOnlyList<int> NormalizePref(IReadOnlyList<int>? pref) =>
        pref is { Count: 3 } && pref.All(n => n is >= 0 and <= 4) ? pref : BalancedPref;
    private static int? SatScore(decimal? sat) =>
        sat is decimal score && score is >= 400 and <= 1600 ? (int)score : null;
    private static double? ToDouble(decimal? value) => value is decimal number ? (double)number : null;
    private static string? Role(string? role) =>
        role is not null && ProfileConstants.ActivityRoles.All.Contains(role) ? role : null;
    private static int Months(ProfileActivity activity)
    {
        if (activity.DurationMonths is int months) return Math.Clamp(months, 0, 120);
        if (activity.StartDate is not DateOnly start) return 0;
        var end = activity.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var count = (end.Year - start.Year) * 12 + end.Month - start.Month;
        return Math.Clamp(count, 0, 120);
    }
    private static string Clip(string title, string? description, int max)
    {
        var text = string.IsNullOrWhiteSpace(description) ? title.Trim() : $"{title.Trim()}. {description.Trim()}";
        return text.Length <= max ? text : text[..max];
    }
}
