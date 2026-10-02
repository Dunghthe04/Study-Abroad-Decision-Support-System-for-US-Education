using StudyAbroad.Domain.Constants;

namespace StudyAbroad.UnitTests;

public class StudyLevelsTests
{
    [Theory]
    [InlineData("secondary")]
    [InlineData("community_college")]
    [InlineData("undergraduate")]
    [InlineData("master")]
    [InlineData("phd")]
    public void IsValid_AcceptsSupportedLevels(string level) => Assert.True(StudyLevels.IsValid(level));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Master")]
    [InlineData("kindergarten")]
    public void IsValid_RejectsUnknownLevels(string? level) => Assert.False(StudyLevels.IsValid(level));
}
