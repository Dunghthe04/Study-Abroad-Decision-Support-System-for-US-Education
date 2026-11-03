using System.Net;
using System.Text;
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
}
