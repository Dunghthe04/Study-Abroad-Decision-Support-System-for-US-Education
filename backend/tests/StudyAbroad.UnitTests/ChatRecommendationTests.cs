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

    // ---- Dữ kiện gửi Claude viết lời cho danh sách
    [Fact]
    public void Note_SendsTheTotalAndOneLinePerGroup_AsInTheCodeSummary()
    {
        var note = ChatRecommendation.Note(Run(Items))!;

        Assert.Equal(
            [
                "Tổng cộng 6 trường phù hợp với hồ sơ của bạn.",
                "Khó vào: Massachusetts Institute of Technology",
                "Vừa sức: University of Illinois Urbana-Champaign, Arizona State University, University of Arizona, University of Oregon",
                "An toàn hơn: Oregon State University",
            ],
            note.Facts);
        Assert.Equal(6, note.Schools.Count); // mọi trường đang hiện, mỗi trường một lần
        Assert.Empty(note.Others);
    }

    [Fact]
    public void Note_CutsAGroupAtFourLikeThePage_AndCountsTheRest()
    {
        var names = string.Join(",", Enumerable.Range(1, 6).Select(i => $$"""{ "code": "S{{i}}", "name": "School {{i}}" }"""));
        var codes = string.Join(",", Enumerable.Range(1, 6).Select(i => $$"""{ "code": "S{{i}}" }"""));
        var note = ChatRecommendation.Note(Run($$"""{ "scored": [ {{names}} ], "match": [ {{codes}} ] }"""))!;

        Assert.Equal(["Tổng cộng 6 trường phù hợp với hồ sơ của bạn.", "Vừa sức: School 1, School 2, School 3, School 4 và 2 trường khác"], note.Facts);
        Assert.Equal(["School 1", "School 2", "School 3", "School 4"], note.Schools);
        Assert.Equal(["School 5", "School 6"], note.Others); // đã chấm nhưng không hiện: advisor chặn nhắc tới
    }

    [Fact]
    public void Note_IsNull_WhenNoGroupHasASchool()
    {
        Assert.Null(ChatRecommendation.Note(Run("""{ "scored": [], "reach": [], "match": [], "safety": [], "aid": [] }""")));
        Assert.Null(ChatRecommendation.Note(Run("{}")));
    }

    [Theory]
    [InlineData("""{"note":true}""", true)]
    [InlineData("""{"note":false}""", false)]
    [InlineData("""{"code":"MIT"}""", false)]
    [InlineData("""{"note":"true"}""", false)]
    [InlineData("[]", false)]
    public void NoteRequested_IsTrueOnlyForNoteTrue(string json, bool expected)
    {
        Assert.Equal(expected, ChatRecommendation.NoteRequested(JsonDocument.Parse(json).RootElement));
    }

    [Fact]
    public void NoteRequested_IsFalse_WhenThereIsNoActionData()
    {
        Assert.False(ChatRecommendation.NoteRequested(null));
    }

    [Fact]
    public void Schools_ListsEveryShownSchoolOnce_InPageOrder_WithItsGroupCode()
    {
        var items = """
            {
              "scored": [
                { "code": "MIT", "name": "Massachusetts Institute of Technology", "g2": "R" },
                { "code": "ASU", "name": "Arizona State University", "g2": "M" },
                { "code": "UO", "name": "University of Oregon" },
                { "code": "XXX", "name": "Không nằm trong nhóm nào" }
              ],
              "reach": [ { "code": "MIT" } ],
              "match": [ { "code": "ASU" }, { "code": "UO" } ],
              "safety": [],
              "aid": [ { "code": "ASU" } ],
              "other": [ { "code": "XXX" } ]
            }
            """;

        var schools = ChatRecommendation.Schools(Run(items));

        Assert.Equal(
            [
                new ChatSchool("MIT", "Massachusetts Institute of Technology", "R"),
                new ChatSchool("ASU", "Arizona State University", "M"),
                new ChatSchool("UO", "University of Oregon", null),
            ],
            schools);
    }

    // Trường đã chấm nhưng không nằm trong khối nào (mỗi nhóm chỉ hiện vài trường) vẫn khớp tên được, chỉ không đếm vị trí
    [Fact]
    public void OtherSchools_AreTheScoredOnesThatNoBlockShows()
    {
        var run = Run("""
            {
              "scored": [
                { "code": "a", "name": "Alpha University", "g2": "M" },
                { "code": "b", "name": "Beta University", "g2": "R" },
                { "code": "c", "name": "Gamma University", "g2": "S" }
              ],
              "reach": [ { "code": "b" } ], "match": [ { "code": "a" } ], "safety": [], "aid": [], "other": []
            }
            """);

        var others = ChatRecommendation.OtherSchools(run);

        Assert.Equal(["c"], others.Select(s => s.Code));
        Assert.DoesNotContain(ChatRecommendation.Schools(run), s => s.Code == "c");   // vị trí chỉ đếm trong các khối đang hiện
    }

    [Fact]
    public void Schools_IsEmptyWhenNothingWasFound()
    {
        Assert.Empty(ChatRecommendation.Schools(Run("""{ "scored": [], "reach": [] }""")));
    }

    [Fact]
    public void Summarize_CutsTheAidGroupToFourNames()
    {
        var items = """
            { "scored": [], "aid": [ {"code":"A1"}, {"code":"A2"}, {"code":"A3"}, {"code":"A4"}, {"code":"A5"}, {"code":"A6"} ] }
            """;

        Assert.Contains("- Chỉ đi được nếu có học bổng lớn: A1, A2, A3, A4 và 2 trường khác", ChatRecommendation.Summarize(Run(items)));
    }

    [Fact]
    public void Summarize_CutsAGroupAtItsPageLimit()
    {
        var items = """
            { "scored": [], "reach": [ {"code":"R1"}, {"code":"R2"}, {"code":"R3"}, {"code":"R4"}, {"code":"R5"} ] }
            """;

        Assert.Contains("- Khó vào: R1, R2, R3, R4 và 1 trường khác", ChatRecommendation.Summarize(Run(items)));
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
        Assert.Equal(AdvisorMessages.RecommendationEmpty, ChatRecommendation.Summarize(Run(items)));
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
