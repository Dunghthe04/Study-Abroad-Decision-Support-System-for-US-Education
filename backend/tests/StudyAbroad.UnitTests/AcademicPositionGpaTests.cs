using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

// Bước 2 của công thức gợi ý trường: vị trí p theo GPA (CONG_THUC_GOI_Y_TRUONG.docx, mục 5.2).
public class AcademicPositionGpaTests
{
    // Trường B trong tài liệu: 0 / 5 / 10 / 20 / 30 / 25 / 10 % theo các khoảng
    // dưới 2,5 · 2,5–2,99 · 3,0–3,24 · 3,25–3,49 · 3,5–3,74 · 3,75–3,99 · đúng 4,0.
    private static readonly decimal[] Dist = [0m, 0.05m, 0.10m, 0.20m, 0.30m, 0.25m, 0.10m];

    // ---------------------------------------------------------------- có phân bố GPA

    [Theory]
    [InlineData(3.2, 0.13)]   // 5% dưới 3,0 + 10% × (0,2 / 0,25)
    [InlineData(3.5, 0.35)]   // đầu khoảng 3,5–3,74: cộng dồn 35%
    [InlineData(3.6, 0.47)]   // 35% + 30% × (0,1 / 0,25)
    [InlineData(3.9, 0.80)]   // 65% + 25% × (0,15 / 0,25)
    [InlineData(4.0, 0.95)]   // 90% dưới 4,0 + một nửa nhóm đúng 4,0
    public void WithDistribution_InterpolatesInsideTheBand(double gpa, double expected)
    {
        var p = AcademicPosition.FromGpa(gpa, Dist, average: null);

        Assert.Equal(expected, p!.Value, 6);
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(1.5)]
    public void WithDistribution_AtOrBelowTheLowestEdge_IsZero(double gpa)
    {
        Assert.Equal(0.0, AcademicPosition.FromGpa(gpa, Dist, average: null)!.Value, 9);
    }

    [Fact]
    public void WithDistribution_ScaleDoesNotMatter_PercentOrFraction()
    {
        // phân bố nhập theo % (tổng 100) hay theo tỉ lệ (tổng 1) đều cho cùng kết quả
        decimal[] percent = [0m, 5m, 10m, 20m, 30m, 25m, 10m];

        var a = AcademicPosition.FromGpa(3.6, Dist, null);
        var b = AcademicPosition.FromGpa(3.6, percent, null);

        Assert.Equal(a!.Value, b!.Value, 9);
    }

    [Fact]
    public void WithDistribution_TotalSlightlyOffOne_IsNormalized()
    {
        // trường làm tròn số nên tổng 0,98: vẫn chia lại cho tổng, p của 4,0 vẫn không vượt 1
        decimal[] roundedDown = [0m, 0.05m, 0.10m, 0.20m, 0.29m, 0.24m, 0.10m];

        var p = AcademicPosition.FromGpa(4.0, roundedDown, null);

        Assert.InRange(p!.Value, 0.0, 1.0);
    }

    [Fact]
    public void WithDistribution_HigherGpa_NeverLowersPosition()
    {
        var previous = -1.0;
        for (var gpa = 1.5; gpa <= 4.0; gpa += 0.05)
        {
            var p = AcademicPosition.FromGpa(gpa, Dist, null)!.Value;
            Assert.True(p >= previous, $"GPA {gpa:F2} thấp hơn điểm trước");
            previous = p;
        }
    }

    [Fact]
    public void Distribution_IsPreferredOverAverage_WhenBothExist()
    {
        // theo trung bình 3,5 thì p = 0,5; theo phân bố thì p = 0,35
        var p = AcademicPosition.FromGpa(3.5, Dist, average: 3.5m);

        Assert.Equal(0.35, p!.Value, 6);
    }

    // ---------------------------------------------------------------- chỉ có GPA trung bình

    [Theory]
    [InlineData(3.5, 0.50)]
    [InlineData(3.2, 0.16)]   // Φ(−1)
    [InlineData(3.9, 0.91)]   // Φ(1,33)
    public void WithAverageOnly_UsesBellCurveAroundTheAverage(double gpa, double expected)
    {
        var p = AcademicPosition.FromGpa(gpa, distribution: null, average: 3.5m);

        Assert.True(Math.Abs(p!.Value - expected) < 0.005, $"GPA {gpa}: {p:F4}, mong {expected}");
    }

    [Fact]
    public void WithAverageOnly_SigmaCanBeChanged()
    {
        // σ = 0,5: (3,2 − 3,5) / 0,5 = −0,6, Φ(−0,6) ≈ 0,274
        var p = AcademicPosition.FromGpa(3.2, null, 3.5m, sigma: 0.5);

        Assert.True(Math.Abs(p!.Value - 0.274) < 0.005);
    }

    // ---------------------------------------------------------------- thiếu hoặc sai dữ liệu

    [Fact]
    public void WithNoDistributionAndNoAverage_ReturnsNull()
    {
        Assert.Null(AcademicPosition.FromGpa(3.5, null, null));
    }

    [Theory]
    [InlineData(6)]   // thiếu một khoảng
    [InlineData(8)]   // thừa một khoảng
    public void DistributionWithWrongLength_FallsBackToAverage(int length)
    {
        var broken = Enumerable.Repeat(0.1m, length).ToArray();

        Assert.Equal(0.5, AcademicPosition.FromGpa(3.5, broken, 3.5m)!.Value, 6);
        Assert.Null(AcademicPosition.FromGpa(3.5, broken, null));
    }

    [Fact]
    public void DistributionOfAllZeros_FallsBackToAverage()
    {
        var zeros = new decimal[7];

        Assert.Equal(0.5, AcademicPosition.FromGpa(3.5, zeros, 3.5m)!.Value, 6);
        Assert.Null(AcademicPosition.FromGpa(3.5, zeros, null));
    }
}
