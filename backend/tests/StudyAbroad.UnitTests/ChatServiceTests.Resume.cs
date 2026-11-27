using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Dashboard.Roadmap;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Bấm "Đúng rồi" mà còn câu đang chờ: lưu hồ sơ TRƯỚC, rồi mới nhờ advisor trả lời bằng lộ trình tính theo hồ sơ mới
public partial class ChatServiceTests
{
    private const string Waiting = "{\"currentStep\":\"B3\",\"pendingAdvice\":{\"type\":\"profile_review\"}}";

    private static AdvisorChatResponse Reply(string answer, string state, bool? resumeNeeded = null, string[]? skills = null,
        IReadOnlyList<AiUsageDto>? usage = null, QuickReply[]? replies = null) =>
        new(answer, [], null, JsonDocument.Parse(state).RootElement, QuickReplies: replies, Skills: skills, AiUsage: usage,
            ResumeNeeded: resumeNeeded);

    private static AdvisorChatResponse Confirmed(IReadOnlyList<AiUsageDto>? usage = null) =>
        Reply("Đã lưu hồ sơ.", Waiting, true, ["collect_profile"], usage);

    private static AdvisorChatResponse Assessed(IReadOnlyList<AiUsageDto>? usage = null) =>
        Reply("Đánh giá mới.", "{\"currentStep\":\"B4\"}", false, ["profile_review"], usage,
            [new QuickReply("Gợi ý trường", "Gợi ý trường cho em")]);

    private static (ChatService Service, ChatSession Session, Guid UserId) ResumeService(
        IAdvisorClient advisor, FakeProfileFlow flow, IRoadmapService? roadmap = null, IAiCallRepository? calls = null,
        IAiUsageCollector? collector = null)
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = AtReview(userId);
        store.Rows.Add(session);
        return (new ChatService(advisor, store, flow, aiCalls: calls, usageCollector: collector, roadmap: roadmap),
            session, userId);
    }

    private static AdvisorChatRequest ConfirmOf(ChatSession session) => new(Hello, session.Id.ToString());

    [Fact]
    public async Task Resume_SavesFirst_ThenAnswersWithTheRoadmapOfTheNewProfile()
    {
        var flow = new FakeProfileFlow();
        var advisor = new ScriptedAdvisor(flow, Confirmed(), Assessed());
        var (service, session, userId) = ResumeService(advisor, flow, new RoadmapAfterSave(flow));

        var res = await service.ChatAsync(userId, ConfirmOf(session));

        Assert.Equal([0, 1], advisor.CommitsAtCall);   // lần gọi thứ hai chạy sau khi đã lưu
        Assert.Equal(["IELTS chưa đạt", "IELTS đã đạt"],
            advisor.Requests.Select(r => r.Roadmap!.Value.GetProperty("summary").GetString()));
        Assert.Equal([false, true], advisor.Requests.Select(r => r.Resume));
        Assert.All(advisor.Requests, r => Assert.True(r.CanResume));
        Assert.True(advisor.Requests[1].State!.Value.TryGetProperty("pendingAdvice", out _));
        Assert.Equal("Đã lưu hồ sơ.\n\nĐánh giá mới.", res.Answer);
        Assert.Equal(["Gợi ý trường"], res.QuickReplies!.Select(q => q.Label));
        Assert.Equal("{\"currentStep\":\"B4\"}", session.StateJson);
        Assert.Equal("B4", session.CurrentStep);
        Assert.True(res.ProfileSaved);
        Assert.Null(res.ResumeNeeded);
    }

    [Fact]
    public async Task Resume_NotCalledWhenTheProfileCouldNotBeSaved()
    {
        var flow = new FakeProfileFlow { CommitError = new InvalidOperationException("Vui lòng nhập GPA hợp lệ.") };
        var advisor = new ScriptedAdvisor(flow, Confirmed(), Assessed());
        var (service, session, userId) = ResumeService(advisor, flow);
        var before = session.StateJson;

        var res = await service.ChatAsync(userId, ConfirmOf(session));

        Assert.Single(advisor.Requests);
        Assert.Contains("Hồ sơ chưa lưu được", res.Answer);
        Assert.Equal(before, session.StateJson);
        Assert.Null(res.ResumeNeeded);
    }

    [Fact]
    public async Task Resume_SecondCallFails_SaysSavedAndOffersToAnswerAgain()
    {
        var flow = new FakeProfileFlow();
        var advisor = new ScriptedAdvisor(flow, Confirmed(), null);
        var (service, session, userId) = ResumeService(advisor, flow);

        var res = await service.ChatAsync(userId, ConfirmOf(session));

        Assert.Equal($"Đã lưu hồ sơ.\n\n{ChatResume.FailedNote}", res.Answer);
        Assert.Equal([ChatResume.RetryValue], res.QuickReplies!.Select(q => q.Value));
        Assert.Equal(Waiting, session.StateJson);   // câu đang chờ còn đó để bấm nút trả lời lại
        Assert.True(res.ProfileSaved);
        Assert.Null(res.ResumeNeeded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resume_TokensOfBothCallsAreLoggedOnce(bool withCollector)
    {
        var flow = new FakeProfileFlow();
        var scripted = new ScriptedAdvisor(flow,
            Confirmed([new AiUsageDto("claude", "m", "doc_ho_so", InputTokens: 100)]),
            Assessed([new AiUsageDto("claude", "m", "viet_tu_van", InputTokens: 200)]));
        var collector = withCollector ? new AiUsageCollector() : null;
        IAdvisorClient advisor = collector is null ? scripted : new UsageCollectingAdvisorClient(scripted, collector);
        var calls = new FakeAiCalls();
        var (service, session, userId) = ResumeService(advisor, flow, calls: calls, collector: collector);

        await service.ChatAsync(userId, ConfirmOf(session));

        Assert.Equal([100, 200], calls.Rows.Select(c => c.PromptTokens ?? 0));
    }

    [Fact]
    public async Task Resume_FlagsSentByTheBrowserAreIgnored()
    {
        var flow = new FakeProfileFlow();
        var advisor = new ScriptedAdvisor(flow, Reply("ok", "{\"currentStep\":\"B3\"}"));
        var (service, session, userId) = ResumeService(advisor, flow);

        await service.ChatAsync(userId, ConfirmOf(session) with { Resume = true, CanResume = false });

        Assert.False(advisor.Requests.Single().Resume);
        Assert.True(advisor.Requests.Single().CanResume);
    }

    // Trả lời theo kịch bản: mỗi lần gọi lấy câu kế tiếp, null là advisor lỗi
    private sealed class ScriptedAdvisor(FakeProfileFlow flow, params AdvisorChatResponse?[] replies) : IAdvisorClient
    {
        public List<AdvisorChatRequest> Requests { get; } = [];
        public List<int> CommitsAtCall { get; } = [];

        public Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            CommitsAtCall.Add(flow.Commits);
            return Task.FromResult(replies[Requests.Count - 1] ?? throw new HttpRequestException("advisor tắt"));
        }

        public Task<ExplainResponseDto> ExplainAsync(ExplainRequestDto request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<GoalNoteResponseDto> WriteGoalNoteAsync(GoalNoteRequestDto request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<ScenarioNoteResponseDto> WriteScenarioNoteAsync(ScenarioNoteRequestDto request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<CompareNoteResponseDto> WriteCompareNoteAsync(CompareNoteRequestDto request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<RecommendNoteResponseDto> WriteRecommendNoteAsync(RecommendNoteRequestDto request, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    // Lộ trình đọc hồ sơ ĐÃ LƯU: chưa lưu thì còn "chưa đạt", lưu rồi mới "đã đạt"
    private sealed class RoadmapAfterSave(FakeProfileFlow flow) : IRoadmapService
    {
        public Task<RoadmapDto?> GetRoadmapAsync(Guid studentId, CancellationToken ct = default) =>
            Task.FromResult<RoadmapDto?>(new RoadmapDto("target", "Fall 2027", false, [],
                flow.Commits == 0 ? "IELTS chưa đạt" : "IELTS đã đạt", [], [], new RoadmapProgressDto(0, 0), false));

        public Task<(bool Linked, RoadmapDto? Roadmap)> GetChildRoadmapAsync(
            Guid parentId, Guid studentId, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<SetProvinceResult> SetProvinceAsync(Guid studentId, string? province, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
