using System.Net;
using System.Text;
using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Infrastructure.Advisor;

namespace StudyAbroad.UnitTests;

/// <summary>AdvisorClient.ChatStreamAsync đọc từng dòng JSON advisor gửi: progress, partial, final, error.</summary>
public class AdvisorClientStreamTests
{
    private static readonly IReadOnlyList<ChatMessage> Hello = [new ChatMessage("user", "chào")];

    private sealed class Stub(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/x-ndjson"),
            });
        }
    }

    private static AdvisorClient ClientFor(Stub stub) =>
        new(new HttpClient(stub) { BaseAddress = new Uri("http://advisor/") });

    private const string Final =
        """{"type":"final","response":{"answer":"đáp 1\n\nđáp 2","citations":[],"disclaimer":null,"state":{"currentStep":"B3"},"quickReplies":[{"label":"A","value":"a"}],"profileChanged":["gpa"]}}""";

    [Fact]
    public async Task ReadsEveryEventInOrder_ThenReturnsTheFinalResponse()
    {
        var stub = new Stub(
            """{"type":"progress","answers":2}""" + "\n" +
            """{"type":"partial","text":"đáp 1"}""" + "\n" +
            """{"type":"partial","text":"đáp 2"}""" + "\n" +
            Final + "\n");
        var seen = new List<AdvisorStreamEvent>();

        var res = await ClientFor(stub).ChatStreamAsync(
            new AdvisorChatRequest(Hello), e => { seen.Add(e); return Task.CompletedTask; });

        Assert.Equal(
            [new AdvisorStreamEvent("progress", Answers: 2), new("partial", Text: "đáp 1"), new("partial", Text: "đáp 2")],
            seen);
        Assert.Equal("đáp 1\n\nđáp 2", res.Answer);
        Assert.Equal("B3", res.State!.Value.GetProperty("currentStep").GetString());
        Assert.Equal("a", Assert.Single(res.QuickReplies!).Value);
        Assert.Equal(["gpa"], res.ProfileChanged);
        Assert.Equal("/api/v1/chat/stream", stub.Request!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task BlankLinesAreIgnored()
    {
        var res = await ClientFor(new Stub("\n" + Final + "\n\n")).ChatStreamAsync(
            new AdvisorChatRequest(Hello), _ => Task.CompletedTask);

        Assert.Equal("đáp 1\n\nđáp 2", res.Answer);
    }

    [Fact]
    public async Task AnErrorLine_IsReportedAsAnAdvisorFailure()
    {
        var stub = new Stub("""{"type":"progress","answers":1}""" + "\n" + """{"type":"error","message":"hỏng"}""" + "\n");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            ClientFor(stub).ChatStreamAsync(new AdvisorChatRequest(Hello), _ => Task.CompletedTask));
    }

    [Fact]
    public async Task AStreamWithoutAFinalLine_IsAnError()
    {
        var stub = new Stub("""{"type":"partial","text":"đáp 1"}""" + "\n");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ClientFor(stub).ChatStreamAsync(new AdvisorChatRequest(Hello), _ => Task.CompletedTask));
    }

    [Fact]
    public async Task AFailingHttpStatus_IsAnAdvisorFailure()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            ClientFor(new Stub("", HttpStatusCode.InternalServerError))
                .ChatStreamAsync(new AdvisorChatRequest(Hello), _ => Task.CompletedTask));
    }

    // ---- dựng lại câu hồ sơ đang chờ (lịch sử chat)

    [Fact]
    public async Task PendingQuestion_PostsToTheAdvisorRoute_InCamelCase_AndReadsTheRebuiltQuestion()
    {
        var stub = new Stub("""{"pending":true,"questionId":"F1","question":"Gia đình bạn chi bao nhiêu?","quickReplies":[{"label":"Dưới 20.000 USD","value":"Dưới 20.000 USD","href":null}],"widget":{"type":"budget","data":{}}}""");
        var state = JsonDocument.Parse("""{"currentStep":"B2","questionId":"F1"}""").RootElement;

        var result = await ClientFor(stub).PendingQuestionAsync(new AdvisorPendingQuestionRequest(state, HasCommittedProfile: true, ExactBudget: false));

        Assert.Equal("http://advisor/api/v1/chat/pending-question", stub.Request!.RequestUri!.ToString());
        var sent = await stub.Request.Content!.ReadAsStringAsync();
        Assert.Contains("\"state\":{\"currentStep\":\"B2\"", sent);
        Assert.Contains("\"hasCommittedProfile\":true", sent);
        Assert.DoesNotContain("\"profile\"", sent);   // trường rỗng không gửi
        Assert.Equal((true, "F1", "budget"), (result.Pending, result.QuestionId, result.Widget!.Type));
        Assert.Equal("Dưới 20.000 USD", Assert.Single(result.QuickReplies).Label);
    }

    // ---- tạo vector cho kho tri thức (backend gọi sau khi moderator nhập file)

    [Fact]
    public async Task EmbedKnowledge_PostsToTheAdvisorRoute_WithoutBody_AndReadsTheCamelCaseAnswer()
    {
        var stub = new Stub("""{"pending":494,"running":true,"done":0,"error":null,"started":true}""");

        var result = await ClientFor(stub).EmbedKnowledgeAsync();

        Assert.Equal(HttpMethod.Post, stub.Request!.Method);
        Assert.Equal("http://advisor/api/v1/knowledge/embed-missing", stub.Request.RequestUri!.ToString());
        Assert.Equal(new KnowledgeEmbedDto(494, true, 0, null, true), result);
    }

    [Fact]
    public async Task EmbedStatus_GetsFromTheAdvisorRoute_AndOnlyReads()
    {
        var stub = new Stub("""{"pending":3,"running":true,"done":7,"error":null}""");

        var result = await ClientFor(stub).GetKnowledgeEmbedStatusAsync();

        Assert.Equal(HttpMethod.Get, stub.Request!.Method);
        Assert.Equal("http://advisor/api/v1/knowledge/embed-status", stub.Request.RequestUri!.ToString());
        Assert.Equal(new KnowledgeEmbedDto(3, true, 7, null, false), result);   // advisor không gửi started: mặc định false
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]   // advisor chạy nhưng không có database
    [InlineData(HttpStatusCode.Unauthorized)]         // sai khóa API
    public async Task EmbedKnowledge_AFailingStatusIsAnAdvisorFailure(HttpStatusCode status)
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => ClientFor(new Stub("", status)).EmbedKnowledgeAsync());
    }
}
