using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

// Goal-seeking trong chat: đọc câu hỏi, hỏi lại khi mơ hồ, tóm tắt kết quả, mở khung kết quả trên trang gợi ý.
public class ChatGoalSeekTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    private static readonly GoalBudgetDto OkBudget = new("ok", 38_000m, 50_000m, 34_600m, null, null);

    private static SchoolGoalResult Result(
        string outcome = "reachable", string current = "R", string target = "M", string best = "M", string[]? reasons = null,
        GoalPlanDto? cheapest = null, GoalPlanDto? recommended = null, GoalPlanDto[]? alone = null,
        GoalBudgetDto? budget = null, GoalAlternativeDto[]? alternatives = null) =>
        new("188429", "Boston University", current, target, outcome, best, reasons ?? [], cheapest, recommended, alone ?? [], [],
            budget ?? OkBudget, null, alternatives ?? []);

    private static GoalPlanDto Sat(decimal from, decimal to, string difficulty = "hard") =>
        new([new GoalChangeDto("sat", from, to)], 1.4, difficulty);

    // ---------------------------------------------------------------- đọc câu hỏi

    [Fact]
    public void Parse_ReadsTheSchoolAndTheTarget()
    {
        var query = ChatGoalSeek.Parse(Json("""{"school":"  BU ","target":"match"}"""))!;

        Assert.Equal(new ChatGoalQuery("BU", "match"), query);
    }

    [Fact]
    public void Parse_AMissingTargetStaysNull()
    {
        Assert.Null(ChatGoalSeek.Parse(Json("""{"school":"MIT","target":null}"""))!.Target);
        Assert.Null(ChatGoalSeek.Parse(Json("""{"school":"MIT"}"""))!.Target);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("""{}""")]
    [InlineData("""{"school":""}""")]
    [InlineData("""{"school":"   "}""")]
    [InlineData("""{"school":42}""")]
    public void Parse_WithoutASchool_IsNull(string? json) => Assert.Null(ChatGoalSeek.Parse(json is null ? null : Json(json)));

    // ---------------------------------------------------------------- hỏi lại

    [Fact]
    public void Choices_EachButtonSendsTheWholeQuestion_SoNoStateNeedsToBeKept()
    {
        SchoolRef[] candidates = [new("1", "Boston University"), new("2", "Baylor University")];

        var buttons = ChatGoalSeek.Choices(candidates, null);

        Assert.Equal(
            [new QuickReply("Boston University", "Cần gì để vào Boston University?"), new QuickReply("Baylor University", "Cần gì để vào Baylor University?")],
            buttons);
    }

    [Theory]
    [InlineData("match", "Cần gì để vào Boston University ở nhóm Vừa sức?")]
    [InlineData("safety", "Cần gì để vào Boston University ở nhóm An toàn hơn?")]
    [InlineData("reach", "Cần gì để vào Boston University ở nhóm Khó vào?")]
    public void Question_KeepsTheTargetTheStudentAskedFor(string target, string expected) =>
        Assert.Equal(expected, ChatGoalSeek.Question("Boston University", target));

    [Fact]
    public void Clarify_AsksWhichSchoolWasMeant()
    {
        Assert.Contains("ý bạn là trường nào", ChatGoalSeek.Clarify("BU", 5));
        Assert.Contains("“BU”", ChatGoalSeek.Clarify("BU", 5));
        Assert.Equal("Ý bạn là trường này phải không?", ChatGoalSeek.Clarify("Hopkins", 1));
    }

    [Fact]
    public void NotFound_SaysTheSchoolIsNotInTheList()
    {
        Assert.Contains("chưa thấy trường “Yale”", ChatGoalSeek.NotFound("Yale"));
    }

    [Fact]
    public void SchoolPicker_CarriesTheTargetSoTheQuestionIsKept()
    {
        var picker = ChatGoalSeek.SchoolPicker("safety");

        Assert.Equal("school", picker.Type);
        Assert.Equal("safety", picker.Data!.Value.GetProperty("target").GetString());
        Assert.Equal(JsonValueKind.Null, ChatGoalSeek.SchoolPicker(null).Data!.Value.GetProperty("target").ValueKind);
    }

    // ---------------------------------------------------------------- tóm tắt kết quả

    [Fact]
    public void Summarize_StartsByRestatingTheSchoolItUnderstood_ThenFinance()
    {
        var lines = ChatGoalSeek.Summarize(Result(alone: [Sat(1290, 1430)])).Split('\n');

        Assert.Equal("Mình hiểu bạn hỏi về Boston University, nhóm muốn đạt: Vừa sức.", lines[0]);
        Assert.StartsWith("Tài chính: Chi phí khoảng 38.000 USD mỗi năm, nằm trong ngân sách", lines[1]);
    }

    // Trường bị ẩn vì tiếng Anh: dòng tiếng Anh đứng trước tài chính, nêu mức tối thiểu từng bài thi
    [Fact]
    public void Summarize_ASchoolHiddenForEnglish_SaysTheScoreNeededBeforeFinance()
    {
        var result = Result(alone: [Sat(1290, 1430)]) with
        {
            FilterReason = "ENGLISH",
            English = new GoalEnglishDto(7.5m, 100m, 130m, 1.5m)
        };

        var lines = ChatGoalSeek.Summarize(result).Split('\n');

        Assert.StartsWith("Tiếng Anh: điểm của bạn chưa đủ", lines[1]);
        Assert.Contains("IELTS 7,5, TOEFL 100 hoặc Duolingo 130 (một bài là đủ)", lines[1]);
        Assert.StartsWith("Tài chính:", lines[2]);
    }

    [Fact]
    public void Summarize_WithoutACertificate_SaysSoAndShowsTheOnlyScoreTheSchoolAsks()
    {
        var result = Result(alone: [Sat(1290, 1430)]) with
        {
            FilterReason = "ENGLISH_NO_CERT",
            English = new GoalEnglishDto(6.5m, null, null, null)
        };

        var line = ChatGoalSeek.Summarize(result).Split('\n')[1];

        Assert.StartsWith("Tiếng Anh: bạn chưa có chứng chỉ", line);
        Assert.Contains("Cần đạt IELTS 6,5.", line);
    }

    [Fact]
    public void Summarize_Reachable_ListsEachFactorAloneTheCombinationAndTheRecommendedLevel()
    {
        var combined = new GoalPlanDto([new("sat", 1290, 1370), new("ecTier", 2, 3)], 1.1, "hard");
        var result = Result(alone: [Sat(1290, 1430), new GoalPlanDto([new("gpa", 3.5m, 3.9m)], 4, "veryHard")],
            cheapest: combined, recommended: Sat(1290, 1460));

        var text = ChatGoalSeek.Summarize(result);

        Assert.Contains("- SAT 1290 → 1430 (khó)", text);
        Assert.Contains("- GPA 3.5 → 3.9 (rất khó)", text);
        Assert.Contains("Kết hợp ít công sức nhất: SAT 1290 → 1370 và bậc ngoại khóa 2 → 3 (khó)", text);
        Assert.Contains("Nên đặt mục tiêu (có đệm): SAT 1290 → 1460 (khó)", text);
    }

    [Fact]
    public void Summarize_WhenNoSingleFactorWorks_SaysACombinationIsNeeded()
    {
        var text = ChatGoalSeek.Summarize(Result(alone: [], cheapest: new GoalPlanDto([new("sat", 1290, 1500), new("gpa", 3.5m, 3.9m)], 3, "veryHard")));

        Assert.Contains("Một yếu tố riêng chưa đủ", text);
    }

    [Fact]
    public void Summarize_Blocked_SaysWhyAndTheBestGroup_AndOffersSimilarSchools()
    {
        var result = Result(outcome: "blocked", current: "R", target: "M", best: "R", reasons: [GroupReasons.LowAcceptRate],
            alternatives: [new("2", "Denison University", "OH", "private", 95_900m, "M"), new("3", "University of Puget Sound", "WA", "private", 89_872m, "M")]);

        var text = ChatGoalSeek.Summarize(result);

        Assert.Contains("chỉ nhận dưới 15% hồ sơ nên luôn xếp Khó vào", text);
        Assert.Contains("Nhóm cao nhất bạn có thể đạt ở trường này là Khó vào", text);
        Assert.Contains("Trường tương tự đang ở nhóm này với bạn: Denison University, University of Puget Sound.", text);
    }

    [Fact]
    public void Summarize_AlreadyReached_SaysNothingIsNeeded()
    {
        var text = ChatGoalSeek.Summarize(Result(outcome: "already_reached", current: "M"));

        Assert.Contains("bạn đã ở nhóm Vừa sức, không cần thay đổi gì", text);
    }

    [Fact]
    public void Summarize_NoData_SaysTheLevelCannotBeComputed()
    {
        var text = ChatGoalSeek.Summarize(Result(outcome: "no_data", current: "NA", reasons: [GroupReasons.NoAcademicData]));

        Assert.Contains("chưa có đủ số liệu học thuật", text);
        Assert.Contains("chưa tính được mức cần đạt", text);
    }

    [Theory]
    [InlineData("need_aid", "vượt ngân sách khoảng 7.000 USD: cần ngân sách từ 56.400 USD mỗi năm hoặc học bổng lớn")]
    [InlineData("over_budget", "vượt ngân sách và trường không có học bổng phù hợp nên không nằm trong danh sách của bạn")]
    [InlineData("no_budget", "Hồ sơ của bạn không đặt trần ngân sách")]
    [InlineData("unknown_cost", "Trường chưa có số liệu chi phí")]
    public void Summarize_FinanceComesFirst_AndSaysWhatTheBudgetMeans(string status, string expected)
    {
        var budget = new GoalBudgetDto(status, 62_000m, status == "no_budget" ? null : 50_000m, 56_400m, 7_000m, null);

        var text = ChatGoalSeek.Summarize(Result(alone: [Sat(1290, 1430)], budget: budget));

        Assert.Contains(expected, text);
    }

    // ---------------------------------------------------------------- dữ kiện gửi Gemini

    [Fact]
    public void Note_SendsTheComputedFactsWithoutTheEchoLineOrTheBulletMarks()
    {
        var result = Result(alone: [Sat(1290, 1430)], recommended: Sat(1290, 1460));

        var note = ChatGoalSeek.Note(result, ["Boston University", "Harvard University"]);

        Assert.Equal(("Boston University", "Vừa sức", "reachable"), (note.School, note.Target, note.Outcome));
        Assert.Equal(
            [
                "Tài chính: Chi phí khoảng 38.000 USD mỗi năm, nằm trong ngân sách của bạn.",
                "Học thuật: để lên nhóm Vừa sức, mức tối thiểu khi chỉ đổi một yếu tố:",
                "SAT 1290 → 1430 (khó)",
                "Nên đặt mục tiêu (có đệm): SAT 1290 → 1460 (khó)",
            ],
            note.Facts);
    }

    [Fact]
    public void Note_OthersAreEveryOtherSchool_ExcludingTheOneAskedAndTheAlternatives()
    {
        var result = Result(outcome: "blocked", alternatives: [new("2", "Denison University", "OH", "private", 95_900m, "M")]);

        var note = ChatGoalSeek.Note(result, ["Boston University", "Denison University", "Harvard University", "Harvard University", "Yale College"]);

        Assert.Equal(["Denison University"], note.Alternatives);
        Assert.Equal(["Harvard University", "Yale College"], note.Others);   // không lặp, không gồm trường hỏi và trường tương tự
    }

    [Fact]
    public void Note_AlternativesAreListedOnlyOnce_NotAlsoAsAFact()
    {
        var result = Result(outcome: "blocked", reasons: [GroupReasons.LowAcceptRate],
            alternatives: [new("2", "Denison University", "OH", "private", 95_900m, "M")]);

        var note = ChatGoalSeek.Note(result, []);

        Assert.DoesNotContain(note.Facts, f => f.StartsWith("Trường tương tự"));
        Assert.Contains(note.Facts, f => f.Contains("chỉ nhận dưới 15% hồ sơ"));
    }

    // ---------------------------------------------------------------- khung kết quả trên trang

    [Theory]
    [InlineData("M", "match")]
    [InlineData("S", "safety")]
    [InlineData("R", "reach")]
    public void WidgetOf_CarriesTheSchoolCodeAndTheTargetForThePage(string target, string expected)
    {
        var widget = ChatGoalSeek.WidgetOf(Result(target: target));

        Assert.Equal("goal_seek", widget.Type);
        Assert.Equal("188429", widget.Data!.Value.GetProperty("code").GetString());
        Assert.Equal(expected, widget.Data.Value.GetProperty("target").GetString());
    }
}
