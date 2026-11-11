using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.ChatReview;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;

namespace StudyAbroad.UnitTests;

// Hàng đợi câu cần xem (no_answer, failed): đọc mã cố định từ meta_json mà KHÔNG lộ nội dung chat, truy vấn, dịch vụ, controller (Admin).
public class ChatReviewTests
{
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");
    private static DateTime Day(int day, int hour = 12) => new(2026, 11, day, hour, 0, 0, DateTimeKind.Utc);

    // Meta của tin học sinh, có đoạn chữ và thực thể thật của học sinh: bộ đọc không được để chúng lọt ra
    private const string SecretSpan = "Marine Biology ở Hawaii học bổng toàn phần";
    private static readonly string QuestionMeta =
        "{\"v\":1,\"slm\":{\"modelIntents\":[{\"type\":\"knowledge\",\"span\":\"" + SecretSpan + "\",\"entities\":{\"gpa\":\"em GPA 3.6\"}}]," +
        "\"intents\":[\"knowledge\"],\"outOfScope\":null,\"confidence\":0.41,\"fallback\":false}}";
    private const string ReplyMeta = "{\"v\":1,\"turnMs\":24500,\"failedSteps\":[\"skill_tra_loi\"],\"local\":{\"calls\":1}}";

    // ---- bộ đọc meta

    [Fact]
    public void ReadModel_GivesTheFixedCodes_AndNeverTheStudentsWords()
    {
        var model = ChatReviewMetaReader.ReadModel(QuestionMeta)!;

        Assert.Equal((0.41, false, "knowledge"), (model.Confidence, model.Fallback, Assert.Single(model.ModelIntentTypes)));
        Assert.Equal(["knowledge"], model.IntentTypes);
        Assert.Null(model.OutOfScope);
        var serialized = JsonSerializer.Serialize(model);
        Assert.DoesNotContain("Hawaii", serialized);
        Assert.DoesNotContain("GPA 3.6", serialized);
        Assert.DoesNotContain("span", serialized);
        Assert.DoesNotContain("entities", serialized);
    }

    [Fact]
    public void ReadModel_KeepsTheFallbackFlagAndTheOutOfScopeKind()
    {
        const string meta = "{\"slm\":{\"modelIntents\":[],\"intents\":[\"out_of_scope\"],\"outOfScope\":\"ambiguous\",\"confidence\":0,\"fallback\":true}}";

        var model = ChatReviewMetaReader.ReadModel(meta)!;

        Assert.Equal((0d, true, "ambiguous"), (model.Confidence, model.Fallback, model.OutOfScope));
        Assert.Empty(model.ModelIntentTypes);   // SLM hỏng: không có đầu ra thật của model
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("không phải JSON")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"v\":1}")]
    [InlineData("{\"slm\":\"chuỗi thay vì đối tượng\"}")]
    public void ReadModel_WithMissingOrBrokenMeta_IsNull_NeverThrows(string? meta) =>
        Assert.Null(ChatReviewMetaReader.ReadModel(meta));

    [Fact]
    public void ReadModel_ToleratesWrongTypesInsideTheBlock()
    {
        const string meta = "{\"slm\":{\"confidence\":\"cao\",\"fallback\":\"có\",\"intents\":\"x\",\"modelIntents\":[1,{\"span\":\"không có type\"},{\"type\":\"scenario\"}]}}";

        var model = ChatReviewMetaReader.ReadModel(meta)!;

        Assert.Null(model.Confidence);
        Assert.False(model.Fallback);
        Assert.Empty(model.IntentTypes);
        Assert.Equal(["scenario"], model.ModelIntentTypes);   // chỉ lấy ý hợp lệ
    }

    [Fact]
    public void ReadReply_GivesFailedStepsTurnTimeAndTheErrorKind()
    {
        var (steps, turnMs, error) = ChatReviewMetaReader.ReadReply(ReplyMeta);
        Assert.Equal(["skill_tra_loi"], steps);
        Assert.Equal((24500L, (string?)null), (turnMs, error));

        var advisorDown = ChatReviewMetaReader.ReadReply("{\"v\":1,\"turnMs\":900,\"error\":\"HttpRequestException\",\"failedSteps\":[\"advisor\"]}");
        Assert.Equal(["advisor"], advisorDown.FailedSteps);
        Assert.Equal((900L, "HttpRequestException"), (advisorDown.TurnMs, advisorDown.Error));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("hỏng")]
    [InlineData("{}")]
    public void ReadReply_WithMissingOrBrokenMeta_IsEmpty_NeverThrows(string? meta)
    {
        var (steps, turnMs, error) = ChatReviewMetaReader.ReadReply(meta);

        Assert.Empty(steps);
        Assert.Null(turnMs);
        Assert.Null(error);
    }

    // ---- truy vấn gộp

    private static ChatSession Session(int n, Guid user) => new() { Id = Id(100 + n), UserId = user };

    private static ChatMessage Msg(int n, int session, string role, DateTime at, string? outcome = "no_answer", string? review = "open",
        int? replyTo = null, string? meta = null) =>
        new()
        {
            Id = Id(200 + n), ChatSessionId = Id(100 + session), Role = role, CreatedAt = at, Outcome = outcome, ReviewStatus = review,
            ReplyToMessageId = replyTo is { } r ? Id(200 + r) : null, MetaJson = meta, Content = "nội dung riêng tư",
        };

    [Fact]
    public void Queue_ListsOnlyMessagesThatNeedReview_NotTheStudentsNorTheAnsweredOnes()
    {
        var sessions = new[] { Session(1, Id(1)) }.AsQueryable();
        var messages = new[]
        {
            Msg(1, 1, "user", Day(2), outcome: null, review: null),                    // tin của học sinh
            Msg(2, 1, "assistant", Day(2), outcome: "answered", review: null),          // trả lời được: không vào hàng đợi
            Msg(3, 1, "assistant", Day(3), outcome: "no_answer"),
            Msg(4, 1, "system", Day(4), outcome: "failed"),
        }.AsQueryable();

        var rows = ChatReviewAggregator.QueuePage(messages, sessions, null, null, null, null, 0, 10).ToList();

        Assert.Equal([Id(204), Id(203)], rows.Select(r => r.MessageId));   // mới nhất trước
        Assert.Equal(2, ChatReviewAggregator.QueueIds(messages, null, null, null, null).Count());
    }

    [Fact]
    public void Queue_FiltersByStatusAndOutcome()
    {
        var sessions = new[] { Session(1, Id(1)) }.AsQueryable();
        var messages = new[]
        {
            Msg(1, 1, "assistant", Day(2), outcome: "no_answer", review: "open"),
            Msg(2, 1, "assistant", Day(2), outcome: "failed", review: "open"),
            Msg(3, 1, "assistant", Day(2), outcome: "no_answer", review: "resolved"),
        }.AsQueryable();

        IEnumerable<Guid> Ids(string? status, string? outcome) =>
            ChatReviewAggregator.QueuePage(messages, sessions, status, outcome, null, null, 0, 10).ToList().Select(r => r.MessageId).OrderBy(i => i);

        Assert.Equal([Id(201), Id(202)], Ids("open", null));
        Assert.Equal([Id(202)], Ids("open", "failed"));
        Assert.Equal([Id(203)], Ids("resolved", null));
        Assert.Equal([Id(201), Id(202), Id(203)], Ids(null, null));
    }

    [Fact]
    public void Queue_CarriesTheOwnerFromTheSession_AndTheQuestionMetaThroughReplyTo()
    {
        var sessions = new[] { Session(1, Id(7)) }.AsQueryable();
        var messages = new[]
        {
            Msg(1, 1, "user", Day(2), outcome: null, review: null, meta: QuestionMeta),
            Msg(2, 1, "assistant", Day(2), replyTo: 1, meta: ReplyMeta),
        }.AsQueryable();

        var row = Assert.Single(ChatReviewAggregator.QueuePage(messages, sessions, "open", null, null, null, 0, 10).ToList());

        Assert.Equal((Id(7), Id(201), Id(101)), (row.UserId, row.QuestionMessageId, row.SessionId));
        Assert.Equal(QuestionMeta, row.QuestionMetaJson);   // meta của CÂU HỎI (nơi có nhãn model gán), không phải của tin trả lời
        Assert.Equal(ReplyMeta, row.ReplyMetaJson);
    }

    [Fact]
    public void Queue_ARowWithoutAQuestion_StillAppears_WithNoQuestionMeta()
    {
        var sessions = new[] { Session(1, Id(1)) }.AsQueryable();
        var messages = new[] { Msg(1, 1, "system", Day(2), outcome: "failed") }.AsQueryable();

        var row = Assert.Single(ChatReviewAggregator.QueuePage(messages, sessions, null, null, null, null, 0, 10).ToList());

        Assert.Null(row.QuestionMessageId);
        Assert.Null(row.QuestionMetaJson);
    }

    [Fact]
    public void Queue_RespectsAnOpenEndedWindow_AndPagesWithoutOverlap()
    {
        var sessions = new[] { Session(1, Id(1)) }.AsQueryable();
        var messages = new[]
        {
            Msg(1, 1, "assistant", Day(1)),
            Msg(2, 1, "assistant", Day(2)),
            Msg(3, 1, "assistant", Day(3)),
            Msg(4, 1, "assistant", Day(4)),
        }.AsQueryable();

        var from = Day(2, 0);
        var toExclusive = Day(4, 0);   // hết ngày 3

        Assert.Equal([Id(203), Id(202)], ChatReviewAggregator.QueuePage(messages, sessions, null, null, from, toExclusive, 0, 10).ToList().Select(r => r.MessageId));
        Assert.Equal([Id(204), Id(203), Id(202), Id(201)], ChatReviewAggregator.QueuePage(messages, sessions, null, null, null, null, 0, 10).ToList().Select(r => r.MessageId));
        Assert.Equal([Id(204), Id(203)], ChatReviewAggregator.QueuePage(messages, sessions, null, null, null, null, 0, 2).ToList().Select(r => r.MessageId));
        Assert.Equal([Id(202), Id(201)], ChatReviewAggregator.QueuePage(messages, sessions, null, null, null, null, 2, 2).ToList().Select(r => r.MessageId));
    }

    [Fact]
    public void Counts_GroupByStatusAndOutcome_IgnoringStudentsAndAnsweredMessages()
    {
        var messages = new[]
        {
            Msg(1, 1, "assistant", Day(2), outcome: "no_answer", review: "open"),
            Msg(2, 1, "assistant", Day(2), outcome: "no_answer", review: "open"),
            Msg(3, 1, "system", Day(2), outcome: "failed", review: "open"),
            Msg(4, 1, "assistant", Day(2), outcome: "no_answer", review: "resolved"),
            Msg(5, 1, "assistant", Day(2), outcome: "answered", review: null),
            Msg(6, 1, "user", Day(2), outcome: null, review: null),
        }.AsQueryable();

        var counts = ChatReviewAggregator.Counts(messages, null, null).ToList().OrderBy(c => c.ReviewStatus).ThenBy(c => c.Outcome).ToList();

        Assert.Equal(
            [("open", "failed", 1), ("open", "no_answer", 2), ("resolved", "no_answer", 1)],
            counts.Select(c => (c.ReviewStatus, c.Outcome, c.Count)));
    }

    [Fact]
    public void TheQueueQueries_TranslateToSql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);
        var from = Day(1, 0);
        var to = Day(8, 0);

        var page = ChatReviewAggregator.QueuePage(db.ChatMessages, db.ChatSessions, "open", "no_answer", from, to, 20, 10).ToQueryString();
        var all = ChatReviewAggregator.QueuePage(db.ChatMessages, db.ChatSessions, null, null, null, null, 0, 10).ToQueryString();
        var ids = ChatReviewAggregator.QueueIds(db.ChatMessages, "open", null, from, to).ToQueryString();
        var counts = ChatReviewAggregator.Counts(db.ChatMessages, from, to).ToQueryString();

        Assert.Contains("LEFT JOIN", page);     // tin hỏi nối trái: dòng lỗi không có tin hỏi vẫn hiện
        Assert.Contains("ORDER BY", page);
        Assert.Contains("LIMIT", page);         // phân trang ở database
        Assert.Contains("review_status", page);
        Assert.Contains("LIMIT", all);
        Assert.Contains("review_status", ids);
        Assert.DoesNotContain("JOIN", ids);     // đếm thẳng trên chat_messages, không cần nối phiên nên nhanh hơn
        Assert.Contains("GROUP BY", counts);
    }

    // ---- dịch vụ

    private sealed class FakeRepo : IChatReviewRepository
    {
        public List<ChatReviewRow> Rows { get; init; } = [];
        public int Total { get; init; }
        public List<ChatReviewCountRow> CountRows { get; init; } = [];
        public (string? Status, string? Outcome, DateTime? From, DateTime? To, int Skip, int Take)? Asked { get; private set; }
        public (DateTime? From, DateTime? To)? AskedCounts { get; private set; }

        public Task<(IReadOnlyList<ChatReviewRow> Rows, int Total)> QueuePageAsync(
            string? status, string? outcome, DateTime? fromUtc, DateTime? toUtcExclusive, int skip, int take, CancellationToken ct = default)
        {
            Asked = (status, outcome, fromUtc, toUtcExclusive, skip, take);
            return Task.FromResult<(IReadOnlyList<ChatReviewRow>, int)>((Rows, Total));
        }

        public Task<IReadOnlyList<ChatReviewCountRow>> CountsAsync(DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken ct = default)
        {
            AskedCounts = (fromUtc, toUtcExclusive);
            return Task.FromResult<IReadOnlyList<ChatReviewCountRow>>(CountRows);
        }
    }

    private static ChatReviewRow Row(int n, string outcome = "no_answer", string status = "open", string? question = null, string? reply = null) =>
        new(Id(200 + n), Id(101), Id(7), Day(2), "assistant", outcome, status, Id(250 + n), reply, question);

    [Fact]
    public async Task Queue_DefaultsToOpen_WithNoDateLimit_AndAllOutcomes()
    {
        var repo = new FakeRepo();

        await new ChatReviewService(repo).GetQueueAsync(null, null, null, null, 1, 20);

        Assert.Equal(("open", null, null, null, 0, 20), repo.Asked);
    }

    [Fact]
    public async Task Queue_PassesTheFiltersAndTurnsDatesIntoUtcBounds()
    {
        var repo = new FakeRepo();

        await new ChatReviewService(repo).GetQueueAsync("resolved", "failed", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7), 1, 20);

        Assert.Equal("resolved", repo.Asked!.Value.Status);
        Assert.Equal("failed", repo.Asked.Value.Outcome);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), repo.Asked.Value.From);
        Assert.Equal(new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), repo.Asked.Value.To);   // hết ngày 7 = đầu ngày 8
    }

    [Fact]
    public async Task Queue_OnlyOneEndOfTheRangeGiven_IsOpenEndedOnTheOtherSide()
    {
        var repo = new FakeRepo();

        await new ChatReviewService(repo).GetQueueAsync(null, null, new DateOnly(2026, 11, 1), null, 1, 20);

        Assert.NotNull(repo.Asked!.Value.From);
        Assert.Null(repo.Asked.Value.To);
    }

    [Theory]
    [InlineData("lạ", null)]                    // trạng thái không có
    [InlineData("answered", null)]              // answered không phải trạng thái xử lý
    [InlineData(null, "answered")]              // câu trả lời được không vào hàng đợi
    [InlineData(null, "lạ")]
    public async Task Queue_RejectsUnknownStatusOrOutcome(string? status, string? outcome)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new ChatReviewService(new FakeRepo()).GetQueueAsync(status, outcome, null, null, 1, 20));
    }

    [Fact]
    public async Task Queue_RejectsAReversedDateRange()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ChatReviewService(new FakeRepo()).GetQueueAsync(null, null, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 1), 1, 20));
    }

    [Theory]
    [InlineData(1, 20, 0, 20)]
    [InlineData(3, 10, 20, 10)]
    [InlineData(0, 10, 0, 10)]
    [InlineData(2, 0, 1, 1)]
    [InlineData(2, 500, 100, 100)]
    public async Task Queue_PagingIsClamped(int page, int pageSize, int skip, int take)
    {
        var repo = new FakeRepo();

        await new ChatReviewService(repo).GetQueueAsync(null, null, null, null, page, pageSize);

        Assert.Equal((skip, take), (repo.Asked!.Value.Skip, repo.Asked.Value.Take));
    }

    [Fact]
    public async Task Queue_ItemsCarryTheModelCodesAndTheFailureInfo_ButNoContent()
    {
        var repo = new FakeRepo
        {
            Rows = [Row(1, question: QuestionMeta, reply: ReplyMeta)],
            Total = 41,
        };

        var page = await new ChatReviewService(repo).GetQueueAsync(null, null, null, null, 2, 20);

        var item = Assert.Single(page.Items);
        Assert.Equal((Id(201), Id(7), Id(251), "no_answer", "open"), (item.MessageId, item.UserId, item.QuestionMessageId, item.Outcome, item.ReviewStatus));
        Assert.Equal(0.41, item.Model!.Confidence);
        Assert.Equal(["skill_tra_loi"], item.FailedSteps);
        Assert.Equal(24500L, item.TurnMs);
        Assert.Equal((2, 20, 41, 3), (page.Page, page.PageSize, page.TotalCount, page.TotalPages));
        Assert.DoesNotContain("Hawaii", JsonSerializer.Serialize(page));
        Assert.DoesNotContain("nội dung riêng tư", JsonSerializer.Serialize(page));
    }

    [Fact]
    public async Task Queue_ARowWithBrokenMeta_IsStillListed()
    {
        var repo = new FakeRepo { Rows = [Row(1, question: "hỏng", reply: "cũng hỏng")], Total = 1 };

        var item = Assert.Single((await new ChatReviewService(repo).GetQueueAsync(null, null, null, null, 1, 20)).Items);

        Assert.Null(item.Model);
        Assert.Empty(item.FailedSteps);
        Assert.Null(item.TurnMs);
    }

    [Fact]
    public async Task Summary_CountsByStatus_AndOpenByOutcome()
    {
        var repo = new FakeRepo
        {
            CountRows =
            [
                new("open", "no_answer", 7), new("open", "failed", 2), new("resolved", "no_answer", 5), new("resolved", "failed", 1), new("ignored", "no_answer", 3),
            ],
        };

        var summary = await new ChatReviewService(repo).GetSummaryAsync(null, null);

        Assert.Equal((9, 6, 3, 7, 2), (summary.Open, summary.Resolved, summary.Ignored, summary.OpenNoAnswer, summary.OpenFailed));
        Assert.Equal((null, null), (repo.AskedCounts!.Value.From, repo.AskedCounts.Value.To));
    }

    [Fact]
    public async Task Summary_WithNoData_IsAllZeros_AndABadRangeIsRejected()
    {
        var summary = await new ChatReviewService(new FakeRepo()).GetSummaryAsync(null, null);

        Assert.Equal((0, 0, 0, 0, 0), (summary.Open, summary.Resolved, summary.Ignored, summary.OpenNoAnswer, summary.OpenFailed));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ChatReviewService(new FakeRepo()).GetSummaryAsync(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 1)));
    }

    // ---- controller

    private sealed class FakeService(PagedResult<ChatReviewItemDto>? queue = null, ChatReviewSummaryDto? summary = null, Exception? error = null) : IChatReviewService
    {
        public (string? Status, string? Outcome, int Page, int PageSize)? Asked { get; private set; }

        public Task<PagedResult<ChatReviewItemDto>> GetQueueAsync(
            string? status, string? outcome, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default)
        {
            Asked = (status, outcome, page, pageSize);
            if (error is not null)
                throw error;
            return Task.FromResult(queue!);
        }

        public Task<ChatReviewSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
        {
            if (error is not null)
                throw error;
            return Task.FromResult(summary!);
        }
    }

    private static readonly PagedResult<ChatReviewItemDto> SampleQueue = new([], 1, 20, 0);
    private static readonly ChatReviewSummaryDto SampleSummary = new(null, null, 1, 2, 3, 1, 0);

    [Fact]
    public async Task Controller_Queue_ReturnsThePage_AndPassesTheArguments()
    {
        var service = new FakeService(queue: SampleQueue);

        var result = await new AdminChatReviewController(service).GetQueue("resolved", "failed", null, null, 2, 50, default);

        Assert.Same(SampleQueue, Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(("resolved", "failed", 2, 50), service.Asked);
    }

    [Fact]
    public async Task Controller_BadArguments_Are400WithTheReason()
    {
        var controller = new AdminChatReviewController(new FakeService(error: new ArgumentException("Trạng thái sai")));

        var queue = await controller.GetQueue("lạ", null, null, null, 1, 20, default);
        var summary = await controller.GetSummary(null, null, default);

        Assert.Contains("Trạng thái sai", Assert.IsType<BadRequestObjectResult>(queue.Result).Value!.ToString());
        Assert.IsType<BadRequestObjectResult>(summary.Result);
    }

    [Fact]
    public async Task Controller_Summary_ReturnsTheCounts()
    {
        var result = await new AdminChatReviewController(new FakeService(summary: SampleSummary)).GetSummary(null, null, default);

        Assert.Same(SampleSummary, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public void Controller_IsForAdminsOnly_WithTheExpectedRoutes()
    {
        var type = typeof(AdminChatReviewController);

        Assert.Equal(UserRoles.Admin, type.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("api/v1/admin/chat-review", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("queue", type.GetMethod(nameof(AdminChatReviewController.GetQueue))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("summary", type.GetMethod(nameof(AdminChatReviewController.GetSummary))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    [Fact]
    public void TheListDtos_CarryNoChatContent()
    {
        // Danh sách chỉ có mã và số đo: nếu ai thêm thuộc tính nội dung vào đây thì test báo lỗi
        var names = new[] { typeof(ChatReviewItemDto), typeof(ChatReviewModelInfo), typeof(ChatReviewSummaryDto) }
            .SelectMany(t => t.GetProperties().Select(p => p.Name));

        Assert.DoesNotContain(names, n => n is "Content" or "Text" or "Span" or "Entities" or "Answer" or "QuestionText");
    }
}
