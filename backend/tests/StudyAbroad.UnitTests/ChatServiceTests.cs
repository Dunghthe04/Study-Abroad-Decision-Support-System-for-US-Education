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

    private sealed class FakeRunner : IRecommendationRunner
    {
        public Recommendation? Result { get; init; }
        public Exception? Error { get; init; }
        public Guid? CalledFor { get; private set; }

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

        public Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            var question = QuestionId is null ? "" : $",\"questionId\":\"{QuestionId}\"";
            var state = JsonDocument.Parse($"{{\"currentStep\":\"{nextStep}\"{question}}}").RootElement;
            return Task.FromResult(new AdvisorChatResponse(
                "ok", [], null, state, QuickReplies: replies, Widget: widget, Profile: Profile, ProfileChanged: Changed, Skills: Skills, Action: Action));
        }

        public Task<EcResponseDto> ReadEcAsync(EcRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ScoreResponseDto> ScoreAsync(ScoreRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PickResponseDto> PickAsync(PickRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ExplainResponseDto> ExplainAsync(ExplainRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
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
