using System.Text.Json;
using StudyAbroad.Application.Advisor;
using Xunit;

namespace StudyAbroad.UnitTests;

public class ChatNextStepsTests
{
    [Fact]
    public void AfterExplain_OffersGoalForThisSchool_AnotherSchoolPicker_AndCenter()
    {
        var steps = ChatNextSteps.AfterExplain("MIT");

        Assert.Equal(3, steps.Count);
        Assert.Equal(("goal_seek", "MIT"), (steps[0].Action!.Type, steps[0].Action!.SchoolCode));
        // nút "trường khác" không nêu tên trường: không có mã, để mở ô chọn trường
        Assert.Equal(("explain", null), (steps[1].Action!.Type, steps[1].Action!.SchoolCode));
        Assert.Equal(ChatNextSteps.CenterLabel, steps[2].Label);
        Assert.Equal(ChatNextSteps.CenterText, steps[2].Value);
    }

    [Fact]
    public void AfterRecommend_OffersPickersOnly_WithoutNamingAnySchool()
    {
        var steps = ChatNextSteps.AfterRecommend();

        Assert.Equal(["explain", "goal_seek"], steps.Select(s => s.Action!.Type));
        Assert.All(steps, s => Assert.Null(s.Action!.SchoolCode));
    }

    [Fact]
    public void WithCenter_AppendsOnce()
    {
        var steps = ChatNextSteps.WithCenter([new QuickReply("A", "A")]);

        Assert.Equal(["A", ChatNextSteps.CenterLabel], steps.Select(s => s.Label));
        Assert.Equal(2, ChatNextSteps.WithCenter(steps).Count);
        Assert.Equal([ChatNextSteps.CenterLabel], ChatNextSteps.WithCenter(null).Select(s => s.Label));
    }

    [Fact]
    public void ExplainActionWithoutSchool_OpensThePickerForExplaining()
    {
        var response = ChatActions.ToResponse(new ChatAction("explain"), null);

        Assert.NotNull(response);
        Assert.Equal(ChatGoalSeek.PickerType, response!.Widget!.Type);
        Assert.Equal("explain", response.Widget.Data!.Value.GetProperty("kind").GetString());
        Assert.Equal(ChatGoalSeek.AskExplainSchool, response.Answer);
    }

    [Fact]
    public void ExplainActionWithCandidateCodes_NarrowsThePickerToThoseSchools()
    {
        var response = ChatActions.ToResponse(new ChatAction("explain", SchoolCodes: ["bu", "ne", " "]), null);

        var codes = response!.Widget!.Data!.Value.GetProperty("codes").EnumerateArray().Select(c => c.GetString());
        Assert.Equal(["bu", "ne"], codes);
    }

    [Fact]
    public void PickersCarryTheSuggestedSchools_CleanedAndCapped()
    {
        var many = new[] { "a", "b", " ", "a", "c", "d", "e", "f", "g", "h" };

        var explain = ChatActions.ToResponse(new ChatAction("explain", Suggested: many), null)!.Widget!.Data!.Value;
        var goal = ChatActions.ToResponse(new ChatAction("goal_seek", Suggested: ["x"]), null)!.Widget!.Data!.Value;

        Assert.Equal(["a", "b", "c", "d", "e", "f"], explain.GetProperty("suggested").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal(["x"], goal.GetProperty("suggested").EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public void ThePickerWithoutSuggestions_HasNoSuggestedList()
    {
        var data = ChatActions.ToResponse(new ChatAction("explain"), null)!.Widget!.Data!.Value;

        Assert.Equal(JsonValueKind.Null, data.GetProperty("suggested").ValueKind);
    }

    [Fact]
    public void AfterExplainAndAfterRecommend_PassTheSuggestionsToThePickerButtons()
    {
        var after = ChatNextSteps.AfterExplain("MIT", ["a", "b"]);
        var list = ChatNextSteps.AfterRecommend(["a", "b"]);

        Assert.Equal(["a", "b"], after[1].Action!.Suggested);
        Assert.All(list, s => Assert.Equal(["a", "b"], s.Action!.Suggested));
    }

    [Fact]
    public void ExplainActionWithSchool_StillRunsTheExplanationDirectly()
    {
        var response = ChatActions.ToResponse(new ChatAction("explain", "MIT"), null);

        Assert.Equal("explain", response!.Action);
        Assert.Equal("MIT", response.ActionData!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public void GoalPickerWithoutKind_KeepsTheOldBehavior()
    {
        var data = ChatGoalSeek.SchoolPicker("match").Data!.Value;

        Assert.Equal("match", data.GetProperty("target").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("kind").ValueKind);
    }
}
