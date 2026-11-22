using StudyAbroad.Application.Recommendations;
using StudyAbroad.Application.Recommendations.Sensitivity;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.UnitTests;

// [UC-85] Bộ hồ sơ giả lập đóng băng: đủ số lượng, phủ đều mọi mức, giá trị đúng khoảng, sửa file là bị phát hiện.
public class SensitivityProfileSetTests
{
    private static readonly SensitivityProfileSet Set = SensitivityProfileSet.LoadV1();
    private const string L = SensitivityProfileSet.Low, M = SensitivityProfileSet.Mid, H = SensitivityProfileSet.High;

    [Fact]
    public void TheFrozenFile_LoadsWithItsHeader_And324Profiles()
    {
        Assert.Equal(SensitivityProfileSet.V1, Set.Version);
        Assert.Equal(85, Set.Seed);
        Assert.Equal("Computer and information sciences", Set.Major);
        Assert.Equal(SensitivityProfileSet.V1Hash, Set.Hash);
        Assert.Equal(324, Set.Profiles.Count);
        Assert.Equal(324, Set.Profiles.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void RegeneratingWithTheSameSeed_GivesExactlyTheFrozenFile()
    {
        // File trong repo đúng là thứ Generate(85) sinh ra, không ai sửa tay
        var again = SensitivityProfileSet.Generate(SensitivityProfileSet.V1Seed);
        Assert.Equal(SensitivityProfileSet.V1Hash, again.Hash);
        Assert.Equal(Set.Profiles, again.Profiles);
    }

    [Fact]
    public void ADifferentSeed_GivesDifferentNumbers_ButTheSameGrid()
    {
        var other = SensitivityProfileSet.Generate(86);
        Assert.NotEqual(SensitivityProfileSet.V1Hash, other.Hash);
        // Các mức giữ nguyên vị trí, chỉ con số cụ thể trong mức đổi
        Assert.Equal(
            Set.Profiles.Select(p => (p.GpaLevel, p.TestLevel, p.BudgetLevel, p.AidLevel, p.EnglishLevel, p.EcTier)),
            other.Profiles.Select(p => (p.GpaLevel, p.TestLevel, p.BudgetLevel, p.AidLevel, p.EnglishLevel, p.EcTier)));
    }

    [Fact]
    public void EveryLevelOfEveryFactor_HasAnEqualShare()
    {
        AssertEvenly(Set.Profiles.Select(p => p.GpaLevel), [L, M, H]);
        AssertEvenly(Set.Profiles.Select(p => p.TestLevel), [L, M, H, SensitivityProfileSet.None]);
        AssertEvenly(Set.Profiles.Select(p => p.BudgetLevel), [L, M, H]);
        AssertEvenly(Set.Profiles.Select(p => p.AidLevel), [L, M, H]);
        AssertEvenly(Set.Profiles.Select(p => p.EnglishLevel), [L, M, H]);
        AssertEvenly(Set.Profiles.Select(p => p.EcTier.ToString()), ["1", "3", "5"]);
    }

    [Fact]
    public void EveryCombinationOfTheFiveGridFactors_AppearsExactlyOnce()
    {
        var combos = Set.Profiles.Select(p => (p.GpaLevel, p.TestLevel, p.BudgetLevel, p.AidLevel, p.EnglishLevel)).ToList();
        Assert.Equal(3 * 4 * 3 * 3 * 3, combos.Distinct().Count());
    }

    [Fact]
    public void TheExtracurricularTier_IsSpreadEvenly_AgainstEachOtherFactor()
    {
        // Ví dụ: trong 108 hồ sơ GPA cao, bậc 1/3/5 mỗi bậc 36 hồ sơ (không dồn bậc 5 vào nhóm GPA cao)
        foreach (var factor in new Func<SensitivityProfile, string>[]
                 { p => p.GpaLevel, p => p.TestLevel, p => p.BudgetLevel, p => p.AidLevel, p => p.EnglishLevel })
            foreach (var level in Set.Profiles.GroupBy(factor))
                AssertEvenly(level.Select(p => p.EcTier.ToString()), ["1", "3", "5"]);
    }

    [Fact]
    public void EveryValue_LiesInsideTheRangeOfItsLevel()
    {
        foreach (var p in Set.Profiles)
        {
            Assert.InRange(p.Gpa, p.GpaLevel switch { L => 2.80m, M => 3.30m, _ => 3.70m }, p.GpaLevel switch { L => 3.20m, M => 3.60m, _ => 4.00m });
            if (p.TestLevel == SensitivityProfileSet.None) Assert.Null(p.Sat);
            else
            {
                Assert.NotNull(p.Sat);
                Assert.Equal(0, p.Sat!.Value % 10);
                Assert.InRange(p.Sat.Value, p.TestLevel switch { L => 1000, M => 1200, _ => 1400 }, p.TestLevel switch { L => 1190, M => 1390, _ => 1560 });
            }
            Assert.Contains(p.Ielts, p.EnglishLevel switch { L => [5.5m, 6.0m], M => [6.5m, 7.0m], _ => new[] { 7.5m, 8.0m } });
            Assert.Equal(p.BudgetLevel switch { L => 35_000m, M => 50_000m, _ => 70_000m }, p.BudgetMaxUsd);
            Assert.Equal(p.AidLevel switch
            {
                L => ProfileConstants.ScholarshipNeeds.Optional,
                M => ProfileConstants.ScholarshipNeeds.Preferred,
                _ => ProfileConstants.ScholarshipNeeds.Mandatory
            }, p.ScholarshipNeed);
        }
    }

    [Fact]
    public void ChangingOneNumberInTheFile_IsCaught()
    {
        var json = Set.ToFileJson();
        Assert.Equal(Set.Profiles, SensitivityProfileSet.Parse(json, SensitivityProfileSet.V1Hash).Profiles);

        // Sửa GPA của P001 từ 2.95 lên 2.96, giữ nguyên dòng hash trong file
        var tampered = json.Replace("\"id\":\"P001\",\"gpaLevel\":\"low\",\"gpa\":2.95", "\"id\":\"P001\",\"gpaLevel\":\"low\",\"gpa\":2.96");
        Assert.NotEqual(json, tampered);
        Assert.Throws<InvalidDataException>(() => SensitivityProfileSet.Parse(tampered, SensitivityProfileSet.V1Hash));
    }

    [Fact]
    public void LineEndingsAndIndentation_DoNotChangeTheHash()
    {
        // Git trên Windows có thể đổi LF thành CRLF; hash tính trên dữ liệu nên vẫn khớp
        var crlf = Set.ToFileJson().Replace("\n", "\r\n").Replace("    {", "  {");
        Assert.Equal(SensitivityProfileSet.V1Hash, SensitivityProfileSet.Parse(crlf, SensitivityProfileSet.V1Hash).Hash);
    }

    [Fact]
    public void EveryProfile_BecomesARunnableStudent_ForTheRealRecommendation()
    {
        foreach (var p in Set.Profiles)
        {
            var student = p.ToStudentProfile(Set.Major);
            Assert.Equal(StudyLevels.Undergraduate, student.TargetLevel);
            Assert.Equal(Set.Major, student.IntendedMajor);
            Assert.Empty(student.PreferredStates);
            Assert.Equal("all", student.TargetSchoolType);
            Assert.Equal(p.EcTier, student.ExtracurricularTier);
            Assert.Equal(p.AidLevel != L, student.NeedsScholarship);
            // Qua được kiểm tra đầu vào của hàm gợi ý (có ngành, có mức học bổng)
            var run = RecommendationService.EvaluateWith(student, [], p.EcTier, RecommendationTuning.Current);
            Assert.Null(run.Picked);
        }
    }

    private static void AssertEvenly(IEnumerable<string> values, string[] levels)
    {
        var counts = values.GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(levels.OrderBy(x => x), counts.Keys.OrderBy(x => x));
        Assert.Single(counts.Values.Distinct());
    }
}
