using StudyAbroad.Application.Recommendations;
using StudyAbroad.Application.Recommendations.Sensitivity;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// [UC-85] Bộ đo độ nhạy: nhích một tham số, chạy lại 324 hồ sơ qua hàm gợi ý thật, đếm thay đổi, gắn nhãn.
public class SensitivityAnalyzerTests
{
    private static readonly SensitivityProfileSet Profiles = SensitivityProfileSet.LoadV1();

    // 24 trường giả trải từ dễ đến khó, rẻ đến đắt, để mọi nhóm và mọi khối đều có trường
    private static readonly IReadOnlyList<RecommendSchoolInput> Schools = Enumerable.Range(0, 24).Select(i =>
    {
        var university = new University
        {
            Code = $"S{i:00}", Name = $"S{i:00}", State = "NY", Control = i % 2 == 0 ? "private" : "public", IsActive = true,
            AcceptanceRate = Math.Round(0.92m - i * 0.035m, 3)
        };
        var satLow = 1000 + i * 20;
        var offering = new UniversityOffering
        {
            UniversityId = university.Id,
            StudyLevel = "undergraduate",
            TuitionUsd = 18_000m + i * 2_600m,
            IntlAidPolicy = i % 3 == 0 ? "none" : "need_and_merit",
            Sat25 = satLow,
            Sat75 = satLow + 160,
            AvgGpa4 = Math.Round(3.0m + i * 0.04m, 2),
            MinIelts = i % 4 == 0 ? 6.5m : null,
            C7EcImportance = i % 2 == 0 ? "important" : "considered"
        };
        return new RecommendSchoolInput(university, offering, [Profiles.Major], [new RankPosition(i + 1, 100)]);
    }).ToList();

    private static readonly string[] AllKeys = SensitivityParameters.All.Select(p => p.Key).ToArray();

    // ------------------------------------------------------------ bất biến: điều chắc chắn phải đúng

    [Fact]
    public void NudgingByZero_ChangesNothing_ForEveryParameter()
    {
        foreach (var key in AllKeys)
        {
            var point = SensitivityAnalyzer.MeasureOne(Profiles, Schools, key, 0);
            Assert.True(point.Pairs > 0, key);
            Assert.Equal(0, point.ChangedPairs);
            Assert.Equal(0, point.ChangedSlots);
        }
    }

    [Fact]
    public void RaisingTheSafetyThreshold_OnlyMovesSchoolsAwayFromSafety_NeverTowardIt()
    {
        // 0,75 lên 0,825: khó vào An toàn hơn, nên chỉ có trường rơi khỏi An toàn
        var up = SensitivityAnalyzer.MeasureOne(Profiles, Schools, "upper_cut", +0.10);
        Assert.True(up.ChangedPairs > 0);
        Assert.Equal(0, up.MovedSafer);
        Assert.Equal(up.ChangedPairs, up.MovedHarder);

        // Hạ ngưỡng thì ngược lại
        var down = SensitivityAnalyzer.MeasureOne(Profiles, Schools, "upper_cut", -0.10);
        Assert.True(down.ChangedPairs > 0);
        Assert.Equal(0, down.MovedHarder);
    }

    [Fact]
    public void RaisingTheReachThreshold_OnlyMakesSchoolsHarder()
    {
        var up = SensitivityAnalyzer.MeasureOne(Profiles, Schools, "lower_cut", +0.20);
        Assert.True(up.ChangedPairs > 0);
        Assert.Equal(0, up.MovedSafer);
    }

    [Fact]
    public void FitWeights_NeverChangeAGroup_OnlyTheTop4()
    {
        foreach (var key in AllKeys.Where(k => k.StartsWith("fit_")))
            foreach (var step in new[] { -0.20, -0.10, 0.10, 0.20 })
            {
                var point = SensitivityAnalyzer.MeasureOne(Profiles, Schools, key, step);
                Assert.Equal(0, point.ChangedPairs);
            }
        // Ít nhất một trọng số đổi được top 4 khi nhích 20% (bộ đo thật sự nhìn thấy thay đổi thứ tự)
        Assert.Contains(AllKeys.Where(k => k.StartsWith("fit_")),
            key => SensitivityAnalyzer.MeasureOne(Profiles, Schools, key, 0.20).ChangedSlots > 0);
    }

    // ------------------------------------------------------------ số phụ: thứ tự đầy đủ trong nhóm ("Xem thêm")

    [Fact]
    public void NudgingByZero_MovesNoSchoolInTheFullOrder()
    {
        foreach (var key in AllKeys)
        {
            var point = SensitivityAnalyzer.MeasureOne(Profiles, Schools, key, 0);
            Assert.True(point.RankedSchools > 0, key);
            Assert.Equal(0, point.RankShiftTotal);
            Assert.Equal(0, point.AvgRankShift);
        }
    }

    [Fact]
    public void AFitWeight_ThatChangesTheTop4_AlsoShiftsTheFullOrder_AndKeepsEverySchoolInItsGroup()
    {
        foreach (var key in AllKeys.Where(k => k.StartsWith("fit_")))
        {
            var baseline = SensitivityAnalyzer.MeasureOne(Profiles, Schools, key, 0);
            var point = SensitivityAnalyzer.MeasureOne(Profiles, Schools, key, 0.20);
            // Trọng số không đổi nhóm nên mọi trường vẫn có mặt trong nhóm cũ: đủ số trường để so
            Assert.Equal(baseline.RankedSchools, point.RankedSchools);
            // Top 4 đổi trường thì chắc chắn thứ tự đầy đủ cũng đổi
            if (point.ChangedSlots > 0) Assert.True(point.RankShiftTotal > 0, key);
            Assert.True(point.AvgRankShift >= 0);
        }
    }

    [Fact]
    public void ARunSavedBeforeTheRankShiftExisted_ShowsNoValue_NotZero()
    {
        // JSON của lần chạy cũ không có hai trường mới
        var old = System.Text.Json.JsonSerializer.Deserialize<SensitivityPoint>(
            """{"step":0.1,"value":0.825,"clamped":false,"pairs":10,"changedPairs":1,"movedSafer":0,"movedHarder":1,"slots":4,"changedSlots":1}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.Null(old.RankedSchools);
        Assert.Null(old.AvgRankShift);
    }

    [Fact]
    public void ExtracurricularParameters_AreSeen_BecauseTheProfilesHaveTier1And5()
    {
        // Nếu mọi hồ sơ đều bậc 3 thì hai tham số này luôn 0% và bị gắn Ổn định sai
        var point = SensitivityAnalyzer.MeasureOne(Profiles, Schools, "ec_step", +0.20);
        Assert.True(point.ChangedPairs > 0);
    }

    // ------------------------------------------------------------ nhích, kẹp, chia lại trọng số

    [Theory]
    [InlineData(0.75, 0.10, 0.825, false)]
    [InlineData(0.75, -0.20, 0.60, false)]
    [InlineData(0.95, 0.10, 1.0, true)]    // 1,045 vượt 1: kẹp về 1 và ghi lại là đã kẹp (AF-2)
    [InlineData(0.0, 0.20, 0.0, false)]    // 0 nhích tương đối vẫn là 0
    public void Nudge_IsRelativeToTheCurrentValue_AndClampedTo0To1(double original, double step, double expected, bool clamped)
    {
        var (value, wasClamped) = SensitivityAnalyzer.Nudge(original, step);
        Assert.Equal(expected, value, 9);
        Assert.Equal(clamped, wasClamped);
    }

    [Fact]
    public void NudgingOneFitWeight_RescalesAllFive_SoTheyStillSumTo1()
    {
        var budget = SensitivityParameters.Find("fit_budget")!;
        var t = budget.With(RecommendationTuning.Current, 0.275);
        var f = t.Fit;
        Assert.Equal(1.0, f.Position + f.Budget + f.Aid + f.Quality + f.English, 9);
        Assert.Equal(0.275 / 1.025, f.Budget, 9);
        // Bốn trọng số còn lại giữ nguyên tỉ lệ với nhau
        Assert.Equal(0.30 / 0.20, f.Position / f.Quality, 9);
    }

    [Fact]
    public void NudgingAGroupingParameter_ChangesOnlyThatOne()
    {
        var t = SensitivityParameters.Find("safety_min_rate")!.With(RecommendationTuning.Current, 0.55);
        Assert.Equal(RecommendationTuning.Current.Grouping with { SafetyMinRate = 0.55 }, t.Grouping);
        Assert.Equal(RecommendationTuning.Current.Fit, t.Fit);
    }

    // ------------------------------------------------------------ nhãn

    [Theory]
    [InlineData(0.0, "stable")]
    [InlineData(0.0199, "stable")]
    [InlineData(0.02, "watch")]
    [InlineData(0.10, "watch")]
    [InlineData(0.1001, "sensitive")]
    public void Label_UsesTheBaThresholds(double rate, string label) =>
        Assert.Equal(label, SensitivityAnalyzer.Label(rate));

    [Fact]
    public void Label_Thresholds_AreConfigurable() =>
        Assert.Equal(SensitivityLabels.Sensitive, SensitivityAnalyzer.Label(0.06, stableBelow: 0.01, watchUpTo: 0.05));

    // ------------------------------------------------------------ cả lần chạy

    [Fact]
    public void Run_ReportsEveryChosenParameter_WithFourPoints_AndTheConditionsItRanUnder()
    {
        var report = SensitivityAnalyzer.Run(Profiles, Schools, new SensitivityRequest(["upper_cut", "fit_budget"], [0.10, 0.20]));

        Assert.Equal(RecommendationService.AlgorithmVersion, report.AlgorithmVersion);
        Assert.Equal(Profiles.Version, report.ProfileSetVersion);
        Assert.Equal(Profiles.Hash, report.ProfileSetHash);
        Assert.Equal(324, report.ProfileCount);
        Assert.Equal(24, report.SchoolCount);
        Assert.Equal(["upper_cut", "fit_budget"], report.Parameters.Select(p => p.Key));

        var upper = report.Parameters[0];
        Assert.Equal(0.75, upper.Baseline);
        Assert.Equal([-0.10, 0.10, -0.20, 0.20], upper.Points.Select(p => p.Step));
        // Nhãn tính trên ±10%: lớn nhất của hai chiều, theo % đổi nhóm
        var at10 = upper.Points.Where(p => Math.Abs(p.Step) == 0.10).Max(p => p.GroupChangeRate);
        Assert.Equal(at10, upper.Rate);
        Assert.Equal(SensitivityAnalyzer.Label(at10), upper.Label);

        // Trọng số fit gắn nhãn theo % đổi top 4
        var budget = report.Parameters[1];
        Assert.Equal(budget.Points.Where(p => Math.Abs(p.Step) == 0.10).Max(p => p.TopChangeRate), budget.Rate);
    }

    [Fact]
    public void Run_With20PercentOnly_GivesNumbers_ButNoLabel()
    {
        var report = SensitivityAnalyzer.Run(Profiles, Schools, new SensitivityRequest(["upper_cut"], [0.20]));
        var upper = Assert.Single(report.Parameters);
        Assert.Equal(2, upper.Points.Count);
        Assert.Null(upper.Rate);
        Assert.Null(upper.Label);
    }

    [Fact]
    public void Run_IsDeterministic_AndLeavesTheRunningParametersAlone()
    {
        var request = new SensitivityRequest(["upper_cut", "ec_cap", "fit_quality"], [0.10]);
        var first = SensitivityAnalyzer.Run(Profiles, Schools, request);
        var second = SensitivityAnalyzer.Run(Profiles, Schools, request);

        Assert.Equal(first.Parameters.SelectMany(p => p.Points), second.Parameters.SelectMany(p => p.Points));
        Assert.Equal(0.75, RecommendationTuning.Current.Grouping.UpperCut);
        Assert.Equal(new FitWeights(), RecommendationTuning.Current.Fit);
    }

    // ------------------------------------------------------------ AF-1: đầu vào sai

    [Fact]
    public void Run_WithoutAnyParameter_IsRejected() =>
        Assert.Throws<ArgumentException>(() => SensitivityAnalyzer.Run(Profiles, Schools, new SensitivityRequest([], [0.10])));

    [Fact]
    public void Run_WithAnUnknownParameter_IsRejected() =>
        Assert.Throws<ArgumentException>(() => SensitivityAnalyzer.Run(Profiles, Schools, new SensitivityRequest(["max_per_block"], [0.10])));

    [Theory]
    [InlineData(0.15)]
    [InlineData(0.0)]
    public void Run_WithAStepOtherThan10Or20Percent_IsRejected(double step) =>
        Assert.Throws<ArgumentException>(() => SensitivityAnalyzer.Run(Profiles, Schools, new SensitivityRequest(["upper_cut"], [step])));

    [Fact]
    public void Run_WithThresholdsInTheWrongOrder_IsRejected() =>
        Assert.Throws<ArgumentException>(() =>
            SensitivityAnalyzer.Run(Profiles, Schools, new SensitivityRequest(["upper_cut"], [0.10], StableBelow: 0.10, WatchUpTo: 0.02)));
}
