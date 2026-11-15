using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Api.Controllers;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using StudyAbroad.Application.Admin.ChatReview;
using StudyAbroad.Application.Audit;
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

    // ---- bộ đọc nhãn đầy đủ (chỉ dùng cho chi tiết)

    [Fact]
    public void ReadModelDetail_KeepsTheStudentsWordsAndEntities_ForTheDetailView()
    {
        var model = ChatReviewMetaReader.ReadModelDetail(QuestionMeta)!;

        Assert.Equal((0.41, false), (model.Confidence, model.Fallback));
        var intent = Assert.Single(model.ModelIntents);
        Assert.Equal(("knowledge", SecretSpan), (intent.Type, intent.Span));
        Assert.Equal("em GPA 3.6", intent.Entities!.Value.GetProperty("gpa").GetString());
        Assert.Equal(["knowledge"], model.IntentTypes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("hỏng")]
    [InlineData("{\"v\":1}")]
    public void ReadModelDetail_WithMissingOrBrokenMeta_IsNull(string? meta) => Assert.Null(ChatReviewMetaReader.ReadModelDetail(meta));

    [Fact]
    public void ReadModelDetail_SkipsMalformedIntents_AndToleratesMissingParts()
    {
        const string meta = "{\"slm\":{\"modelIntents\":[1,{\"span\":\"thiếu type\"},{\"type\":\"scenario\"}],\"fallback\":true}}";

        var model = ChatReviewMetaReader.ReadModelDetail(meta)!;

        var intent = Assert.Single(model.ModelIntents);
        Assert.Equal(("scenario", ""), (intent.Type, intent.Span));   // thiếu span thì để trống
        Assert.Null(intent.Entities);
        Assert.True(model.Fallback);
    }

    // ---- truy vấn chi tiết

    [Fact]
    public void ReplyDetail_OnlyReturnsMessagesOfTheQueue_SoItCannotReadArbitraryChats()
    {
        var sessions = new[] { Session(1, Id(7)) }.AsQueryable();
        var messages = new[]
        {
            Msg(1, 1, "user", Day(2), outcome: null, review: null),                       // tin của học sinh
            Msg(2, 1, "assistant", Day(2), outcome: "answered", review: null),             // đã trả lời được
            Msg(3, 1, "assistant", Day(2), outcome: "no_answer", review: "open"),          // thuộc hàng đợi
            Msg(4, 1, "system", Day(2), outcome: "failed", review: "open"),
        }.AsQueryable();

        Assert.Empty(ChatReviewAggregator.ReplyDetail(messages, sessions, Id(201)).ToList());
        Assert.Empty(ChatReviewAggregator.ReplyDetail(messages, sessions, Id(202)).ToList());
        Assert.Single(ChatReviewAggregator.ReplyDetail(messages, sessions, Id(203)).ToList());
        Assert.Single(ChatReviewAggregator.ReplyDetail(messages, sessions, Id(204)).ToList());
        Assert.Empty(ChatReviewAggregator.ReplyDetail(messages, sessions, Id(999)).ToList());
    }

    [Fact]
    public void ReplyDetail_CarriesTheContentOwnerAndTheQuestionLink()
    {
        var sessions = new[] { Session(1, Id(7)) }.AsQueryable();
        var messages = new[] { Msg(2, 1, "assistant", Day(2), replyTo: 1, meta: ReplyMeta) }.AsQueryable();

        var row = Assert.Single(ChatReviewAggregator.ReplyDetail(messages, sessions, Id(202)).ToList());

        Assert.Equal((Id(7), Id(101), Id(201), "nội dung riêng tư"), (row.UserId, row.SessionId, row.QuestionMessageId, row.Content));
        Assert.Equal(ReplyMeta, row.MetaJson);
    }

    [Fact]
    public void QuestionDetail_FindsTheQuestionWithItsMeta()
    {
        var messages = new[] { Msg(1, 1, "user", Day(2), outcome: null, review: null, meta: QuestionMeta) }.AsQueryable();

        var row = Assert.Single(ChatReviewAggregator.QuestionDetail(messages, Id(201)).ToList());

        Assert.Equal((Id(201), QuestionMeta), (row.MessageId, row.MetaJson));
    }

    [Fact]
    public void ContextBefore_IsThePreviousUserAndAssistantMessagesOfTheSameSession_NearestFirst_Limited()
    {
        var messages = new[]
        {
            Msg(1, 1, "user", Day(2, 8), outcome: null, review: null),
            Msg(2, 1, "assistant", Day(2, 9), outcome: "answered", review: null),
            Msg(3, 1, "system", Day(2, 10), outcome: "failed"),                             // dòng lỗi: không tính là ngữ cảnh
            Msg(4, 1, "user", Day(2, 11), outcome: null, review: null),
            Msg(5, 2, "user", Day(2, 11), outcome: null, review: null),                     // phiên khác
            Msg(6, 1, "user", Day(2, 13), outcome: null, review: null),                     // sau câu hỏi
            Msg(7, 1, "user", Day(2, 12), outcome: null, review: null),                     // chính câu hỏi: loại ra
        }.AsQueryable();

        var all = ChatReviewAggregator.ContextBefore(messages, Id(101), Day(2, 12), Id(207), 10).ToList();
        var limited = ChatReviewAggregator.ContextBefore(messages, Id(101), Day(2, 12), Id(207), 2).ToList();

        Assert.Equal([Id(204), Id(202), Id(201)], all.Select(r => r.MessageId));
        Assert.Equal([Id(204), Id(202)], limited.Select(r => r.MessageId));
    }

    [Fact]
    public void TheDetailQueries_TranslateToSql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);

        var reply = ChatReviewAggregator.ReplyDetail(db.ChatMessages, db.ChatSessions, Id(203)).ToQueryString();
        var question = ChatReviewAggregator.QuestionDetail(db.ChatMessages, Id(201)).ToQueryString();
        var context = ChatReviewAggregator.ContextBefore(db.ChatMessages, Id(101), Day(2), Id(207), 6).ToQueryString();

        Assert.Contains("review_status", reply);   // chỉ tin thuộc hàng đợi
        Assert.Contains("JOIN", reply);
        Assert.Contains("content", question);
        Assert.Contains("LIMIT", context);
        Assert.Contains("ORDER BY", context);
    }

    // ---- bản giả của kho nhật ký và hàm dựng dịch vụ

    private sealed class FakeAudit : IAuditLogRepository
    {
        public List<AuditLog> Entries { get; } = [];
        public bool Fail { get; init; }

        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
        {
            if (Fail)
                throw new InvalidOperationException("Không ghi được nhật ký");
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private static ChatReviewService Svc(FakeRepo repo, FakeAudit? audit = null) => new(repo, audit ?? new FakeAudit());

    // ---- dịch vụ

    private sealed class FakeRepo : IChatReviewRepository
    {
        public List<ChatReviewRow> Rows { get; init; } = [];
        public int Total { get; init; }
        public List<ChatReviewCountRow> CountRows { get; init; } = [];
        public (string? Status, string? Outcome, DateTime? From, DateTime? To, int Skip, int Take)? Asked { get; private set; }
        public (DateTime? From, DateTime? To)? AskedCounts { get; private set; }
        public ChatReviewDetailRows? Detail { get; init; }
        public int DetailQueries { get; private set; }
        public (Guid MessageId, int ContextCount)? AskedDetail { get; private set; }

        // Trạng thái xử lý hiện có của các tin thuộc hàng đợi; mã không có ở đây là tin ngoài hàng đợi
        public Dictionary<Guid, string> Statuses { get; init; } = [];
        public AuditLog? StatusAudit { get; private set; }
        public int StatusCalls { get; private set; }

        public Task<string?> SetStatusAsync(Guid messageId, string status, Func<string, AuditLog> audit, CancellationToken ct = default)
        {
            StatusCalls++;
            if (!Statuses.TryGetValue(messageId, out var previous))
                return Task.FromResult<string?>(null);
            if (previous != status)   // giống kho thật: trùng thì không đổi, không ghi nhật ký
            {
                Statuses[messageId] = status;
                StatusAudit = audit(previous);
            }
            return Task.FromResult<string?>(previous);
        }

        public Task<ChatReviewDetailRows?> FindDetailAsync(Guid messageId, int contextCount, CancellationToken ct = default)
        {
            DetailQueries++;
            AskedDetail = (messageId, contextCount);
            return Task.FromResult(Detail is not null && Detail.Reply.MessageId == messageId ? Detail : null);
        }

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

        await Svc(repo).GetQueueAsync(null, null, null, null, 1, 20);

        Assert.Equal(("open", null, null, null, 0, 20), repo.Asked);
    }

    [Fact]
    public async Task Queue_PassesTheFiltersAndTurnsDatesIntoUtcBounds()
    {
        var repo = new FakeRepo();

        await Svc(repo).GetQueueAsync("resolved", "failed", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7), 1, 20);

        Assert.Equal("resolved", repo.Asked!.Value.Status);
        Assert.Equal("failed", repo.Asked.Value.Outcome);
        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), repo.Asked.Value.From);
        Assert.Equal(new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), repo.Asked.Value.To);   // hết ngày 7 = đầu ngày 8
    }

    [Fact]
    public async Task Queue_OnlyOneEndOfTheRangeGiven_IsOpenEndedOnTheOtherSide()
    {
        var repo = new FakeRepo();

        await Svc(repo).GetQueueAsync(null, null, new DateOnly(2026, 11, 1), null, 1, 20);

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
        await Assert.ThrowsAsync<ArgumentException>(() => Svc(new FakeRepo()).GetQueueAsync(status, outcome, null, null, 1, 20));
    }

    [Fact]
    public async Task Queue_RejectsAReversedDateRange()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Svc(new FakeRepo()).GetQueueAsync(null, null, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 1), 1, 20));
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

        await Svc(repo).GetQueueAsync(null, null, null, null, page, pageSize);

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

        var page = await Svc(repo).GetQueueAsync(null, null, null, null, 2, 20);

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

        var item = Assert.Single((await Svc(repo).GetQueueAsync(null, null, null, null, 1, 20)).Items);

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

        var summary = await Svc(repo).GetSummaryAsync(null, null);

        Assert.Equal((9, 6, 3, 7, 2), (summary.Open, summary.Resolved, summary.Ignored, summary.OpenNoAnswer, summary.OpenFailed));
        Assert.Equal((null, null), (repo.AskedCounts!.Value.From, repo.AskedCounts.Value.To));
    }

    [Fact]
    public async Task Summary_WithNoData_IsAllZeros_AndABadRangeIsRejected()
    {
        var summary = await Svc(new FakeRepo()).GetSummaryAsync(null, null);

        Assert.Equal((0, 0, 0, 0, 0), (summary.Open, summary.Resolved, summary.Ignored, summary.OpenNoAnswer, summary.OpenFailed));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Svc(new FakeRepo()).GetSummaryAsync(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 1)));
    }

    // ---- dịch vụ: chi tiết và nhật ký người xem

    private static ChatReviewDetailRows DetailRows(string role = "assistant") => new(
        new ChatReviewReplyRow(Id(202), Id(101), Id(7), Day(2, 12), role, "no_answer", "open", "Mình chưa có số liệu ngành này.", ReplyMeta, Id(201)),
        new ChatReviewQuestionRow(Id(201), Day(2, 11), SecretSpan, QuestionMeta),
        [
            new ChatReviewContextRow(Id(204), "assistant", Day(2, 10), "Chào bạn"),
            new ChatReviewContextRow(Id(203), "user", Day(2, 9), "Chào"),
        ]);

    [Fact]
    public async Task Detail_BuildsTheFullPicture_WithContextInTimeOrder()
    {
        var repo = new FakeRepo { Detail = DetailRows() };

        var detail = (await Svc(repo).GetDetailAsync(Id(202), Id(1), "10.0.0.5", "Mozilla"))!;

        Assert.Equal((Id(202), Id(7), "no_answer", "open"), (detail.MessageId, detail.UserId, detail.Outcome, detail.ReviewStatus));
        Assert.Equal(SecretSpan, detail.Question!.Content);
        Assert.Equal(SecretSpan, detail.Question.Model!.ModelIntents[0].Span);      // nhãn model gán đầy đủ, có đoạn chữ
        Assert.Equal("Mình chưa có số liệu ngành này.", detail.Reply.Content);
        Assert.Equal(["Chào", "Chào bạn"], detail.Context.Select(c => c.Content));  // đảo lại theo thứ tự thời gian
        Assert.Equal(["skill_tra_loi"], detail.FailedSteps);
        Assert.Equal(24500L, detail.TurnMs);
        Assert.Equal((Id(202), ChatReviewService.ContextMessages), repo.AskedDetail);
    }

    [Fact]
    public async Task Detail_WritesTheAuditEntry_WithWhoWhereAndWhat_ButNoChatContent()
    {
        var audit = new FakeAudit();

        await Svc(new FakeRepo { Detail = DetailRows() }, audit).GetDetailAsync(Id(202), Id(1), "10.0.0.5", "Mozilla/5.0");

        var entry = Assert.Single(audit.Entries);
        Assert.Equal((Id(1), "chat_review.view", "chat_message", Id(202).ToString()),
            (entry.ActorUserId, entry.Action, entry.EntityType, entry.EntityId));
        Assert.Equal(("10.0.0.5", "Mozilla/5.0"), (entry.IpAddress, entry.UserAgent));
        var detailJson = entry.DetailJson!;
        Assert.Contains(Id(7).ToString(), detailJson);                  // ghi chủ cuộc chat
        Assert.Contains("no_answer", detailJson);
        Assert.DoesNotContain("Hawaii", detailJson);                    // KHÔNG nhân bản nội dung chat sang bảng nhật ký
        Assert.DoesNotContain("Mình chưa có số liệu", detailJson);
    }

    [Fact]
    public async Task Detail_IfTheAuditCannotBeWritten_TheContentIsNotReturned()
    {
        // Không có dấu vết thì không được đọc: lỗi ghi nhật ký phải ném lên, không âm thầm trả nội dung
        var service = Svc(new FakeRepo { Detail = DetailRows() }, new FakeAudit { Fail = true });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetDetailAsync(Id(202), Id(1), null, null));
    }

    [Fact]
    public async Task Detail_OfAMessageNotInTheQueue_IsNull_AndWritesNoAudit()
    {
        var audit = new FakeAudit();

        var detail = await Svc(new FakeRepo { Detail = DetailRows() }, audit).GetDetailAsync(Id(999), Id(1), null, null);

        Assert.Null(detail);
        Assert.Empty(audit.Entries);   // không có gì được xem nên không có gì để ghi
    }

    [Fact]
    public async Task Detail_WithoutAnActor_IsRejectedBeforeReadingAnything()
    {
        var repo = new FakeRepo { Detail = DetailRows() };
        var audit = new FakeAudit();

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo, audit).GetDetailAsync(Id(202), Guid.Empty, null, null));

        Assert.Equal(0, repo.DetailQueries);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task Detail_ClipsAVeryLongUserAgentAndAddress_SoTheAuditStillWrites()
    {
        var audit = new FakeAudit();

        await Svc(new FakeRepo { Detail = DetailRows() }, audit)
            .GetDetailAsync(Id(202), Id(1), new string('1', 200), new string('a', 2000));

        var entry = Assert.Single(audit.Entries);
        Assert.Equal((64, 512), (entry.IpAddress!.Length, entry.UserAgent!.Length));   // khớp độ rộng cột audit_logs
    }

    [Fact]
    public async Task Detail_OfAFailureRowWithoutAQuestion_HasNoQuestion_ButStillTheReply()
    {
        var rows = new ChatReviewDetailRows(
            new ChatReviewReplyRow(Id(202), Id(101), Id(7), Day(2), "system", "failed", "open", "Advisor không phản hồi (HttpRequestException).", null, null),
            null, []);

        var detail = (await Svc(new FakeRepo { Detail = rows }).GetDetailAsync(Id(202), Id(1), null, null))!;

        Assert.Null(detail.Question);
        Assert.Equal("system", detail.Reply.Role);
        Assert.Empty(detail.Context);
    }

    [Fact]
    public async Task Detail_ToleratesBrokenMeta()
    {
        var rows = new ChatReviewDetailRows(
            new ChatReviewReplyRow(Id(202), Id(101), Id(7), Day(2), "assistant", "no_answer", "open", "x", "hỏng", Id(201)),
            new ChatReviewQuestionRow(Id(201), Day(2), "câu hỏi", "cũng hỏng"), []);

        var detail = (await Svc(new FakeRepo { Detail = rows }).GetDetailAsync(Id(202), Id(1), null, null))!;

        Assert.Null(detail.Question!.Model);
        Assert.Empty(detail.FailedSteps);
    }

    // ---- controller: chi tiết

    private static ChatReviewDetailDto SampleDetail() => new(
        Id(202), Id(101), Id(7), Day(2), "assistant", "no_answer", "open", [], null, null, null,
        new ChatReviewMessageDto(Id(202), "assistant", Day(2), "trả lời"), []);

    private static AdminChatReviewController ControllerFor(FakeService service, Guid? actor, string? ip = "10.0.0.5", string? userAgent = "Mozilla")
    {
        var http = new DefaultHttpContext();
        if (actor is { } id)
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"));
        if (ip is not null)
            http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
        if (userAgent is not null)
            http.Request.Headers.UserAgent = userAgent;
        return new AdminChatReviewController(service) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    [Fact]
    public async Task Controller_Detail_PassesTheActorAddressAndBrowserToTheAudit()
    {
        var service = new FakeService(detail: SampleDetail());

        var result = await ControllerFor(service, Id(1)).GetDetail(Id(202), default);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal((Id(202), Id(1), "10.0.0.5", "Mozilla"), service.AskedDetail);
    }

    [Fact]
    public async Task Controller_Detail_WithoutALoggedInUser_Is401_AndNothingIsRead()
    {
        var service = new FakeService(detail: SampleDetail());

        var result = await ControllerFor(service, actor: null).GetDetail(Id(202), default);

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Null(service.AskedDetail);
    }

    [Fact]
    public async Task Controller_Detail_OfAMessageNotInTheQueue_Is404()
    {
        var result = await ControllerFor(new FakeService(detail: null), Id(1)).GetDetail(Id(999), default);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void Controller_DetailRoute_IsDeclared()
    {
        var method = typeof(AdminChatReviewController).GetMethod(nameof(AdminChatReviewController.GetDetail))!;

        Assert.Equal("items/{messageId:guid}", method.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    // ---- controller

    private sealed class FakeService(
        PagedResult<ChatReviewItemDto>? queue = null, ChatReviewSummaryDto? summary = null, Exception? error = null,
        ChatReviewDetailDto? detail = null, ChatReviewStatusDto? statusResult = null) : IChatReviewService
    {
        public (Guid MessageId, Guid Actor, string? Ip, string? UserAgent)? AskedDetail { get; private set; }
        public (Guid MessageId, string? Status, Guid Actor, string? Ip, string? UserAgent)? AskedStatus { get; private set; }

        public Task<ChatReviewStatusDto?> SetStatusAsync(
            Guid messageId, string? status, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default)
        {
            AskedStatus = (messageId, status, actorUserId, ipAddress, userAgent);
            if (error is not null)
                throw error;
            return Task.FromResult(statusResult);
        }

        public Task<ChatReviewDetailDto?> GetDetailAsync(Guid messageId, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default)
        {
            AskedDetail = (messageId, actorUserId, ipAddress, userAgent);
            return Task.FromResult(detail);
        }

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

    // ---- đổi trạng thái xử lý

    [Fact]
    public void QueueMessage_SelectsOnlyAReviewableAssistantOrSystemMessage()
    {
        var messages = new[]
        {
            Msg(1, 1, "assistant", Day(2)),                                              // trong hàng đợi
            Msg(2, 1, "system", Day(2), outcome: "failed"),                              // dòng lỗi: cũng trong hàng đợi
            Msg(3, 1, "assistant", Day(2), outcome: "answered", review: null),           // trả lời được: không có trạng thái
            Msg(4, 1, "user", Day(2), outcome: null, review: "open"),                    // của học sinh: không bao giờ
        }.AsQueryable();

        Assert.Single(ChatReviewAggregator.QueueMessage(messages, Id(201)));
        Assert.Single(ChatReviewAggregator.QueueMessage(messages, Id(202)));
        Assert.Empty(ChatReviewAggregator.QueueMessage(messages, Id(203)));
        Assert.Empty(ChatReviewAggregator.QueueMessage(messages, Id(204)));
        Assert.Empty(ChatReviewAggregator.QueueMessage(messages, Id(999)));
    }

    [Theory]
    [InlineData("resolved")]
    [InlineData("ignored")]
    public async Task SetStatus_ChangesTheStatus_AndAuditsFromAndTo_WithoutAnyChatContent(string target)
    {
        var repo = new FakeRepo { Statuses = { [Id(201)] = "open" } };

        var result = await Svc(repo).SetStatusAsync(Id(201), target, Id(1), "10.0.0.5", "Mozilla");

        Assert.Equal(new ChatReviewStatusDto(Id(201), target, "open"), result);
        Assert.Equal(target, repo.Statuses[Id(201)]);
        var entry = repo.StatusAudit!;
        Assert.Equal((Id(1), ChatReviewService.StatusAction, "chat_message", Id(201).ToString()), (entry.ActorUserId, entry.Action, entry.EntityType, entry.EntityId));
        Assert.Equal(("10.0.0.5", "Mozilla"), (entry.IpAddress, entry.UserAgent));
        using var detail = JsonDocument.Parse(entry.DetailJson!);
        Assert.Equal("open", detail.RootElement.GetProperty("from").GetString());
        Assert.Equal(target, detail.RootElement.GetProperty("to").GetString());
        Assert.DoesNotContain("nội dung", entry.DetailJson!);   // chỉ trạng thái, không có chữ của cuộc chat
    }

    [Fact]
    public async Task SetStatus_CanReopenAHandledQuestion()
    {
        var repo = new FakeRepo { Statuses = { [Id(201)] = "ignored" } };

        var result = await Svc(repo).SetStatusAsync(Id(201), "open", Id(1), null, null);

        Assert.Equal(("open", "ignored"), (result!.ReviewStatus, result.PreviousStatus));
    }

    [Fact]
    public async Task SetStatus_ToTheSameStatus_Succeeds_ButWritesNoAudit()
    {
        var repo = new FakeRepo { Statuses = { [Id(201)] = "resolved" } };

        var result = await Svc(repo).SetStatusAsync(Id(201), "resolved", Id(1), null, null);

        Assert.Equal(("resolved", "resolved"), (result!.ReviewStatus, result.PreviousStatus));
        Assert.Null(repo.StatusAudit);
    }

    [Fact]
    public async Task SetStatus_OfAMessageNotInTheQueue_IsNull()
    {
        var repo = new FakeRepo { Statuses = { [Id(201)] = "open" } };

        Assert.Null(await Svc(repo).SetStatusAsync(Id(999), "resolved", Id(1), null, null));
        Assert.Null(repo.StatusAudit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("lạ")]
    [InlineData("answered")]   // kết quả, không phải trạng thái xử lý
    public async Task SetStatus_WithAnUnknownStatus_Throws_AndTouchesNothing(string? status)
    {
        var repo = new FakeRepo { Statuses = { [Id(201)] = "open" } };

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).SetStatusAsync(Id(201), status, Id(1), null, null));

        Assert.Equal(0, repo.StatusCalls);
        Assert.Equal("open", repo.Statuses[Id(201)]);
    }

    [Fact]
    public async Task SetStatus_WithoutAnActor_Throws_AndTouchesNothing()
    {
        var repo = new FakeRepo { Statuses = { [Id(201)] = "open" } };

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).SetStatusAsync(Id(201), "resolved", Guid.Empty, null, null));

        Assert.Equal(0, repo.StatusCalls);
    }

    [Fact]
    public async Task SetStatus_TrimsTheStatus()
    {
        var repo = new FakeRepo { Statuses = { [Id(201)] = "open" } };

        var result = await Svc(repo).SetStatusAsync(Id(201), "  resolved ", Id(1), null, null);

        Assert.Equal("resolved", result!.ReviewStatus);
    }

    [Fact]
    public async Task Controller_SetStatus_ReturnsTheResult_AndPassesTheActorAddressAndBrowser()
    {
        var dto = new ChatReviewStatusDto(Id(202), "resolved", "open");
        var service = new FakeService(statusResult: dto);

        var result = await ControllerFor(service, Id(1)).SetStatus(Id(202), new ChatReviewStatusRequest("resolved"), default);

        Assert.Same(dto, Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal((Id(202), "resolved", Id(1), "10.0.0.5", "Mozilla"), service.AskedStatus);
    }

    [Fact]
    public async Task Controller_SetStatus_WithoutALoggedInUser_Is401_AndNothingChanges()
    {
        var service = new FakeService(statusResult: new ChatReviewStatusDto(Id(202), "resolved", "open"));

        var result = await ControllerFor(service, actor: null).SetStatus(Id(202), new ChatReviewStatusRequest("resolved"), default);

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Null(service.AskedStatus);
    }

    [Fact]
    public async Task Controller_SetStatus_OfAMessageNotInTheQueue_Is404()
    {
        var result = await ControllerFor(new FakeService(), Id(1)).SetStatus(Id(999), new ChatReviewStatusRequest("resolved"), default);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Controller_SetStatus_WithABadStatus_Is400WithTheReason()
    {
        var controller = ControllerFor(new FakeService(error: new ArgumentException("Trạng thái sai")), Id(1));

        var result = await controller.SetStatus(Id(202), new ChatReviewStatusRequest("lạ"), default);

        Assert.Contains("Trạng thái sai", Assert.IsType<BadRequestObjectResult>(result.Result).Value!.ToString());
    }

    [Fact]
    public async Task Controller_SetStatus_WithAnEmptyBody_PassesNullStatus_AndIs400()
    {
        var service = new FakeService(error: new ArgumentException("Trạng thái sai"));

        var result = await ControllerFor(service, Id(1)).SetStatus(Id(202), null!, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(service.AskedStatus!.Value.Status);
    }

    [Fact]
    public void Controller_SetStatusRoute_IsAPatchOfTheItemStatus_UnderTheAdminOnlyController()
    {
        var method = typeof(AdminChatReviewController).GetMethod(nameof(AdminChatReviewController.SetStatus))!;

        Assert.Equal("items/{messageId:guid}/status", method.GetCustomAttribute<HttpPatchAttribute>()!.Template);
        Assert.Equal(UserRoles.Admin, typeof(AdminChatReviewController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Fact]
    public void TheStatusDtos_CarryNoChatContent()
    {
        var names = new[] { typeof(ChatReviewStatusRequest), typeof(ChatReviewStatusDto) }.SelectMany(t => t.GetProperties().Select(p => p.Name));

        Assert.DoesNotContain(names, n => n is "Content" or "Text" or "Span" or "Entities" or "Answer" or "QuestionText");
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
