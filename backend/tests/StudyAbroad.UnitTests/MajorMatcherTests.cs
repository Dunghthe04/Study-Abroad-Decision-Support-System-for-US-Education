using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class MajorMatcherTests
{
    private static readonly MajorMatcher M = new(new RecommendSettings().MajorGroups);

    [Theory]
    [InlineData("Computer Science", "Computer Science, BS")]   // bỏ hậu tố bằng cấp
    [InlineData("computer science", "Computer Science")]       // không phân biệt hoa thường
    [InlineData("Computer  Science ", "Computer Science")]     // khoảng trắng thừa
    [InlineData("IT", "Computer Science")]                     // đồng nghĩa → nhóm
    [InlineData("Công nghệ thông tin", "Computer Science")]    // tiếng Việt
    [InlineData("Computer Science", "Data Science")]           // HS chọn nhóm, trường ghi ngành con
    [InlineData("Mechanical Engineering", "Engineering")]      // HS ghi ngành con, trường ghi nhóm
    [InlineData("Business", "Business Administration, BBA")]  // nhóm + hậu tố bằng cấp
    public void Matches(string student, string school) => Assert.True(M.Matches(student, [school]));

    [Theory]
    [InlineData("Mechanical Engineering", "Civil Engineering")] // hai ngành con khác nhau trong cùng nhóm
    [InlineData("IT", "Data Science")]                          // hai tên con khác nhau trong cùng nhóm
    [InlineData("Computer Science", "Nursing")]
    public void DoesNotMatch(string student, string school) => Assert.False(M.Matches(student, [school]));

    [Fact]
    public void NoMajor_MatchesAll() => Assert.True(M.Matches(null, ["Nursing"]));

    [Fact]
    public void AnySchoolMajorMatching_IsEnough() =>
        Assert.True(M.Matches("Computer Science", ["Nursing", "Psychology", "Data Science"]));

    [Theory]
    [InlineData("Computer Science, BS", "computer science")]
    [InlineData("  Mechanical   Engineering, BSME ", "mechanical engineering")]
    public void Normalize_RemovesDegreeSuffixAndExtraSpaces(string raw, string expected) =>
        Assert.Equal(expected, MajorMatcher.Normalize(raw));
}
