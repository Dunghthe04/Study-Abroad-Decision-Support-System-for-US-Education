using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Recommendations;
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

    [Fact]
    public void AfterSchoolInfo_OffersTheGoalForThisSchool_ThenUpToThreeFieldsNotAskedYet()
    {
        var steps = ChatNextSteps.AfterSchoolInfo("PU", "Purdue", [InfoField.Cost]);

        Assert.Equal(["Cần gì để vào Purdue?", "Học bổng", "Tiếng Anh cần", "Hạn nộp"], steps.Select(s => s.Label));
        Assert.Equal(ChatGoalSeek.Action, steps[0].Action?.Type);
        Assert.Equal("PU", steps[0].Action?.SchoolCode);
        Assert.Equal("Purdue có học bổng cho du học sinh không?", steps[1].Value);
    }

    // UC-156: câu hỏi học bổng dẫn tới trang Tìm học bổng, lọc theo tên trường
    [Theory]
    [InlineData(InfoField.Aid)]
    [InlineData(InfoField.Scholarships)]
    public void AfterSchoolInfo_ScholarshipQuestion_AddsALinkToTheScholarshipPage_FilteredByTheSchoolName(InfoField asked)
    {
        var steps = ChatNextSteps.AfterSchoolInfo("BU", "Boston University", [asked]);

        var link = Assert.Single(steps, s => s.Label == ChatNextSteps.ScholarshipPageLabel);
        Assert.Equal("/scholarships?search=Boston%20University", link.Href);
        Assert.Null(link.Action);
        Assert.Equal("Cần gì để vào Boston University?", steps[0].Label);   // nút "Cần gì để vào" vẫn đứng đầu
        Assert.True(steps.Count <= 4);
    }

    [Fact]
    public void AfterSchoolInfo_NonScholarshipQuestion_HasNoScholarshipPageLink()
    {
        var steps = ChatNextSteps.AfterSchoolInfo("PU", "Purdue", [InfoField.Cost]);

        Assert.DoesNotContain(steps, s => s.Label == ChatNextSteps.ScholarshipPageLabel);
    }

    [Fact]
    public void ScholarshipPage_WithoutASchoolName_OpensThePlainPage()
    {
        Assert.Equal("/scholarships", ChatNextSteps.ScholarshipPage().Href);
    }

    [Fact]
    public void AfterSchoolOverview_KeepsTheSixFieldsInOrder()
    {
        Assert.Equal(["Học phí", "Học bổng", "Tiếng Anh cần", "Hạn nộp", "GPA", "Xếp hạng"],
            ChatNextSteps.AfterSchoolOverview("UCLA").Select(s => s.Label));
    }

    [Fact]
    public void AfterCompare_OffersTheGoalForTheFirstTwoSchools_ThenTheCenter()
    {
        var steps = ChatNextSteps.AfterCompare([("PU", "Purdue"), ("DU", "Duke"), ("YU", "Yale")]);

        Assert.Equal(["Cần gì để vào Purdue?", "Cần gì để vào Duke?", ChatNextSteps.CenterLabel], steps.Select(s => s.Label));
        Assert.Equal("DU", steps[1].Action?.SchoolCode);
    }

    [Fact]
    public void AfterExplainFailed_OffersAnotherSchoolAndTheCenter()
    {
        Assert.Equal([ChatNextSteps.AnotherExplainLabel, ChatNextSteps.CenterLabel],
            ChatNextSteps.AfterExplainFailed().Select(s => s.Label));
    }

    [Fact]
    public void SearchHelp_AndNoSchoolData_AlwaysGiveAWayForward()
    {
        Assert.NotEmpty(ChatNextSteps.SearchHelp());
        Assert.Contains(ChatNextSteps.SearchHelp(), s => s.Label == ChatNextSteps.Recommend.Label);
        Assert.Equal([ChatNextSteps.Recommend.Label, ChatNextSteps.CenterLabel], ChatNextSteps.NoSchoolData().Select(s => s.Label));
    }

    [Fact]
    public void AfterSearch_ByRanking_OffersTopTwentyAndFifty_ThenTheUsualSteps_AtMostFive()
    {
        var items = Enumerable.Range(1, 5).Select(i => new SchoolSearchItem($"s{i}", $"S{i}", null, null, null, null, null, null, null, null, i)).ToList();

        var steps = ChatNextSteps.AfterSearch(new SchoolSearchResult(100, items, []), SchoolSort.Quality, new SchoolSearchFilters());

        Assert.Equal(["Chỉ top 20", "Chỉ top 50", "So sánh hai trường đầu", ChatNextSteps.Recommend.Label], steps.Select(s => s.Label));
        Assert.True(steps.Count <= ChatNextSteps.MaxSteps);
        Assert.Equal(ChatSchoolInfo.SearchAction, steps[0].Action?.Type);
        Assert.Equal(20, steps[0].Action!.Search!.Value.GetProperty("filters").GetProperty("maxRank").GetInt32());
    }

    [Fact]
    public void AfterSearch_WhenTheCostConditionWasIgnored_OffersCostLevels()
    {
        var ignored = new[] { new ChatSchoolInfo.IgnoredFilter("max_cost", "mức chi phí", "rẻ nhất") };

        var steps = ChatNextSteps.AfterSearch(new SchoolSearchResult(10, [], []), SchoolSort.LowestTuition, new SchoolSearchFilters(), ignored);

        Assert.Equal(["Dưới 30.000 USD", "Dưới 40.000 USD"], steps.Take(2).Select(s => s.Label));
    }

    [Fact]
    public void AfterSearch_AlreadyLimitedByRank_DoesNotOfferTopAgain()
    {
        var steps = ChatNextSteps.AfterSearch(new SchoolSearchResult(100, [], []), SchoolSort.Quality, new SchoolSearchFilters(MaxRank: 20));

        Assert.DoesNotContain(steps, s => s.Label.StartsWith("Chỉ top"));
    }

    [Fact]
    public void AfterNoResult_WithNothingToDrop_GivesSampleSearches()
    {
        var steps = ChatNextSteps.AfterNoResult(new SchoolSearchResult(0, [], [new RelaxedFilter("bang", 0)]), SchoolSort.Quality, new SchoolSearchFilters());

        Assert.Equal(ChatNextSteps.SearchHelp().Select(s => s.Label), steps.Select(s => s.Label));
    }

    [Fact]
    public void AfterSearch_TestOptionalWithHeavyTestSchools_OffersToKeepOnlyLowTestWeight_WithoutReordering()
    {
        var items = new[]
        {
            new SchoolSearchItem("a", "A", null, null, null, null, null, null, null, null, 1, null, "very_important"),
            new SchoolSearchItem("b", "B", null, null, null, null, null, null, null, null, 2, null, "considered"),
        };
        var filters = new SchoolSearchFilters(TestPolicy: "optional");

        var steps = ChatNextSteps.AfterSearch(new SchoolSearchResult(2, items, []), SchoolSort.LowestCost, filters);

        var button = Assert.Single(steps, s => s.Label == "Chỉ trường ít coi trọng điểm thi");
        var data = button.Action!.Search!.Value;
        Assert.True(data.GetProperty("filters").GetProperty("lowTestWeight").GetBoolean());
        Assert.Equal("optional", data.GetProperty("filters").GetProperty("testPolicy").GetString());
        Assert.Equal("lowestCost", data.GetProperty("sort").GetString());   // giữ cách xếp học sinh đã chọn
    }

    [Fact]
    public void AfterSearch_NoHeavyTestSchoolsOrAlreadyLowTestWeight_DoesNotOfferIt()
    {
        var light = new[] { new SchoolSearchItem("b", "B", null, null, null, null, null, null, null, null, 2, null, "considered") };
        var heavy = new[] { new SchoolSearchItem("a", "A", null, null, null, null, null, null, null, null, 1, null, "very_important") };

        var noHeavy = ChatNextSteps.AfterSearch(new SchoolSearchResult(1, light, []), SchoolSort.Quality, new SchoolSearchFilters(TestPolicy: "optional"));
        var already = ChatNextSteps.AfterSearch(new SchoolSearchResult(1, heavy, []), SchoolSort.Quality, new SchoolSearchFilters(TestPolicy: "optional", LowTestWeight: true));
        var notOptional = ChatNextSteps.AfterSearch(new SchoolSearchResult(1, heavy, []), SchoolSort.Quality, new SchoolSearchFilters());

        Assert.DoesNotContain(noHeavy, s => s.Label == "Chỉ trường ít coi trọng điểm thi");
        Assert.DoesNotContain(already, s => s.Label == "Chỉ trường ít coi trọng điểm thi");
        Assert.DoesNotContain(notOptional, s => s.Label == "Chỉ trường ít coi trọng điểm thi");
    }

    [Fact]
    public void Without_DropsTheLowTestWeightCondition()
    {
        var f = ChatNextSteps.Without(new SchoolSearchFilters(TestPolicy: "optional", LowTestWeight: true), "it_coi_trong_diem_thi");

        Assert.False(f.LowTestWeight);
        Assert.Equal("optional", f.TestPolicy);
    }
}

