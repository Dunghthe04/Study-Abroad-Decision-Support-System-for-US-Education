using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class RecommendationInputsTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);
    private static readonly IReadOnlySet<string> Cip = new HashSet<string> { "Computer and information sciences", "Engineering" };

    [Theory]
    [InlineData("Computer Science", "Computer and information sciences")]
    [InlineData("computer science", "Computer and information sciences")]
    [InlineData("Mechanical Engineering", "Engineering")]
    [InlineData("Engineering", "Engineering")]                 // chọn thẳng nhóm CIP
    [InlineData("Undecided / Exploratory", null)]              // chưa quyết định: không lọc ngành
    [InlineData(null, null)]
    public void ToCipMajor_MapsDropdownNamesToCipFamilies(string? major, string? expected) =>
        Assert.Equal(expected, RecommendationInputs.ToCipMajor(major, Cip));

    [Theory]
    [InlineData("mandatory", AidNeed.Required)]
    [InlineData("preferred", AidNeed.Preferred)]
    [InlineData("optional", AidNeed.None)]
    [InlineData(null, AidNeed.None)]
    public void ToCriteria_MapsScholarshipNeed(string? need, AidNeed expected) =>
        Assert.Equal(expected, RecommendationInputs.ToCriteria(new StudentProfile { ScholarshipNeed = need }, null).AidNeed);

    [Theory]
    [InlineData("all", null)]
    [InlineData("public", "public")]
    [InlineData("private", "private")]
    public void ToCriteria_SchoolTypeAllMeansNoFilter(string type, string? expected) =>
        Assert.Equal(expected, RecommendationInputs.ToCriteria(new StudentProfile { TargetSchoolType = type }, null).Control);

    [Fact]
    public void ToEcRequest_MonthsFromDatesWhenNotDeclared()
    {
        var ongoing = new ProfileActivity { Kind = "extracurricular", Title = "CLB", StartDate = new DateOnly(2025, 4, 1), IsOngoing = true };
        var ended = new ProfileActivity { Kind = "experience", Title = "Thực tập", StartDate = new DateOnly(2025, 1, 1), EndDate = new DateOnly(2025, 7, 1) };
        var declared = new ProfileActivity { Kind = "extracurricular", Title = "Tình nguyện", DurationMonths = 300 };

        var acts = RecommendationInputs.ToEcRequest([ongoing, ended, declared], Today).Activities;
        Assert.Equal([18, 6, 120], acts.Select(a => a.Months));    // đang tham gia tính đến hôm nay; tối đa 120
    }

    [Fact]
    public void ToEcRequest_MissingLevelIsSchoolLevelAndAwardsSeparated()
    {
        var req = RecommendationInputs.ToEcRequest(
            [new ProfileActivity { Kind = "extracurricular", Title = "CLB" },
             new ProfileActivity { Kind = "award", Title = "Giải Ba", ImpactLevel = 9 }], Today);

        Assert.Equal(1, Assert.Single(req.Activities).Scope);     // thiếu phạm vi: cấp trường, không nâng hồ sơ
        Assert.Equal(5, Assert.Single(req.Awards).Level);         // ngoài 1–5 thì kẹp lại
    }

    [Fact]
    public void ToEcRequest_KeepsAtMostTenActivitiesAndTrimsText()
    {
        var many = Enumerable.Range(0, 12).Select(i => new ProfileActivity
            { Kind = "extracurricular", Title = new string('x', 600) }).ToList();
        var acts = RecommendationInputs.ToEcRequest(many, Today).Activities;
        Assert.Equal(10, acts.Count);
        Assert.All(acts, a => Assert.True(a.Text.Length <= 500));
    }
}
