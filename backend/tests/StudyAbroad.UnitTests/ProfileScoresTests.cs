using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Điểm thi cho gợi ý trường đọc từ bảng chứng chỉ: chỉ bài đã thi, nhiều lần thì lấy cao nhất.
public class ProfileScoresTests
{
    private static ProfileCertification Cert(string type, decimal? score, bool isTarget = false) =>
        new() { TestType = type, OverallScore = score, IsTarget = isTarget, Category = "english" };

    [Fact]
    public void NoCertificates_GivesNoScores()
    {
        Assert.Equal(ProfileScores.Empty, ProfileScores.From([]));
    }

    [Fact]
    public void TakenScores_AreReadPerType()
    {
        var scores = ProfileScores.From([Cert("ielts", 6.5m), Cert("toefl", 90m), Cert("duolingo", 120m), Cert("sat", 1300m), Cert("act", 28m)]);

        Assert.Equal(new ProfileScores(6.5m, 90m, 120m, 1300m, 28m), scores);
    }

    [Fact]
    public void SeveralAttemptsOfOneType_TakeTheHighest()
    {
        var scores = ProfileScores.From([Cert("ielts", 6.0m), Cert("ielts", 7.0m), Cert("ielts", 6.5m)]);

        Assert.Equal(7.0m, scores.Ielts);
    }

    [Fact]
    public void TargetRows_AreIgnored_EvenWhenTheTypeHasNoTakenScore()
    {
        var scores = ProfileScores.From([Cert("ielts", 7.5m, isTarget: true), Cert("sat", 1500m, isTarget: true)]);

        Assert.Null(scores.Ielts);
        Assert.Null(scores.Sat);
    }

    [Fact]
    public void TargetRow_DoesNotRaiseATakenScore()
    {
        var scores = ProfileScores.From([Cert("ielts", 6.0m), Cert("ielts", 8.0m, isTarget: true)]);

        Assert.Equal(6.0m, scores.Ielts);
    }

    [Fact]
    public void RowsWithoutANumericScore_AreIgnored_AndOtherTypesDoNotCount()
    {
        var scores = ProfileScores.From([Cert("ielts", null), Cert("ap", 5m), Cert("gre", 320m)]);

        Assert.Equal(ProfileScores.Empty, scores);
    }
}
