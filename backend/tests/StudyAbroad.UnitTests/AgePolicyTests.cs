using StudyAbroad.Domain.Constants;

namespace StudyAbroad.UnitTests;

public class AgePolicyTests
{
    private static DateOnly D(string s) => DateOnly.Parse(s);

    [Theory]
    [InlineData("2010-10-27", "2026-10-27", 16)] // đúng sinh nhật
    [InlineData("2010-10-28", "2026-10-27", 15)] // mai mới sinh nhật
    [InlineData("2008-10-27", "2026-10-27", 18)]
    [InlineData("2008-10-28", "2026-10-27", 17)]
    [InlineData("2008-02-29", "2026-02-28", 17)] // sinh 29/2, năm không nhuận
    [InlineData("2008-02-29", "2026-03-01", 18)]
    public void GetAge_CountsFullYears(string dob, string today, int expected)
        => Assert.Equal(expected, AgePolicy.GetAge(D(dob), D(today)));

    [Theory]
    [InlineData("2010-10-28", AgeGroups.Child)]
    [InlineData("2010-10-27", AgeGroups.Minor)]
    [InlineData("2008-10-28", AgeGroups.Minor)]
    [InlineData("2008-10-27", AgeGroups.Adult)]
    public void GetAgeGroup_UsesBoundaries16And18(string dob, string expected)
        => Assert.Equal(expected, AgePolicy.GetAgeGroup(D(dob), D("2026-10-27")));

    [Fact]
    public void GetAgeGroup_NoDateOfBirth_ReturnsNull()
        => Assert.Null(AgePolicy.GetAgeGroup(null, D("2026-10-27")));

    [Fact]
    public void TodayInVietnam_IsUtcPlus7()
    {
        // 17:30 UTC ngày 26/10 = 00:30 ngày 27/10 giờ VN
        Assert.Equal(D("2026-10-27"), AgePolicy.TodayInVietnam(new DateTime(2026, 10, 26, 17, 30, 0, DateTimeKind.Utc)));
        Assert.Equal(D("2026-10-26"), AgePolicy.TodayInVietnam(new DateTime(2026, 10, 26, 16, 59, 0, DateTimeKind.Utc)));
    }
}
