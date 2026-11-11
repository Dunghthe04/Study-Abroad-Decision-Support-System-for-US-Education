using StudyAbroad.Application.Advisor;

namespace StudyAbroad.UnitTests;

// "Giỏ chung" gom số đo của mọi lần gọi advisor trong một lượt, và lớp bọc IAdvisorClient bỏ số đo vào giỏ.
public class AiUsageCollectorTests
{
    private static AiUsageDto Call(string purpose, int tokens = 100) => new("claude", "m", purpose, "1.0", tokens, 10, 0, true, null, 50);

    // ---- giỏ

    [Fact]
    public void Collects_InTheOrderAdded()
    {
        var basket = new AiUsageCollector();

        basket.Add([Call("a")]);
        basket.Add([Call("b"), Call("c")]);

        Assert.Equal(["a", "b", "c"], basket.Snapshot().Select(c => c.Purpose));
    }

    [Fact]
    public void NullOrEmpty_IsIgnored_SoAnOldAdvisorWithoutAiUsageIsHarmless()
    {
        var basket = new AiUsageCollector();

        basket.Add(null);
        basket.Add([]);

        Assert.Empty(basket.Snapshot());
    }

    [Fact]
    public void Snapshot_IsACopy_ThatLaterAdditionsDoNotChange()
    {
        var basket = new AiUsageCollector();
        basket.Add([Call("a")]);

        var snapshot = basket.Snapshot();
        basket.Add([Call("b")]);

        Assert.Single(snapshot);
    }

    [Fact]
    public void Clear_EmptiesTheBasket()
    {
        var basket = new AiUsageCollector();
        basket.Add([Call("a")]);

        basket.Clear();

        Assert.Empty(basket.Snapshot());
    }

    [Fact]
    public async Task ParallelCalls_AreAllKept()
    {
        var basket = new AiUsageCollector();

        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(() => basket.Add([Call($"p{i}")]))));

        Assert.Equal(200, basket.Snapshot().Count);
    }

    // ---- lớp bọc

    [Fact]
    public async Task EveryKindOfAdvisorCall_PutsItsUsageIntoTheBasket_AndReturnsTheResponseUntouched()
    {
        var basket = new AiUsageCollector();
        var inner = new StubAdvisor();
        var advisor = new UsageCollectingAdvisorClient(inner, basket);

        var chat = await advisor.ChatAsync(new AdvisorChatRequest([]));
        var streamed = await advisor.ChatStreamAsync(new AdvisorChatRequest([]), _ => Task.CompletedTask);
        var explain = await advisor.ExplainAsync(new ExplainRequestDto(new(null, null, null, null, null), new("n", null, null, "M", "MAIN", [], null, null, null, null, null, null, null)));
        var goal = await advisor.WriteGoalNoteAsync(new GoalNoteRequestDto("s", "t", "o", [], [], []));
        var scenario = await advisor.WriteScenarioNoteAsync(new ScenarioNoteRequestDto("l", [], [], "none", new(null, null), [], []));
        var compare = await advisor.WriteCompareNoteAsync(new CompareNoteRequestDto([], [], []));

        Assert.Equal(["chat", "stream", "explain", "goal", "scenario", "compare"], basket.Snapshot().Select(c => c.Purpose));
        Assert.Same(inner.LastChat, chat);          // lớp bọc trả đúng đối tượng advisor vừa trả, không dựng lại
        Assert.Same(inner.LastStream, streamed);
        Assert.Equal("explain", explain.AiUsage![0].Purpose);
        Assert.Equal("goal", goal.AiUsage![0].Purpose);
        Assert.Equal("scenario", scenario.AiUsage![0].Purpose);
        Assert.Equal("compare", compare.AiUsage![0].Purpose);
    }

    [Fact]
    public async Task ACallThatThrows_RecordsNothing_AndTheErrorStillPropagates()
    {
        var basket = new AiUsageCollector();
        var advisor = new UsageCollectingAdvisorClient(new StubAdvisor { Error = new HttpRequestException("sập") }, basket);

        await Assert.ThrowsAsync<HttpRequestException>(() => advisor.ChatAsync(new AdvisorChatRequest([])));

        Assert.Empty(basket.Snapshot());
    }

    [Fact]
    public async Task ResponseWithoutAiUsage_AddsNothing()
    {
        var basket = new AiUsageCollector();
        var advisor = new UsageCollectingAdvisorClient(new StubAdvisor { WithoutUsage = true }, basket);

        await advisor.ChatAsync(new AdvisorChatRequest([]));

        Assert.Empty(basket.Snapshot());
    }

    private sealed class StubAdvisor : IAdvisorClient
    {
        public Exception? Error { get; init; }
        public bool WithoutUsage { get; init; }
        public AdvisorChatResponse? LastChat { get; private set; }
        public AdvisorChatResponse? LastStream { get; private set; }

        private IReadOnlyList<AiUsageDto>? Usage(string purpose) =>
            WithoutUsage ? null : [new AiUsageDto("claude", "m", purpose, "1.0", 100, 10, 0, true, null, 50)];

        public Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default)
        {
            if (Error is not null)
                throw Error;
            return Task.FromResult(LastChat = new AdvisorChatResponse("x", [], null, AiUsage: Usage("chat")));
        }

        public Task<AdvisorChatResponse> ChatStreamAsync(AdvisorChatRequest request, Func<AdvisorStreamEvent, Task> onEvent, CancellationToken ct = default) =>
            Task.FromResult(LastStream = new AdvisorChatResponse("x", [], null, AiUsage: Usage("stream")));

        public Task<ExplainResponseDto> ExplainAsync(ExplainRequestDto request, CancellationToken ct = default) =>
            Task.FromResult(new ExplainResponseDto("1", "m", "OK", "s", [], [], Usage("explain")));

        public Task<GoalNoteResponseDto> WriteGoalNoteAsync(GoalNoteRequestDto request, CancellationToken ct = default) =>
            Task.FromResult(new GoalNoteResponseDto("1", "m", "OK", "a", Usage("goal")));

        public Task<ScenarioNoteResponseDto> WriteScenarioNoteAsync(ScenarioNoteRequestDto request, CancellationToken ct = default) =>
            Task.FromResult(new ScenarioNoteResponseDto("1", "m", "OK", "a", Usage("scenario")));

        public Task<CompareNoteResponseDto> WriteCompareNoteAsync(CompareNoteRequestDto request, CancellationToken ct = default) =>
            Task.FromResult(new CompareNoteResponseDto("1", "m", "OK", "a", Usage("compare")));
    }
}
