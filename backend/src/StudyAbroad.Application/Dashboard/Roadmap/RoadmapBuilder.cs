using System.Text.Json;
using System.Text.RegularExpressions;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Dashboard.Roadmap;

/// <summary>Mốc trung bình các trường (cùng cách tính với ChatBenchmarks) cho chế độ average.</summary>
public record RoadmapAverageMarks(decimal? Gpa4, IReadOnlyDictionary<string, decimal> English);

/// <summary>Toàn bộ dữ liệu đã đọc từ DB; RoadmapBuilder chỉ tính, không đọc gì thêm.</summary>
public record RoadmapInputs(
    StudentProfile Profile,
    IReadOnlyList<ProfileCertification> Certifications,
    IReadOnlyList<TargetSchoolDto> Targets,
    IReadOnlyDictionary<Guid, RoadmapOfferingRow> Offerings,
    IReadOnlyDictionary<Guid, UniversityCostRow> Costs,
    IReadOnlyList<RoadmapScholarshipRow> Scholarships,
    RoadmapAverageMarks? Average);

/// <summary>
/// Luật tính lộ trình (kế hoạch mục 6.1–6.3). Hàm thuần, ngày hôm nay truyền vào để test được.
/// Hằng số mốc giống skill_lo_trinh.py; khi advisor nhận roadmap thì dùng số ở đây, không tự tính lại.
/// </summary>
public static partial class RoadmapBuilder
{
    public const int MaxSchools = 5;
    public const decimal GpaNear = 0.1m;
    public const int TestLeadMonths = 2;
    public const int VisaLeadMonths = 3;

    private static readonly Dictionary<string, int> IntakeMonth = new() { ["Fall"] = 8, ["Spring"] = 1, ["Summer"] = 6 };
    // (lệch năm so với năm nhập học, tháng) của hạn Regular Decision ước chừng
    private static readonly Dictionary<string, (int Year, int Month)> RdDeadline = new()
    {
        ["Fall"] = (0, 1), ["Spring"] = (-1, 10), ["Summer"] = (0, 3),
    };
    private static readonly (int Year, int Month) EarlyDeadline = (-1, 11);

    private static readonly string[] EnglishTypes = ["ielts", "toefl", "duolingo"];
    private static readonly Dictionary<string, string> EnglishNames = new()
    {
        ["ielts"] = "IELTS", ["toefl"] = "TOEFL iBT", ["duolingo"] = "Duolingo",
    };
    private static readonly Dictionary<string, string[]> EnglishServices = new()
    {
        ["ielts"] = ["LUYEN_THI_IELTS"], ["toefl"] = ["LUYEN_THI"], ["duolingo"] = ["LUYEN_THI"],
    };
    private static readonly HashSet<string> SubmittedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "submitted", "admitted", "rejected",
    };

    [GeneratedRegex(@"^(Fall|Spring|Summer)\s+(\d{4})$", RegexOptions.IgnoreCase)]
    private static partial Regex IntakePattern();

    public static RoadmapDto Build(RoadmapInputs input, DateOnly today)
    {
        var profile = input.Profile;
        var (term, intakeYear, intakeEstimate) = ParseIntake(profile.TargetIntake, today, HighSchoolGraduationYear(profile, today));
        var intakeDate = new DateOnly(intakeYear, IntakeMonth[term], 1);
        var scores = RealScores(input.Certifications);
        var targets = input.Targets.Take(MaxSchools).ToList();
        var mode = targets.Count > 0 ? "target" : "average";

        // 1. Mức sẵn sàng
        var schools = new List<RoadmapSchoolDto>();
        if (mode == "target")
        {
            foreach (var t in targets)
            {
                input.Offerings.TryGetValue(t.UniversityId, out var offering);
                input.Costs.TryGetValue(t.UniversityId, out var cost);
                var criteria = new List<RoadmapCriterionDto>
                {
                    GpaCriterion(profile.OverallGpa, offering?.AvgGpa4),
                    EnglishCriterion(scores, input.Certifications, offering),
                };
                if (SatCriterion(scores, input.Certifications, offering) is { } sat) criteria.Add(sat);
                criteria.Add(BudgetCriterion(cost?.TotalCostUsd, profile.BudgetMaxUsd));

                var scholarships = input.Scholarships
                    .Where(s => s.UniversityId == t.UniversityId && (s.Deadline is null || s.Deadline >= today))
                    .Select(s => new RoadmapScholarshipDto(s.Id, s.Name, s.Deadline, ScholarshipEligible(s, profile.OverallGpa, scores)))
                    .ToList();
                schools.Add(School(t.UniversityId, t.UniversityName, t.Category, criteria, scholarships));
            }
        }
        else if (input.Average is { } avg)
        {
            var avgOffering = new RoadmapOfferingRow(Guid.Empty, avg.Gpa4,
                avg.English.GetValueOrDefault("ielts"), avg.English.GetValueOrDefault("toefl"),
                avg.English.GetValueOrDefault("duolingo"), null, null, null, null);
            schools.Add(School(null, "TB các trường", null,
                [GpaCriterion(profile.OverallGpa, avg.Gpa4), EnglishCriterion(scores, input.Certifications, ZeroToNull(avgOffering))],
                []));
        }

        var ready = schools.Count(s => s.KnownCount > 0 && s.MetCount == s.KnownCount);
        var summary = mode == "target"
            ? $"Đạt đủ điều kiện {ready}/{schools.Count} trường mục tiêu"
            : "Chưa có trường mục tiêu, đang so với trung bình các trường";

        // 2. Mốc thời gian
        var milestones = new List<RoadmapMilestoneDto>();
        foreach (var t in targets)
        {
            input.Offerings.TryGetValue(t.UniversityId, out var offering);
            var (date, round, estimate) = ResolveDeadline(t, offering?.DeadlinesJson, term, intakeYear);
            milestones.Add(new RoadmapMilestoneDto(date, "deadline", $"Hạn nộp {t.UniversityName}", round, t.UniversityId, estimate));
        }
        if (mode == "average")
        {
            var (y, m) = RdDeadline[term];
            milestones.Add(new RoadmapMilestoneDto(new DateOnly(intakeYear + y, m, 1), "deadline",
                "Hạn nộp hồ sơ (vòng Regular, ước chừng)", "RD", null, true));
        }
        var earliestDeadline = milestones.Where(m => m.Kind == "deadline").Min(m => m.Date);

        foreach (var school in schools)
            foreach (var s in school.Scholarships.Where(s => s.Eligible && s.Deadline is not null))
                milestones.Add(new RoadmapMilestoneDto(s.Deadline!.Value, "scholarship", $"Hạn học bổng {s.Name}", null, school.UniversityId, false));

        var testDue = earliestDeadline.AddMonths(-TestLeadMonths);
        var visaDue = intakeDate.AddMonths(-VisaLeadMonths);

        // 3. Nhiệm vụ
        var tasks = new List<RoadmapTaskDto>();

        if (mode == "average")
            tasks.Add(MakeTask("target:choose", "Chọn trường mục tiêu", null, false, true, today, [], []));

        foreach (var type in EnglishTypes)
        {
            var marks = schools
                .Select(s => (s.UniversityId, Criterion: s.Criteria.FirstOrDefault(c => c.Key == "english" && c.Type == type)))
                .Where(x => x.Criterion?.Mark is not null)
                .ToList();
            if (marks.Count == 0) continue;
            var goal = marks.Max(x => x.Criterion!.Mark!.Value);
            var mine = scores.GetValueOrDefault(type);
            AddTestTasks(tasks, milestones, input.Certifications, type, EnglishNames[type], goal, mine,
                marks.Select(x => x.UniversityId).OfType<Guid>().ToList(), EnglishServices[type], "english_test",
                testDue, today);
        }

        var satMarks = schools
            .Select(s => (s.UniversityId, Criterion: s.Criteria.FirstOrDefault(c => c.Key == "sat")))
            .Where(x => x.Criterion?.Mark is not null)
            .ToList();
        if (satMarks.Count > 0)
        {
            AddTestTasks(tasks, milestones, input.Certifications, "sat", "SAT", satMarks.Max(x => x.Criterion!.Mark!.Value),
                scores.GetValueOrDefault("sat"), satMarks.Select(x => x.UniversityId).OfType<Guid>().ToList(),
                ["LUYEN_THI_SAT"], "sat_test", testDue, today);
        }

        foreach (var t in targets)
        {
            var deadline = milestones.First(m => m.Kind == "deadline" && m.UniversityId == t.UniversityId);
            var roundText = deadline.Round is null ? "" : $" ({deadline.Round})";
            tasks.Add(MakeTask($"apply:{t.UniversityId}", $"Nộp hồ sơ {t.UniversityName}{roundText}", deadline.Date,
                SubmittedStatuses.Contains(t.Status), true, today, [t.UniversityId], []));
        }

        foreach (var school in schools)
            foreach (var s in school.Scholarships.Where(s => s.Eligible))
                tasks.Add(MakeTask($"scholarship:{s.Id}", $"Nộp học bổng {s.Name}", s.Deadline, false, false, today,
                    school.UniversityId is { } id ? [id] : [], ["SAN_HOC_BONG"]));

        var schoolIds = targets.Select(t => t.UniversityId).ToList();
        tasks.Add(MakeTask("essay", "Hoàn thiện bài luận", earliestDeadline, false, false, today, schoolIds, ["SUA_BAI_LUAN"]));
        tasks.Add(MakeTask("visa", "Chuẩn bị hồ sơ visa và phỏng vấn", visaDue, false, false, today, schoolIds,
            ["HO_SO_VISA", "LUYEN_PHONG_VAN", "CHUNG_MINH_TAI_CHINH"]));

        milestones.Add(new RoadmapMilestoneDto(visaDue, "visa", "Nộp hồ sơ visa", null, null, true));
        milestones.Add(new RoadmapMilestoneDto(intakeDate, "intake", $"Nhập học {term} {intakeYear}", null, null, intakeEstimate));

        var checkable = tasks.Where(t => t.Checkable).ToList();
        return new RoadmapDto(
            mode,
            $"{term} {intakeYear}",
            intakeEstimate,
            schools,
            summary,
            milestones.OrderBy(m => m.Date).ToList(),
            tasks.OrderBy(t => t.Due ?? DateOnly.MaxValue).ToList(),
            new RoadmapProgressDto(checkable.Count(t => t.Status == "done"), checkable.Count),
            string.IsNullOrWhiteSpace(profile.Province),
            string.IsNullOrWhiteSpace(profile.Province) ? null : profile.Province.Trim());
    }

    // ---------- Mức sẵn sàng ----------

    public static RoadmapCriterionDto GpaCriterion(decimal? mine, decimal? mark)
    {
        if (mine is not > 0 || mark is null) return new("gpa", null, mine is > 0 ? mine : null, mark, "unknown");
        var diff = Math.Round(mine.Value - mark.Value, 2);
        var status = diff >= 0 ? "met" : -diff <= GpaNear ? "near" : "below";
        return new("gpa", null, mine, mark, status);
    }

    // So với Min* của đúng loại chứng chỉ học sinh có, không quy đổi. Chưa có điểm thật thì lấy loại học sinh
    // định thi (chứng chỉ mục tiêu), không có nữa thì loại đầu tiên trường có mốc; mine = null -> unknown
    public static RoadmapCriterionDto EnglishCriterion(
        IReadOnlyDictionary<string, decimal> scores, IReadOnlyList<ProfileCertification> certs, RoadmapOfferingRow? offering)
    {
        foreach (var type in EnglishTypes)
        {
            if (!scores.TryGetValue(type, out var mine)) continue;
            var mark = EnglishMark(offering, type);
            if (mark is null) continue;
            return new("english", type, mine, mark, mine >= mark ? "met" : "below");
        }

        var planned = certs.Where(c => c.IsTarget).Select(c => c.TestType.ToLowerInvariant()).FirstOrDefault(EnglishTypes.Contains);
        var fallback = planned is not null && EnglishMark(offering, planned) is not null
            ? planned
            : EnglishTypes.FirstOrDefault(t => EnglishMark(offering, t) is not null);
        if (fallback is null)
        {
            var anyType = EnglishTypes.FirstOrDefault(scores.ContainsKey);
            return new("english", anyType, anyType is null ? null : scores[anyType], null, "unknown");
        }
        return new("english", fallback, null, EnglishMark(offering, fallback), "unknown");
    }

    // Chỉ xét SAT khi học sinh có điểm thật hoặc đã đặt mục tiêu SAT; trường không nhận SAT thì bỏ.
    // Mốc = trung điểm 25–75 (làm tròn 10); đạt mốc -> met, từ Sat25 -> near, dưới -> below
    public static RoadmapCriterionDto? SatCriterion(
        IReadOnlyDictionary<string, decimal> scores, IReadOnlyList<ProfileCertification> certs, RoadmapOfferingRow? offering)
    {
        var hasReal = scores.TryGetValue("sat", out var mine);
        var planned = certs.Any(c => c.IsTarget && string.Equals(c.TestType, "sat", StringComparison.OrdinalIgnoreCase));
        if (!hasReal && !planned) return null;
        if (string.Equals(offering?.SatPolicy, "not_accepted", StringComparison.OrdinalIgnoreCase)) return null;

        if (offering?.Sat25 is not { } low || offering.Sat75 is not { } high)
            return new("sat", "sat", hasReal ? mine : null, null, "unknown");
        var mark = Math.Round((low + high) / 2m / 10m, MidpointRounding.AwayFromZero) * 10m;
        if (!hasReal) return new("sat", "sat", null, mark, "unknown");
        var status = mine >= mark ? "met" : mine >= low ? "near" : "below";
        return new("sat", "sat", mine, mark, status);
    }

    public static RoadmapCriterionDto BudgetCriterion(decimal? cost, decimal? budget)
    {
        if (cost is not > 0 || budget is not > 0) return new("budget", null, budget, cost, "unknown");
        return new("budget", null, budget, cost, cost <= budget ? "met" : "below");
    }

    // Đạt khi mọi Min* có ghi đều có điểm thật đạt; thiếu điểm của một Min* có ghi thì chưa đạt
    public static bool ScholarshipEligible(RoadmapScholarshipRow s, decimal? gpa4, IReadOnlyDictionary<string, decimal> scores)
    {
        static bool Ok(decimal? min, decimal? mine) => min is null || (mine is not null && mine >= min);
        return Ok(s.MinGpa4, gpa4 is > 0 ? gpa4 : null)
            && Ok(s.MinSat, scores.TryGetValue("sat", out var sat) ? sat : null)
            && EnglishOk(s, scores);
    }

    private static bool EnglishOk(RoadmapScholarshipRow s, IReadOnlyDictionary<string, decimal> scores)
    {
        var mins = new Dictionary<string, decimal?> { ["ielts"] = s.MinIelts, ["toefl"] = s.MinToefl, ["duolingo"] = s.MinDuolingo }
            .Where(kv => kv.Value is not null).ToList();
        if (mins.Count == 0) return true;
        // học bổng thường nhận một trong các chứng chỉ: đạt một loại là đủ
        return mins.Any(kv => scores.TryGetValue(kv.Key, out var mine) && mine >= kv.Value);
    }

    private static RoadmapSchoolDto School(Guid? id, string name, string? category,
        List<RoadmapCriterionDto> criteria, List<RoadmapScholarshipDto> scholarships) =>
        new(id, name, category, criteria,
            criteria.Count(c => c.Status == "met"),
            criteria.Count(c => c.Status != "unknown"),
            scholarships);

    private static decimal? EnglishMark(RoadmapOfferingRow? o, string type) => type switch
    {
        "ielts" => o?.MinIelts,
        "toefl" => o?.MinToefl,
        "duolingo" => o?.MinDuolingo,
        _ => null,
    };

    private static RoadmapOfferingRow ZeroToNull(RoadmapOfferingRow o) => o with
    {
        MinIelts = o.MinIelts is > 0 ? o.MinIelts : null,
        MinToefl = o.MinToefl is > 0 ? o.MinToefl : null,
        MinDuolingo = o.MinDuolingo is > 0 ? o.MinDuolingo : null,
    };

    // Điểm thật (không phải mục tiêu) cao nhất của từng loại
    public static IReadOnlyDictionary<string, decimal> RealScores(IReadOnlyList<ProfileCertification> certs) =>
        certs.Where(c => !c.IsTarget && c.OverallScore is > 0)
            .GroupBy(c => c.TestType.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.Max(c => c.OverallScore!.Value));

    // ---------- Nhiệm vụ ----------

    // "Đạt X" tự tích khi điểm thật >= mốc cao nhất các trường; "Đăng ký thi" chỉ sinh khi chưa có điểm thật,
    // tự tích khi đã có ngày thi dự kiến. Có ngày thi thì mốc thi lấy ngày đó, không thì hạn nộp sớm nhất - 2 tháng
    private static void AddTestTasks(List<RoadmapTaskDto> tasks, List<RoadmapMilestoneDto> milestones,
        IReadOnlyList<ProfileCertification> certs, string type, string name, decimal goal, decimal mine,
        List<Guid> schoolIds, string[] services, string milestoneKind, DateOnly defaultDue, DateOnly today)
    {
        var plannedDate = certs
            .Where(c => c.IsTarget && c.TestDate is not null && string.Equals(c.TestType, type, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.TestDate)
            .Min();
        var due = plannedDate ?? defaultDue;
        var reached = mine > 0 && mine >= goal;

        tasks.Add(MakeTask(type == "sat" ? "sat" : $"english:{type}", $"Đạt {name} {goal:0.##}", due, reached, true, today, schoolIds, services));
        if (mine <= 0)
            tasks.Add(MakeTask($"register:{type}", $"Đăng ký thi {name}", due, plannedDate is not null, true, today, schoolIds, []));
        if (!reached)
            milestones.Add(new RoadmapMilestoneDto(due, milestoneKind, $"Thi {name}", null, null, plannedDate is null));
    }

    private static RoadmapTaskDto MakeTask(string id, string label, DateOnly? due, bool done, bool checkable, DateOnly today,
        IReadOnlyList<Guid> schoolIds, IReadOnlyList<string> services)
    {
        var status = done ? "done" : checkable && due is not null && due < today ? "overdue" : "todo";
        return new RoadmapTaskDto(id, label, due, status, checkable, schoolIds, services, []);
    }

    // ---------- Mốc ----------

    public static (string Term, int Year, bool Estimate) ParseIntake(string? intake, DateOnly today,
        int? highSchoolGraduationYear = null)
    {
        var match = IntakePattern().Match(intake?.Trim() ?? "");
        if (match.Success)
        {
            var term = char.ToUpperInvariant(match.Groups[1].Value[0]) + match.Groups[1].Value[1..].ToLowerInvariant();
            return (term, int.Parse(match.Groups[2].Value), false);
        }
        // chưa chọn kỳ: ước chừng Fall của năm tốt nghiệp THPT; không biết hoặc đã tới năm đó thì Fall năm sau
        return ("Fall", highSchoolGraduationYear is { } year && year > today.Year ? year : today.Year + 1, true);
    }

    // Năm tốt nghiệp THPT của học sinh còn đang học phổ thông. Hồ sơ cấp THCS ghi năm tốt nghiệp là của THCS
    // nên đoán lại theo lớp tính tới lớp 12. Cấp học khác trả null
    private static int? HighSchoolGraduationYear(StudentProfile profile, DateOnly today)
    {
        var now = today.ToDateTime(TimeOnly.MinValue);
        return profile.CurrentEducationLevel switch
        {
            "high_school" => ChatProfileMapper.EffectiveGraduationYear("high_school", profile.CurrentGrade,
                profile.GraduationYear, now),
            "middle_school" => ChatProfileMapper.GuessGraduationYear("high_school", profile.CurrentGrade, now),
            _ => null,
        };
    }

    /// <summary>
    /// Hạn nộp: TargetSchool.Deadline -> DeadlinesJson của trường (vòng đã chọn, không có thì RD) -> ước chừng.
    /// Ngày trong DeadlinesJson giữ tháng/ngày, đổi năm theo kỳ nhập học: tháng trước tháng nhập học thì cùng năm, còn lại năm trước.
    /// </summary>
    public static (DateOnly Date, string? Round, bool Estimate) ResolveDeadline(
        TargetSchoolDto target, string? deadlinesJson, string term, int intakeYear)
    {
        var chosen = string.IsNullOrWhiteSpace(target.ApplicationRound) ? null : TargetSchoolService.CanonicalRound(target.ApplicationRound);
        if (target.Deadline is { } fixedDate)
            return (DateOnly.FromDateTime(fixedDate), chosen, false);

        var rounds = ParseDeadlines(deadlinesJson);
        var key = chosen is not null && rounds.ContainsKey(chosen) ? chosen : rounds.ContainsKey("RD") ? "RD" : null;
        if (key is not null)
        {
            var raw = rounds[key];
            var year = raw.Month < IntakeMonth[term] ? intakeYear : intakeYear - 1;
            var day = Math.Min(raw.Day, DateTime.DaysInMonth(year, raw.Month));
            return (new DateOnly(year, raw.Month, day), key, false);
        }

        var early = chosen is "ED" or "ED2" or "EA" or "REA" && term == "Fall";
        var (yOff, month) = early ? EarlyDeadline : RdDeadline[term];
        return (new DateOnly(intakeYear + yOff, month, 1), chosen ?? "RD", true);
    }

    private static Dictionary<string, DateOnly> ParseDeadlines(string? json)
    {
        var result = new Dictionary<string, DateOnly>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String) continue;
                if (!DateOnly.TryParse(prop.Value.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var date)) continue;
                if (TargetSchoolService.CanonicalRound(prop.Name) is { } key)
                    result.TryAdd(key, date);
            }
        }
        catch (JsonException)
        {
            // dữ liệu trường sai định dạng thì coi như không có, dùng mốc ước chừng
        }
        return result;
    }
}
