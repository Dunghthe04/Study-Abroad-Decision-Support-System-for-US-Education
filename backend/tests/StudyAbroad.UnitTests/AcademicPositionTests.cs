using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

// Bước 1 của công thức gợi ý trường: vị trí p theo điểm thi từ P25, P75 (CONG_THUC_GOI_Y_TRUONG.docx, mục 5.1).
public class AcademicPositionTests
{
    // Trường X trong tài liệu: P25 = 1200, P75 = 1380 (μ = 1290, σ ≈ 133).
    private const double P25 = 1200, P75 = 1380;

    [Theory]
    [InlineData(1200, 0.25)]   // đúng P25
    [InlineData(1290, 0.50)]   // đúng giữa
    [InlineData(1380, 0.75)]   // đúng P75
    public void AtTheQuartilesAndMidpoint_MatchesTheirDefinition(double score, double expected)
    {
        Assert.Equal(expected, AcademicPosition.FromQuartiles(score, P25, P75), 3);
    }

    [Theory]
    [InlineData(1000, 0.015)]
    [InlineData(1100, 0.077)]
    [InlineData(1300, 0.530)]
    [InlineData(1450, 0.885)]
    [InlineData(1550, 0.974)]
    public void ElsewhereOnTheBell_MatchesTheDocumentTable(double score, double expected)
    {
        var actual = AcademicPosition.FromQuartiles(score, P25, P75);

        Assert.True(Math.Abs(actual - expected) < 0.005, $"SAT {score}: {actual:F4}, mong {expected}");
    }

    [Fact]
    public void HigherScore_NeverLowersPosition_AndStaysWithinZeroAndOne()
    {
        var previous = -1.0;
        for (var score = 400; score <= 1600; score += 50)
        {
            var p = AcademicPosition.FromQuartiles(score, P25, P75);
            Assert.InRange(p, 0.0, 1.0);
            Assert.True(p >= previous, $"SAT {score} thấp hơn điểm trước");
            previous = p;
        }
    }

    [Fact]
    public void ScoresOneBandApart_AreNotThrownIntoDifferentBuckets()
    {
        // 1199 và 1201 chỉ hơn kém 2 điểm nên p gần như bằng nhau (không nhảy bậc)
        var a = AcademicPosition.FromQuartiles(1199, P25, P75);
        var b = AcademicPosition.FromQuartiles(1201, P25, P75);

        Assert.True(b - a < 0.01);
    }

    [Theory]
    [InlineData(1200, 1200)]
    [InlineData(1380, 1200)]
    public void Quartiles_MustBeInIncreasingOrder(double p25, double p75)
    {
        Assert.Throws<ArgumentException>(() => AcademicPosition.FromQuartiles(1300, p25, p75));
    }

    [Fact]
    public void Phi_MatchesKnownValues()
    {
        Assert.Equal(0.5, AcademicPosition.Phi(0), 6);
        Assert.Equal(0.8413, AcademicPosition.Phi(1), 4);
        Assert.Equal(0.0228, AcademicPosition.Phi(-2), 4);
        Assert.Equal(0.75, AcademicPosition.Phi(0.6744897501960817), 6);
    }
}
