using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Entities;

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
        Assert.Equal(3.57m, Math.Round(TranscriptGpa.Gpa4(step2)!.Value, 2)); // 8.22 nội suy giữa 8 → 3.5 và 9.5 → 4.0 ≈ 3.573
    }

    [Fact]
    public void SubjectAverage10_NoScores_IsNull()
    {
        Assert.Null(TranscriptGpa.SubjectAverage10(new FlowTranscriptDraftDto([new FlowAcademicYearDraftDto("Lớp 10", [])])));
        Assert.Null(TranscriptGpa.SubjectAverage10((FlowTranscriptDraftDto?)null));
    }

    // Cùng ví dụ trên nhưng đọc từ transcript_scores đã lưu (lớp 11 là TermOrder 1–2, lớp 12 là 3–4): phải ra cùng 8.22
    [Fact]
    public void SubjectAverage10_FromSavedScores_MatchesDraft()
    {
        TranscriptScore Row(int termOrder, int semester, string subject, decimal score) =>
            new() { TermOrder = termOrder, Semester = semester, Subject = subject, Score = score };

        var scores = new[]
        {
            Row(1, 1, "Toán", 8m), Row(2, 2, "Toán ", 9m), Row(1, 1, "Văn", 7m), Row(4, 2, "Toán", 9m),
        };

        Assert.Equal(8.22m, TranscriptGpa.SubjectAverage10(scores));
        Assert.Null(TranscriptGpa.SubjectAverage10(Array.Empty<TranscriptScore>()));
    }

    // Bảng mặc định mốc THPT EducationUSA, nội suy giữa hai mốc kề nhau: không còn nhảy bậc ở sát mốc
    [Theory]
    [InlineData(10, 4.0)]
    [InlineData(9.5, 4.0)]
    [InlineData(8.9, 3.8)]
    [InlineData(8.0, 3.5)]
    [InlineData(7.995, 3.49875)]
    [InlineData(7.0, 3.25)]
    [InlineData(4.0, 1.6)]
    [InlineData(3.99, 1.596)]
    [InlineData(0, 0)]
    public void To4_InterpolatesDefaultAnchors(double gpa10, double expected)
    {
        Assert.Equal((decimal)expected, TranscriptGpa.To4((decimal)gpa10));
    }
}
