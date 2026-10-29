using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

/// <summary>Gợi ý trường trong chat: tóm tắt kết quả gợi ý thành câu chat và bật hasResults.</summary>
public class ChatRecommendationTests
{
    // Đúng dạng ItemsJson RecommendationService lưu: scored có code + name; các nhóm có code, x, why
    private const string Items = """
        {
          "scored": [
            { "code": "MIT", "name": "Massachusetts Institute of Technology" },
            { "code": "UIUC", "name": "University of Illinois Urbana-Champaign" },
            { "code": "ASU", "name": "Arizona State University" },
            { "code": "UA", "name": "University of Arizona" },
            { "code": "UO", "name": "University of Oregon" },
            { "code": "OSU", "name": "Oregon State University" }
          ],
          "reach": [ { "code": "MIT", "x": 1, "why": "..." } ],
          "match": [
            { "code": "UIUC" }, { "code": "ASU" }, { "code": "UA" }, { "code": "UO" }
          ],
          "safety": [ { "code": "OSU" } ],
          "aid": [],
          "other": [ { "code": "UO" } ]
        }
        """;

    private static Recommendation Run(string items) => new() { ItemsJson = items };

    [Fact]
    public void Summarize_ListsEachGroupWithSchoolNames_InPageOrder()
    {
        var text = ChatRecommendation.Summarize(Run(Items));

        Assert.Equal(
            "Mình đã tìm được các trường phù hợp với hồ sơ của bạn:\n" +
            "- Khó vào: Massachusetts Institute of Technology\n" +
            "- Vừa sức: University of Illinois Urbana-Champaign, Arizona State University, University of Arizona, University of Oregon\n" +
            "- An toàn hơn: Oregon State University\n" +
            ChatRecommendation.More,
            text);
    }

    [Fact]
    public void Summarize_CutsTheAidGroupToThreeNames()
    {
        var items = """
            { "scored": [], "aid": [ {"code":"A1"}, {"code":"A2"}, {"code":"A3"}, {"code":"A4"}, {"code":"A5"} ] }
            """;

        Assert.Contains("- Chỉ đi được nếu có học bổng lớn: A1, A2, A3 và 2 trường khác", ChatRecommendation.Summarize(Run(items)));
    }

    [Fact]
    public void Summarize_CutsAGroupAtItsPageLimit()
    {
        var items = """
            { "scored": [], "reach": [ {"code":"R1"}, {"code":"R2"}, {"code":"R3"}, {"code":"R4"} ] }
            """;

        Assert.Contains("- Khó vào: R1, R2, R3 và 1 trường khác", ChatRecommendation.Summarize(Run(items)));
    }

    [Fact]
    public void Summarize_SkipsEmptyGroups_AndTheOtherGroup()
    {
        var text = ChatRecommendation.Summarize(Run(Items));

        Assert.DoesNotContain("học bổng lớn", text); // nhóm aid rỗng
        Assert.Equal(4, text.Split("\n- ").Length); // 3 dòng nhóm: nhóm other không hiện trong chat
    }

    [Fact]
    public void Summarize_FallsBackToTheCode_WhenTheNameIsMissing()
    {
        var text = ChatRecommendation.Summarize(Run("""{ "scored": [], "reach": [ { "code": "XYZ" } ] }"""));

        Assert.Contains("- Khó vào: XYZ", text);
    }

    [Theory]
    [InlineData("""{ "scored": [], "reach": [], "match": [], "safety": [], "aid": [] }""")]
    [InlineData("""{}""")]
    [InlineData("""[]""")]
    public void Summarize_SaysNothingMatched_WhenThereIsNoSchool(string items)
    {
        Assert.Equal(ChatRecommendation.Empty, ChatRecommendation.Summarize(Run(items)));
    }

    [Fact]
    public void WithResults_TurnsOnHasResults_AndKeepsTheRestOfTheState()
    {
        var state = JsonDocument.Parse("""{ "currentStep": "B4", "hasResults": false, "asked": { "X2": 1 } }""").RootElement;

        var updated = ChatRecommendation.WithResults(state)!.Value;

        Assert.True(updated.GetProperty("hasResults").GetBoolean());
        Assert.Equal("B4", updated.GetProperty("currentStep").GetString());
        Assert.Equal(1, updated.GetProperty("asked").GetProperty("X2").GetInt32());
    }

    [Fact]
    public void WithResults_LeavesAMissingState_Alone()
    {
        Assert.Null(ChatRecommendation.WithResults(null));
    }
}
