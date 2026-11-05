using StudyAbroad.Application.Profile.Flow;

namespace StudyAbroad.UnitTests;

// TB cả năm theo cách tính Việt Nam: HK2 hệ số 2 → (HK1 + 2×HK2) / 3, không làm tròn giữa chừng
public class TranscriptGpaTests
{
    [Theory]
    [InlineData(8, 9, 26, 3)]
    [InlineData(7, 8, 23, 3)]
    [InlineData(6, 6, 6, 1)]
    public void YearAverage_BothSemesters_WeightsSecondSemesterTwice(int s1, int s2, int numerator, int denominator)
    {
        Assert.Equal((decimal)numerator / denominator, TranscriptGpa.YearAverage(s1, s2));
    }

    [Fact]
    public void YearAverage_OneSemester_UsesThatScore()
    {
        Assert.Equal(7.5m, TranscriptGpa.YearAverage(7.5m, null));
        Assert.Equal(8m, TranscriptGpa.YearAverage(null, 8m));
    }

    [Fact]
    public void YearAverage_NoScore_IsNull()
    {
        Assert.Null(TranscriptGpa.YearAverage(null, null));
    }

    // Lớp 11: Toán (8, 9) = 8.666…, Văn (7, –) = 7; Lớp 12: Toán (–, 9) = 9 → (8.666… + 7 + 9) / 3 = 8.22
    [Fact]
    public void SubjectAverage10_AveragesYearAveragesAcrossYears_RoundsOnlyAtTheEnd()
    {
        var step2 = new FlowTranscriptDraftDto(
        [
            new FlowAcademicYearDraftDto("Lớp 11", [new FlowSubjectDraftDto("Toán", 8m, 9m), new FlowSubjectDraftDto("Văn", 7m)]),
            new FlowAcademicYearDraftDto("Lớp 12", [new FlowSubjectDraftDto("Toán", null, 9m), new FlowSubjectDraftDto("Anh")]),
        ]);

        Assert.Equal(8.22m, TranscriptGpa.SubjectAverage10(step2));
        Assert.Equal(3.29m, TranscriptGpa.Gpa4(step2));
    }

    [Fact]
    public void SubjectAverage10_NoScores_IsNull()
    {
        Assert.Null(TranscriptGpa.SubjectAverage10(new FlowTranscriptDraftDto([new FlowAcademicYearDraftDto("Lớp 10", [])])));
        Assert.Null(TranscriptGpa.SubjectAverage10(null));
    }
}
