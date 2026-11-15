using System.Text.Json;
using System.Text.Json.Nodes;

namespace StudyAbroad.Application.Recommendations;

// ============================== THÔNG TIN MỘT TRƯỜNG ==============================
// Số liệu thật của một trường để chat trả lời "học phí Harvard bao nhiêu", "UCLA có học bổng không"...
// Không phụ thuộc hồ sơ học sinh và không cần AI: code đọc cơ sở dữ liệu, nên không bịa.

// Chi phí mỗi năm (USD). Total null khi trường chưa có học phí (xem TotalCost).
public record SchoolCostInfo(decimal? Tuition, decimal? Living, decimal? Fees, decimal? Other, decimal? Total);

// Mức tiếng Anh tối thiểu của trường (null nếu trường không công bố bài thi đó)
public record SchoolEnglishInfo(decimal? MinIelts, decimal? MinToefl, decimal? MinDuolingo);

// Điểm thi. SatPolicy: required | optional | not_accepted. Mốc SAT sai thang (điểm một phần thi) đã bỏ, xem SatScale.
public record SchoolTestInfo(string? SatPolicy, int? Sat25, int? Sat75, int? Act25, int? Act75);

// Tỉ lệ nhận (0..1), chung và của du học sinh
public record SchoolAdmissionInfo(decimal? AcceptRate, decimal? IntlAcceptRate);

// Học bổng cho du học sinh. Policy: none | need_only | merit_only | need_and_merit.
// AvgUsd là mức trung bình của người ĐƯỢC NHẬN học bổng, không phải mức học sinh sẽ nhận.
public record SchoolAidInfo(string? Policy, decimal? AvgUsd, int? Recipients);

public record SchoolOutcomeInfo(decimal? RetentionRate1Yr, decimal? GradRate6Yr);

// GPA của sinh viên đã trúng tuyển (thang 4): trung bình, tỉ lệ có GPA 4,0 và tỉ lệ có GPA từ 3,75 trở lên (0..1)
public record SchoolGpaInfo(decimal? Avg, decimal? ShareFour, decimal? ShareAbove375);

// Một vòng nộp hồ sơ. Round là mã vòng (ED, EA, RD, Spring...), Date là chữ gốc trong dữ liệu (thường yyyy-MM-dd).
public record SchoolDeadline(string Round, string Date);

// Hạng của trường trong một bảng xếp hạng (ví dụ U.S. News 2027 National Universities). RankDisplay là hạng để hiển thị ("#34"), IsTie khi đồng hạng.
public record SchoolRanking(string ListName, string? Group, int? Year, int Rank, string RankDisplay, bool IsTie);

public record SchoolInfoDto(
    string Code,
    string Name,
    string? City,
    string? State,
    string? Control,
    string? Website,
    SchoolCostInfo Cost,
    SchoolEnglishInfo English,
    SchoolTestInfo Test,
    SchoolAdmissionInfo Admission,
    SchoolAidInfo Aid,
    SchoolOutcomeInfo Outcomes,
    IReadOnlyList<SchoolDeadline> Deadlines,
    IReadOnlyList<string> Majors,
    IReadOnlyList<SchoolRanking> Rankings,
    string? SourceUrl,
    DateOnly? RetrievedAt,
    SchoolGpaInfo? Gpa = null);

public partial class RecommendationService
{
    // Thông tin một trường theo mã. Bậc học lấy theo hồ sơ (chưa có thì đại học), giống FindSchoolsAsync.
    public async Task<SchoolInfoDto> SchoolInfoAsync(Guid userId, string code, CancellationToken ct)
    {
        var profile = await profiles.GetProfileByUserIdAsync(userId, ct);
        var level = string.IsNullOrWhiteSpace(profile?.TargetLevel) ? "undergraduate" : profile!.TargetLevel!;
        var schools = await recommendations.ListSchoolsAsync(level, ct);
        var school = schools.FirstOrDefault(s => string.Equals(s.University.Code, code, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Chưa có trường {code} trong dữ liệu.");
        var rankings = await recommendations.ListSchoolRankingsAsync(school.University.Id, ct);
        return ToSchoolInfo(school, rankings);
    }

    public async Task<IReadOnlyList<SchoolRef>> FindMentionedSchoolsAsync(Guid userId, string text, CancellationToken ct)
    {
        var profile = await profiles.GetProfileByUserIdAsync(userId, ct);
        var level = string.IsNullOrWhiteSpace(profile?.TargetLevel) ? "undergraduate" : profile!.TargetLevel!;
        var schools = await recommendations.ListSchoolsAsync(level, ct);
        return SchoolNameMatcher.FindMentioned(text, schools.Select(s => new SchoolRef(s.University.Code, s.University.Name)).ToList());
    }

    private static SchoolInfoDto ToSchoolInfo(RecommendSchoolInput input, IReadOnlyList<SchoolRanking> rankings)
    {
        var u = input.University;
        var o = input.Offering;
        var (sat25, sat75) = SatScale.Clean(o.Sat25, o.Sat75);
        return new SchoolInfoDto(
            u.Code, u.Name, u.City, u.State, u.Control, u.Website,
            new SchoolCostInfo(o.TuitionUsd, o.LivingUsd, o.FeesUsd, o.OtherExpensesUsd, TotalCost(o)),
            new SchoolEnglishInfo(o.MinIelts, o.MinToefl, o.MinDuolingo),
            new SchoolTestInfo(o.SatPolicy, sat25, sat75, o.Act25, o.Act75),
            new SchoolAdmissionInfo(AcceptRate(input), IntlAcceptRate(input)),
            new SchoolAidInfo(o.IntlAidPolicy, o.IntlAidAvgUsd, o.IntlAidRecipients),
            new SchoolOutcomeInfo(o.RetentionRate1Yr, o.GradRate6Yr),
            ParseDeadlines(o.DeadlinesJson),
            input.Majors,
            rankings,
            o.SourceUrl, o.RetrievedAt,
            new SchoolGpaInfo(o.AvgGpa4, o.GpaPct40, o.GpaPct375To399 is { } a && o.GpaPct40 is { } b ? a + b : null));
    }

    // DeadlinesJson dạng {"ED":"2026-11-01","RD":"2027-01-05"}. Hỏng hoặc không đúng dạng thì coi như không có hạn nộp, không làm hỏng câu trả lời.
    public static IReadOnlyList<SchoolDeadline> ParseDeadlines(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            if (JsonNode.Parse(json) is not JsonObject obj)
                return [];
            return obj
                .Where(kv => kv.Value is JsonValue v && v.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                .Select(kv => new SchoolDeadline(kv.Key, kv.Value!.GetValue<string>()))
                .OrderBy(d => d.Date, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return [];
        }
    }
}
