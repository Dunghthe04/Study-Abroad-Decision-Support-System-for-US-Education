using System.Text.Json;
using StudyAbroad.Application.Advisor;
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

        var res = await new ChatService(advisor, store).ChatAsync(userId, new AdvisorChatRequest(Hello));

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

        await new ChatService(advisor, store).ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

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

        var res = await new ChatService(advisor, store)
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

        var res = await new ChatService(advisor, new FakeSessions()).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(replies, res.QuickReplies);
        Assert.Equal(widget, res.Widget);
        Assert.Null(res.State); // trạng thái nội bộ vẫn không lộ ra ngoài
    }

    private sealed class FakeAdvisor(string nextStep, IReadOnlyList<QuickReply>? replies = null, Widget? widget = null) : IAdvisorClient
    {
        public AdvisorChatRequest? LastRequest { get; private set; }

        public Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            var state = JsonDocument.Parse($"{{\"currentStep\":\"{nextStep}\"}}").RootElement;
            return Task.FromResult(new AdvisorChatResponse("ok", [], null, state, QuickReplies: replies, Widget: widget));
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
}
