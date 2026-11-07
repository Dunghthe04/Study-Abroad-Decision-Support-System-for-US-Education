using System.Text.Json;
using System.Text.Json.Nodes;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class ChatServiceTests
{
    private static readonly IReadOnlyList<Application.Advisor.ChatMessage> Hello = [new Application.Advisor.ChatMessage("user", "chào")];

    [Fact]
    public async Task NewChat_SendsNoState_ThenSavesStateFromAdvisor()
    {
        var advisor = new FakeAdvisor("B0");
        var store = new FakeSessions();
        var userId = Guid.NewGuid();

        var res = await new ChatService(advisor, store, new FakeProfileFlow()).ChatAsync(userId, new AdvisorChatRequest(Hello));

        Assert.Null(advisor.LastRequest!.State);        // chưa có phiên thì chưa có trạng thái
        var saved = Assert.Single(store.Rows);
        Assert.Equal(userId, saved.UserId);
        Assert.Equal("B0", saved.CurrentStep);
        Assert.Contains("\"currentStep\":\"B0\"", saved.StateJson);
        Assert.Equal(saved.Id.ToString(), res.SessionId); // trình duyệt nhận mã phiên
        Assert.Null(res.State);                           // trạng thái nội bộ không lộ ra ngoài
    }

    [Fact]
    public async Task NextTurn_SendsSavedState_AndUpdatesStep()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = new ChatSession { UserId = userId, StateJson = "{\"currentStep\":\"B1\"}", CurrentStep = "B1" };
        store.Rows.Add(session);
        var advisor = new FakeAdvisor("B2");

        await new ChatService(advisor, store, new FakeProfileFlow()).ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Equal("B1", advisor.LastRequest!.State!.Value.GetProperty("currentStep").GetString());
        Assert.Single(store.Rows);                       // cập nhật phiên cũ, không tạo phiên mới
        Assert.Equal("B2", session.CurrentStep);
    }

    [Fact]
    public async Task OtherUsersSession_IsIgnored_AndClientStateIsNotTrusted()
    {
        var store = new FakeSessions();
        var other = new ChatSession { UserId = Guid.NewGuid(), StateJson = "{\"currentStep\":\"B4\"}" };
        store.Rows.Add(other);
        var advisor = new FakeAdvisor("B0");
        var fake = JsonDocument.Parse("{\"currentStep\":\"B6\"}").RootElement;

        var res = await new ChatService(advisor, store, new FakeProfileFlow())
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello, other.Id.ToString(), State: fake));

        Assert.Null(advisor.LastRequest!.State);         // không đọc phiên người khác, không tin state từ trình duyệt
        Assert.Equal(2, store.Rows.Count);
        Assert.NotEqual(other.Id.ToString(), res.SessionId);
        Assert.Equal("{\"currentStep\":\"B4\"}", other.StateJson);
    }

    [Fact]
    public async Task QuickRepliesAndWidget_FromAdvisor_ReachTheBrowser()
    {
        var replies = new List<QuickReply> { new("Điền bằng form", "Điền bằng form") };
        var widget = new Widget("major");
        var advisor = new FakeAdvisor("B1", replies, widget);

        var res = await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow()).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(replies, res.QuickReplies);
        Assert.Equal(widget, res.Widget);
        Assert.Null(res.State); // trạng thái nội bộ vẫn không lộ ra ngoài
    }

    // C4: hồ sơ gửi sang advisor lấy từ nháp, chat đổi gì thì ghi vào nháp
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private const string BudgetChanged = """{ "budget": { "value": "tier_20k_35k", "status": "confirmed" } }""";

    [Fact]
    public async Task SendsProfileFromDraft_NotFromBrowser()
    {
        var flow = new FakeProfileFlow { Draft = new ProfileFlowDraftDto(Step4: new FlowFinancialDraftDto("tier_35k_50k")) };
        var advisor = new FakeAdvisor("B2");
        var fromBrowser = Json("""{ "budget": { "value": "tier_above_70k", "status": "confirmed" } }""");

        await new ChatService(advisor, new FakeSessions(), flow)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello, Profile: fromBrowser));

        var budget = advisor.LastRequest!.Profile!.Value.GetProperty("budget");
        Assert.Equal("tier_35k_50k", budget.GetProperty("value").GetString());
        Assert.Equal("confirmed", budget.GetProperty("status").GetString());
    }

    // Học bạ theo năm gửi thành mục riêng, không nằm trong hồ sơ chat (chat chỉ đọc, chỉ form nhập được)
    [Fact]
    public async Task SendsTranscriptBesideProfile_NotInsideIt()
    {
        var flow = new FakeProfileFlow
        {
            Draft = new ProfileFlowDraftDto(Step2: new FlowTranscriptDraftDto(
                [new FlowAcademicYearDraftDto("Lớp 10", [new FlowSubjectDraftDto("Toán", 8m, 9m)])])),
        };
        var advisor = new FakeAdvisor("B2");

        await new ChatService(advisor, new FakeSessions(), flow).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        var year = advisor.LastRequest!.Transcript!.Value.GetProperty("years")[0];
        Assert.Equal("Lớp 10", year.GetProperty("grade").GetString());
        Assert.False(advisor.LastRequest.Profile!.Value.TryGetProperty("transcript", out _));
    }

    // Mốc GPA / tiếng Anh tính từ trường cùng bậc học mục tiêu, gửi riêng cạnh hồ sơ
    [Fact]
    public async Task SendsBenchmarksOfTargetLevel()
    {
        var flow = new FakeProfileFlow { Draft = new ProfileFlowDraftDto(Step1: new FlowGoalDraftDto(TargetDegreeLevel: "master")) };
        var marks = new FakeBenchmarks([new BenchmarkRow(3.6m, 6.5m, null, null)]);
        var advisor = new FakeAdvisor("B2");

        await new ChatService(advisor, new FakeSessions(), flow, benchmarks: marks).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal("master", marks.Level);
        var sent = advisor.LastRequest!.Benchmarks!.Value;
        Assert.Equal(3.6m, sent.GetProperty("gpa4").GetDecimal());
        Assert.Equal(6.5m, sent.GetProperty("english").GetProperty("ielts").GetDecimal());
    }

    // Hồ sơ chưa chọn bậc học thì lấy đại học; chưa có dữ liệu trường thì không gửi mốc
    [Fact]
    public async Task NoTargetLevel_UsesUndergraduate_NoDataSendsNull()
    {
        var marks = new FakeBenchmarks([]);
        var advisor = new FakeAdvisor("B2");

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), benchmarks: marks)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal("undergraduate", marks.Level);
        Assert.Null(advisor.LastRequest!.Benchmarks);
    }

    // Nhập GPA tổng: không gửi học bạ
    [Fact]
    public async Task OverallGpa_SendsNoTranscript()
    {
        var flow = new FakeProfileFlow { Draft = new ProfileFlowDraftDto(Step2: new FlowTranscriptDraftDto(ManualGpa10Scale: 8m, GpaMode: "overall")) };
        var advisor = new FakeAdvisor("B2");

        await new ChatService(advisor, new FakeSessions(), flow).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Null(advisor.LastRequest!.Transcript);
    }

    [Fact]
    public async Task KeepsNoneStatusFromPreviousTurn_WhenDraftHasNoValue()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = new ChatSession
        {
            UserId = userId,
            StateJson = """{"currentStep":"B2","profile":{"sat":{"value":null,"status":"none"}}}""",
        };
        store.Rows.Add(session);
        var advisor = new FakeAdvisor("B2");

        await new ChatService(advisor, store, new FakeProfileFlow())
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        // nháp không lưu được "chưa thi SAT", lấy lại từ lượt trước để chat không hỏi lại
        Assert.Equal("none", advisor.LastRequest!.Profile!.Value.GetProperty("sat").GetProperty("status").GetString());
    }

    [Fact]
    public async Task ChangedKeys_ArePatchedIntoDraft_AndProfileStaysInternal()
    {
        var flow = new FakeProfileFlow();
        var advisor = new FakeAdvisor("B2") { Profile = Json(BudgetChanged), Changed = ["budget"] };

        var res = await new ChatService(advisor, new FakeSessions(), flow).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(["budget"], flow.PatchedKeys);
        Assert.Equal("tier_20k_35k", flow.PatchedProfile!["budget"]!["value"]!.GetValue<string>());
        Assert.Equal(["budget"], res.ProfileChanged);
        Assert.Null(res.Profile);
    }

    [Fact]
    public async Task NothingChanged_DoesNotTouchDraft()
    {
        var flow = new FakeProfileFlow();
        var advisor = new FakeAdvisor("B2") { Profile = Json(BudgetChanged), Changed = [] };

        var res = await new ChatService(advisor, new FakeSessions(), flow).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Null(flow.PatchedKeys);
        Assert.Empty(res.ProfileChanged!);
    }

    [Fact]
    public async Task DraftOverLimit_ChatStillAnswers_AndReportsNoChange()
    {
        var store = new FakeSessions();
        var flow = new FakeProfileFlow { PatchError = new ArgumentException("Dữ liệu nháp quá lớn") };
        var advisor = new FakeAdvisor("B2") { Profile = Json(BudgetChanged), Changed = ["budget"] };

        var res = await new ChatService(advisor, store, flow).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal("ok", res.Answer);
        Assert.Empty(res.ProfileChanged!);
        Assert.Single(store.Rows); // phiên chat vẫn được lưu
    }

    // Mục 7: trả trang form của câu chat đang hỏi
    [Theory]
    [InlineData("B1.3", 1)]
    [InlineData("D2", 2)]
    [InlineData("E2", 3)]
    [InlineData("F1", 4)]
    [InlineData("D5", 5)]
    [InlineData("X1", 6)]
    [InlineData("N4", null)]
    [InlineData(null, null)]
    public async Task Response_HasFormStepOfAskedQuestion(string? questionId, int? expected)
    {
        var advisor = new FakeAdvisor("B2") { QuestionId = questionId };

        var res = await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow())
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(expected, res.FormStep);
    }

    // C5: học sinh xác nhận ở X1 (chat sang B3) thì lưu hồ sơ chính thức
    private static ChatSession AtReview(Guid userId, string step = "B2") =>
        new() { UserId = userId, StateJson = $"{{\"currentStep\":\"{step}\",\"questionId\":\"X1\"}}", CurrentStep = step };

    [Fact]
    public async Task ConfirmAtReview_CommitsProfile()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = AtReview(userId);
        store.Rows.Add(session);
        var flow = new FakeProfileFlow();

        var res = await new ChatService(new FakeAdvisor("B3"), store, flow)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Equal(1, flow.Commits);
        Assert.Equal("ok", res.Answer);
        Assert.Equal("B3", session.CurrentStep);
    }

    // Đánh giá chạy ngay trong lượt xác nhận và chuyển chat sang B4: vẫn phải lưu hồ sơ chính thức
    [Fact]
    public async Task ConfirmAtReview_AssessedToB4_CommitsProfile()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = AtReview(userId);
        store.Rows.Add(session);
        var flow = new FakeProfileFlow();

        await new ChatService(new FakeAdvisor("B4"), store, flow)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Equal(1, flow.Commits);
        Assert.Equal("B4", session.CurrentStep);
    }

    // Chỉ lưu khi rời bảng rà soát X1, sang B4 từ câu khác thì không lưu
    [Fact]
    public async Task ToB4WithoutReview_DoesNotCommit()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = new ChatSession
        {
            UserId = userId, StateJson = "{\"currentStep\":\"B0\",\"questionId\":\"B0.2\"}", CurrentStep = "B0",
        };
        store.Rows.Add(session);
        var flow = new FakeProfileFlow();

        await new ChatService(new FakeAdvisor("B4"), store, flow)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Equal(0, flow.Commits);
    }

    [Fact]
    public async Task CommitFails_KeepsReviewStateAndDraft_AndExplainsInVietnamese()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = AtReview(userId);
        var oldState = session.StateJson;
        store.Rows.Add(session);
        var flow = new FakeProfileFlow { CommitError = new InvalidOperationException("Vui lòng nhập GPA hợp lệ.") };
        var advisor = new FakeAdvisor("B3", [new QuickReply("Đánh giá hồ sơ", "Đánh giá hồ sơ")])
        {
            Profile = Json(BudgetChanged),
            Changed = ["budget"],
        };

        var res = await new ChatService(advisor, store, flow)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Contains("Hồ sơ chưa lưu được", res.Answer);
        Assert.Contains("Vui lòng nhập GPA hợp lệ.", res.Answer);
        Assert.Null(res.QuickReplies);           // không mời sang bước sau khi chưa lưu được
        Assert.Equal(oldState, session.StateJson); // vẫn ở bảng rà soát, xác nhận lại được
        Assert.Equal("B2", session.CurrentStep);
        Assert.Equal(["budget"], flow.PatchedKeys); // nháp vẫn giữ những gì vừa nói
        Assert.Equal(["budget"], res.ProfileChanged);
    }

    [Fact]
    public async Task AlreadyPastReview_DoesNotCommitAgain()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = AtReview(userId, "B3");
        store.Rows.Add(session);
        var flow = new FakeProfileFlow();

        await new ChatService(new FakeAdvisor("B3"), store, flow)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Equal(0, flow.Commits);
    }

    // Đánh giá chạy ngay khi xác nhận X1: lưu câu đánh giá vào analysis_results, không trả danh sách skill cho trình duyệt
    [Fact]
    public async Task ConfirmAtReview_SavesAssessment()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = AtReview(userId);
        store.Rows.Add(session);
        var analyses = new FakeAnalyses();
        var advisor = new FakeAdvisor("B4") { Skills = ["skill_ho_so", "skill_danh_gia"] };

        var res = await new ChatService(advisor, store, new FakeProfileFlow(), analyses)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        var saved = Assert.Single(analyses.Saved);
        Assert.Equal((userId, "strengths_weaknesses", "{\"answer\":\"ok\"}", "chat:skill_danh_gia"), saved);
        Assert.Null(res.Skills);
    }

    [Fact]
    public async Task Roadmap_SavedAsStrategy()
    {
        var analyses = new FakeAnalyses();
        var advisor = new FakeAdvisor("B4") { Skills = ["skill_lo_trinh"] };

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), analyses)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal("strategy", Assert.Single(analyses.Saved).Kind);
    }

    // Hồ sơ chưa lưu được thì câu đánh giá bị thay bằng câu báo lỗi: không lưu đánh giá
    [Fact]
    public async Task CommitFails_DoesNotSaveAssessment()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = AtReview(userId);
        store.Rows.Add(session);
        var analyses = new FakeAnalyses();
        var flow = new FakeProfileFlow { CommitError = new InvalidOperationException("Thiếu GPA.") };

        await new ChatService(new FakeAdvisor("B4") { Skills = ["skill_danh_gia"] }, store, flow, analyses)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Empty(analyses.Saved);
    }

    [Fact]
    public async Task OtherSkills_SaveNothing()
    {
        var analyses = new FakeAnalyses();
        var advisor = new FakeAdvisor("B2") { Skills = ["skill_ho_so", "skill_tra_loi"] };

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), analyses)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Empty(analyses.Saved);
    }

    // Gợi ý trường trong chat: advisor trả action "recommend" (lộ trình đại học thẳng) thì chạy gợi ý có sẵn
    private const string OneSchool = """{ "scored": [ { "code": "ASU", "name": "Arizona State University" } ], "match": [ { "code": "ASU" } ] }""";

    [Fact]
    public async Task RecommendAction_RunsTheRecommendation_AndAddsTheSummaryToTheAnswer()
    {
        var userId = Guid.NewGuid();
        var runner = new FakeRunner { Result = new Recommendation { ItemsJson = OneSchool } };
        var store = new FakeSessions();

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "recommend" }, store, new FakeProfileFlow(), null, runner)
            .ChatAsync(userId, new AdvisorChatRequest(Hello));

        Assert.Equal(userId, runner.CalledFor);
        Assert.StartsWith("ok\n\nMình đã tìm được các trường phù hợp", res.Answer); // câu lộ trình giữ nguyên ở đầu
        Assert.Contains("- Vừa sức: Arizona State University", res.Answer);
        Assert.Equal(new Widget(ChatRecommendation.WidgetType), res.Widget); // nút xem đầy đủ
        Assert.Null(res.Action); // action là việc nội bộ, không trả cho trình duyệt
        Assert.Contains("\"hasResults\":true", Assert.Single(store.Rows).StateJson); // lượt sau skill_explain chạy được
    }

    [Fact]
    public async Task NoAction_DoesNotRunTheRecommendation()
    {
        var runner = new FakeRunner();

        var res = await new ChatService(new FakeAdvisor("B4"), new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Null(runner.CalledFor);
        Assert.Equal("ok", res.Answer);
    }

    [Fact]
    public async Task RecommendationSummary_IsNotSavedAsPartOfTheRoadmapAnalysis()
    {
        var analyses = new FakeAnalyses();
        var runner = new FakeRunner { Result = new Recommendation { ItemsJson = OneSchool } };
        var advisor = new FakeAdvisor("B4") { Skills = ["skill_lo_trinh"], Action = "recommend" };

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), analyses, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.DoesNotContain("Arizona", Assert.Single(analyses.Saved).Json);
    }

    [Theory]
    [InlineData("Hồ sơ chưa chọn ngành, cần chọn ngành trước khi gợi ý trường.", "Mình chưa tạo được danh sách trường: Hồ sơ chưa chọn ngành")]
    [InlineData("Gemini trả code lạ", ChatRecommendation.Invalid)]
    public async Task RecommendationFails_KeepsTheRoadmap_AndExplains(string error, string expected)
    {
        var runner = new FakeRunner { Error = new InvalidOperationException(error) };
        var store = new FakeSessions();

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "recommend" }, store, new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.StartsWith("ok\n\n", res.Answer);
        Assert.Contains(expected, res.Answer);
        Assert.Null(res.Widget);
        Assert.DoesNotContain("hasResults", Assert.Single(store.Rows).StateJson); // chưa có kết quả thì không bật
    }

    [Fact]
    public async Task RecommendationServiceDown_SaysBusy()
    {
        var runner = new FakeRunner { Error = new HttpRequestException("advisor tắt") };

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "recommend" }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal($"ok\n\n{ChatRecommendation.Busy}", res.Answer);
    }

    // Gemini treo: advisor thử lại nhiều lần, HttpClient của .NET hết giờ (TaskCanceledException). Không được làm lỗi 500 cả lượt chat
    [Fact]
    public async Task RecommendationTimesOut_SaysBusy_AndKeepsTheRoadmap()
    {
        var runner = new FakeRunner { Error = new TaskCanceledException("HttpClient.Timeout") };

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "recommend" }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal($"ok\n\n{ChatRecommendation.Busy}", res.Answer);
    }

    // Trả lời theo từng ý (stream): các tin của advisor đi thẳng ra ngoài, còn lại chạy y như lượt chat thường
    [Fact]
    public async Task OnEvent_ForwardsTheAdvisorEvents_AndStillSavesTheSession()
    {
        var advisor = new FakeAdvisor("B2") { Events = [new("progress", Answers: 2), new("partial", Text: "đáp 1")] };
        var store = new FakeSessions();
        var seen = new List<AdvisorStreamEvent>();

        var res = await new ChatService(advisor, store, new FakeProfileFlow()).ChatAsync(
            Guid.NewGuid(), new AdvisorChatRequest(Hello), default, e => { seen.Add(e); return Task.CompletedTask; });

        Assert.Equal(advisor.Events, seen);
        Assert.Equal("B2", Assert.Single(store.Rows).CurrentStep);
        Assert.Null(res.State);                      // trạng thái nội bộ vẫn không lộ ra ngoài
    }

    [Fact]
    public async Task WithoutOnEvent_TheAdvisorIsNotAskedToStream()
    {
        var advisor = new FakeAdvisor("B2") { Events = [new("partial", Text: "không được gửi")] };

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow()).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(0, advisor.StreamCalls);
    }

    // Câu cuối cùng vẫn do bước sau advisor quyết định (gợi ý trường thay câu trả lời), nên dòng final mới là câu để hiện
    [Fact]
    public async Task OnEvent_TheFinalAnswerStillGoesThroughTheExplainStep()
    {
        var userId = Guid.NewGuid();
        var runner = new FakeRunner { Explanation = Explanation() };
        var advisor = new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode, Events = [new("partial", Text: "câu tạm")] };

        var res = await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(userId, new AdvisorChatRequest(Hello), default, _ => Task.CompletedTask);

        Assert.StartsWith("University of Iowa:", res.Answer);
    }

    // Đường /chat/stream trả từng dòng JSON: các tin, rồi final là đúng JSON của đường /chat
    [Fact]
    public async Task StreamEndpoint_WritesOneJsonLinePerEvent_ThenTheFinalResponse()
    {
        var advisor = new FakeAdvisor("B2") { Events = [new("progress", Answers: 1), new("partial", Text: "đáp 1")] };
        var controller = StreamController(advisor, out var body);

        var result = await controller.ChatStream(new AdvisorChatRequest(Hello), Json(), CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.EmptyResult>(result);
        Assert.Equal("application/x-ndjson", controller.Response.ContentType);
        Assert.Equal("no", controller.Response.Headers["X-Accel-Buffering"]);
        var lines = Lines(body);
        Assert.Equal(3, lines.Count);
        Assert.Equal(("progress", 1), (lines[0].GetProperty("type").GetString(), lines[0].GetProperty("answers").GetInt32()));
        Assert.Equal("đáp 1", lines[1].GetProperty("text").GetString());
        var final = lines[2];
        Assert.Equal("final", final.GetProperty("type").GetString());
        Assert.Equal("ok", final.GetProperty("response").GetProperty("answer").GetString());
        Assert.False(final.GetProperty("response").TryGetProperty("action", out var action) && action.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task StreamEndpoint_WhenTheAdvisorFails_EndsWithAnErrorLine()
    {
        var advisor = new FakeAdvisor("B2") { Events = [new("progress", Answers: 1)], StreamError = new HttpRequestException("advisor tắt") };
        var controller = StreamController(advisor, out var body);

        await controller.ChatStream(new AdvisorChatRequest(Hello), Json(), CancellationToken.None);

        var lines = Lines(body);
        Assert.Equal(["progress", "error"], lines.Select(l => l.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task StreamEndpoint_RejectsAnEmptyMessageListBeforeStreaming()
    {
        var controller = StreamController(new FakeAdvisor("B2"), out var body);

        var result = await controller.ChatStream(new AdvisorChatRequest([]), Json(), CancellationToken.None);

        Assert.IsNotType<Microsoft.AspNetCore.Mvc.EmptyResult>(result);
        Assert.Equal(0, body.Length);
    }

    private static Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> Json() =>
        Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Mvc.JsonOptions());

    private static StudyAbroad.Api.Controllers.AdvisorController StreamController(FakeAdvisor advisor, out MemoryStream body)
    {
        body = new MemoryStream();
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test")),
        };
        http.Response.Body = body;
        var service = new ChatService(advisor, new FakeSessions(), new FakeProfileFlow());
        return new StudyAbroad.Api.Controllers.AdvisorController(service, Microsoft.Extensions.Logging.Abstractions.NullLogger<StudyAbroad.Api.Controllers.AdvisorController>.Instance)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http },
        };
    }

    private static List<JsonElement> Lines(MemoryStream body) =>
        System.Text.Encoding.UTF8.GetString(body.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => JsonDocument.Parse(l).RootElement.Clone())
            .ToList();

    // Học sinh đóng trang giữa chừng (request bị hủy) thì không nuốt lỗi: để ASP.NET dừng lượt chat như bình thường
    [Fact]
    public async Task RequestCancelledByTheUser_IsNotSwallowed()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var runner = new FakeRunner { Error = new TaskCanceledException() };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ChatService(new FakeAdvisor("B4") { Action = "recommend" }, new FakeSessions(), new FakeProfileFlow(), null, runner)
                .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello), cts.Token));
    }

    // Hỏi giải thích một trường: advisor trả action explain kèm mã trường, .NET gọi phần tư vấn từng trường có sẵn
    private static readonly JsonElement IowaCode = JsonDocument.Parse("""{"code":"iowa"}""").RootElement;

    private static SchoolExplanation Explanation(string status = "OK") =>
        new("iowa", "University of Iowa", status, "Iowa là trường khó vào với hồ sơ hiện tại.",
            ["Ngoại khóa nổi bật."], ["Nâng SAT lên khoảng 1450."]);

    [Fact]
    public async Task ExplainAction_ReplacesTheTempAnswerWithTheSchoolAdvice()
    {
        var userId = Guid.NewGuid();
        var runner = new FakeRunner { Explanation = Explanation() };

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(userId, new AdvisorChatRequest(Hello));

        Assert.Equal(("iowa", userId), (runner.ExplainedCode, runner.ExplainedFor!.Value));
        Assert.Equal(
            "University of Iowa: Iowa là trường khó vào với hồ sơ hiện tại.\n\n" +
            "Điểm mạnh của hồ sơ với trường này:\n- Ngoại khóa nổi bật.\n\n" +
            "Bạn có thể cải thiện:\n- Nâng SAT lên khoảng 1450.",
            res.Answer);
        Assert.Null(res.Action);     // việc nội bộ, không trả cho trình duyệt
        Assert.Null(res.ActionData);
    }

    // Thẻ giải thích do code dựng gửi trước, rồi mới tới lời tư vấn (AI); widget cũng nằm trong câu trả lời cuối
    [Fact]
    public async Task ExplainAction_SendsTheCardBeforeTheAdviceIsWritten()
    {
        var order = new List<string>();
        var runner = new FakeRunner { Explanation = Explanation(), OnExplain = () => order.Add("advice") };
        AdvisorStreamEvent? card = null;

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello), default,
                e => { if (e.Type == "card") { card = e; order.Add("card"); } return Task.CompletedTask; });

        Assert.Equal(["card", "advice"], order);
        Assert.Equal("explain", card!.Widget!.Type);
        Assert.Equal("explain", res.Widget!.Type);
        Assert.Equal("University of Iowa", res.Widget.Data!.Value.GetProperty("card").GetProperty("name").GetString());
        Assert.Equal("R", res.Widget.Data.Value.GetProperty("card").GetProperty("g2").GetString());
    }

    // Giải thích đi theo cái học sinh đang xem trên trang: có giá trị giả định thì theo giả định và thẻ ghi rõ gốc
    private static async Task<(AdvisorChatResponse Res, FakeRunner Runner)> ExplainWith(
        SimulationOverrides? simulation, string? basis = null)
    {
        var runner = new FakeRunner { Explanation = Explanation() };
        var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello, Simulation: simulation, Action: basis is null ? null : new ChatAction("explain", "iowa", null, basis)));
        return (res, runner);
    }

    private static readonly SimulationOverrides Gpa36 = new(3.6m, null, null, null, null, null, null);

    [Fact]
    public async Task Explain_WhileTheStudentIsSimulating_FollowsTheSimulationAndLabelsIt()
    {
        var (res, runner) = await ExplainWith(Gpa36);

        Assert.Equal(Gpa36, runner.ExplainOverrides);
        Assert.Equal("GPA 3.6", runner.ExplainBasisLabel);
        var basis = res.Widget!.Data!.Value.GetProperty("basis");
        Assert.Equal(("simulated", "GPA 3.6"), (basis.GetProperty("kind").GetString(), basis.GetProperty("label").GetString()));
    }

    [Fact]
    public async Task Explain_WithoutASimulation_IsTheRealProfileAndSaysSo()
    {
        var (res, runner) = await ExplainWith(null);

        Assert.Null(runner.ExplainOverrides);
        Assert.Null(runner.ExplainBasisLabel);
        Assert.Equal("real", res.Widget!.Data!.Value.GetProperty("basis").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Explain_TheRealProfileButton_IgnoresTheSimulationOnThePage()
    {
        var (res, runner) = await ExplainWith(Gpa36, basis: "real");

        Assert.Null(runner.ExplainOverrides);
        Assert.Equal("real", res.Widget!.Data!.Value.GetProperty("basis").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Explain_AnEmptySimulationIsNotASimulation()
    {
        var (_, runner) = await ExplainWith(new SimulationOverrides(null, null, null, null, null, null, null));

        Assert.Null(runner.ExplainOverrides);
    }

    [Fact]
    public async Task ExplainAction_WithoutACode_SaysItFailed()
    {
        var runner = new FakeRunner { Explanation = Explanation() };

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain" }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(ChatExplanation.Failed, res.Answer);
        Assert.Null(runner.ExplainedCode);
    }

    [Fact]
    public async Task ExplainAction_WhenTheAdviceIsNotOk_SaysItFailed()
    {
        var runner = new FakeRunner { Explanation = Explanation("FAILED") };

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(ChatExplanation.Failed, res.Answer);
    }

    [Theory]
    [InlineData("Chưa có trường iowa trong lần gợi ý gần nhất.", "Mình chưa giải thích được trường này: Chưa có trường iowa")]
    [InlineData("Dữ liệu lưu bị hỏng", ChatRecommendation.Invalid)]
    public async Task ExplainAction_WhenTheServiceRejects_ExplainsWhy(string error, string expected)
    {
        var runner = new FakeRunner { ExplainError = new InvalidOperationException(error) };

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Contains(expected, res.Answer);
    }

    // Hỏi "vì sao" về trường không nằm trong danh sách (bị bộ lọc cứng loại): nói đúng lý do bằng thẻ goal-seeking, không báo lỗi
    [Fact]
    public async Task ExplainAction_ForASchoolOutsideTheList_AnswersWithTheReasonItIsExcluded()
    {
        var excluded = GoalOf() with { Position = new GoalPosition("excluded", "M", "STATE", false), FilterReason = "STATE" };
        var runner = new FakeRunner
        {
            ExplainError = new InvalidOperationException("Chưa có trường 188429 trong lần gợi ý gần nhất."),
            GoalResult = excluded,
        };

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal("goal_seek", res.Widget!.Type);
        Assert.DoesNotContain("chưa giải thích được", res.Answer);
        Assert.Equal("Sửa hồ sơ", res.QuickReplies!.Single().Label);
    }

    [Fact]
    public async Task ExplainAction_WhenTheServiceIsBusyOrTimesOut_SaysBusy()
    {
        foreach (Exception error in new Exception[] { new HttpRequestException("advisor tắt"), new TaskCanceledException("HttpClient.Timeout") })
        {
            var runner = new FakeRunner { ExplainError = error };

            var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode }, new FakeSessions(), new FakeProfileFlow(), null, runner)
                .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

            Assert.Equal(ChatExplanation.Busy, res.Answer);
        }
    }

    // What-if: advisor trả action simulate kèm giá trị đã kiểm tra, .NET chạy mô phỏng có sẵn rồi nhờ advisor viết lời
    private static readonly JsonElement Sat1500 = JsonDocument.Parse("""{"changes":{"sat":1500},"unsupported":[]}""").RootElement;

    private static SimulationResult Simulation() => new(
        new SimulationProfile(3.5m, 1290, null, 6.5m, 40_000m, "preferred", 3),
        new GroupSummary(2, 1, 1, 0, 0, 0), new GroupSummary(2, 0, 2, 0, 0, 0),
        [new SchoolChange("bu", "Boston University", "R", "M")],
        "{}",
        """{"scored":[{"code":"bu","name":"Boston University"},{"code":"ne","name":"Northeastern University"}],"reach":[],"match":[{"code":"bu","why":["SAT_ABOVE"]},{"code":"ne","why":[]}],"safety":[],"aid":[],"other":[]}""");

    private static ScenarioNoteResponseDto Written(string status = "OK", string answer = "Với SAT 1500, Boston University lên Vừa sức.") =>
        new("2.0", "gemini", status, answer);

    private static async Task<AdvisorChatResponse> SimulateChat(FakeAdvisor advisor, FakeRunner runner) =>
        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

    [Fact]
    public async Task SimulateAction_RunsTheSimulationAndAnswersWithTheWordsOfTheAdvisor()
    {
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = Sat1500, Note = Written() };
        var runner = new FakeRunner { Simulation = Simulation() };

        var res = await SimulateChat(advisor, runner);

        Assert.Equal(1500m, runner.SimulatedWith!.Sat);
        Assert.Equal("SAT 1500 (hiện tại 1290)", advisor.LastNote!.Label);
        Assert.Equal("Với SAT 1500, Boston University lên Vừa sức.", res.Answer);
        Assert.Equal("simulate", res.Widget!.Type);
        Assert.Equal(1500, res.Widget.Data!.Value.GetProperty("sat").GetInt32());
        Assert.Null(res.Action);     // việc nội bộ, không trả cho trình duyệt
        Assert.Null(res.ActionData);
    }

    // ---- what-if về ưu tiên (chi phí thấp, học bổng nhiều, danh tiếng): code tính, chỉ đổi thứ tự trong nhóm

    private static SimulationResult OrderedSimulation(params string[] matchOrder) => new(
        new SimulationProfile(3.5m, 1290, null, 6.5m, 40_000m, "preferred", 3),
        new GroupSummary(2, 0, 2, 0, 0, 0), new GroupSummary(2, 0, 2, 0, 0, 0), [],
        "{}",
        "{\"scored\":[{\"code\":\"bu\",\"name\":\"Boston University\",\"cost\":52000,\"aidPolicy\":\"none\",\"h\":0,\"q\":0.9},"
        + "{\"code\":\"ne\",\"name\":\"Northeastern University\",\"cost\":31000,\"aidAvg\":25000,\"aidPolicy\":\"need_and_merit\",\"h\":1,\"q\":0.7}],"
        + "\"reach\":[],\"match\":[" + string.Join(",", matchOrder.Select(c => "{\"code\":\"" + c + "\",\"why\":[]}")) + "],\"safety\":[],\"aid\":[],\"other\":[]}");

    private static readonly JsonElement AidFirst =
        JsonDocument.Parse("""{"changes":{},"unsupported":[],"priorities":{"aid":4}}""").RootElement;

    [Fact]
    public async Task SimulateAction_WithOnlyPriorities_ReordersInsideTheGroupAndSaysSo()
    {
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = AidFirst };
        var runner = new FakeRunner
        {
            Latest = new Recommendation { CriteriaJson = """{"pref":[3,2,1]}""" },
            // ưu tiên học bổng 4 (giữ chi phí 3, danh tiếng 1): Northeastern (học bổng cao) lên đầu
            SimulationFor = pref => pref!.SequenceEqual([3, 4, 1]) ? OrderedSimulation("ne", "bu") : OrderedSimulation("bu", "ne"),
        };

        var res = await SimulateChat(advisor, runner);

        Assert.Equal([3, 4, 1], runner.SimulatedPrefs[0]);          // học bổng đổi, hai mức kia giữ như lần gợi ý đã lưu
        Assert.Equal([3, 2, 1], runner.SimulatedPrefs[1]);          // danh sách so sánh: cùng hồ sơ, ưu tiên cũ
        Assert.Contains("học bổng nhiều 4/4 (trước là 2/4)", res.Answer);
        Assert.Contains("không đổi nhóm của trường nào", res.Answer);
        Assert.Contains("Northeastern University (Vừa sức): từ vị trí 2 lên vị trí 1, người được học bổng nhận trung bình 25.000 USD mỗi năm (khoảng 81% chi phí)", res.Answer);
        Assert.Contains("Boston University (Vừa sức): từ vị trí 1 xuống vị trí 2, trường không có học bổng cho du học sinh", res.Answer);
        Assert.DoesNotContain("/100", res.Answer);   // nói bằng USD thật, không nói điểm
        Assert.Null(advisor.LastNote);                              // không có giá trị hồ sơ nên không nhờ AI viết lời
        Assert.Equal([3, 4, 1], res.Widget!.Data!.Value.GetProperty("pref").EnumerateArray().Select(e => e.GetInt32()));
    }

    // ---- nút bước tiếp theo sau what-if: hiểu vì sao đổi, rồi xem cần gì để vào trường khó

    private static SimulationResult WithReach() => new(
        new SimulationProfile(3.5m, 1290, null, 6.5m, 40_000m, "preferred", 3),
        new GroupSummary(3, 1, 2, 0, 0, 0), new GroupSummary(3, 1, 2, 0, 0, 0),
        [new SchoolChange("bu", "Boston University", "R", "M"), new SchoolChange("gone", "Gone University", "M", "OUT")],
        "{}",
        """{"scored":[{"code":"bu","name":"Boston University"},{"code":"ne","name":"Northeastern University"},{"code":"mit","name":"MIT"}],"reach":[{"code":"bu","why":[]},{"code":"mit","why":[]}],"match":[{"code":"ne","why":[]}],"safety":[],"aid":[],"other":[]}""");

    [Fact]
    public async Task SimulateAction_OffersTheNextSteps_AsButtonsThatRunByCode()
    {
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = Sat1500, Note = Written() };
        var runner = new FakeRunner { Simulation = WithReach() };

        var res = await SimulateChat(advisor, runner);

        Assert.Equal(2, res.QuickReplies!.Count);
        var why = res.QuickReplies[0];
        Assert.Equal("Vì sao Boston University lên Vừa sức?", why.Label);      // trường bị loại (Gone) không được đưa ra
        Assert.Equal(new ChatAction("explain", "bu"), why.Action);
        var need = res.QuickReplies[1];
        Assert.Equal("Cần gì để vào trường em muốn?", need.Label);               // không chọn sẵn trường: học sinh chưa nói quan tâm trường nào
        Assert.Equal(new ChatAction("goal_seek"), need.Action);
    }

    [Fact]
    public async Task SimulateAction_WithOnlyPriorities_OffersToExplainTheSchoolThatMovedMost()
    {
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = AidFirst };
        var runner = new FakeRunner
        {
            SimulationFor = pref => pref!.SequenceEqual([2, 4, 2]) ? OrderedSimulation("ne", "bu") : OrderedSimulation("bu", "ne"),
        };

        var res = await SimulateChat(advisor, runner);

        var why = res.QuickReplies!.Single();
        Assert.Equal("Vì sao Northeastern University lên vị trí 1?", why.Label);
        Assert.Equal(new ChatAction("explain", "ne"), why.Action);
    }

    [Fact]
    public async Task SimulateAction_WhenNothingChanged_OffersNoButtons()
    {
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = Sat1500, Note = Written() };
        var runner = new FakeRunner { Simulation = OrderedSimulation("bu", "ne") };   // không đổi nhóm, không có trường Khó vào

        var res = await SimulateChat(advisor, runner);

        Assert.Null(res.QuickReplies);
    }

    [Fact]
    public async Task SimulateAction_WhenTheOrderDoesNotChange_SaysSoAndInventsNoMoves()
    {
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = AidFirst };
        var runner = new FakeRunner { Simulation = OrderedSimulation("bu", "ne") };   // không có Latest: mức hiện tại 2-2-2

        var res = await SimulateChat(advisor, runner);

        Assert.Contains("gần như không đổi", res.Answer);
        Assert.DoesNotContain("từ vị trí", res.Answer);
    }

    [Fact]
    public async Task SimulateAction_WithPrioritiesAndProfileValues_AnswersBoth()
    {
        var data = JsonDocument.Parse("""{"changes":{"sat":1500},"unsupported":[],"priorities":{"cost":4}}""").RootElement;
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = data, Note = Written() };
        var runner = new FakeRunner { Simulation = OrderedSimulation("bu", "ne") };

        var res = await SimulateChat(advisor, runner);

        Assert.Equal(1500m, runner.SimulatedWith!.Sat);
        Assert.StartsWith("Với SAT 1500, Boston University lên Vừa sức.", res.Answer);
        Assert.Contains("chi phí thấp 4/4", res.Answer);
        var widget = res.Widget!.Data!.Value;
        Assert.Equal(1500, widget.GetProperty("sat").GetInt32());
        Assert.Equal(3, widget.GetProperty("pref").GetArrayLength());
    }

    [Theory]
    [InlineData("""{"changes":{},"priorities":{"aid":5}}""")]       // mức ngoài 0..4
    [InlineData("""{"changes":{},"priorities":{"aid":"cao"}}""")]
    [InlineData("""{"changes":{},"priorities":{}}""")]
    public void Parse_IgnoresPrioritiesThatAreNotALevelFromZeroToFour(string json)
    {
        Assert.Null(ChatSimulation.Parse(JsonDocument.Parse(json).RootElement));
    }

    [Fact]
    public void BasePref_ReadsTheSavedOneAndFallsBackToBalanced()
    {
        Assert.Equal([0, 4, 2], ChatSimulation.BasePref("""{"pref":[0,4,2]}"""));
        Assert.Equal([2, 2, 2], ChatSimulation.BasePref(null));
        Assert.Equal([2, 2, 2], ChatSimulation.BasePref("""{"pref":[9,0,0]}"""));
        Assert.Equal([2, 2, 2], ChatSimulation.BasePref("không phải json"));
        Assert.Equal([3, 2, 4], ChatSimulation.MergePref([2, 2, 4], new ChatPriorities(3, null, null)));
    }

    [Theory]
    [InlineData("FAILED")]   // Gemini lỗi hoặc lời không qua kiểm tra số liệu
    [InlineData("OK")]       // trả rỗng cũng coi như lỗi
    public async Task SimulateAction_WhenTheWordsAreMissing_UsesTheSentenceBuiltByCode(string status)
    {
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = Sat1500, Note = Written(status, "") };

        var res = await SimulateChat(advisor, new FakeRunner { Simulation = Simulation() });

        Assert.StartsWith("Với SAT 1500 (hiện tại 1290) thì mình gợi ý như sau:", res.Answer);
        Assert.Contains("- Vừa sức: Boston University, Northeastern University", res.Answer);
        Assert.Equal("simulate", res.Widget!.Type);   // panel vẫn hiện kết quả
    }

    [Fact]
    public async Task SimulateAction_WhenTheAdvisorIsDown_UsesTheSentenceBuiltByCode()
    {
        foreach (Exception error in new Exception[] { new HttpRequestException("advisor tắt"), new TaskCanceledException("HttpClient.Timeout") })
        {
            var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = Sat1500, NoteError = error };

            var res = await SimulateChat(advisor, new FakeRunner { Simulation = Simulation() });

            Assert.StartsWith("Với SAT 1500 (hiện tại 1290) thì mình gợi ý như sau:", res.Answer);
        }
    }

    [Fact]
    public async Task SimulateAction_WithoutUsableValues_SaysItFailed_AndDoesNotSimulate()
    {
        var runner = new FakeRunner { Simulation = Simulation() };

        var res = await SimulateChat(new FakeAdvisor("B4") { Action = "simulate" }, runner);

        Assert.Equal(ChatSimulation.Failed, res.Answer);
        Assert.Null(runner.SimulatedWith);
        Assert.Null(res.Widget);
    }

    [Theory]
    [InlineData(typeof(ArgumentException), "SAT giả định phải từ 400 đến 1600.", "Mình chưa thử được kịch bản này: SAT giả định phải từ 400 đến 1600.")]
    [InlineData(typeof(InvalidOperationException), "Chưa có hồ sơ.", "Mình chưa thử được kịch bản này: Chưa có hồ sơ.")]
    [InlineData(typeof(InvalidOperationException), "Hồ sơ chưa có bậc học muốn theo.", "Mình chưa thử được kịch bản này: Hồ sơ chưa có bậc học")]
    [InlineData(typeof(InvalidOperationException), "Dữ liệu lưu bị hỏng", ChatSimulation.Failed)]
    public async Task SimulateAction_WhenTheSimulationIsRejected_ExplainsWhy(Type type, string message, string expected)
    {
        var runner = new FakeRunner { SimulateError = (Exception)Activator.CreateInstance(type, message)! };

        var res = await SimulateChat(new FakeAdvisor("B4") { Action = "simulate", ActionData = Sat1500 }, runner);

        Assert.Contains(expected, res.Answer);
        Assert.Null(res.Widget);
    }

    // "Cần gì để vào trường X" trong chat: advisor trả action goal_seek kèm tên trường chữ thô, .NET khớp tên rồi hỏi lại hoặc chạy goal-seeking
    private static readonly JsonElement BuAction = JsonDocument.Parse("""{"school":"BU","target":"match"}""").RootElement;

    private static NameMatch Found(string code = "188429", string name = "Boston University") =>
        new(NameMatchKind.Found, [new SchoolRef(code, name)]);

    private static SchoolGoalResult GoalOf() => new(
        "188429", "Boston University", "R", "M", "reachable", "M", [],
        new GoalPlanDto([new("sat", 1290, 1430)], 1.4, "hard"), null, [new GoalPlanDto([new("sat", 1290, 1430)], 1.4, "hard")], [],
        new GoalBudgetDto("ok", 38_000m, 50_000m, 34_600m, null, null), null, []);

    private static async Task<AdvisorChatResponse> GoalChat(JsonElement? data, FakeRunner runner) =>
        await new ChatService(new FakeAdvisor("B4") { Action = "goal_seek", ActionData = data }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

    [Fact]
    public async Task GoalSeekAction_WhenTheSchoolIsClear_RunsGoalSeekingAndAnswersWithTheSummary()
    {
        var runner = new FakeRunner { Match = Found(), GoalResult = GoalOf() };

        var res = await GoalChat(BuAction, runner);

        Assert.Equal("BU", runner.SearchedFor);
        Assert.Equal(("188429", "match"), runner.GoalCalledWith);
        Assert.StartsWith("Mình hiểu bạn hỏi về Boston University, nhóm muốn đạt: Vừa sức.", res.Answer);
        Assert.Equal("goal_seek", res.Widget!.Type);
        Assert.Null(res.QuickReplies);
        Assert.Null(res.Action);     // việc nội bộ, không trả cho trình duyệt
    }

    // ---- hành động có cấu trúc từ nút trên thẻ trường: không qua advisor, không khớp tên

    private static async Task<(AdvisorChatResponse Res, FakeAdvisor Advisor)> ActionChat(ChatAction? action, FakeRunner runner)
    {
        var advisor = new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode };   // nếu advisor bị gọi nhầm sẽ trả kết quả khác hẳn
        var res = await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello, Action: action));
        return (res, advisor);
    }

    [Fact]
    public async Task ChatAction_GoalSeek_RunsOnTheCodeWithoutTheAdvisorOrNameMatching()
    {
        var runner = new FakeRunner { GoalResult = GoalOf() };

        var (res, advisor) = await ActionChat(new ChatAction("goal_seek", "188429", "safety"), runner);

        Assert.Null(advisor.LastRequest);                 // không gọi SLM
        Assert.Null(runner.SearchedFor);                  // không khớp tên
        Assert.Equal(("188429", "safety"), runner.GoalCalledWith);
        Assert.StartsWith("Mình hiểu bạn hỏi về Boston University", res.Answer);
        Assert.Equal("goal_seek", res.Widget!.Type);
        Assert.Null(res.Action);
    }

    // Nút "Cần gì để vào trường em muốn?" không kèm trường: mở ô chọn trường, không gọi advisor và không chạy goal-seeking
    [Fact]
    public async Task ChatAction_GoalSeekWithoutASchool_AsksWhichSchoolWithThePicker()
    {
        var runner = new FakeRunner { GoalResult = GoalOf() };

        var (res, advisor) = await ActionChat(new ChatAction("goal_seek"), runner);

        Assert.Null(advisor.LastRequest);
        Assert.Null(runner.GoalCalledWith);
        Assert.Equal(ChatGoalSeek.AskSchool, res.Answer);
        Assert.Equal("school", res.Widget!.Type);
    }

    // Thẻ kết quả do code dựng được gửi TRƯỚC khi Gemini được gọi (Gemini chậm), rồi lời mới tới sau trong dòng final
    [Fact]
    public async Task GoalSeek_SendsTheCardBeforeAskingGemini()
    {
        var order = new List<string>();
        var advisor = new FakeAdvisor("B4")
        {
            Action = "goal_seek", ActionData = BuAction,
            GoalNote = new GoalNoteResponseDto("1.0", "gemini", "OK", "Để lên nhóm Vừa sức ở Boston University, bạn cần nâng SAT."),
            OnGoalNote = () => order.Add("gemini"),
        };
        var runner = new FakeRunner { Match = Found(), GoalResult = GoalOf() };
        AdvisorStreamEvent? card = null;

        var res = await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), null, runner).ChatAsync(
            Guid.NewGuid(), new AdvisorChatRequest(Hello), default,
            e => { if (e.Type == "card") { card = e; order.Add("card"); } return Task.CompletedTask; });

        Assert.Equal(["card", "gemini"], order);
        Assert.Equal("goal_seek", card!.Widget!.Type);
        Assert.StartsWith("Để lên nhóm Vừa sức", res.Answer);
    }

    [Fact]
    public async Task ChatAction_GoalSeek_WithoutATarget_LetsTheSystemPickOneGroupUp()
    {
        var runner = new FakeRunner { GoalResult = GoalOf() };

        await ActionChat(new ChatAction("goal_seek", "188429"), runner);

        Assert.Equal(("188429", null), runner.GoalCalledWith);
    }

    [Fact]
    public async Task ChatAction_Explain_RunsTheSchoolAdviceOnTheCode()
    {
        var runner = new FakeRunner { Explanation = Explanation() };

        var (res, advisor) = await ActionChat(new ChatAction("explain", "iowa"), runner);

        Assert.Null(advisor.LastRequest);
        Assert.Equal("iowa", runner.ExplainedCode);
        Assert.StartsWith("University of Iowa: ", res.Answer);
    }

    // Hành động sai (loại lạ, giải thích thiếu mã, goal_seek có nhóm đích mà thiếu mã, nhóm đích không hợp lệ, mã quá dài): bỏ qua, chat chạy như thường qua advisor.
    // goal_seek không có mã và không có nhóm đích là hợp lệ: mở ô chọn trường (test riêng)
    [Theory]
    [InlineData("unknown", "188429", null)]
    [InlineData("explain", "", null)]
    [InlineData("explain", null, null)]
    [InlineData("goal_seek", "", "match")]
    [InlineData("goal_seek", "188429", "reach")]
    [InlineData("goal_seek", "188429", "xyz")]
    [InlineData("explain", "0123456789012345678901234567890123", null)]
    public async Task ChatAction_Invalid_FallsBackToTheNormalAdvisorTurn(string type, string? code, string? target)
    {
        var runner = new FakeRunner { Explanation = Explanation() };

        var (_, advisor) = await ActionChat(new ChatAction(type, code, target), runner);

        Assert.NotNull(advisor.LastRequest);   // advisor vẫn được gọi
        Assert.Null(runner.GoalCalledWith);
    }

    [Fact]
    public async Task ChatAction_WithoutARunner_IsIgnored()
    {
        var advisor = new FakeAdvisor("B4");

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow())
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello, Action: new ChatAction("goal_seek", "188429")));

        Assert.NotNull(advisor.LastRequest);
    }

    private static async Task<AdvisorChatResponse> GoalChatWith(FakeAdvisor advisor, FakeRunner runner) =>
        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

    [Fact]
    public async Task GoalSeekAction_AnswersWithTheWordsOfTheAdvisor_WhenTheyPassTheCheck()
    {
        var advisor = new FakeAdvisor("B4")
        {
            Action = "goal_seek", ActionData = BuAction,
            GoalNote = new GoalNoteResponseDto("1.0", "gemini", "OK", "Để lên nhóm Vừa sức ở Boston University, bạn cần nâng SAT."),
        };
        var runner = new FakeRunner { Match = Found(), GoalResult = GoalOf() };

        var res = await GoalChatWith(advisor, runner);

        Assert.Equal("Để lên nhóm Vừa sức ở Boston University, bạn cần nâng SAT.", res.Answer);
        Assert.Equal("goal_seek", res.Widget!.Type);   // khung kết quả trên trang vẫn mở
        // Dữ liệu gửi Gemini: trường đã hiểu, nhóm đích, dữ kiện đã tính, và tên các trường khác (không gồm trường đang hỏi)
        var note = advisor.LastGoalNote!;
        Assert.Equal(("Boston University", "Vừa sức", "reachable"), (note.School, note.Target, note.Outcome));
        Assert.StartsWith("Tài chính:", note.Facts[0]);
        Assert.Equal(["Baylor University", "Harvard University"], note.Others);
    }

    [Theory]
    [InlineData("FAILED")]   // Gemini lỗi hoặc lời không qua kiểm tra
    [InlineData("OK")]       // trả rỗng cũng coi như lỗi
    public async Task GoalSeekAction_WhenTheWordsAreMissing_UsesTheSentenceBuiltByCode(string status)
    {
        var advisor = new FakeAdvisor("B4") { Action = "goal_seek", ActionData = BuAction, GoalNote = new GoalNoteResponseDto("1.0", "gemini", status, "") };

        var res = await GoalChatWith(advisor, new FakeRunner { Match = Found(), GoalResult = GoalOf() });

        Assert.StartsWith("Mình hiểu bạn hỏi về Boston University, nhóm muốn đạt: Vừa sức.", res.Answer);
        Assert.Equal("goal_seek", res.Widget!.Type);
    }

    [Fact]
    public async Task GoalSeekAction_WhenTheAdvisorIsDown_UsesTheSentenceBuiltByCode()
    {
        foreach (Exception error in new Exception[] { new HttpRequestException("advisor tắt"), new TaskCanceledException("HttpClient.Timeout") })
        {
            var advisor = new FakeAdvisor("B4") { Action = "goal_seek", ActionData = BuAction, GoalNoteError = error };

            var res = await GoalChatWith(advisor, new FakeRunner { Match = Found(), GoalResult = GoalOf() });

            Assert.StartsWith("Mình hiểu bạn hỏi về Boston University", res.Answer);
        }
    }

    [Fact]
    public async Task GoalSeekAction_WhenTheNameCouldBeSeveralSchools_AsksWhichOneWithButtons_AndDoesNotRunGoalSeeking()
    {
        var match = new NameMatch(NameMatchKind.Ambiguous, [new SchoolRef("1", "Boston University"), new SchoolRef("2", "Baylor University")]);
        var runner = new FakeRunner { Match = match, GoalResult = GoalOf() };

        var res = await GoalChat(BuAction, runner);

        Assert.Contains("ý bạn là trường nào", res.Answer);
        Assert.Equal(
            [new QuickReply("Boston University", "Cần gì để vào Boston University ở nhóm Vừa sức?"), new QuickReply("Baylor University", "Cần gì để vào Baylor University ở nhóm Vừa sức?")],
            res.QuickReplies);
        Assert.Null(runner.GoalCalledWith);
        // Ngoài các nút còn có ô chọn trường có gợi ý, giữ nhóm đích để câu hỏi gửi lại đúng ý
        Assert.Equal("school", res.Widget!.Type);
        Assert.Equal("match", res.Widget.Data!.Value.GetProperty("target").GetString());
    }

    [Fact]
    public async Task GoalSeekAction_WhenTheSchoolIsNotInTheList_SaysSo()
    {
        var runner = new FakeRunner { Match = new NameMatch(NameMatchKind.NotFound, []) };

        var res = await GoalChat(JsonDocument.Parse("""{"school":"Yale"}""").RootElement, runner);

        Assert.Contains("chưa thấy trường “Yale”", res.Answer);
        Assert.Null(runner.GoalCalledWith);
        Assert.Equal("school", res.Widget!.Type);          // không còn ngõ cụt: học sinh tìm trong ô chọn
        Assert.Equal(JsonValueKind.Null, res.Widget.Data!.Value.GetProperty("target").ValueKind);
    }

    [Fact]
    public async Task GoalSeekAction_WithoutASchool_SaysItFailed_AndDoesNotSearch()
    {
        var runner = new FakeRunner { Match = Found() };

        var res = await GoalChat(null, runner);

        Assert.Equal(ChatGoalSeek.Failed, res.Answer);
        Assert.Null(runner.SearchedFor);
    }

    [Theory]
    [InlineData(typeof(ArgumentException), "Nhóm đích phải là reach, match hoặc safety.", "Mình chưa tính được: Nhóm đích phải là reach, match hoặc safety.")]
    [InlineData(typeof(InvalidOperationException), "Hồ sơ chưa có bậc học muốn theo.", "Mình chưa tính được: Hồ sơ chưa có bậc học muốn theo.")]
    [InlineData(typeof(InvalidOperationException), "Dữ liệu lưu bị hỏng", ChatGoalSeek.Failed)]
    public async Task GoalSeekAction_WhenGoalSeekingIsRejected_ExplainsWhy(Type type, string message, string expected)
    {
        var runner = new FakeRunner { Match = Found(), GoalError = (Exception)Activator.CreateInstance(type, message)! };

        var res = await GoalChat(BuAction, runner);

        Assert.Equal(expected, res.Answer);
        Assert.Null(res.Widget);
    }

    // Có kết quả rồi thì mỗi lượt gửi kèm danh sách trường mới nhất cho advisor. Lấy từ DB, không dựa vào trạng thái chat
    [Fact]
    public async Task WithResults_SendsTheLatestSchoolListToTheAdvisor()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = new ChatSession { UserId = userId, StateJson = "{\"currentStep\":\"B4\",\"hasResults\":true}", CurrentStep = "B4" };
        store.Rows.Add(session);
        var runner = new FakeRunner { Latest = new Recommendation { ItemsJson = OneSchool } };
        var advisor = new FakeAdvisor("B4");

        await new ChatService(advisor, store, new FakeProfileFlow(), null, runner)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Equal(new ChatSchool("ASU", "Arizona State University", null), Assert.Single(advisor.LastRequest!.Schools!));
    }

    [Fact]
    public async Task ARecommendationMadeOnThePage_IsSentEvenThoughTheChatNeverRanOne()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = new ChatSession { UserId = userId, StateJson = "{\"currentStep\":\"B4\"}", CurrentStep = "B4" };
        store.Rows.Add(session);
        var runner = new FakeRunner { Latest = new Recommendation { ItemsJson = OneSchool } };
        var advisor = new FakeAdvisor("B4");

        await new ChatService(advisor, store, new FakeProfileFlow(), null, runner)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Equal(new ChatSchool("ASU", "Arizona State University", null), Assert.Single(advisor.LastRequest!.Schools!));
    }

    [Fact]
    public async Task WithoutAnyRecommendation_SendsNoSchoolList()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = new ChatSession { UserId = userId, StateJson = "{\"currentStep\":\"B4\"}", CurrentStep = "B4" };
        store.Rows.Add(session);
        var runner = new FakeRunner();
        var advisor = new FakeAdvisor("B4");

        await new ChatService(advisor, store, new FakeProfileFlow(), null, runner)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Null(advisor.LastRequest!.Schools);
        Assert.True(runner.LatestRead);
    }

    private sealed class FakeRunner : IRecommendationRunner
    {
        public Recommendation? Result { get; init; }
        public Exception? Error { get; init; }
        public Guid? CalledFor { get; private set; }
        public Recommendation? Latest { get; init; }
        public bool LatestRead { get; private set; }
        public SchoolExplanation? Explanation { get; init; }
        public Exception? ExplainError { get; init; }
        public string? ExplainedCode { get; private set; }
        public Guid? ExplainedFor { get; private set; }

        public Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct)
        {
            LatestRead = true;
            return Task.FromResult(Latest);
        }

        public ExplainSchoolDto ExplainCard { get; init; } =
            new("University of Iowa", "IA", "public", "R", "MAIN", ["GPA_MID", "BUDGET_OK"], 0.17, 1200, 1400, 40.0, 30.0, 30_000, null);
        public Action? OnExplain { get; init; }   // chạy ngay khi lời tư vấn được viết, để kiểm thứ tự

        public SimulationOverrides? ExplainOverrides { get; private set; }
        public string? ExplainBasisLabel { get; private set; }

        public Task<ExplainSchoolDto> ExplainCardAsync(Guid userId, string code, CancellationToken ct, SimulationOverrides? overrides = null) => Task.FromResult(ExplainCard);

        public Task<SchoolExplanation> ExplainAsync(Guid userId, string code, CancellationToken ct, SimulationOverrides? overrides = null, string? basis = null)
        {
            ExplainOverrides = overrides;
            ExplainBasisLabel = basis;
            OnExplain?.Invoke();
            ExplainedFor = userId;
            ExplainedCode = code;
            if (ExplainError is not null)
                throw ExplainError;
            return Task.FromResult(Explanation!);
        }

        public IReadOnlyList<SchoolOption> Options { get; init; } =
            [new SchoolOption("188429", "Boston University", "MA"), new SchoolOption("1", "Baylor University", "TX"), new SchoolOption("2", "Harvard University", "MA")];

        public Task<IReadOnlyList<SchoolOption>> ListSchoolOptionsAsync(Guid userId, CancellationToken ct) => Task.FromResult(Options);

        public NameMatch? Match { get; init; }
        public SchoolGoalResult? GoalResult { get; init; }
        public Exception? GoalError { get; init; }
        public string? SearchedFor { get; private set; }
        public (string Code, string? Target)? GoalCalledWith { get; private set; }

        public Task<NameMatch> FindSchoolsAsync(Guid userId, string text, CancellationToken ct)
        {
            SearchedFor = text;
            return Task.FromResult(Match!);
        }

        public Task<SchoolGoalResult> GoalSeekAsync(Guid userId, string code, string? target, CancellationToken ct)
        {
            GoalCalledWith = (code, target);
            if (GoalError is not null)
                throw GoalError;
            if (GoalResult is null)
                throw new InvalidOperationException($"Chưa có trường {code} trong dữ liệu.");
            return Task.FromResult(GoalResult);
        }

        public SimulationResult? Simulation { get; init; }
        public Exception? SimulateError { get; init; }
        public SimulationOverrides? SimulatedWith { get; private set; }
        public List<IReadOnlyList<int>?> SimulatedPrefs { get; } = [];
        public Func<IReadOnlyList<int>?, SimulationResult>? SimulationFor { get; init; }   // kết quả theo ưu tiên, để kiểm thứ tự đổi

        public Task<SimulationResult> SimulateAsync(Guid userId, SimulationOverrides overrides, IReadOnlyList<int>? pref, CancellationToken ct)
        {
            SimulatedWith = overrides;
            SimulatedPrefs.Add(pref);
            if (SimulateError is not null)
                throw SimulateError;
            return Task.FromResult(SimulationFor?.Invoke(pref) ?? Simulation!);
        }

        public Task<Recommendation> CreateAsync(Guid userId, IReadOnlyList<int>? pref, CancellationToken ct)
        {
            CalledFor = userId;
            if (Error is not null)
                throw Error;
            return Task.FromResult(Result!);
        }
    }

    private sealed class FakeAnalyses : IChatAnalysisRepository
    {
        public List<(Guid UserId, string Kind, string Json, string Model)> Saved { get; } = [];

        public Task SaveAsync(Guid userId, string kind, string resultJson, string modelVersion, CancellationToken ct = default)
        {
            Saved.Add((userId, kind, resultJson, modelVersion));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAdvisor(string nextStep, IReadOnlyList<QuickReply>? replies = null, Widget? widget = null) : IAdvisorClient
    {
        public AdvisorChatRequest? LastRequest { get; private set; }
        public JsonElement? Profile { get; init; }
        public IReadOnlyList<string>? Changed { get; init; }
        public string? QuestionId { get; init; }
        public IReadOnlyList<string>? Skills { get; init; }
        public string? Action { get; init; }
        public JsonElement? ActionData { get; init; }

        public Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            var question = QuestionId is null ? "" : $",\"questionId\":\"{QuestionId}\"";
            var state = JsonDocument.Parse($"{{\"currentStep\":\"{nextStep}\"{question}}}").RootElement;
            return Task.FromResult(new AdvisorChatResponse(
                "ok", [], null, state, QuickReplies: replies, Widget: widget, Profile: Profile, ProfileChanged: Changed, Skills: Skills, Action: Action, ActionData: ActionData));
        }

        // Trả lời theo từng ý: gọi onEvent cho từng tin trong Events rồi trả kết quả như ChatAsync
        public IReadOnlyList<AdvisorStreamEvent> Events { get; init; } = [];
        public int StreamCalls { get; private set; }
        public Exception? StreamError { get; init; }

        public async Task<AdvisorChatResponse> ChatStreamAsync(
            AdvisorChatRequest request, Func<AdvisorStreamEvent, Task> onEvent, CancellationToken ct = default)
        {
            StreamCalls++;
            foreach (var e in Events)
                await onEvent(e);
            if (StreamError is not null)
                throw StreamError;
            return await ChatAsync(request, ct);
        }

        public Task<ExplainResponseDto> ExplainAsync(ExplainRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();

        // Mặc định Gemini lỗi: goal-seeking dùng câu ghép bằng code. Test cần lời của Gemini thì đặt GoalNote
        public GoalNoteResponseDto GoalNote { get; init; } = new("1.0", "gemini", "FAILED", "");
        public Exception? GoalNoteError { get; init; }
        public GoalNoteRequestDto? LastGoalNote { get; private set; }
        public Action? OnGoalNote { get; init; }   // chạy ngay khi Gemini được gọi, để kiểm thứ tự

        public Task<GoalNoteResponseDto> WriteGoalNoteAsync(GoalNoteRequestDto request, CancellationToken ct = default)
        {
            OnGoalNote?.Invoke();
            LastGoalNote = request;
            if (GoalNoteError is not null)
                throw GoalNoteError;
            return Task.FromResult(GoalNote);
        }

        public ScenarioNoteResponseDto? Note { get; init; }
        public Exception? NoteError { get; init; }
        public ScenarioNoteRequestDto? LastNote { get; private set; }

        public Task<ScenarioNoteResponseDto> WriteScenarioNoteAsync(ScenarioNoteRequestDto request, CancellationToken ct = default)
        {
            LastNote = request;
            if (NoteError is not null)
                throw NoteError;
            return Task.FromResult(Note!);
        }
    }

    private sealed class FakeSessions : IChatSessionRepository
    {
        public List<ChatSession> Rows { get; } = [];

        public Task<ChatSession?> FindAsync(Guid id, Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(s => s.Id == id && s.UserId == userId));

        public Task AddAsync(ChatSession session, CancellationToken ct = default)
        {
            Rows.Add(session);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeBenchmarks(IReadOnlyList<BenchmarkRow> rows) : IChatBenchmarkRepository
    {
        public string? Level { get; private set; }

        public Task<IReadOnlyList<BenchmarkRow>> ListAsync(string studyLevel, CancellationToken ct = default)
        {
            Level = studyLevel;
            return Task.FromResult(rows);
        }
    }

    private sealed class FakeProfileFlow : IProfileFlowService
    {
        public ProfileFlowDraftDto Draft { get; init; } = new();
        public Exception? PatchError { get; init; }
        public Exception? CommitError { get; init; }
        public int Commits { get; private set; }
        public JsonObject? PatchedProfile { get; private set; }
        public IReadOnlyCollection<string>? PatchedKeys { get; private set; }

        public Task<ProfileFlowStateDto> GetFlowStateAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new ProfileFlowStateDto(1, [], Draft, null, null));

        public Task<ProfileFlowStateDto> PatchDraftAsync(
            Guid userId, JsonObject chatProfile, IReadOnlyCollection<string> changedKeys, CancellationToken ct = default)
        {
            if (PatchError is not null) throw PatchError;
            PatchedProfile = chatProfile;
            PatchedKeys = changedKeys;
            return Task.FromResult(new ProfileFlowStateDto(1, [], Draft, null, null));
        }

        public Task<ProfileFlowStateDto> SaveDraftAsync(Guid userId, SaveProfileDraftRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UploadDraftTranscriptResultDto> UploadDraftTranscriptAsync(Guid userId, Stream fileStream, string fileName, string contentType, long length, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CommitProfileFlowResponse> CommitFlowAsync(Guid userId, CommitProfileFlowRequest? request, CancellationToken ct = default)
        {
            if (CommitError is not null) throw CommitError;
            Commits++;
            return Task.FromResult<CommitProfileFlowResponse>(null!);
        }
        public Task ClearDraftAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
