using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

// What-if trong chat: đọc giá trị advisor đã kiểm tra, dựng dữ liệu cho Gemini viết lời, câu mẫu dự phòng, dữ liệu cho panel.
public class ChatSimulationTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    private static ChatScenario Scenario(SimulationOverrides o, params string[] unsupported) => new(o, unsupported);

    private static SimulationOverrides Only(
        decimal? gpa = null, decimal? sat = null, decimal? act = null, decimal? ielts = null,
        decimal? budget = null, string? need = null) => new(gpa, sat, act, ielts, budget, need, null);

    private static readonly SimulationProfile Profile = new(3.5m, 1290, null, 6.5m, 40_000m, "preferred", 3);
    private static readonly GroupSummary Counts = new(120, 50, 40, 20, 6, 4);

    // Kết quả mô phỏng: Boston và Northeastern vừa lên từ Khó vào; Harvard có trong kết quả nhưng không thuộc khối nào đang nêu
    private const string Items = """
        {"scored":[
          {"code":"cornell","name":"Cornell University"},{"code":"duke","name":"Duke University"},
          {"code":"bu","name":"Boston University"},{"code":"ne","name":"Northeastern University"},
          {"code":"rutgers","name":"Rutgers University"},{"code":"harvard","name":"Harvard University"}],
         "reach":[{"code":"cornell","why":["LOW_RATE"]},{"code":"duke","why":[]}],
         "match":[{"code":"bu","why":["SAT_ABOVE","BUDGET_OK","INTL_LOW"]},{"code":"ne","why":["SAT_ABOVE"]}],
         "safety":[{"code":"rutgers","why":[]}],
         "aid":[],"other":[]}
        """;

    private static SimulationResult Result(string items = Items, params SchoolChange[] changes) =>
        new(Profile, Counts, Counts, changes, "{}", items);

    private static readonly SchoolChange[] Better =
    [
        new("bu", "Boston University", "R", "M"),
        new("ne", "Northeastern University", "R", "M"),
    ];

    // ---------------------------------------------------------------- đọc giá trị advisor đã kiểm tra

    [Fact]
    public void Parse_ReadsTheCheckedValues()
    {
        var data = Json("""{"changes":{"sat":1500,"ielts":7.5,"budgetUsd":50000,"scholarshipNeed":"optional"},"unsupported":["ngành"]}""");

        var scenario = ChatSimulation.Parse(data)!;

        Assert.Equal(Only(sat: 1500, ielts: 7.5m, budget: 50_000m, need: "optional"), scenario.Overrides);
        Assert.Equal(["ngành"], scenario.Unsupported);
    }

    [Fact]
    public void Parse_ReadsToeflAndDuolingo_AndLabelsThemWithTheCurrentValue()
    {
        var scenario = ChatSimulation.Parse(Json("""{"changes":{"toefl":100,"duolingo":120}}"""))!;

        Assert.Equal(100m, scenario.Overrides.Toefl);
        Assert.Equal(120m, scenario.Overrides.Duolingo);
        var profile = new SimulationProfile(null, null, null, null, null, null, 1, Toefl: 80m);
        var note = ChatSimulation.Note(new SimulationResult(profile, Counts, Counts, [], "{}", "{}"), scenario);
        Assert.Equal("TOEFL 100 (hiện tại 80), Duolingo 120", note.Label);
        Assert.Equal(["toefl", "duolingo"], note.Factors);
        var widget = ChatSimulation.WidgetOf(scenario);
        Assert.Equal(100m, widget.Data!.Value.GetProperty("toefl").GetDecimal());
    }

    [Theory]
    [InlineData("""{"changes":{"gpa":{"value":3.8,"scale":4}}}""", 3.8)]
    [InlineData("""{"changes":{"gpa":{"value":8.9,"scale":10}}}""", 3.8)]   // thang 10 đổi sang thang 4 theo bảng mặc định như hồ sơ
    [InlineData("""{"changes":{"gpa":{"value":3.8}}}""", 3.8)]              // không nói thang: thang 4
    public void Parse_ConvertsGpaToTheFourPointScale(string json, double expected)
    {
        Assert.Equal((decimal)expected, ChatSimulation.Parse(Json(json))!.Overrides.Gpa);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("""{}""")]
    [InlineData("""{"changes":{}}""")]
    [InlineData("""{"changes":"sat 1500"}""")]
    [InlineData("""{"changes":{"sat":"1500"}}""")]   // số phải là số, không đoán từ chữ
    public void Parse_WithoutAnyUsableValue_IsNull(string? json)
    {
        Assert.Null(ChatSimulation.Parse(json is null ? null : Json(json)));
    }

    // ---------------------------------------------------------------- dữ liệu cho Gemini

    [Fact]
    public void Note_ListsTheGroupsOfTheSimulatedResult_AndMarksTheSchoolsThatMoved()
    {
        var note = ChatSimulation.Note(Result(Items, Better), Scenario(Only(sat: 1500)));

        Assert.Equal("better", note.Direction);
        Assert.Equal(["reach", "match", "safety"], note.Groups.Select(g => g.Key));   // khối trống thì bỏ qua
        Assert.Equal(["Khó vào", "Vừa sức", "An toàn hơn"], note.Groups.Select(g => g.Title));

        var match = note.Groups[1];
        Assert.Equal(["Boston University", "Northeastern University"], match.Schools.Select(s => s.Name));
        Assert.All(match.Schools, s => Assert.Equal("Khó vào", s.MovedFrom));
        Assert.Equal(["SAT_ABOVE", "BUDGET_OK"], match.Schools[0].Reasons);   // tối đa 2 lý do
        Assert.All(note.Groups[0].Schools, s => Assert.Null(s.MovedFrom));    // trường không đổi nhóm không kèm lý do
        Assert.All(note.Groups[0].Schools, s => Assert.Empty(s.Reasons));
    }

    [Fact]
    public void Note_StatesWhatWasTriedWithTheCurrentValues()
    {
        var note = ChatSimulation.Note(Result(Items, Better), Scenario(Only(sat: 1500), "ngành"));

        Assert.Equal("SAT 1500 (hiện tại 1290)", note.Label);
        Assert.Equal(["sat"], note.Factors);
        Assert.Equal(["ngành"], note.Unsupported);
        Assert.Equal(new ScenarioStudentDto(40_000, "nên có"), note.Student);
    }

    [Fact]
    public void Note_FormatsEveryKindOfValue()
    {
        var overrides = Only(gpa: 3.8m, ielts: 7.5m, budget: 50_000m, need: "mandatory");

        var note = ChatSimulation.Note(Result(Items, Better), Scenario(overrides));

        Assert.Equal(
            "GPA 3.8 (hiện tại 3.5), IELTS 7.5 (hiện tại 6.5), ngân sách 50.000 USD mỗi năm (hiện tại 40.000 USD), mức cần học bổng bắt buộc (hiện tại nên có)",
            note.Label);
        Assert.Equal(["gpa", "ielts", "budget", "scholarship"], note.Factors);
        Assert.Equal(new ScenarioStudentDto(50_000, "bắt buộc"), note.Student);   // ngân sách và học bổng lấy từ kịch bản
    }

    [Fact]
    public void Note_OmitsTheCurrentValueWhenTheProfileHasNone()
    {
        var result = new SimulationResult(
            new SimulationProfile(null, null, null, null, null, null, 1), Counts, Counts, Better, "{}", Items);

        var note = ChatSimulation.Note(result, Scenario(Only(sat: 1500)));

        Assert.Equal("SAT 1500", note.Label);
    }

    [Fact]
    public void Note_ListsTheOtherSchoolsOfTheResult_SoTheAdvisorCanRejectInventedNames()
    {
        var note = ChatSimulation.Note(Result(Items, Better), Scenario(Only(sat: 1500)));

        Assert.Equal(["Harvard University"], note.Others);
    }

    [Fact]
    public void Note_WhenNothingMovedGroups_SendsNoList()
    {
        var note = ChatSimulation.Note(Result(Items), Scenario(Only(ielts: 7.5m)));

        Assert.Equal("none", note.Direction);
        Assert.Empty(note.Groups);
    }

    [Theory]
    [InlineData("S", "M", "worse")]
    [InlineData("M", "R", "worse")]
    [InlineData("M", "AID", "worse")]
    [InlineData("OUT", "M", "better")]
    [InlineData("AID", "R", "better")]
    [InlineData("NA", "M", "better")]
    [InlineData("R", "NA", "none")]    // chưa đủ dữ liệu coi như Khó vào, không phải thay đổi để nói
    public void Note_DirectionComesFromTheRankOfTheGroups(string from, string to, string expected)
    {
        var note = ChatSimulation.Note(Result(Items, new SchoolChange("bu", "Boston University", from, to)), Scenario(Only(sat: 1500)));

        Assert.Equal(expected, note.Direction);
    }

    [Fact]
    public void Note_WithBothDirections_IsMixed()
    {
        SchoolChange[] changes = [new("bu", "Boston University", "R", "M"), new("rutgers", "Rutgers University", "S", "M")];

        Assert.Equal("mixed", ChatSimulation.Note(Result(Items, changes), Scenario(Only(gpa: 3m))).Direction);
    }

    [Fact]
    public void Note_ShowsAtMostFourSchoolsPerGroup_AndCountsTheRest()
    {
        var scored = string.Join(",", Enumerable.Range(0, 6).Select(i => $"{{\"code\":\"s{i}\",\"name\":\"School {i}\"}}"));
        var reach = string.Join(",", Enumerable.Range(0, 6).Select(i => $"{{\"code\":\"s{i}\",\"why\":[]}}"));
        var items = $"{{\"scored\":[{scored}],\"reach\":[{reach}],\"match\":[],\"safety\":[],\"aid\":[],\"other\":[]}}";

        var note = ChatSimulation.Note(Result(items, new SchoolChange("s5", "School 5", "OUT", "R")), Scenario(Only(sat: 1500)));

        var reachGroup = Assert.Single(note.Groups);
        Assert.Equal(4, reachGroup.Schools.Count);
        Assert.Equal(2, reachGroup.More);
        Assert.Equal(["School 4", "School 5"], note.Others.Where(n => n is "School 4" or "School 5").OrderBy(n => n));
    }

    [Fact]
    public void Note_MarksTheSchoolsOfTheAidBlock()
    {
        var items = """{"scored":[{"code":"bu","name":"Boston University"}],"reach":[],"match":[],"safety":[],"aid":[{"code":"bu","why":["NEED_AID"]}],"other":[]}""";

        var note = ChatSimulation.Note(Result(items, new SchoolChange("bu", "Boston University", "OUT", "AID")), Scenario(Only(budget: 30_000m)));

        var aid = Assert.Single(note.Groups);
        Assert.Equal(("aid", "Cần học bổng lớn"), (aid.Key, aid.Title));
        Assert.True(aid.Schools[0].NeedAid);
        Assert.Equal("Bị loại", aid.Schools[0].MovedFrom);
    }

    // ---------------------------------------------------------------- câu mẫu dự phòng

    [Fact]
    public void Fallback_ListsTheGroupsLikeTheRecommendationSummary()
    {
        var note = ChatSimulation.Note(Result(Items, Better), Scenario(Only(sat: 1500), "ngành"));

        var text = ChatSimulation.Fallback(note);

        Assert.Equal(
            "Với SAT 1500 (hiện tại 1290) thì mình gợi ý như sau:\n" +
            "- Khó vào: Cornell University, Duke University\n" +
            "- Vừa sức: Boston University, Northeastern University\n" +
            "- An toàn hơn: Rutgers University\n" +
            "Bạn xem chi tiết ở bảng kết quả nhé.\n" +
            "Mình chưa thử được: ngành.",
            text);
    }

    [Fact]
    public void Fallback_WhenNothingMoved_SaysSo()
    {
        var note = ChatSimulation.Note(Result(Items), Scenario(Only(ielts: 7.5m)));

        Assert.Equal(
            "Với IELTS 7.5 (hiện tại 6.5), danh sách trường không đổi nhóm. Bạn xem chi tiết ở bảng kết quả nhé.",
            ChatSimulation.Fallback(note));
    }

    // ---------------------------------------------------------------- dữ liệu cho panel

    [Fact]
    public void WidgetOf_SendsOnlyTheValuesTheStudentGave()
    {
        var widget = ChatSimulation.WidgetOf(Scenario(Only(sat: 1500, budget: 50_000m, need: "optional")));

        Assert.Equal("simulate", widget.Type);
        var data = widget.Data!.Value;
        Assert.Equal(1500, data.GetProperty("sat").GetInt32());
        Assert.Equal(50_000, data.GetProperty("budgetUsd").GetInt32());
        Assert.Equal("optional", data.GetProperty("scholarshipNeed").GetString());
        Assert.False(data.TryGetProperty("gpa", out _));
    }
}
