using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.AiQuality;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;
using StudyAbroad.Infrastructure.Persistence.Repositories;

namespace StudyAbroad.UnitTests;

// UC-90, phần lấy dữ liệu và API: truy vấn lượt chat (đúng số liệu, EF dịch ra SQL được), dịch vụ (khoảng ngày) và controller (chỉ Admin).
public class AiQualityApiTests
{
    private static readonly DateTime From = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ToExclusive = new(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);

    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");
    private static DateTime At(int day, int hour = 12) => new(2026, 11, day, hour, 0, 0, DateTimeKind.Utc);

    private static ChatMessage Msg(
        int n, string role, DateTime at, string? outcome = null, string? meta = null, int? replyTo = null) =>
        new()
        {
            Id = Id(200 + n), ChatSessionId = Id(100), Role = role, CreatedAt = at, Outcome = outcome, MetaJson = meta, Content = "x",
            ReplyToMessageId = replyTo is { } r ? Id(200 + r) : null,
        };

    private static AiCall Call(int message, bool fallback) =>
        new() { Kind = "claude", ChatMessageId = Id(200 + message), FallbackUsed = fallback, CreatedAt = At(2) };

    // ---- truy vấn

    [Fact]
    public void Turns_AreTheSystemRows_AssistantRepliesAndSystemFailures_NotTheStudentsMessages()
    {
        var messages = new[]
        {
            Msg(1, "user", At(2, 9)),
            Msg(2, "assistant", At(2, 10), "answered", replyTo: 1),
            Msg(3, "user", At(3, 9)),
            Msg(4, "system", At(3, 10), "failed"),   // advisor sập hẳn: vẫn là một lượt hỏng
        }.AsQueryable();

        var rows = AiQualityQuery.Turns(messages, Array.Empty<AiCall>().AsQueryable(), From, ToExclusive).ToList();

        Assert.Equal(["answered", "failed"], rows.Select(r => r.Outcome));
    }

    [Fact]
    public void Turns_CarryTheQuestionsMeta_AndNullWhenThereIsNoQuestion()
    {
        var messages = new[]
        {
            Msg(1, "user", At(2, 9), meta: "{\"slm\":{\"fallback\":true}}"),
            Msg(2, "assistant", At(2, 10), "answered", meta: "{\"turnMs\":900}", replyTo: 1),
            Msg(3, "assistant", At(3, 10), "answered", meta: "{\"turnMs\":100}"),   // không có tin hỏi (ví dụ tin hỏi đã bị xóa)
        }.AsQueryable();

        var rows = AiQualityQuery.Turns(messages, Array.Empty<AiCall>().AsQueryable(), From, ToExclusive).ToList();

        Assert.Equal(("{\"turnMs\":900}", "{\"slm\":{\"fallback\":true}}"), (rows[0].ReplyMetaJson, rows[0].QuestionMetaJson));
        Assert.Equal(("{\"turnMs\":100}", null), (rows[1].ReplyMetaJson, rows[1].QuestionMetaJson));
    }

    [Fact]
    public void Turns_FlagTheTurnsWhoseCallsUsedTheFallback()
    {
        var messages = new[] { Msg(1, "assistant", At(2), "answered"), Msg(2, "assistant", At(2, 13), "answered") }.AsQueryable();
        var calls = new[] { Call(1, fallback: true), Call(1, fallback: false), Call(2, fallback: false) }.AsQueryable();

        var rows = AiQualityQuery.Turns(messages, calls, From, ToExclusive).ToList();

        Assert.Equal([true, false], rows.Select(r => r.UsedFallback));
    }

    [Fact]
    public void Turns_UseAHalfOpenWindow_AndComeOldestFirst()
    {
        var messages = new[]
        {
            Msg(1, "assistant", At(4, 23), "answered"),
            Msg(2, "assistant", From, "no_answer"),                  // đúng đầu khoảng: có
            Msg(3, "assistant", ToExclusive, "failed"),              // đúng cuối khoảng (loại trừ): không
            Msg(4, "assistant", From.AddSeconds(-1), "answered"),    // ngay trước khoảng: không
        }.AsQueryable();

        var rows = AiQualityQuery.Turns(messages, Array.Empty<AiCall>().AsQueryable(), From, ToExclusive).ToList();

        Assert.Equal(["no_answer", "answered"], rows.Select(r => r.Outcome));
    }

    [Fact]
    public void Turns_TranslateToSql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);

        var sql = AiQualityQuery.Turns(db.ChatMessages, db.AiCalls, From, ToExclusive).ToQueryString();

        Assert.Contains("chat_messages", sql);
        Assert.Contains("fallback_used", sql);
        Assert.Contains("LEFT JOIN", sql);
    }

    // ---- dịch vụ

    private sealed class FakeRepository(params AiQualityTurnRow[] rows) : IAiQualityRepository
    {
        public (DateTime From, DateTime ToExclusive)? Asked { get; private set; }

        public Task<IReadOnlyList<AiQualityTurnRow>> TurnsAsync(DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default)
        {
            Asked = (fromUtc, toUtcExclusive);
            return Task.FromResult<IReadOnlyList<AiQualityTurnRow>>(rows);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly TimeProvider Now = new FixedClock(new DateTimeOffset(2026, 11, 20, 15, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Service_DefaultsToTheLast30DaysEndingToday_AndAsksTheRepositoryForAHalfOpenRange()
    {
        var repo = new FakeRepository();

        var report = await new AiQualityService(repo, Now).GetReportAsync(null, null);

        Assert.Equal((new DateOnly(2026, 10, 22), new DateOnly(2026, 11, 20)), (report.From, report.To));
        Assert.Equal(30, report.ByDay.Count);
        Assert.Equal((new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc)), repo.Asked);
    }

    [Fact]
    public async Task Service_BuildsTheReportFromTheRowsAndTheirMeta()
    {
        var repo = new FakeRepository(
            new AiQualityTurnRow(At(2), "answered", "{\"turnMs\":2000,\"skills\":[\"knowledge\"],\"failedSteps\":[]}", "{\"slm\":{\"fallback\":false}}", false),
            new AiQualityTurnRow(At(3), "failed", "{\"turnMs\":9000,\"skills\":[\"knowledge\"],\"failedSteps\":[\"slm_intents\"]}", "{\"slm\":{\"fallback\":true}}", true),
            new AiQualityTurnRow(At(3, 15), "refused", null, null, false));

        var report = await new AiQualityService(repo, Now).GetReportAsync(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 4));

        Assert.Equal((3, 1, 1, 1), (report.Total.Total, report.Total.Answered, report.Total.Refused, report.Total.Failed));
        Assert.Equal((2, 1, 1), (report.Latency.Measured, report.SlmFallbackTurns, report.FallbackTurns));
        Assert.Equal(("slm_intents", 1), (report.FailedSteps[0].Step, report.FailedSteps[0].Turns));
        Assert.Equal(["knowledge", "other"], report.ByFeature.Select(f => f.Feature));
        Assert.Equal([1, 2, 0], report.ByDay.Select(d => d.Counts.Total));
    }

    [Fact]
    public async Task Service_RejectsABackwardsOrTooLongRange()
    {
        var service = new AiQualityService(new FakeRepository(), Now);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetReportAsync(new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 1)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetReportAsync(new DateOnly(2025, 1, 1), new DateOnly(2026, 11, 1)));
        // 366 ngày là vừa đủ
        await service.GetReportAsync(new DateOnly(2025, 11, 1), new DateOnly(2026, 11, 1));
    }

    // ---- controller

    private sealed class StubService(AiQualityReport? report = null, Exception? fail = null) : IAiQualityService
    {
        public Task<AiQualityReport> GetReportAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default) =>
            fail is not null ? throw fail : Task.FromResult(report!);
    }

    [Fact]
    public void Controller_IsAdminOnly()
    {
        var authorize = typeof(AdminAiQualityController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(UserRoles.Admin, authorize!.Roles);   // đúng một vai trò: Moderator không xem
        Assert.Equal("api/v1/admin/ai-quality", typeof(AdminAiQualityController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    [Fact]
    public async Task Controller_ReturnsTheReport_AndBadRequestForABadRange()
    {
        var empty = AiQualityCalculator.Build([], new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1));

        var ok = await new AdminAiQualityController(new StubService(empty), new StubSamples()).Get(null, null, default);
        var bad = await new AdminAiQualityController(new StubService(fail: new ArgumentException("sai khoảng")), new StubSamples()).Get(null, null, default);

        Assert.Same(empty, Assert.IsType<OkObjectResult>(ok.Result).Value);
        Assert.IsType<BadRequestObjectResult>(bad.Result);
    }

    // ---- API mẫu hội thoại (6c)

    private sealed class StubSamples(Exception? fail = null) : IAiQualitySampleService
    {
        public (Guid Actor, string Outcome, string? Ip, string? Agent)? Called { get; private set; }

        public Task<AiQualitySamplePage> GetPageAsync(
            Guid actorUserId, DateOnly? from, DateOnly? to, string outcome, int page, int pageSize,
            string? ipAddress, string? userAgent, CancellationToken ct = default)
        {
            Called = (actorUserId, outcome, ipAddress, userAgent);
            return fail is not null ? throw fail : Task.FromResult(new AiQualitySamplePage([], page, pageSize, 0, 0));
        }
    }

    private static AdminAiQualityController ControllerFor(StubSamples samples, Guid? userId)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.7");
        http.Request.Headers.UserAgent = "unit-test";
        if (userId is { } id)
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"));
        return new AdminAiQualityController(new StubService(), samples) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    [Fact]
    public async Task Samples_PassTheAdminTheIpAndTheAgentOn_AndTheResponseIsNeverCached()
    {
        var samples = new StubSamples();
        var controller = ControllerFor(samples, Id(7));

        var result = await controller.GetSamples("no_answer", null, null, 1, 10, default);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal((Id(7), "no_answer", "10.0.0.7", "unit-test"), samples.Called);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Samples_WithoutALoggedInUser_AreUnauthorized_AndNothingIsCalled()
    {
        var samples = new StubSamples();

        var result = await ControllerFor(samples, null).GetSamples("no_answer", null, null, 1, 10, default);

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Null(samples.Called);
    }

    [Fact]
    public async Task Samples_ABadOutcomeOrRange_IsABadRequest_AndAMissingOutcomeIsTreatedAsBad()
    {
        var bad = await ControllerFor(new StubSamples(new ArgumentException("sai loại")), Id(7)).GetSamples("answered", null, null, 1, 10, default);
        var missing = new StubSamples();
        await ControllerFor(missing, Id(7)).GetSamples(null, null, null, 1, 10, default);

        Assert.IsType<BadRequestObjectResult>(bad.Result);
        Assert.Equal(string.Empty, missing.Called!.Value.Outcome);   // rỗng: dịch vụ sẽ từ chối
    }

    [Fact]
    public void SamplesEndpoint_IsOnTheAdminOnlyController_AtTheSamplesPath()
    {
        var method = typeof(AdminAiQualityController).GetMethod(nameof(AdminAiQualityController.GetSamples))!;

        Assert.Equal("samples", method.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal(UserRoles.Admin, typeof(AdminAiQualityController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    // ---- truy vấn thật của mẫu hội thoại dịch ra SQL được, và bước một không đọc nội dung

    private static AppDbContext SchemaOnlyDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void CandidateQuery_TranslatesToSql_AndNeverReadsAnyChatTextColumn()
    {
        using var db = SchemaOnlyDb();

        var sql = AiQualitySampleRepository.CandidateRows(db, From, ToExclusive, "no_answer").ToQueryString();

        Assert.Contains("date_of_birth", sql);
        Assert.Contains("LEFT JOIN", sql);
        Assert.Contains("ORDER BY", sql);
        // Bước một chỉ quyết định ai được xem: không cột nội dung hay meta nào của tin nhắn được chọn
        Assert.DoesNotContain("content", sql);
        Assert.DoesNotContain("meta_json", sql);
    }

    [Fact]
    public void ReplyQuery_TranslatesToSql_AndReadsTheContentOnlyOfTheAskedMessages()
    {
        using var db = SchemaOnlyDb();

        var sql = AiQualitySampleRepository.ReplyRows(db, [Id(1), Id(2)]).ToQueryString();

        Assert.Contains("content", sql);
        Assert.Contains("full_name", sql);
        Assert.Contains("reply_to_message_id", sql);
        Assert.Contains("ANY", sql);   // Npgsql dịch danh sách mã thành = ANY(...): chỉ các mã được hỏi, không đọc cả bảng
    }
}
