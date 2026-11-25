using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Recommendations;
using Xunit;

namespace StudyAbroad.UnitTests;

// UC-154: so sánh 2 đến 4 trường theo độ phù hợp với hồ sơ (nhóm và điểm năm thành phần), khác so sánh số liệu của trường (UC-156)
public class ChatFitCompareTests
{
    private static IReadOnlyList<FitPartDto> Parts(double? position, double? budget, double? quality, double? aid, double? english) =>
    [
        new FitPartDto("position", position, 0.30),
        new FitPartDto("budget", budget, 0.25),
        new FitPartDto("quality", quality, 0.20),
        new FitPartDto("aid", aid, 0.15),
        new FitPartDto("english", english, 0.10),
    ];

    private static FitCompareSchoolDto Bu() =>
        new("bu", "Boston University", "MA", FitCompareStatus.InList, "M", null, 0.61, Parts(0.40, 0.90, 0.70, 0.40, 1.0), ["GPA_MID"]);

    private static FitCompareSchoolDto Nyu() =>
        new("nyu", "New York University", "NY", FitCompareStatus.InList, "R", null, 0.45, Parts(0.20, 0.50, 0.80, 0.50, 0.90), ["LOW_RATE"]);

    private static FitCompareSchoolDto Out() =>
        new("tx", "Texas State", "TX", FitCompareStatus.Excluded, null, "BUDGET", null, [], []);

    private static FitCompareResult Result(params FitCompareSchoolDto[] schools) => new(schools, false);

    [Fact]
    public void Text_ListsEachSchoolWithItsGroupTheTotalAndTheFiveParts()
    {
        var text = ChatFitCompare.Text(Result(Bu(), Nyu()), null);

        Assert.Contains("Boston University: nhóm Vừa sức, điểm phù hợp 61/100", text);
        Assert.Contains("vừa tầm học thuật 40, ngân sách 90, chất lượng trường 70, học bổng 40, tiếng Anh 100", text);
        Assert.Contains("New York University: nhóm Khó vào, điểm phù hợp 45/100", text);
        Assert.Contains("chỉ dùng để sắp xếp các trường trong cùng một nhóm", text);
        Assert.DoesNotContain("nên chọn", text);   // hệ thống không chọn trường thay học sinh
    }

    [Fact]
    public void Text_PutsTheAiNoteFirstWhenThereIsOne()
    {
        var text = ChatFitCompare.Text(Result(Bu(), Nyu()), "Hai trường khác nhau ở nhóm.");

        Assert.StartsWith("Hai trường khác nhau ở nhóm.\n\n- Boston University", text);
    }

    [Fact]
    public void Text_ASchoolOutsideTheList_SaysWhyAndHasNoGroupOrScore()
    {
        var text = ChatFitCompare.Text(Result(Bu(), Out()), null);

        Assert.Contains("Texas State: không nằm trong gợi ý vì chi phí vượt ngân sách của bạn.", text);
        Assert.DoesNotContain("Texas State: nhóm", text);
    }

    [Theory]
    [InlineData("MAJOR", "không có ngành bạn chọn")]
    [InlineData("STATE", "ngoài các bang bạn chọn")]
    [InlineData("CONTROL", "khác loại trường")]
    [InlineData("ENGLISH", "tiếng Anh của bạn thấp hơn")]
    [InlineData("ENGLISH_NO_CERT", "chưa có chứng chỉ tiếng Anh")]
    public void Text_NamesEachExcludeReasonInWords(string reason, string expected)
    {
        var school = Out() with { ExcludeReason = reason };

        Assert.Contains(expected, ChatFitCompare.Text(Result(Bu(), school), null));
    }

    [Fact]
    public void Text_AidAndNoDataSchools_AreLabelledLikeTheResultsPage()
    {
        var aid = Bu() with { Code = "a", Name = "Colgate", Status = FitCompareStatus.Aid };
        var none = Nyu() with { Code = "n", Name = "Empty College", Status = FitCompareStatus.NoData, Group = "NA", Total = null, Parts = Parts(null, null, null, null, null) };

        var text = ChatFitCompare.Text(Result(aid, none), null);

        Assert.Contains("Colgate: nhóm Cần học bổng lớn", text);
        Assert.Contains("Empty College: nhóm Chưa đủ dữ liệu, chưa tính được điểm phù hợp", text);
        Assert.Contains("chất lượng trường chưa có số liệu", text);
    }

    [Fact]
    public void Facts_AreOneLinePerItemWithTheValueOfEachSchool_AndTheLowestPartIsFoundByCode()
    {
        var facts = ChatFitCompare.Facts(Result(Bu(), Nyu(), Out()));

        Assert.Equal("Nhóm: Boston University Vừa sức · New York University Khó vào", facts[0]);
        Assert.Equal("Điểm phù hợp: Boston University 61/100 · New York University 45/100", facts[1]);
        // BU: vừa tầm 40 và học bổng 40 cùng thấp nhất; NYU: vừa tầm 20
        Assert.Equal("Thành phần điểm thấp nhất: Boston University vừa tầm học thuật và học bổng (40) · New York University vừa tầm học thuật (20)", facts[2]);
        Assert.Contains(facts, f => f.StartsWith("Texas State: không nằm trong gợi ý vì chi phí vượt ngân sách"));
        Assert.Contains(facts, f => f.Contains("không làm trường đổi nhóm"));
    }

    [Fact]
    public void Facts_WhenEverySchoolIsOutside_HaveNoGroupOrScoreLines()
    {
        var facts = ChatFitCompare.Facts(Result(Out(), Out() with { Code = "x", Name = "Other" }));

        Assert.DoesNotContain(facts, f => f.StartsWith("Nhóm:") || f.StartsWith("Điểm phù hợp:"));
        Assert.Equal(3, facts.Count);   // hai trường bị loại và dòng nguyên tắc
    }

    [Fact]
    public void Widget_CarriesTheSchoolsPartsAndTheCoveredTextForTheCard()
    {
        var widget = ChatFitCompare.WidgetOf(new FitCompareResult([Bu(), Out()], true), "Nhận xét", "toàn văn");

        Assert.Equal("fit_compare", widget.Type);
        var data = widget.Data!.Value;
        Assert.True(data.GetProperty("priorityAdjusted").GetBoolean());
        Assert.Equal("Nhận xét", data.GetProperty("aiNote").GetString());
        Assert.Equal("toàn văn", data.GetProperty("coveredText").GetString());
        var first = data.GetProperty("schools")[0];
        Assert.Equal("M", first.GetProperty("group").GetString());
        Assert.Equal(0.61, first.GetProperty("total").GetDouble(), 6);
        Assert.Equal(5, first.GetProperty("parts").GetArrayLength());
        Assert.Equal("position", first.GetProperty("parts")[0].GetProperty("key").GetString());
        Assert.Equal("BUDGET", data.GetProperty("schools")[1].GetProperty("excludeReason").GetString());
    }

    [Fact]
    public void Widget_WithoutAnAiNote_HasNoAiNoteKey()
    {
        var widget = ChatFitCompare.WidgetOf(Result(Bu(), Nyu()), null, "t");

        Assert.False(widget.Data!.Value.TryGetProperty("aiNote", out _));
    }

    [Fact]
    public void NextSteps_OpenTheRecommendationsPage_ThenGoalSeekingForTheFirstTwoSchoolsStillInTheList()
    {
        var steps = ChatFitCompare.NextSteps(Result(Out(), Bu(), Nyu(), Bu() with { Code = "c", Name = "Third" }));

        Assert.Equal(("Mở trang Gợi ý trường", "/recommendations"), (steps[0].Label, steps[0].Href));
        Assert.Equal(3, steps.Count);   // trang gợi ý và hai trường đầu còn trong gợi ý, bỏ trường bị loại
        Assert.DoesNotContain(steps, s => s.Label.Contains("Texas State"));
    }
}
