using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

// Bước 3 của công thức gợi ý trường: gộp p_g, p_s thành p, rồi hiệu chỉnh ngoại khóa thành p'
// (CONG_THUC_GOI_Y_TRUONG.docx, mục 5.3 và 5.4).
public class AcademicBlendTests
{
    // ---------------------------------------------------------------- trọng số

    [Theory]
    [InlineData("very_important", 1.0)]
    [InlineData("important", 0.75)]
    [InlineData("considered", 0.5)]
    [InlineData("not_considered", 0.25)]
    [InlineData("VERY_IMPORTANT", 1.0)]   // không phân biệt hoa thường
    [InlineData(" important ", 0.75)]
    [InlineData(null, 0.5)]
    [InlineData("khong_ro", 0.5)]
    public void Weight_FollowsHowMuchTheSchoolCares(string? level, double expected)
    {
        Assert.Equal(expected, AcademicBlend.Weight(level));
    }

    [Theory]
    [InlineData("very_important", 1.0)]
    [InlineData("important", 0.7)]
    [InlineData("considered", 0.4)]
    [InlineData("not_considered", 0.0)]
    [InlineData(null, 0.0)]
    [InlineData("khong_ro", 0.0)]
    public void EcWeight_UnknownOrIgnoredMeansNoAdjustment(string? level, double expected)
    {
        Assert.Equal(expected, AcademicBlend.EcWeight(level));
    }

    // ---------------------------------------------------------------- gộp p

    [Theory]
    [InlineData("very_important", "important", null, 0.43)]
    [InlineData("very_important", "not_considered", null, 0.39)]
    [InlineData("very_important", "important", 0.3, 0.40)]    // chỉ 30% nộp SAT: trọng số SAT × 0,5
    public void Combine_MatchesTheDocumentExamples(string gpaImp, string testImp, double? submit, double expected)
    {
        // p_g = 0,35 và p_s = 0,53 (trường B trong tài liệu)
        var p = AcademicBlend.Combine(0.35, 0.53, gpaImp, testImp, (decimal?)submit);

        Assert.True(Math.Abs(p!.Value - expected) < 0.005, $"p = {p:F4}, mong {expected}");
    }

    [Fact]
    public void Combine_EqualWeights_IsThePlainAverage()
    {
        Assert.Equal(0.5, AcademicBlend.Combine(0.4, 0.6, "important", "important")!.Value, 9);
        Assert.Equal(0.5, AcademicBlend.Combine(0.4, 0.6, null, null)!.Value, 9);
    }

    [Fact]
    public void Combine_TheMoreImportantMetricPullsPTowardItself()
    {
        var gpaHeavy = AcademicBlend.Combine(0.2, 0.8, "very_important", "not_considered")!.Value;
        var testHeavy = AcademicBlend.Combine(0.2, 0.8, "not_considered", "very_important")!.Value;

        Assert.True(gpaHeavy < 0.5);
        Assert.True(testHeavy > 0.5);
    }

    [Fact]
    public void Combine_WithOneMetric_EqualsThatMetric()
    {
        Assert.Equal(0.35, AcademicBlend.Combine(0.35, null, "very_important", "important")!.Value, 9);
        Assert.Equal(0.53, AcademicBlend.Combine(null, 0.53, "very_important", "important")!.Value, 9);
    }

    [Fact]
    public void Combine_WithNoMetric_ReturnsNull()
    {
        Assert.Null(AcademicBlend.Combine(null, null, "important", "important"));
    }

    [Fact]
    public void Combine_TheLowSubmitRuleOnlyTriggersBelowTheThreshold()
    {
        var exactlyHalf = AcademicBlend.Combine(0.2, 0.8, "important", "important", 0.5m)!.Value;
        var justBelow = AcademicBlend.Combine(0.2, 0.8, "important", "important", 0.49m)!.Value;

        Assert.Equal(0.5, exactlyHalf, 9);            // 0,5 chưa tính là ít người nộp
        Assert.True(justBelow < exactlyHalf);          // SAT bớt trọng số nên p nghiêng về GPA (0,2)
    }

    [Fact]
    public void Combine_ResultStaysBetweenTheTwoMetrics()
    {
        var p = AcademicBlend.Combine(0.1, 0.9, "considered", "very_important")!.Value;

        Assert.InRange(p, 0.1, 0.9);
    }

    [Fact]
    public void Combine_LowSubmitSettingsCanBeChanged()
    {
        var strict = new GroupingOptions(LowSubmitFactor: 0.0);   // bỏ hẳn SAT khi ít người nộp

        var p = AcademicBlend.Combine(0.2, 0.8, "important", "important", 0.1m, strict);

        Assert.Equal(0.2, p!.Value, 9);
    }

    // ---------------------------------------------------------------- hiệu chỉnh ngoại khóa

    [Theory]
    [InlineData(5, "very_important", 0.10)]
    [InlineData(4, "important", 0.035)]
    [InlineData(5, "considered", 0.04)]
    [InlineData(3, "very_important", 0.0)]
    [InlineData(1, "very_important", -0.10)]
    [InlineData(5, "not_considered", 0.0)]
    [InlineData(5, null, 0.0)]
    public void AdjustForActivities_ShiftsByTierAndSchoolWeight(int tier, string? importance, double expectedShift)
    {
        var adjusted = AcademicBlend.AdjustForActivities(0.43, tier, importance);

        Assert.Equal(0.43 + expectedShift, adjusted, 9);
    }

    [Fact]
    public void AdjustForActivities_CanPushABorderlineStudentAcrossTheLine()
    {
        Assert.Equal(0.32, AcademicBlend.AdjustForActivities(0.22, 5, "very_important"), 9);
        Assert.Equal(0.12, AcademicBlend.AdjustForActivities(0.22, 1, "very_important"), 9);
    }

    [Fact]
    public void AdjustForActivities_CannotRescueAVeryWeakStudent()
    {
        Assert.Equal(0.15, AcademicBlend.AdjustForActivities(0.05, 5, "very_important"), 9);
    }

    [Theory]
    [InlineData(0.02, 1)]
    [InlineData(0.98, 5)]
    public void AdjustForActivities_StaysWithinZeroAndOne(double p, int tier)
    {
        var adjusted = AcademicBlend.AdjustForActivities(p, tier, "very_important");

        Assert.InRange(adjusted, 0.0, 1.0);
    }

    [Fact]
    public void AdjustForActivities_TheCapLimitsTheShift()
    {
        // hệ số 0,2 mỗi bậc: bậc 5 sẽ là +0,4 nhưng trần 0,10 giữ ở +0,10
        var options = new GroupingOptions(EcStep: 0.2, EcCap: 0.10);

        Assert.Equal(0.60, AcademicBlend.AdjustForActivities(0.5, 5, "very_important", options), 9);
        Assert.Equal(0.40, AcademicBlend.AdjustForActivities(0.5, 1, "very_important", options), 9);
    }

    [Fact]
    public void AdjustForActivities_ZeroCapSwitchesEcOff()
    {
        var off = new GroupingOptions(EcCap: 0.0);

        Assert.Equal(0.43, AcademicBlend.AdjustForActivities(0.43, 5, "very_important", off), 9);
    }

    [Fact]
    public void AdjustForActivities_HigherTier_NeverLowersP()
    {
        var previous = -1.0;
        for (var tier = 1; tier <= 5; tier++)
        {
            var p = AcademicBlend.AdjustForActivities(0.5, tier, "important");
            Assert.True(p >= previous);
            previous = p;
        }
    }

    // ---------------------------------------------------------------- ví dụ trong tài liệu

    [Fact]
    public void WorkedExampleSchoolB_PIs043_AndPPrimeIs046()
    {
        // GPA 3,5 (p_g 0,35), SAT 1300 (p_s 0,53), ngoại khóa bậc 4; trường coi trọng GPA very_important,
        // điểm thi và ngoại khóa important
        var p = AcademicBlend.Combine(0.35, 0.53, "very_important", "important")!.Value;
        var pPrime = AcademicBlend.AdjustForActivities(p, 4, "important");

        Assert.True(Math.Abs(p - 0.43) < 0.005);
        Assert.True(Math.Abs(pPrime - 0.46) < 0.005);
    }
}
