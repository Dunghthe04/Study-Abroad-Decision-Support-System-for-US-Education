using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Bậc ngoại khóa k (1 đến 5) tính bằng code (CONG_THUC_GOI_Y_TRUONG.docx, bước 1b).
public class ActivityTierTests
{
    private static EcActivityEntry Act(decimal role, int scope, int months) => new(role, scope, months);

    // ---------------------------------------------------------------- điểm một hoạt động

    [Theory]
    [InlineData(0.8, 3, 18, 0.71)]    // trưởng, cấp tỉnh, 18 tháng: 0,24 + 0,28 + 0,1875
    [InlineData(0.3, 1, 12, 0.31)]    // thành viên, cấp trường, 12 tháng: 0,08 + 0,105 + 0,125
    [InlineData(1.0, 5, 24, 1.00)]    // mức tối đa
    [InlineData(0.3, 1, 0, 0.19)]     // thấp nhất khi có hoạt động: 0,08 + 0,105
    public void ActivityPoints_FollowsTheFormula(double role, int scope, int months, double expected)
    {
        Assert.Equal((decimal)expected, ActivityTier.ActivityPoints((decimal)role, scope, months));
    }

    [Fact]
    public void ActivityPoints_MonthsStopCountingAfterTwoYears()
    {
        Assert.Equal(ActivityTier.ActivityPoints(0.8m, 3, 24), ActivityTier.ActivityPoints(0.8m, 3, 60));
        Assert.True(ActivityTier.ActivityPoints(0.8m, 3, 12) < ActivityTier.ActivityPoints(0.8m, 3, 24));
    }

    [Fact]
    public void ActivityPoints_HigherRoleOrScope_NeverLowersThePoints()
    {
        decimal[] roles = [0.3m, 0.6m, 0.8m, 1.0m];
        for (var i = 1; i < roles.Length; i++)
            Assert.True(ActivityTier.ActivityPoints(roles[i], 3, 12) > ActivityTier.ActivityPoints(roles[i - 1], 3, 12));
        for (var scope = 2; scope <= 5; scope++)
            Assert.True(ActivityTier.ActivityPoints(0.8m, scope, 12) > ActivityTier.ActivityPoints(0.8m, scope - 1, 12));
    }

    [Fact]
    public void ActivityPoints_ClampsOutOfRangeInputs()
    {
        Assert.Equal(ActivityTier.ActivityPoints(0.8m, 5, 24), ActivityTier.ActivityPoints(0.8m, 9, 24));
        Assert.Equal(ActivityTier.ActivityPoints(0.8m, 1, 12), ActivityTier.ActivityPoints(0.8m, 0, 12));
        Assert.Equal(ActivityTier.ActivityPoints(0.8m, 3, 0), ActivityTier.ActivityPoints(0.8m, 3, -5));
    }

    // ---------------------------------------------------------------- ví dụ trong tài liệu

    [Fact]
    public void Compute_WorkedExample_IsTier3()
    {
        // hoạt động 1: trưởng CLB cấp tỉnh 18 tháng (0,71); hoạt động 2: thành viên CLB trường 12 tháng (0,31); một giải cấp quốc gia
        var score = ActivityTier.Compute([Act(0.8m, 3, 18), Act(0.3m, 1, 12)], [4]);

        Assert.Equal(1.02m, score.TongHd);
        Assert.Equal(0.20m, score.Bonus);
        Assert.Equal(0.31m, score.M);    // (1,02 + 0,20) / 4 = 0,305 làm tròn lên 0,31
        Assert.Equal(3, score.Tier);
    }

    // ---------------------------------------------------------------- bậc theo m

    [Theory]
    [InlineData(0.00, 1)]
    [InlineData(0.09, 1)]
    [InlineData(0.10, 2)]    // đúng mốc: lên bậc
    [InlineData(0.24, 2)]
    [InlineData(0.25, 3)]
    [InlineData(0.44, 3)]
    [InlineData(0.45, 4)]
    [InlineData(0.64, 4)]
    [InlineData(0.65, 5)]
    [InlineData(1.00, 5)]
    public void TierOf_UsesTheFourCuts(double m, int expected)
    {
        Assert.Equal(expected, ActivityTier.TierOf((decimal)m));
    }

    [Fact]
    public void Compute_WithNothing_IsTierOne()
    {
        var score = ActivityTier.Compute([], []);

        Assert.Equal(new EcScore(0m, 0m, 0m, 1), score);
    }

    [Fact]
    public void Compute_StrongProfile_ReachesTierFive()
    {
        // bốn hoạt động quốc tế do sáng lập, 2 năm, và ba giải quốc tế
        var score = ActivityTier.Compute(Enumerable.Repeat(Act(1.0m, 5, 24), 4), [5, 5, 5]);

        Assert.Equal(4.00m, score.TongHd);
        Assert.Equal(0.50m, score.Bonus);   // 3 × 0,25 = 0,75, bị chặn ở 0,5
        Assert.Equal(1.00m, score.M);       // bị chặn ở 1
        Assert.Equal(5, score.Tier);
    }

    // ---------------------------------------------------------------- quy tắc chọn

    [Fact]
    public void Compute_OnlyTheFourBestActivitiesCount()
    {
        var strong = Enumerable.Repeat(Act(1.0m, 5, 24), 4).ToList();
        var weak = Enumerable.Repeat(Act(0.3m, 1, 1), 5).ToList();

        var withWeak = ActivityTier.Compute(strong.Concat(weak), []);
        var strongOnly = ActivityTier.Compute(strong, []);

        Assert.Equal(strongOnly, withWeak);   // thêm hoạt động yếu không làm đổi điểm
    }

    [Fact]
    public void Compute_OnlyTheTenFirstActivitiesAreConsidered()
    {
        var firstTen = Enumerable.Repeat(Act(0.3m, 1, 1), 10);
        var eleventh = Act(1.0m, 5, 24);   // hoạt động thứ 11 mạnh nhưng không được xét

        Assert.Equal(ActivityTier.Compute(firstTen, []), ActivityTier.Compute(firstTen.Append(eleventh), []));
    }

    [Fact]
    public void Compute_OnlyTheThreeBestAwardsCount()
    {
        var three = ActivityTier.Compute([], [3, 3, 3]);
        var more = ActivityTier.Compute([], [3, 3, 3, 1, 1, 2]);

        Assert.Equal(three, more);
        Assert.Equal(0.45m, three.Bonus);   // 9 × 0,05
    }

    [Fact]
    public void Compute_AnActivityOrderDoesNotChangeTheResult()
    {
        List<EcActivityEntry> list = [Act(0.8m, 3, 18), Act(0.3m, 1, 12), Act(1.0m, 2, 6), Act(0.6m, 4, 30)];

        Assert.Equal(ActivityTier.Compute(list, [2, 4]), ActivityTier.Compute(Enumerable.Reverse(list), [4, 2]));
    }

    [Fact]
    public void Compute_SameInputSameResult()
    {
        var a = ActivityTier.Compute([Act(0.8m, 3, 18)], [4]);
        var b = ActivityTier.Compute([Act(0.8m, 3, 18)], [4]);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Compute_AddingAnActivityOrAnAward_NeverLowersTheTier()
    {
        var baseline = ActivityTier.Compute([Act(0.6m, 2, 10)], []);
        var withMore = ActivityTier.Compute([Act(0.6m, 2, 10), Act(0.8m, 3, 12)], [3]);

        Assert.True(withMore.Tier >= baseline.Tier);
        Assert.True(withMore.M >= baseline.M);
    }

    // ---------------------------------------------------------------- mã vai trò

    [Theory]
    [InlineData("founder", 1.0)]
    [InlineData("leader", 0.8)]
    [InlineData("vice_leader", 0.6)]
    [InlineData("member", 0.3)]
    [InlineData(null, 0.3)]
    [InlineData("chủ nhiệm", 0.3)]   // dữ liệu cũ là chữ tự do: coi là thành viên
    public void RoleWeight_MapsTheFormCodes(string? role, double expected)
    {
        Assert.Equal((decimal)expected, ActivityTier.RoleWeight(role));
    }

    // ---------------------------------------------------------------- từ hồ sơ

    private static ProfileActivity Item(
        string kind = "extracurricular", string title = "CLB", string? role = "leader",
        int? impact = 3, int? months = 18) => new()
        {
            Kind = kind, Title = title, Role = role, ImpactLevel = impact, DurationMonths = months
        };

    [Fact]
    public void FromProfile_MatchesCompute_ForTheWorkedExample()
    {
        var items = new[]
        {
            Item(role: "leader", impact: 3, months: 18),
            Item(title: "CLB trường", role: "member", impact: 1, months: 12),
            Item(kind: "award", title: "Giải quốc gia", role: null, impact: 4, months: null),
        };

        Assert.Equal(new EcScore(1.02m, 0.20m, 0.31m, 3), ActivityTier.FromProfile(items));
    }

    [Fact]
    public void FromProfile_SkipsItemsWithoutATitleOrAValidScope()
    {
        var items = new[]
        {
            Item(title: "  "),             // không có tên
            Item(impact: null),            // chưa chọn phạm vi
            Item(impact: 0),               // phạm vi sai
            Item(impact: 6),
        };

        Assert.Equal(ActivityTier.Compute([], []), ActivityTier.FromProfile(items));
    }

    [Fact]
    public void FromProfile_AwardsAreNotCountedAsActivities()
    {
        var onlyAward = new[] { Item(kind: "award", impact: 5, months: 24) };

        var score = ActivityTier.FromProfile(onlyAward);

        Assert.Equal(0m, score.TongHd);
        Assert.Equal(0.25m, score.Bonus);
    }

    [Fact]
    public void FromProfile_AwardKindIsCaseInsensitive()
    {
        Assert.Equal(0.25m, ActivityTier.FromProfile([Item(kind: "AWARD", impact: 5)]).Bonus);
    }

    [Fact]
    public void FromProfile_ExperienceCountsAsAnActivity()
    {
        Assert.True(ActivityTier.FromProfile([Item(kind: "experience")]).TongHd > 0);
    }

    [Fact]
    public void FromProfile_TakesMonthsFromTheDeclaredDurationFirst_ThenFromDates_ThenZero()
    {
        var declared = Item(months: 24);
        var fromDates = Item(months: null);
        fromDates.StartDate = new DateOnly(2024, 1, 1);
        fromDates.EndDate = new DateOnly(2025, 1, 1);   // 12 tháng
        var none = Item(months: null);

        Assert.Equal(ActivityTier.ActivityPoints(0.8m, 3, 24), ActivityTier.FromProfile([declared]).TongHd);
        Assert.Equal(ActivityTier.ActivityPoints(0.8m, 3, 12), ActivityTier.FromProfile([fromDates]).TongHd);
        Assert.Equal(ActivityTier.ActivityPoints(0.8m, 3, 0), ActivityTier.FromProfile([none]).TongHd);
    }

    [Fact]
    public void FromProfile_ClampsAnAbsurdDuration()
    {
        Assert.Equal(
            ActivityTier.FromProfile([Item(months: 24)]),
            ActivityTier.FromProfile([Item(months: 5000)]));
    }

    [Fact]
    public void FromProfile_ConsidersOnlyTheFirstTenActivities()
    {
        var items = Enumerable.Range(0, 10).Select(_ => Item(role: "member", impact: 1, months: 1)).ToList();
        var withStrongEleventh = items.Append(Item(role: "founder", impact: 5, months: 24)).ToList();

        Assert.Equal(ActivityTier.FromProfile(items), ActivityTier.FromProfile(withStrongEleventh));
    }
}
