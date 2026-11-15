namespace StudyAbroad.Application.Recommendations;

// ============================== TÌM TRƯỜNG THEO ĐIỀU KIỆN ==============================
// "Trường nào ở California có học bổng cho du học sinh". Không dùng hồ sơ học sinh: ai hỏi cùng điều kiện cũng nhận cùng danh sách.
// Điều kiện đã được advisor chuẩn hóa (mã bang, tên ngành trong danh mục, USD, tỉ lệ 0..1), ở đây chỉ lọc và sắp xếp bằng số liệu thật.

// Mọi điều kiện đều tùy chọn; điều kiện nào có thì trường phải thỏa (và có số liệu để kiểm: thiếu số liệu thì không nhận vào).
// States: mã bang, trường ở một trong các bang. Control: public | private. Aid: any | need | merit.
// TestPolicy: optional (test-optional hoặc không nhận SAT, ACT) | required. Các mức tiếng Anh là mức TỐI THIỂU trường yêu cầu không được vượt.
// MinAcceptRate, MaxAcceptRate, MinGradRate là tỉ lệ 0..1. MaxRank: có hạng từ mức này trở lên trong một bảng xếp hạng chung.
public record SchoolSearchFilters(
    IReadOnlyList<string>? States = null,
    string? City = null,
    string? Major = null,
    string? Control = null,
    string? Aid = null,
    string? TestPolicy = null,
    decimal? MaxCostUsd = null,
    decimal? MaxIelts = null,
    decimal? MaxToefl = null,
    decimal? MaxDuolingo = null,
    decimal? MinAcceptRate = null,
    decimal? MaxAcceptRate = null,
    int? MaxRank = null,
    decimal? MinGradRate = null,
    bool Ivy = false);

// Quality: điểm chất lượng 0..1 từ hạng trong các bảng chung (null nếu chưa có hạng); BestRank: hạng tốt nhất trong các bảng chung
public record SchoolSearchItem(
    string Code, string Name, string? City, string? State, string? Control,
    decimal? CostUsd, decimal? AcceptRate, string? AidPolicy, decimal? AidAvgUsd, double? Quality, int? BestRank);

public enum SchoolSort
{
    Quality,
    LowestCost,
    EasiestAdmission,
    MostSelective,
}

// Total: số trường thỏa điều kiện. Items: tối đa MaxItems trường đầu theo cách sắp xếp (đủ để giao diện mở "Xem thêm").
// Relaxed: khi không có trường nào, mỗi điều kiện đứng riêng nếu bỏ đi thì ra bao nhiêu trường (để gợi ý nới điều kiện nào); rỗng khi có kết quả.
public record SchoolSearchResult(int Total, IReadOnlyList<SchoolSearchItem> Items, IReadOnlyList<RelaxedFilter> Relaxed);

public record RelaxedFilter(string Filter, int Total);

public partial class RecommendationService
{
    public const int MaxSearchItems = 100;

    public async Task<SchoolSearchResult> SearchSchoolsAsync(
        Guid userId, SchoolSearchFilters filters, SchoolSort sort, CancellationToken ct)
    {
        var profile = await profiles.GetProfileByUserIdAsync(userId, ct);
        var level = string.IsNullOrWhiteSpace(profile?.TargetLevel) ? "undergraduate" : profile!.TargetLevel!;
        var schools = await recommendations.ListSchoolsAsync(level, ct);
        return Search(schools, filters, sort);
    }

    // Mỗi điều kiện là một phép thử có tên (khóa của hợp đồng với model), để đếm "bỏ điều kiện nào thì ra bao nhiêu trường"
    private static List<(string Key, Func<RecommendSchoolInput, bool> Test)> SearchTests(SchoolSearchFilters f)
    {
        var tests = new List<(string, Func<RecommendSchoolInput, bool>)>();
        if (f.States is { Count: > 0 })
            tests.Add(("bang", s => f.States.Contains(s.University.State ?? "", StringComparer.OrdinalIgnoreCase)));
        if (!string.IsNullOrWhiteSpace(f.City))
        {
            var city = SchoolNameMatcher.Normalize(f.City);
            tests.Add(("thanh_pho", s => SchoolNameMatcher.Normalize(s.University.City).Contains(city, StringComparison.Ordinal)));
        }
        if (!string.IsNullOrWhiteSpace(f.Major))
            tests.Add(("nganh", s => s.Majors.Contains(f.Major, StringComparer.OrdinalIgnoreCase)));   // không có danh sách ngành: không xác nhận được
        if (!string.IsNullOrWhiteSpace(f.Control))
            tests.Add(("loai_truong", s => string.Equals(s.University.Control, f.Control, StringComparison.OrdinalIgnoreCase)));
        if (!string.IsNullOrWhiteSpace(f.Aid))
            tests.Add(("hoc_bong", s => AidMatches(s.Offering.IntlAidPolicy, f.Aid)));
        if (!string.IsNullOrWhiteSpace(f.TestPolicy))
            tests.Add(("chinh_sach_thi", s => f.TestPolicy == "optional"
                ? s.Offering.SatPolicy is "optional" or "not_accepted"
                : s.Offering.SatPolicy == f.TestPolicy));
        if (f.MaxCostUsd is { } maxCost)
            tests.Add(("chi_phi_toi_da", s => TotalCost(s.Offering) is { } cost && cost <= maxCost));
        if (f.MaxIelts is { } ielts)
            tests.Add(("ielts_toi_da", s => s.Offering.MinIelts is { } min && min <= ielts));
        if (f.MaxToefl is { } toefl)
            tests.Add(("toefl_toi_da", s => s.Offering.MinToefl is { } min && min <= toefl));
        if (f.MaxDuolingo is { } duolingo)
            tests.Add(("duolingo_toi_da", s => s.Offering.MinDuolingo is { } min && min <= duolingo));
        if (f.MinAcceptRate is { } minRate)
            tests.Add(("ty_le_nhan_toi_thieu", s => AcceptRate(s) is { } rate && rate >= minRate));
        if (f.MaxAcceptRate is { } maxRate)
            tests.Add(("ty_le_nhan_toi_da", s => AcceptRate(s) is { } rate && rate <= maxRate));
        if (f.MaxRank is { } maxRank)
            tests.Add(("xep_hang_toi_da", s => s.Rankings is { Count: > 0 } && s.Rankings.Any(r => r.Rank <= maxRank)));
        if (f.MinGradRate is { } minGrad)
            tests.Add(("ty_le_tot_nghiep_toi_thieu", s => s.Offering.GradRate6Yr is { } grad && grad >= minGrad));
        if (f.Ivy)
            tests.Add(("ivy", s => s.University.IsIvy));
        return tests;
    }

    // any: có học bổng cho du học sinh (không phải "none", và có khai báo). need: theo nhu cầu. merit: theo thành tích
    private static bool AidMatches(string? policy, string wanted) => wanted switch
    {
        "need" => policy is "need_only" or "need_and_merit",
        "merit" => policy is "merit_only" or "need_and_merit",
        _ => policy is "need_only" or "merit_only" or "need_and_merit",
    };

    private static SchoolSearchResult Search(IReadOnlyList<RecommendSchoolInput> schools, SchoolSearchFilters filters, SchoolSort sort)
    {
        var tests = SearchTests(filters);
        var matched = schools.Where(s => tests.All(t => t.Test(s))).ToList();
        var relaxed = matched.Count == 0 && tests.Count > 1
            ? tests
                .Select(skip => new RelaxedFilter(skip.Key, schools.Count(s => tests.Where(t => t.Key != skip.Key).All(t => t.Test(s)))))
                .Where(r => r.Total > 0)
                .OrderByDescending(r => r.Total)
                .ToList()
            : [];
        var items = matched.Select(ToSearchItem);
        items = sort switch
        {
            SchoolSort.LowestCost => items.OrderBy(i => i.CostUsd is null).ThenBy(i => i.CostUsd).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
            SchoolSort.EasiestAdmission => items.OrderBy(i => i.AcceptRate is null).ThenByDescending(i => i.AcceptRate).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
            SchoolSort.MostSelective => items.OrderBy(i => i.AcceptRate is null).ThenBy(i => i.AcceptRate).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
            _ => items.OrderBy(i => i.Quality is null).ThenByDescending(i => i.Quality).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
        };
        return new SchoolSearchResult(matched.Count, items.Take(MaxSearchItems).ToList(), relaxed);
    }

    private static SchoolSearchItem ToSearchItem(RecommendSchoolInput s) => new(
        s.University.Code, s.University.Name, s.University.City, s.University.State, s.University.Control,
        TotalCost(s.Offering), AcceptRate(s), s.Offering.IntlAidPolicy, s.Offering.IntlAidAvgUsd,
        SchoolRanker.QualityFromRanks(s.Rankings),
        s.Rankings is { Count: > 0 } ranks ? ranks.Min(r => r.Rank) : null);
}
