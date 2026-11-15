using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.AiUsage;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;

namespace StudyAbroad.UnitTests;

// API token tổng thể cho admin: truy vấn gộp (số liệu đúng, EF dịch ra SQL được), dịch vụ (khoảng ngày, cộng dồn) và controller (chỉ Admin).
public class AiUsageReportTests
{
    private static AdminAiUsageController Controller(IAiUsageReportService service) => new(service, TestPricing.Haiku());

    private static readonly DateTime From = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ToExclusive = new(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);

    private static AiCall Call(string kind, string? purpose, DateTime at, int? input = 100, int? output = 10, int? cached = 0,
        int? latency = 1000, bool success = true, Guid? user = null, Guid? message = null, decimal? cost = null) =>
        new() { Kind = kind, Purpose = purpose, CreatedAt = at, PromptTokens = input, CompletionTokens = output, CachedTokens = cached,
            LatencyMs = latency, Success = success, UserId = user, ChatMessageId = message, CostUsd = cost };

    // Phiên chat và tin nhắn: mã phiên bắt đầu từ 100, mã tin nhắn từ 200, mã người dùng là 1, 2, ...
    private static ChatSession Session(int n, Guid user) => new() { Id = Id(100 + n), UserId = user };

    private static ChatMessage Msg(int n, int session, string role, DateTime at, string? outcome = "answered") =>
        new() { Id = Id(200 + n), ChatSessionId = Id(100 + session), Role = role, CreatedAt = at, Outcome = outcome, Content = "x" };

    // Mã cố định theo thứ tự tăng dần để kiểm thứ tự khi bằng nhau
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static DateTime Day(int day, int hour = 12) => new(2026, 11, day, hour, 0, 0, DateTimeKind.Utc);

    // ---- truy vấn gộp

    [Fact]
    public void ByKindAndPurpose_SumsTokensCallsAndFailuresPerGroup()
    {
        var calls = new[]
        {
            Call("claude", "skill_tra_loi", Day(2), input: 2000, output: 500, cached: 50, latency: 2000),
            Call("claude", "skill_tra_loi", Day(3), input: 1000, output: 300, cached: 0, latency: 4000, success: false),
            Call("ollama", "slm_intents", Day(2), input: 1700, output: 180, latency: 21000),
        }.AsQueryable();

        var rows = AiUsageAggregator.ByKindAndPurpose(calls, From, ToExclusive).ToList();

        var claude = Assert.Single(rows, r => r.Kind == "claude");
        Assert.Equal(("skill_tra_loi", 2, 1, 3000L, 800L, 50L, 6000L),
            (claude.Purpose, claude.Calls, claude.FailedCalls, claude.InputTokens, claude.OutputTokens, claude.CachedTokens, claude.LatencySumMs));
        var local = Assert.Single(rows, r => r.Kind == "ollama");
        Assert.Equal(("slm_intents", 1, 0, 1700L), (local.Purpose, local.Calls, local.FailedCalls, local.InputTokens));
    }

    [Fact]
    public void ByKindAndPurpose_SumsCost_AndCountsClaudeCallsWithoutAPrice()
    {
        var calls = new[]
        {
            Call("claude", "skill_tra_loi", Day(2), cost: 0.0003m),
            Call("claude", "skill_tra_loi", Day(3), cost: 0.0002m),
            Call("claude", "skill_tra_loi", Day(3), cost: null),   // model Claude chưa có trong bảng giá: chưa biết giá
            Call("ollama", "slm_intents", Day(2), cost: 0m),
            Call("ollama", "slm_intents", Day(2), cost: null),     // dòng cũ của AI local chưa có tiền: không phải "thiếu giá"
        }.AsQueryable();

        var rows = AiUsageAggregator.ByKindAndPurpose(calls, From, ToExclusive).ToList();

        var claude = Assert.Single(rows, r => r.Kind == "claude");
        Assert.Equal((0.0005m, 1), (claude.CostUsd, claude.UnpricedCalls));
        var local = Assert.Single(rows, r => r.Kind == "ollama");
        Assert.Equal((0m, 0), (local.CostUsd, local.UnpricedCalls));
    }

    [Fact]
    public void CostColumns_TranslateToSql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);

        var byPurpose = AiUsageAggregator.ByKindAndPurpose(db.AiCalls, From, ToExclusive).ToQueryString();
        var byDay = AiUsageAggregator.ByDayAndKind(db.AiCalls, From, ToExclusive).ToQueryString();
        var byUser = AiUsageAggregator.ByUserPage(db.AiCalls, From, ToExclusive, 0, 10).ToQueryString();
        var turns = AiUsageAggregator.TurnsPage(db.ChatMessages, db.ChatSessions, db.AiCalls, Id(1), From, ToExclusive, 0, 10).ToQueryString();
        var calls = AiUsageAggregator.CallsOfTurn(db.AiCalls, Id(201)).ToQueryString();

        foreach (var sql in new[] { byPurpose, byDay, byUser, turns, calls })
            Assert.Contains("cost_usd", sql);
    }

    [Fact]
    public void MissingTokensAndLatency_CountAsZero_NotAsAnError()
    {
        // Dòng cũ hoặc lần gọi lỗi có thể thiếu token, thời gian
        var calls = new[] { Call("ollama", null, Day(2), input: null, output: null, cached: null, latency: null) }.AsQueryable();

        var row = Assert.Single(AiUsageAggregator.ByKindAndPurpose(calls, From, ToExclusive).ToList());

        Assert.Equal((1, 0L, 0L, 0L, 0L), (row.Calls, row.InputTokens, row.OutputTokens, row.CachedTokens, row.LatencySumMs));
        Assert.Null(row.Purpose);
    }

    [Fact]
    public void TheWindowIncludesTheFirstInstant_AndExcludesTheEndInstant()
    {
        var calls = new[]
        {
            Call("claude", "a", From.AddTicks(-1)),   // trước khoảng
            Call("claude", "b", From),                // đúng đầu khoảng: tính
            Call("claude", "c", ToExclusive.AddTicks(-1)),   // sát cuối khoảng: tính
            Call("claude", "d", ToExclusive),         // đúng cuối khoảng: không tính
        }.AsQueryable();

        var purposes = AiUsageAggregator.ByKindAndPurpose(calls, From, ToExclusive).Select(r => r.Purpose).OrderBy(p => p).ToList();

        Assert.Equal(["b", "c"], purposes);
    }

    [Fact]
    public void ByDayAndKind_GroupsByTheUtcDay_AndSplitsLocalFromCloud()
    {
        var calls = new[]
        {
            Call("claude", "x", Day(2, 1)),
            Call("claude", "x", Day(2, 23)),
            Call("ollama", "y", Day(2, 12)),
            Call("claude", "x", Day(3, 0)),
        }.AsQueryable();

        var rows = AiUsageAggregator.ByDayAndKind(calls, From, ToExclusive).ToList()
            .OrderBy(r => r.Day).ThenBy(r => r.Kind).ToList();

        Assert.Equal(
            [("claude", Day(2).Date, 2), ("ollama", Day(2).Date, 1), ("claude", Day(3).Date, 1)],
            rows.Select(r => (r.Kind, r.Day!.Value.Date, r.Calls)));
    }

    // ---- lọc theo người và xếp hạng người dùng

    [Fact]
    public void UserFilter_CountsOnlyThatUsersCalls_InBothGroupings()
    {
        var calls = new[]
        {
            Call("claude", "x", Day(2), user: Id(1)),
            Call("claude", "x", Day(2), user: Id(1)),
            Call("ollama", "y", Day(3), user: Id(1)),
            Call("claude", "x", Day(2), user: Id(2)),
            Call("claude", "x", Day(2)),   // không có người dùng
        }.AsQueryable();

        var byPurpose = AiUsageAggregator.ByKindAndPurpose(calls, From, ToExclusive, Id(1)).ToList();
        var byDay = AiUsageAggregator.ByDayAndKind(calls, From, ToExclusive, Id(1)).ToList();

        Assert.Equal(3, byPurpose.Sum(r => r.Calls));
        Assert.Equal(3, byDay.Sum(r => r.Calls));
        Assert.Equal(5, AiUsageAggregator.ByKindAndPurpose(calls, From, ToExclusive).ToList().Sum(r => r.Calls));   // không lọc thì tính tất cả
    }

    [Fact]
    public void ByUserPage_RanksByTotalTokensDescending_AndTiesFallBackToTheUserId()
    {
        var calls = new[]
        {
            Call("claude", "x", Day(2), input: 10, output: 1, user: Id(1)),     // 11
            Call("claude", "x", Day(2), input: 500, output: 50, user: Id(3)),   // 550
            Call("claude", "x", Day(2), input: 500, output: 50, user: Id(2)),   // 550, bằng Id(3) nên Id(2) đứng trước
            Call("claude", "x", Day(2), input: 100, output: 20, user: Id(4)),   // 120
            Call("claude", "x", Day(3), input: 100, output: 20, user: Id(4)),   // cộng dồn cùng người: 240
        }.AsQueryable();

        var rows = AiUsageAggregator.ByUserPage(calls, From, ToExclusive, 0, 10).ToList();

        Assert.Equal([Id(2), Id(3), Id(4), Id(1)], rows.Select(r => r.UserId));
        Assert.Equal((2, 200L, 40L), (rows[2].Calls, rows[2].InputTokens, rows[2].OutputTokens));
    }

    [Fact]
    public void ByUserPage_SkipAndTake_GiveConsecutivePagesWithoutOverlap()
    {
        var calls = Enumerable.Range(1, 5).Select(n => Call("claude", "x", Day(2), input: n * 100, user: Id(n))).AsQueryable();

        var page1 = AiUsageAggregator.ByUserPage(calls, From, ToExclusive, 0, 2).Select(r => r.UserId).ToList();
        var page2 = AiUsageAggregator.ByUserPage(calls, From, ToExclusive, 2, 2).Select(r => r.UserId).ToList();
        var page3 = AiUsageAggregator.ByUserPage(calls, From, ToExclusive, 4, 2).Select(r => r.UserId).ToList();

        Assert.Equal([Id(5), Id(4)], page1);
        Assert.Equal([Id(3), Id(2)], page2);
        Assert.Equal([Id(1)], page3);
    }

    [Fact]
    public void CallsWithoutAUser_AreLeftOutOfTheRanking_AndOutOfTheUserCount()
    {
        var calls = new[]
        {
            Call("claude", "x", Day(2), input: 999999),   // ngoài phiên đăng nhập: không xếp hạng
            Call("claude", "x", Day(2), user: Id(1)),
            Call("claude", "x", Day(3), user: Id(1)),
            Call("claude", "x", Day(2), user: Id(2)),
        }.AsQueryable();

        Assert.Equal([Id(1), Id(2)], AiUsageAggregator.ByUserPage(calls, From, ToExclusive, 0, 10).Select(r => r.UserId).OrderBy(i => i));
        Assert.Equal(2, AiUsageAggregator.UsersInWindow(calls, From, ToExclusive).Count());   // mỗi người một lần dù gọi nhiều lần
    }

    [Fact]
    public void TheUserCountAndRankingRespectTheWindow()
    {
        var calls = new[] { Call("claude", "x", From.AddTicks(-1), user: Id(1)), Call("claude", "x", Day(2), user: Id(2)) }.AsQueryable();

        Assert.Equal([Id(2)], AiUsageAggregator.UsersInWindow(calls, From, ToExclusive).ToList());
    }

    [Fact]
    public void TheUserQueries_TranslateToSqlToo()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);

        var page = AiUsageAggregator.ByUserPage(db.AiCalls, From, ToExclusive, 20, 10).ToQueryString();
        var count = AiUsageAggregator.UsersInWindow(db.AiCalls, From, ToExclusive).ToQueryString();
        var filtered = AiUsageAggregator.ByKindAndPurpose(db.AiCalls, From, ToExclusive, Id(1)).ToQueryString();

        Assert.Contains("GROUP BY", page);
        Assert.Contains("ORDER BY", page);
        Assert.Contains("LIMIT", page);      // phân trang ở database, không kéo cả danh sách người dùng về
        Assert.Contains("OFFSET", page);
        Assert.Contains("DISTINCT", count);
        Assert.Contains("user_id", filtered);
    }

    // ---- theo lượt chat

    [Fact]
    public void TurnsPage_SumsTheCallsOfEachTurn_NewestFirst_AndLeavesOutTheStudentsOwnMessages()
    {
        var sessions = new[] { Session(1, Id(1)) }.AsQueryable();
        var messages = new[]
        {
            Msg(1, 1, "user", Day(2, 9)),                       // tin của học sinh: không phải một lượt
            Msg(2, 1, "assistant", Day(2, 10)),
            Msg(3, 1, "assistant", Day(3, 10), outcome: "no_answer"),
        }.AsQueryable();
        var calls = new[]
        {
            Call("ollama", "slm_intents", Day(2, 10), input: 1700, output: 180, latency: 21000, message: Id(202)),
            Call("claude", "skill_kich_ban", Day(2, 10), input: 2400, output: 500, latency: 3000, message: Id(202)),
            Call("claude", "skill_tra_loi", Day(3, 10), input: 10, output: 1, latency: 100, success: false, message: Id(203)),
        }.AsQueryable();

        var rows = AiUsageAggregator.TurnsPage(messages, sessions, calls, Id(1), From, ToExclusive, 0, 10).ToList();

        Assert.Equal([Id(203), Id(202)], rows.Select(r => r.MessageId));                  // mới nhất trước
        var turn = rows[1];
        Assert.Equal((2, 0, 4100L, 680L, 24000L), (turn.Calls, turn.FailedCalls, turn.InputTokens, turn.OutputTokens, turn.LatencySumMs));
        Assert.Equal(("answered", Id(101)), (turn.Outcome, turn.SessionId));
        Assert.Equal((1, 1, "no_answer"), (rows[0].Calls, rows[0].FailedCalls, rows[0].Outcome));
    }

    [Fact]
    public void TurnsPage_OnlyIncludesTheUsersOwnSessions()
    {
        var sessions = new[] { Session(1, Id(1)), Session(2, Id(2)) }.AsQueryable();
        var messages = new[] { Msg(1, 1, "assistant", Day(2)), Msg(2, 2, "assistant", Day(2)) }.AsQueryable();

        var rows = AiUsageAggregator.TurnsPage(messages, sessions, Array.Empty<AiCall>().AsQueryable(), Id(1), From, ToExclusive, 0, 10).ToList();

        Assert.Equal([Id(201)], rows.Select(r => r.MessageId));   // tin của người khác không lộ ra
    }

    [Fact]
    public void ATurnWithoutAnyAiCall_ShowsZeros_AndTheSystemFailureRowIsStillListed()
    {
        var sessions = new[] { Session(1, Id(1)) }.AsQueryable();
        var messages = new[] { Msg(1, 1, "system", Day(2), outcome: "failed") }.AsQueryable();   // advisor sập: không có lần gọi nào

        var row = Assert.Single(AiUsageAggregator.TurnsPage(messages, sessions, Array.Empty<AiCall>().AsQueryable(), Id(1), From, ToExclusive, 0, 10).ToList());

        Assert.Equal(("system", "failed", 0, 0L), (row.Role, row.Outcome, row.Calls, row.InputTokens));
    }

    [Fact]
    public void TurnsPage_RespectsTheWindow_AndPagesWithoutOverlap()
    {
        var sessions = new[] { Session(1, Id(1)) }.AsQueryable();
        var messages = new[]
        {
            Msg(0, 1, "assistant", From.AddTicks(-1)),    // trước khoảng
            Msg(1, 1, "assistant", Day(2)),
            Msg(2, 1, "assistant", Day(3)),
            Msg(3, 1, "assistant", Day(4)),
            Msg(4, 1, "assistant", ToExclusive),          // đúng cuối khoảng: không tính
        }.AsQueryable();
        var none = Array.Empty<AiCall>().AsQueryable();

        var page1 = AiUsageAggregator.TurnsPage(messages, sessions, none, Id(1), From, ToExclusive, 0, 2).Select(r => r.MessageId).ToList();
        var page2 = AiUsageAggregator.TurnsPage(messages, sessions, none, Id(1), From, ToExclusive, 2, 2).Select(r => r.MessageId).ToList();

        Assert.Equal([Id(203), Id(202)], page1);
        Assert.Equal([Id(201)], page2);
        Assert.Equal(3, AiUsageAggregator.TurnIds(messages, sessions, Id(1), From, ToExclusive).Count());
    }

    [Fact]
    public void TurnHeader_FindsTheOwnerThroughTheSession_AndIsEmptyForAnUnknownMessage()
    {
        var sessions = new[] { Session(1, Id(7)) }.AsQueryable();
        var messages = new[] { Msg(1, 1, "assistant", Day(2)) }.AsQueryable();

        var header = Assert.Single(AiUsageAggregator.TurnHeader(messages, sessions, Id(201)).ToList());

        Assert.Equal((Id(201), Id(101), Id(7)), (header.MessageId, header.SessionId, header.UserId));
        Assert.Empty(AiUsageAggregator.TurnHeader(messages, sessions, Id(999)).ToList());
    }

    [Fact]
    public void CallsOfTurn_AreInTimeOrder_OnlyOfThatTurn_WithMissingNumbersAsZero()
    {
        var calls = new[]
        {
            Call("claude", "sau", Day(2, 11), message: Id(201)),
            Call("ollama", "dau", Day(2, 10), input: null, output: null, cached: null, latency: null, message: Id(201)),
            Call("claude", "khac", Day(2, 10), message: Id(202)),
        }.AsQueryable();

        var details = AiUsageAggregator.CallsOfTurn(calls, Id(201)).ToList();

        Assert.Equal(["dau", "sau"], details.Select(d => d.Purpose));
        Assert.Equal((0, 0, 0, 0), (details[0].InputTokens, details[0].OutputTokens, details[0].CachedTokens, details[0].LatencyMs));
    }

    [Fact]
    public void TheTurnQueries_TranslateToSql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);

        var page = AiUsageAggregator.TurnsPage(db.ChatMessages, db.ChatSessions, db.AiCalls, Id(1), From, ToExclusive, 20, 10).ToQueryString();
        var count = AiUsageAggregator.TurnIds(db.ChatMessages, db.ChatSessions, Id(1), From, ToExclusive).ToQueryString();
        var header = AiUsageAggregator.TurnHeader(db.ChatMessages, db.ChatSessions, Id(201)).ToQueryString();
        var calls = AiUsageAggregator.CallsOfTurn(db.AiCalls, Id(201)).ToQueryString();

        Assert.Contains("JOIN", page);
        Assert.Contains("ORDER BY", page);
        Assert.Contains("LIMIT", page);          // phân trang ở database
        Assert.Contains("chat_message_id", page);   // số đo cộng từ ai_calls qua chat_message_id
        Assert.Contains("JOIN", count);
        Assert.Contains("JOIN", header);
        Assert.Contains("ORDER BY", calls);
    }

    // Kiểm tra EF dịch được ra SQL: nếu biểu thức không dịch được thì ToQueryString ném lỗi (không cần kết nối database)
    [Fact]
    public void TheAggregations_TranslateToSqlWithGroupBy_SoTheDatabaseDoesTheWork()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);

        var byPurpose = AiUsageAggregator.ByKindAndPurpose(db.AiCalls, From, ToExclusive).ToQueryString();
        var byDay = AiUsageAggregator.ByDayAndKind(db.AiCalls, From, ToExclusive).ToQueryString();

        Assert.Contains("GROUP BY", byPurpose);
        Assert.Contains("GROUP BY", byDay);
        Assert.Contains("ai_calls", byPurpose);
    }

    // ---- dịch vụ

    private sealed class FakeRepo : IAiUsageReportRepository
    {
        public List<AiUsageGroupRow> KindPurpose { get; init; } = [];
        public List<AiUsageGroupRow> DayKind { get; init; } = [];
        public List<AiUsageUserRow> UserRows { get; init; } = [];
        public int TotalUsers { get; init; }
        public List<AiUsageUserInfo> Users { get; init; } = [];
        public (DateTime From, DateTime ToExclusive)? Asked { get; private set; }
        public Guid? AskedUser { get; private set; }
        public (int Skip, int Take)? AskedPage { get; private set; }

        public Task<IReadOnlyList<AiUsageGroupRow>> ByKindAndPurposeAsync(
            DateTime fromUtc, DateTime toUtcExclusive, Guid? userId = null, CancellationToken ct = default)
        {
            Asked = (fromUtc, toUtcExclusive);
            AskedUser = userId;
            return Task.FromResult<IReadOnlyList<AiUsageGroupRow>>(KindPurpose);
        }

        public Task<IReadOnlyList<AiUsageGroupRow>> ByDayAndKindAsync(
            DateTime fromUtc, DateTime toUtcExclusive, Guid? userId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AiUsageGroupRow>>(DayKind);

        public Task<(IReadOnlyList<AiUsageUserRow> Rows, int TotalUsers)> ByUserPageAsync(
            DateTime fromUtc, DateTime toUtcExclusive, int skip, int take, CancellationToken ct = default)
        {
            Asked = (fromUtc, toUtcExclusive);
            AskedPage = (skip, take);
            return Task.FromResult<(IReadOnlyList<AiUsageUserRow>, int)>((UserRows, TotalUsers));
        }

        public List<AiUsageTurnRow> TurnRows { get; init; } = [];
        public int TotalTurns { get; init; }
        public AiUsageTurnHeader? Header { get; init; }
        public List<AiUsageCallDetailDto> TurnCalls { get; init; } = [];
        public Guid? AskedTurnsUser { get; private set; }

        public Task<(IReadOnlyList<AiUsageTurnRow> Rows, int TotalTurns)> TurnsPageAsync(
            Guid userId, DateTime fromUtc, DateTime toUtcExclusive, int skip, int take, CancellationToken ct = default)
        {
            Asked = (fromUtc, toUtcExclusive);
            AskedPage = (skip, take);
            AskedTurnsUser = userId;
            return Task.FromResult<(IReadOnlyList<AiUsageTurnRow>, int)>((TurnRows, TotalTurns));
        }

        public Task<AiUsageTurnHeader?> FindTurnAsync(Guid messageId, CancellationToken ct = default) =>
            Task.FromResult(Header is not null && Header.MessageId == messageId ? Header : null);

        public Task<IReadOnlyList<AiUsageCallDetailDto>> CallsOfTurnAsync(Guid messageId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AiUsageCallDetailDto>>(TurnCalls);

        public Task<IReadOnlyList<AiUsageUserInfo>> UsersByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AiUsageUserInfo>>(Users.Where(u => ids.Contains(u.Id)).ToList());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static AiUsageGroupRow Row(string kind, string? purpose, int calls, long input, long output, long latencySum, int failed = 0, DateTime? day = null, decimal cost = 0m, int unpriced = 0) =>
        new(kind, purpose, day, calls, failed, input, output, 0, latencySum, cost, unpriced);

    private static AiUsageReportService Service(FakeRepo repo) =>
        new(repo, new FixedClock(new DateTimeOffset(2026, 11, 11, 15, 30, 0, TimeSpan.Zero)));

    [Fact]
    public async Task WithoutDates_ItCoversTheLast30DaysEndingToday_Inclusive()
    {
        var repo = new FakeRepo();

        var overview = await Service(repo).GetOverviewAsync(null, null);

        Assert.Equal((new DateOnly(2026, 10, 13), new DateOnly(2026, 11, 11)), (overview.From, overview.To));   // 30 ngày kể cả hôm nay
        Assert.Equal(new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc), repo.Asked!.Value.From);
        Assert.Equal(new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc), repo.Asked.Value.ToExclusive);   // hết ngày 11 = đầu ngày 12
    }

    [Fact]
    public async Task ExplicitDates_BecomeUtcDayBounds_BothEndsIncluded()
    {
        var repo = new FakeRepo();

        await Service(repo).GetOverviewAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7));

        Assert.Equal((From, ToExclusive), repo.Asked);
    }

    [Fact]
    public async Task OnlyFromGiven_EndsToday()
    {
        var overview = await Service(new FakeRepo()).GetOverviewAsync(new DateOnly(2026, 11, 1), null);

        Assert.Equal(new DateOnly(2026, 11, 11), overview.To);
    }

    [Fact]
    public async Task TotalsAndKindRollUp_AddUpTheGroups()
    {
        var repo = new FakeRepo
        {
            KindPurpose =
            [
                Row("claude", "skill_tra_loi", calls: 2, input: 3000, output: 800, latencySum: 6000, failed: 1),
                Row("claude", "skill_kich_ban", calls: 1, input: 2400, output: 500, latencySum: 3000),
                Row("ollama", "slm_intents", calls: 3, input: 5100, output: 540, latencySum: 63000),
            ],
        };

        var overview = await Service(repo).GetOverviewAsync(null, null);

        Assert.Equal((6, 1, 10500L, 1840L), (overview.Total.Calls, overview.Total.FailedCalls, overview.Total.InputTokens, overview.Total.OutputTokens));
        var claude = overview.ByKind.Single(k => k.Kind == "claude").Metrics;
        Assert.Equal((3, 1, 5400L, 1300L), (claude.Calls, claude.FailedCalls, claude.InputTokens, claude.OutputTokens));
        var local = overview.ByKind.Single(k => k.Kind == "ollama").Metrics;
        Assert.Equal((3, 5100L), (local.Calls, local.InputTokens));
    }

    [Fact]
    public async Task CostAndUnpricedCalls_RollUpFromGroupsToKindsAndTotal()
    {
        var repo = new FakeRepo
        {
            KindPurpose =
            [
                Row("claude", "skill_tra_loi", calls: 3, input: 3000, output: 800, latencySum: 6000, cost: 0.0007m, unpriced: 1),
                Row("claude", "skill_kich_ban", calls: 1, input: 2400, output: 500, latencySum: 3000, cost: 0.0004m),
                Row("ollama", "slm_intents", calls: 3, input: 5100, output: 540, latencySum: 63000),
            ],
        };

        var overview = await Service(repo).GetOverviewAsync(null, null);

        Assert.Equal((0.0011m, 1), (overview.Total.CostUsd, overview.Total.UnpricedCalls));
        var claude = overview.ByKind.Single(k => k.Kind == "claude").Metrics;
        Assert.Equal((0.0011m, 1), (claude.CostUsd, claude.UnpricedCalls));
        var local = overview.ByKind.Single(k => k.Kind == "ollama").Metrics;
        Assert.Equal((0m, 0), (local.CostUsd, local.UnpricedCalls));
        Assert.Equal(0.0007m, overview.ByPurpose.Single(p => p.Purpose == "skill_tra_loi").Metrics.CostUsd);
    }

    [Fact]
    public async Task TurnDetail_TotalsTheCostOfItsCalls_AndFlagsAClaudeCallWithoutAPrice()
    {
        var repo = new FakeRepo
        {
            Header = new AiUsageTurnHeader(Id(201), Id(101), Id(1), Day(2), "assistant", "answered"),
            TurnCalls = [CallDetail("claude", "skill_tra_loi", 2000, 500, 800, cost: 0.00045m), CallDetail("claude", "skill_kich_ban", 100, 10, 90, cost: null)],
        };

        var turn = await Service(repo).GetTurnAsync(Id(201));

        Assert.Equal((0.00045m, 1), (turn!.Total.CostUsd, turn.Total.UnpricedCalls));
    }

    [Fact]
    public async Task AverageLatency_IsTotalTimeDividedByTotalCalls_NotAnAverageOfAverages()
    {
        // 1 lần 1000 ms và 9 lần 100 ms: trung bình đúng là 190 ms, trung bình của hai trung bình sẽ là 550 ms
        var repo = new FakeRepo { KindPurpose = [Row("claude", "a", 1, 0, 0, 1000), Row("claude", "b", 9, 0, 0, 900)] };

        var overview = await Service(repo).GetOverviewAsync(null, null);

        Assert.Equal(190, overview.Total.AvgLatencyMs);
        Assert.Equal(1000, overview.ByPurpose.Single(p => p.Purpose == "a").Metrics.AvgLatencyMs);
        Assert.Equal(100, overview.ByPurpose.Single(p => p.Purpose == "b").Metrics.AvgLatencyMs);
    }

    [Fact]
    public async Task ByPurpose_ListsTheBiggestTokenUsersFirst()
    {
        var repo = new FakeRepo
        {
            KindPurpose = [Row("claude", "nhỏ", 1, 100, 10, 0), Row("claude", "lớn", 1, 9000, 900, 0), Row("ollama", "vừa", 1, 2000, 200, 0)],
        };

        var overview = await Service(repo).GetOverviewAsync(null, null);

        Assert.Equal(["lớn", "vừa", "nhỏ"], overview.ByPurpose.Select(p => p.Purpose));
    }

    [Fact]
    public async Task ByDay_IsInDateOrder_WithTheDateAsADateOnly()
    {
        var repo = new FakeRepo
        {
            DayKind =
            [
                Row("claude", null, 1, 10, 1, 0, day: Day(3, 0)),
                Row("ollama", null, 1, 10, 1, 0, day: Day(2, 0)),
                Row("claude", null, 1, 10, 1, 0, day: Day(2, 0)),
            ],
        };

        var overview = await Service(repo).GetOverviewAsync(null, null);

        Assert.Equal(
            [(new DateOnly(2026, 11, 2), "claude"), (new DateOnly(2026, 11, 2), "ollama"), (new DateOnly(2026, 11, 3), "claude")],
            overview.ByDay.Select(d => (d.Date, d.Kind)));
    }

    [Fact]
    public async Task NoData_GivesZerosAndEmptyLists_NotAnError()
    {
        var overview = await Service(new FakeRepo()).GetOverviewAsync(null, null);

        Assert.Equal(new AiUsageMetrics(0, 0, 0, 0, 0, 0), overview.Total);
        Assert.Empty(overview.ByKind);
        Assert.Empty(overview.ByPurpose);
        Assert.Empty(overview.ByDay);
    }

    [Fact]
    public async Task StartAfterEnd_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(new FakeRepo()).GetOverviewAsync(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public async Task ARangeOf366Days_IsAllowed_And367IsRejected()
    {
        var end = new DateOnly(2026, 11, 11);

        await Service(new FakeRepo()).GetOverviewAsync(end.AddDays(-365), end);   // đúng 366 ngày kể cả hai đầu

        await Assert.ThrowsAsync<ArgumentException>(() => Service(new FakeRepo()).GetOverviewAsync(end.AddDays(-366), end));
    }

    // ---- dịch vụ: theo người

    private static AiUsageUserRow UserRow(int n, int calls, long input, long output, long latencySum, int failed = 0, decimal cost = 0m, int unpriced = 0) =>
        new(Id(n), calls, failed, input, output, 0, latencySum, cost, unpriced);

    [Fact]
    public async Task Users_AreJoinedWithTheirNameAndEmail_AndKeepTheirRankOrder()
    {
        var repo = new FakeRepo
        {
            UserRows = [UserRow(2, 3, 9000, 900, 30000, failed: 1), UserRow(1, 1, 100, 10, 500)],
            TotalUsers = 2,
            Users = [new AiUsageUserInfo(Id(1), "Lan", "lan@x.vn"), new AiUsageUserInfo(Id(2), "Nam", "nam@x.vn")],
        };

        var page = await Service(repo).GetUsersAsync(null, null, 1, 20);

        Assert.Equal([Id(2), Id(1)], page.Items.Select(i => i.UserId));
        Assert.Equal(("Nam", "nam@x.vn"), (page.Items[0].FullName, page.Items[0].Email));
        Assert.Equal((3, 1, 9000L, 900L, 10000d), (page.Items[0].Metrics.Calls, page.Items[0].Metrics.FailedCalls,
            page.Items[0].Metrics.InputTokens, page.Items[0].Metrics.OutputTokens, page.Items[0].Metrics.AvgLatencyMs));
    }

    [Fact]
    public async Task ADeletedAccount_StillAppearsInTheRanking_WithoutNameOrEmail()
    {
        // Dòng nhật ký còn nhưng tài khoản đã bị xóa: token đã tiêu thật nên vẫn phải thấy
        var repo = new FakeRepo { UserRows = [UserRow(9, 1, 500, 50, 800)], TotalUsers = 1 };

        var item = Assert.Single((await Service(repo).GetUsersAsync(null, null, 1, 20)).Items);

        Assert.Equal(Id(9), item.UserId);
        Assert.Null(item.FullName);
        Assert.Null(item.Email);
        Assert.Equal(500, item.Metrics.InputTokens);
    }

    [Theory]
    [InlineData(1, 20, 0, 20)]
    [InlineData(3, 10, 20, 10)]
    [InlineData(0, 10, 0, 10)]        // trang nhỏ hơn 1 thì về trang 1
    [InlineData(-5, 10, 0, 10)]
    [InlineData(2, 0, 1, 1)]          // cỡ trang nhỏ hơn 1 thì là 1
    [InlineData(2, 500, 100, 100)]    // cỡ trang bị giới hạn 100
    public async Task Paging_IsClampedAndTurnedIntoSkipAndTake(int page, int pageSize, int expectedSkip, int expectedTake)
    {
        var repo = new FakeRepo();

        await Service(repo).GetUsersAsync(null, null, page, pageSize);

        Assert.Equal((expectedSkip, expectedTake), repo.AskedPage);
    }

    [Fact]
    public async Task Users_ReportsPageAndTotals_ForThePager()
    {
        var repo = new FakeRepo { TotalUsers = 45 };

        var page = await Service(repo).GetUsersAsync(null, null, 3, 20);

        Assert.Equal((3, 20, 45, 3), (page.Page, page.PageSize, page.TotalCount, page.TotalPages));
    }

    [Fact]
    public async Task Users_UseTheSameDateRangeRules_AsTheOverview()
    {
        var repo = new FakeRepo();

        await Service(repo).GetUsersAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7), 1, 20);
        Assert.Equal((From, ToExclusive), repo.Asked);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(repo).GetUsersAsync(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 1), 1, 20));
    }

    [Fact]
    public async Task UserOverview_IsFilteredToThatUser_AndCarriesTheirName()
    {
        var repo = new FakeRepo
        {
            Users = [new AiUsageUserInfo(Id(1), "Lan", "lan@x.vn")],
            KindPurpose = [Row("claude", "skill_kich_ban", 2, 4800, 1000, 6000)],
        };

        var result = await Service(repo).GetUserOverviewAsync(Id(1), null, null);

        Assert.Equal(Id(1), repo.AskedUser);   // truy vấn được lọc đúng người
        Assert.Equal(("Lan", "lan@x.vn"), (result!.FullName, result.Email));
        Assert.Equal((2, 4800L), (result.Overview.Total.Calls, result.Overview.Total.InputTokens));
        Assert.Equal("claude", Assert.Single(result.Overview.ByKind).Kind);
    }

    [Fact]
    public async Task UserOverview_OfAnUnknownUser_IsNull_WithoutQueryingTheCalls()
    {
        var repo = new FakeRepo();

        Assert.Null(await Service(repo).GetUserOverviewAsync(Id(77), null, null));
        Assert.Null(repo.Asked);   // không có người đó thì khỏi gộp số liệu
    }

    [Fact]
    public async Task UserOverview_OfAUserWhoNeverCalledTheAi_IsZerosNotAnError()
    {
        var repo = new FakeRepo { Users = [new AiUsageUserInfo(Id(1), "Lan", "lan@x.vn")] };

        var result = await Service(repo).GetUserOverviewAsync(Id(1), null, null);

        Assert.Equal(new AiUsageMetrics(0, 0, 0, 0, 0, 0), result!.Overview.Total);
    }

    [Fact]
    public async Task UserOverview_WithABadRange_ThrowsEvenWhenTheUserDoesNotExist()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(new FakeRepo()).GetUserOverviewAsync(Id(77), new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public async Task TheSystemOverview_IsNotFilteredByUser()
    {
        var repo = new FakeRepo();

        await Service(repo).GetOverviewAsync(null, null);

        Assert.Null(repo.AskedUser);
    }

    // ---- dịch vụ: theo lượt chat

    private static AiUsageTurnRow TurnRow(int n, string role = "assistant", string? outcome = "answered", int calls = 2, long input = 4100, long output = 680, long latency = 24000, int failed = 0, decimal cost = 0m, int unpriced = 0) =>
        new(Id(200 + n), Id(101), Day(2), role, outcome, calls, failed, input, output, 0, latency, cost, unpriced);

    private static FakeRepo WithUser(FakeRepo? repo = null) => repo ?? new FakeRepo { Users = [new AiUsageUserInfo(Id(1), "Lan", "lan@x.vn")] };

    [Fact]
    public async Task UserTurns_MapEachTurnToItsMetrics_InTheOrderTheDatabaseGave()
    {
        var repo = new FakeRepo
        {
            Users = [new AiUsageUserInfo(Id(1), "Lan", "lan@x.vn")],
            TurnRows = [TurnRow(2, outcome: "no_answer", calls: 1, input: 10, output: 1, latency: 100, failed: 1), TurnRow(1)],
            TotalTurns = 2,
        };

        var page = (await Service(repo).GetUserTurnsAsync(Id(1), null, null, 1, 20))!;

        Assert.Equal([Id(202), Id(201)], page.Items.Select(i => i.MessageId));
        Assert.Equal(("no_answer", 1, 1), (page.Items[0].Outcome, page.Items[0].Metrics.Calls, page.Items[0].Metrics.FailedCalls));
        Assert.Equal((2, 4100L, 680L, 12000d), (page.Items[1].Metrics.Calls, page.Items[1].Metrics.InputTokens,
            page.Items[1].Metrics.OutputTokens, page.Items[1].Metrics.AvgLatencyMs));
        Assert.Equal(Id(1), repo.AskedTurnsUser);
    }

    [Fact]
    public async Task UserTurns_OfAnUnknownUser_AreNull_WithoutQueryingTheMessages()
    {
        var repo = new FakeRepo();

        Assert.Null(await Service(repo).GetUserTurnsAsync(Id(77), null, null, 1, 20));
        Assert.Null(repo.AskedPage);
    }

    [Theory]
    [InlineData(1, 20, 0, 20)]
    [InlineData(3, 10, 20, 10)]
    [InlineData(0, 10, 0, 10)]
    [InlineData(2, 500, 100, 100)]
    public async Task UserTurns_PagingIsClampedAndTurnedIntoSkipAndTake(int page, int pageSize, int skip, int take)
    {
        var repo = WithUser();

        await Service(repo).GetUserTurnsAsync(Id(1), null, null, page, pageSize);

        Assert.Equal((skip, take), repo.AskedPage);
    }

    [Fact]
    public async Task UserTurns_UseTheSameDateRangeRules()
    {
        var repo = WithUser();

        await Service(repo).GetUserTurnsAsync(Id(1), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7), 1, 20);
        Assert.Equal((From, ToExclusive), repo.Asked);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(repo).GetUserTurnsAsync(Id(1), new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 1), 1, 20));
        // Khoảng sai báo lỗi kể cả khi người dùng không tồn tại
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(new FakeRepo()).GetUserTurnsAsync(Id(77), new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 1), 1, 20));
    }

    [Fact]
    public async Task UserTurns_ReportPageAndTotals()
    {
        var repo = new FakeRepo { Users = [new AiUsageUserInfo(Id(1), "Lan", "lan@x.vn")], TotalTurns = 45 };

        var page = (await Service(repo).GetUserTurnsAsync(Id(1), null, null, 3, 20))!;

        Assert.Equal((3, 20, 45, 3), (page.Page, page.PageSize, page.TotalCount, page.TotalPages));
    }

    private static AiUsageCallDetailDto CallDetail(string kind, string purpose, int input, int output, int latency, bool ok = true, string? error = null, decimal? cost = 0m) =>
        new(Guid.NewGuid(), Day(2), kind, purpose, "1.0", "model", input, output, 0, latency, ok, error, cost);

    [Fact]
    public async Task TurnDetail_TotalsTheCalls_AndKeepsThemInOrder()
    {
        var repo = new FakeRepo
        {
            Header = new AiUsageTurnHeader(Id(201), Id(101), Id(1), Day(2), "assistant", "failed"),
            TurnCalls =
            [
                CallDetail("ollama", "slm_intents", 1700, 180, 21000),
                CallDetail("claude", "skill_tra_loi", 0, 0, 3000, ok: false, error: "HTTP 500"),
            ],
        };

        var turn = (await Service(repo).GetTurnAsync(Id(201)))!;

        Assert.Equal(["slm_intents", "skill_tra_loi"], turn.Calls.Select(c => c.Purpose));
        Assert.Equal((2, 1, 1700L, 180L, 12000d), (turn.Total.Calls, turn.Total.FailedCalls, turn.Total.InputTokens, turn.Total.OutputTokens, turn.Total.AvgLatencyMs));
        Assert.Equal((Id(1), "failed"), (turn.UserId, turn.Outcome));
        Assert.Equal("HTTP 500", turn.Calls[1].Error);
    }

    [Fact]
    public async Task TurnDetail_OfAnUnknownMessage_IsNull()
    {
        Assert.Null(await Service(new FakeRepo()).GetTurnAsync(Id(999)));
    }

    [Fact]
    public async Task TurnDetail_OfATurnWithoutCalls_IsZeros()
    {
        var repo = new FakeRepo { Header = new AiUsageTurnHeader(Id(201), Id(101), Id(1), Day(2), "system", "failed") };

        var turn = (await Service(repo).GetTurnAsync(Id(201)))!;

        Assert.Equal(new AiUsageMetrics(0, 0, 0, 0, 0, 0), turn.Total);
        Assert.Empty(turn.Calls);
    }

    // ---- controller

    private sealed class FakeService(
        AiUsageOverviewDto? overview = null, Exception? error = null,
        PagedResult<AiUsageUserRankDto>? users = null, AiUsageUserOverviewDto? userOverview = null,
        PagedResult<AiUsageTurnRowDto>? turns = null, AiUsageTurnDetailDto? turn = null) : IAiUsageReportService
    {
        public Task<PagedResult<AiUsageTurnRowDto>?> GetUserTurnsAsync(
            Guid userId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default)
        {
            Asked = (from, to);
            AskedPage = (page, pageSize);
            AskedUser = userId;
            if (error is not null)
                throw error;
            return Task.FromResult(turns);
        }

        public Task<AiUsageTurnDetailDto?> GetTurnAsync(Guid messageId, CancellationToken ct = default)
        {
            AskedTurn = messageId;
            return Task.FromResult(turn);
        }

        public Guid? AskedTurn { get; private set; }

        public (DateOnly? From, DateOnly? To)? Asked { get; private set; }
        public (int Page, int PageSize)? AskedPage { get; private set; }
        public Guid? AskedUser { get; private set; }

        public Task<AiUsageOverviewDto> GetOverviewAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
        {
            Asked = (from, to);
            if (error is not null)
                throw error;
            return Task.FromResult(overview!);
        }

        public Task<PagedResult<AiUsageUserRankDto>> GetUsersAsync(DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default)
        {
            Asked = (from, to);
            AskedPage = (page, pageSize);
            if (error is not null)
                throw error;
            return Task.FromResult(users!);
        }

        public Task<AiUsageUserOverviewDto?> GetUserOverviewAsync(Guid userId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
        {
            Asked = (from, to);
            AskedUser = userId;
            if (error is not null)
                throw error;
            return Task.FromResult(userOverview);
        }
    }

    private static readonly AiUsageOverviewDto Sample = new(
        new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7), new AiUsageMetrics(1, 0, 10, 1, 0, 5), [], [], []);

    [Fact]
    public async Task Controller_ReturnsTheOverview_AndPassesTheDatesThrough()
    {
        var service = new FakeService(Sample);

        var result = await Controller(service).GetOverview(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7), default);

        Assert.Same(Sample, Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal((new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7)), service.Asked);
    }

    [Fact]
    public async Task Controller_AnInvalidRange_Is400WithTheReason()
    {
        var result = await Controller(new FakeService(error: new ArgumentException("Khoảng sai")))
            .GetOverview(null, null, default);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("Khoảng sai", bad.Value!.ToString());
    }

    [Fact]
    public void Controller_IsForAdminsOnly_NotModerators()
    {
        var authorize = typeof(AdminAiUsageController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.Equal(UserRoles.Admin, authorize!.Roles);   // token và chi phí là việc giám sát, Moderator không xem
        Assert.Equal("api/v1/admin/ai-usage", typeof(AdminAiUsageController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    // ---- controller: theo người

    private static readonly PagedResult<AiUsageUserRankDto> SampleUsers = new(
        [new AiUsageUserRankDto(Id(1), "Lan", "lan@x.vn", new AiUsageMetrics(1, 0, 10, 1, 0, 5))], 1, 20, 1);

    [Fact]
    public async Task Controller_Users_ReturnsThePage_AndPassesTheArguments()
    {
        var service = new FakeService(users: SampleUsers);

        var result = await Controller(service).GetUsers(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7), 2, 50, default);

        Assert.Same(SampleUsers, Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal((2, 50), service.AskedPage);
        Assert.Equal((new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7)), service.Asked);
    }

    [Fact]
    public async Task Controller_Users_ABadRangeIs400()
    {
        var result = await Controller(new FakeService(error: new ArgumentException("Khoảng sai")))
            .GetUsers(null, null, 1, 20, default);

        Assert.Contains("Khoảng sai", Assert.IsType<BadRequestObjectResult>(result.Result).Value!.ToString());
    }

    [Fact]
    public async Task Controller_UserOverview_ReturnsTheOverviewOfThatUser()
    {
        var overview = new AiUsageUserOverviewDto(Id(1), "Lan", "lan@x.vn", Sample);
        var service = new FakeService(userOverview: overview);

        var result = await Controller(service).GetUserOverview(Id(1), null, null, default);

        Assert.Same(overview, Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(Id(1), service.AskedUser);
    }

    [Fact]
    public async Task Controller_UserOverview_OfAnUnknownUserIs404()
    {
        var result = await Controller(new FakeService(userOverview: null)).GetUserOverview(Id(77), null, null, default);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Controller_UserOverview_ABadRangeIs400()
    {
        var result = await Controller(new FakeService(error: new ArgumentException("Khoảng sai")))
            .GetUserOverview(Id(1), null, null, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ---- controller: theo lượt chat

    private static readonly PagedResult<AiUsageTurnRowDto> SampleTurns = new(
        [new AiUsageTurnRowDto(Id(201), Id(101), Day(2), "assistant", "answered", new AiUsageMetrics(2, 0, 4100, 680, 0, 12000))], 1, 20, 1);

    private static readonly AiUsageTurnDetailDto SampleTurn = new(
        Id(201), Id(101), Id(1), Day(2), "assistant", "answered", new AiUsageMetrics(0, 0, 0, 0, 0, 0), []);

    [Fact]
    public async Task Controller_UserTurns_ReturnsThePage_AndPassesTheArguments()
    {
        var service = new FakeService(turns: SampleTurns);

        var result = await Controller(service).GetUserTurns(Id(1), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7), 2, 50, default);

        Assert.Same(SampleTurns, Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal((Id(1), 2, 50), (service.AskedUser, service.AskedPage?.Page, service.AskedPage?.PageSize));
    }

    [Fact]
    public async Task Controller_UserTurns_OfAnUnknownUserIs404_AndABadRangeIs400()
    {
        var notFound = await Controller(new FakeService(turns: null)).GetUserTurns(Id(77), null, null, 1, 20, default);
        var bad = await Controller(new FakeService(error: new ArgumentException("Khoảng sai"))).GetUserTurns(Id(1), null, null, 1, 20, default);

        Assert.IsType<NotFoundResult>(notFound.Result);
        Assert.IsType<BadRequestObjectResult>(bad.Result);
    }

    [Fact]
    public async Task Controller_Turn_ReturnsTheDetail_Or404()
    {
        var service = new FakeService(turn: SampleTurn);

        var found = await Controller(service).GetTurn(Id(201), default);
        var missing = await Controller(new FakeService(turn: null)).GetTurn(Id(999), default);

        Assert.Same(SampleTurn, Assert.IsType<OkObjectResult>(found.Result).Value);
        Assert.Equal(Id(201), service.AskedTurn);
        Assert.IsType<NotFoundResult>(missing.Result);
    }

    [Fact]
    public void Controller_TurnRoutes_AreDeclared_AndTheDtosCarryNoChatContent()
    {
        var type = typeof(AdminAiUsageController);

        Assert.Equal("users/{userId:guid}/turns", type.GetMethod(nameof(AdminAiUsageController.GetUserTurns))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("turns/{messageId:guid}", type.GetMethod(nameof(AdminAiUsageController.GetTurn))!.GetCustomAttribute<HttpGetAttribute>()!.Template);

        // Màn token không được lộ nội dung chat: không DTO nào có thuộc tính nội dung, câu hỏi, câu trả lời
        var names = new[] { typeof(AiUsageTurnRowDto), typeof(AiUsageTurnDetailDto), typeof(AiUsageCallDetailDto), typeof(AiUsageTurnHeader) }
            .SelectMany(t => t.GetProperties().Select(p => p.Name));
        Assert.DoesNotContain(names, n => n.Contains("Content") || n.Contains("Question") || n.Contains("Answer") || n.Contains("Meta"));
    }

    [Fact]
    public void Controller_UserRoutes_AreDeclaredAsExpected()
    {
        var type = typeof(AdminAiUsageController);

        Assert.Equal("users", type.GetMethod(nameof(AdminAiUsageController.GetUsers))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("users/{userId:guid}", type.GetMethod(nameof(AdminAiUsageController.GetUserOverview))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }
}
