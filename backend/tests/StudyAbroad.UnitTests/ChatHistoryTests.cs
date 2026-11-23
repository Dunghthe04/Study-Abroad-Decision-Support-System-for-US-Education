using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.Advisor.History;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;
using ChatMessageEntity = StudyAbroad.Domain.Entities.ChatMessage;

namespace StudyAbroad.UnitTests;

// Lịch sử chat của chính người dùng: tiêu đề phiên, con trỏ phân trang, truy vấn (chủ phiên, phiên có tin, thứ tự), dịch vụ và controller.
public class ChatHistoryTests
{
    private static readonly Guid Me = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");
    private static DateTime At(int day, int hour = 12, int minute = 0) => new(2026, 11, day, hour, minute, 0, DateTimeKind.Utc);

    private static ChatSession Session(int n, Guid owner, DateTime created, DateTime? updated = null, string? title = null) =>
        new() { Id = Id(n), UserId = owner, CreatedAt = created, UpdatedAt = updated, Title = title };

    private static ChatMessageEntity Msg(int n, int session, string role, DateTime at, string content = "x", string? sources = null) =>
        new() { Id = Id(1000 + n), ChatSessionId = Id(session), Role = role, CreatedAt = at, Content = content, SourcesJson = sources };

    // ---- tiêu đề

    [Theory]
    [InlineData("Học phí Harvard bao nhiêu?", "Học phí Harvard bao nhiêu?")]
    [InlineData("  Học   phí\n\nHarvard\tbao nhiêu  ", "Học phí Harvard bao nhiêu")]
    [InlineData("a\u0000b\u0007c", "a b c")]
    public void Title_CollapsesWhitespaceAndControlCharacters(string text, string expected) =>
        Assert.Equal(expected, ChatSessionTitle.From(text));

    [Fact]
    public void Title_LongerThan60Characters_IsCutWithAnEllipsis_WithoutATrailingSpace()
    {
        var exactly60 = new string('a', 60);
        var cutAtSpace = new string('a', 59) + " bcdef";

        Assert.Equal(exactly60, ChatSessionTitle.From(exactly60));
        Assert.Equal(new string('a', 59) + "…", ChatSessionTitle.From(cutAtSpace));   // ký tự thứ 60 là dấu cách: bỏ rồi mới thêm "…"
        Assert.Equal(61, ChatSessionTitle.From(new string('b', 100))!.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public void Title_OfAnEmptyQuestion_IsNull(string? text) => Assert.Null(ChatSessionTitle.From(text));

    // ---- con trỏ

    [Fact]
    public void Cursor_RoundTrips_AndIsUrlSafe()
    {
        var cursor = new ChatHistoryCursor(new DateTime(2026, 11, 21, 8, 30, 15, 123, DateTimeKind.Utc).AddTicks(4560), Id(7));

        var text = cursor.Encode();

        Assert.Equal(cursor, ChatHistoryCursor.Decode(text));
        Assert.DoesNotContain('+', text);
        Assert.DoesNotContain('/', text);
        Assert.DoesNotContain('=', text);
    }

    [Theory]
    [InlineData("khong-phai-base64!!")]
    [InlineData("YWJj")]                  // "abc": không có dấu |
    [InlineData("MTIzfHh5eg")]            // "123|xyz": mã không phải Guid
    [InlineData("LTF8MDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDE")]   // số tick âm
    public void Cursor_ThatWasNotProducedByTheServer_IsRejected(string text) =>
        Assert.Throws<ArgumentException>(() => ChatHistoryCursor.Decode(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Cursor_Missing_MeansTheFirstPage(string? text) => Assert.Null(ChatHistoryCursor.Decode(text));

    // ---- truy vấn danh sách phiên

    private static (List<ChatSession> Sessions, List<ChatMessageEntity> Messages) Data()
    {
        var sessions = new List<ChatSession>
        {
            Session(1, Me, At(1), At(3), "Học phí Harvard"),     // trao đổi gần nhất ngày 3
            Session(2, Me, At(2), null, "Ngành CS"),             // chưa có UpdatedAt: dùng CreatedAt (ngày 2)
            Session(3, Me, At(5), At(5)),                        // không có tin nào (tạo trước khi bật lưu tin): không hiện
            Session(4, Other, At(4), At(6), "Của người khác"),   // của người khác: không bao giờ hiện
            Session(5, Me, At(4), At(4, 9)),                     // không có tiêu đề
        };
        var messages = new List<ChatMessageEntity>
        {
            Msg(1, 1, "user", At(1)), Msg(2, 1, "assistant", At(1, 12, 1)), Msg(3, 1, "user", At(3)), Msg(4, 1, "assistant", At(3, 12, 1)),
            Msg(5, 2, "user", At(2)), Msg(6, 2, "assistant", At(2, 12, 1)),
            Msg(7, 4, "user", At(4)), Msg(8, 4, "assistant", At(4, 12, 1)),
            Msg(9, 5, "user", At(4, 9)), Msg(10, 5, "system", At(4, 9, 1)),
        };
        return (sessions, messages);
    }

    [Fact]
    public void Sessions_AreOnlyMineAndOnlyThoseWithMessages_LatestActivityFirst()
    {
        var (sessions, messages) = Data();

        var rows = ChatHistoryQuery.Sessions(sessions.AsQueryable(), messages.AsQueryable(), Me, null, 10).ToList();

        Assert.Equal([Id(5), Id(1), Id(2)], rows.Select(r => r.Id));   // ngày 4 (9h), ngày 3, ngày 2
        Assert.Equal([2, 4, 2], rows.Select(r => r.MessageCount));
        Assert.Equal(At(2), rows[2].LastActivityAt);                    // phiên chưa có UpdatedAt dùng CreatedAt
    }

    [Fact]
    public void Sessions_WithTheSameActivityTime_AreOrderedById_SoTheCursorNeverSkipsOrRepeats()
    {
        var sessions = Enumerable.Range(1, 5).Select(n => Session(n, Me, At(1), At(9))).ToList();
        var messages = Enumerable.Range(1, 5).Select(n => Msg(n, n, "user", At(1))).ToList();

        var first = ChatHistoryQuery.Sessions(sessions.AsQueryable(), messages.AsQueryable(), Me, null, 2).ToList();
        var cursor = new ChatHistoryCursor(first[^1].LastActivityAt, first[^1].Id);
        var rest = ChatHistoryQuery.Sessions(sessions.AsQueryable(), messages.AsQueryable(), Me, cursor, 10).ToList();

        Assert.Equal(5, first.Concat(rest).Select(r => r.Id).Distinct().Count());
        Assert.Empty(first.Select(r => r.Id).Intersect(rest.Select(r => r.Id)));
    }

    // ---- dịch vụ (kho dữ liệu chạy đúng truy vấn thật trên danh sách trong bộ nhớ)

    private sealed class MemoryRepository(List<ChatSession> sessions, List<ChatMessageEntity> messages) : IChatHistoryRepository
    {
        public Task<IReadOnlyList<ChatSessionRow>> SessionsAsync(Guid userId, ChatHistoryCursor? after, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ChatSessionRow>>(ChatHistoryQuery.Sessions(sessions.AsQueryable(), messages.AsQueryable(), userId, after, take).ToList());

        public Task<(bool Found, string? Title)> SessionTitleAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
        {
            var s = ChatHistoryQuery.Active(sessions.AsQueryable(), sessionId, userId).FirstOrDefault();
            return Task.FromResult(s is null ? (false, (string?)null) : (true, s.Title));
        }

        public Task<IReadOnlyList<ChatHistoryMessageRow>> MessagesAsync(Guid sessionId, ChatHistoryCursor? before, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ChatHistoryMessageRow>>(ChatHistoryQuery.Messages(messages.AsQueryable(), sessionId, before, take).ToList());

        public Task<bool> RenameAsync(Guid sessionId, Guid userId, string title, CancellationToken ct = default)
        {
            var s = ChatHistoryQuery.Active(sessions.AsQueryable(), sessionId, userId).FirstOrDefault();
            if (s is not null)
                s.Title = title;
            return Task.FromResult(s is not null);
        }

        public Task<bool> SoftDeleteAsync(Guid sessionId, Guid userId, DateTime deletedAtUtc, CancellationToken ct = default)
        {
            var s = ChatHistoryQuery.Active(sessions.AsQueryable(), sessionId, userId).FirstOrDefault();
            if (s is not null)
                s.DeletedAt = deletedAtUtc;
            return Task.FromResult(s is not null);
        }
    }

    private static ChatHistoryService Service()
    {
        var (sessions, messages) = Data();
        return new ChatHistoryService(new MemoryRepository(sessions, messages));
    }

    [Fact]
    public async Task ListSessions_PagesWithACursor_UntilTheEnd_WithNoDuplicates()
    {
        var service = Service();

        var first = await service.ListSessionsAsync(Me, null, 2);
        var second = await service.ListSessionsAsync(Me, first.NextCursor, 2);

        Assert.Equal([Id(5), Id(1)], first.Items.Select(s => s.Id));
        Assert.NotNull(first.NextCursor);
        Assert.Equal([Id(2)], second.Items.Select(s => s.Id));
        Assert.Null(second.NextCursor);   // hết
    }

    [Fact]
    public async Task ListSessions_ASessionWithoutATitle_GetsTheDefaultName()
    {
        var page = await Service().ListSessionsAsync(Me, null, 10);

        Assert.Equal(ChatSessionTitle.Untitled, page.Items.Single(s => s.Id == Id(5)).Title);
        Assert.Equal("Học phí Harvard", page.Items.Single(s => s.Id == Id(1)).Title);
    }

    [Fact]
    public async Task ListSessions_LimitIsClamped_AndABadCursorIsRejected()
    {
        var service = Service();

        Assert.Single((await service.ListSessionsAsync(Me, null, 0)).Items);
        Assert.Equal(3, (await service.ListSessionsAsync(Me, null, 10_000)).Items.Count);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListSessionsAsync(Me, "rác", 10));
    }

    [Fact]
    public async Task GetMessages_OfSomeoneElsesSessionOrAMissingOne_IsNotFound_TheSameWay()
    {
        var service = Service();

        Assert.Null(await service.GetMessagesAsync(Me, Id(4), null, 50));      // phiên của người khác
        Assert.Null(await service.GetMessagesAsync(Me, Id(999), null, 50));    // không có phiên này
        Assert.NotNull(await service.GetMessagesAsync(Other, Id(4), null, 50)); // chủ phiên thì xem được
    }

    [Fact]
    public async Task GetMessages_AreOldestFirst_AndTheOlderCursorLoadsTheEarlierOnes()
    {
        var service = Service();

        var latest = await service.GetMessagesAsync(Me, Id(1), null, 2);
        var earlier = await service.GetMessagesAsync(Me, Id(1), latest!.OlderCursor, 2);

        Assert.Equal([Id(1003), Id(1004)], latest.Items.Select(m => m.Id));    // hai tin mới nhất, cũ trước mới sau
        Assert.NotNull(latest.OlderCursor);
        Assert.Equal([Id(1001), Id(1002)], earlier!.Items.Select(m => m.Id));
        Assert.Null(earlier.OlderCursor);                                       // đã tới tin đầu tiên
        Assert.Equal("Học phí Harvard", latest.Title);
    }

    [Fact]
    public async Task GetMessages_ReplacesTheTechnicalErrorRow_WithAPlainSentence()
    {
        var items = (await Service().GetMessagesAsync(Me, Id(5), null, 50))!.Items;

        var system = Assert.Single(items, m => m.Role == "system");
        Assert.Equal(ChatHistoryService.SystemFailureText, system.Content);
    }

    [Fact]
    public async Task GetMessages_ReadsTheCitations_AndIgnoresBrokenOnes()
    {
        var sessions = new List<ChatSession> { Session(1, Me, At(1)) };
        var messages = new List<ChatMessageEntity>
        {
            Msg(1, 1, "assistant", At(1), sources: """[{"title":"Kho kiến thức","url":"https://a.edu","retrievedAt":"2026-11-01"},{"url":"thiếu tiêu đề"}]"""),
            Msg(2, 1, "assistant", At(1, 13), sources: "{không phải json"),
        };
        var items = (await new ChatHistoryService(new MemoryRepository(sessions, messages)).GetMessagesAsync(Me, Id(1), null, 50))!.Items;

        var citation = Assert.Single(items[0].Citations);
        Assert.Equal(("Kho kiến thức", "https://a.edu", new DateOnly(2026, 11, 1)), (citation.Title, citation.Url, citation.RetrievedAt));
        Assert.Empty(items[1].Citations);
    }

    [Fact]
    public async Task GetMessages_ABadCursorIsRejected_EvenForASessionThatDoesNotExist()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().GetMessagesAsync(Me, Id(999), "rác", 50));
    }

    // ---- truy vấn dịch ra SQL được

    [Fact]
    public void Queries_TranslateToSql_FilteringByOwnerInTheDatabase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);
        var cursor = new ChatHistoryCursor(At(1), Id(1));

        var sessions = ChatHistoryQuery.Sessions(db.ChatSessions, db.ChatMessages, Me, cursor, 20).ToQueryString();
        var messages = ChatHistoryQuery.Messages(db.ChatMessages, Id(1), cursor, 100).ToQueryString();

        Assert.Contains("user_id", sessions);
        Assert.Contains("EXISTS", sessions);   // chỉ phiên có tin
        Assert.Contains("COALESCE", sessions); // lần trao đổi gần nhất
        Assert.Contains("chat_session_id", messages);
        Assert.Contains("ORDER BY", messages);
    }

    // ---- controller

    private sealed class StubHistory(ChatHistoryMessagesDto? messages = null, Exception? fail = null) : IChatHistoryService
    {
        public Guid? AskedUser { get; private set; }

        public Task<ChatSessionPageDto> ListSessionsAsync(Guid userId, string? cursor, int limit, CancellationToken ct = default)
        {
            AskedUser = userId;
            return fail is not null ? throw fail : Task.FromResult(new ChatSessionPageDto([], null));
        }

        public Task<ChatHistoryMessagesDto?> GetMessagesAsync(Guid userId, Guid sessionId, string? before, int limit, CancellationToken ct = default)
        {
            AskedUser = userId;
            return fail is not null ? throw fail : Task.FromResult(messages);
        }

        public string? RenameResult { get; init; } = "Tên mới";
        public bool DeleteResult { get; init; } = true;

        public Task<string?> RenameAsync(Guid userId, Guid sessionId, string? title, CancellationToken ct = default)
        {
            AskedUser = userId;
            return fail is not null ? throw fail : Task.FromResult(RenameResult);
        }

        public Task<bool> DeleteAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
        {
            AskedUser = userId;
            return Task.FromResult(DeleteResult);
        }
    }

    private sealed class StubPending(PendingQuestionDto? result = null) : IChatPendingQuestion
    {
        public (Guid User, Guid Session, bool ExactBudget)? Asked { get; private set; }

        public Task<PendingQuestionDto?> PendingQuestionAsync(Guid userId, Guid sessionId, bool exactBudget, CancellationToken ct = default)
        {
            Asked = (userId, sessionId, exactBudget);
            return Task.FromResult(result);
        }
    }

    private static ChatHistoryController ControllerFor(StubHistory history, Guid? userId, StubPending? pending = null, Guid? actingParent = null)
    {
        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString()) };
            if (actingParent is { } parent)
                claims.Add(new Claim(ActingSessionGuard.ActorClaim, $"{{\"sub\":\"{parent}\"}}"));   // token nhập hộ: act = {"sub": mã phụ huynh}
            http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }
        return new ChatHistoryController(history, pending ?? new StubPending()) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    [Fact]
    public void Controller_RequiresTheSameAgePolicyAsTheChat()
    {
        var authorize = typeof(ChatHistoryController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.Equal(AgeFeatures.ProcessStudentData, authorize!.Policy);
        Assert.Equal("api/v1/advisor/sessions", typeof(ChatHistoryController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    [Fact]
    public async Task Controller_UsesTheLoggedInUser_AndTheResponseIsNeverCached()
    {
        var history = new StubHistory();
        var controller = ControllerFor(history, Me);

        var result = await controller.List(null);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(Me, history.AskedUser);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Controller_WithoutALoggedInUser_IsUnauthorized()
    {
        var history = new StubHistory();

        Assert.IsType<UnauthorizedResult>((await ControllerFor(history, null).List(null)).Result);
        Assert.IsType<UnauthorizedResult>((await ControllerFor(history, null).Messages(Id(1), null)).Result);
        Assert.Null(history.AskedUser);
    }

    [Fact]
    public async Task Controller_AMissingOrForeignSession_IsNotFound_AndABadCursorIsABadRequest()
    {
        var notFound = await ControllerFor(new StubHistory(messages: null), Me).Messages(Id(1), null);
        var bad = await ControllerFor(new StubHistory(fail: new ArgumentException("rác")), Me).List("rác");

        Assert.IsType<NotFoundResult>(notFound.Result);
        Assert.IsType<BadRequestObjectResult>(bad.Result);
    }

    // ---- câu hồ sơ đang chờ

    [Fact]
    public async Task Pending_ReturnsTheRebuiltQuestion_ForTheLoggedInUser_NeverCached()
    {
        var question = new PendingQuestionDto(true, "F1", "Gia đình bạn chi bao nhiêu?", [new QuickReply("Dưới 20.000 USD", "Dưới 20.000 USD")], new Widget("budget"));
        var pending = new StubPending(question);
        var controller = ControllerFor(new StubHistory(), Me, pending);

        var result = await controller.Pending(Id(1));

        Assert.Same(question, Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal((Me, Id(1), false), pending.Asked);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Pending_AParentEnteringForTheirChild_AsksTheExactBudget_LikeTheChat()
    {
        var pending = new StubPending(PendingQuestionDto.None);

        await ControllerFor(new StubHistory(), Me, pending, actingParent: Other).Pending(Id(1));

        Assert.True(pending.Asked!.Value.ExactBudget);
    }

    [Fact]
    public async Task Pending_ForAMissingOrForeignSession_IsNotFound_AndWithoutAUserIsUnauthorized()
    {
        Assert.IsType<NotFoundResult>((await ControllerFor(new StubHistory(), Me, new StubPending(null)).Pending(Id(1))).Result);
        Assert.IsType<UnauthorizedResult>((await ControllerFor(new StubHistory(), null).Pending(Id(1))).Result);
    }

    // ---- đổi tên

    [Fact]
    public void UserTitle_IsCleanedButNeverCut_AndAnEmptyOrTooLongNameIsRejected()
    {
        Assert.Equal("Du học Mỹ ngành CS", ChatSessionTitle.FromUser("  Du học   Mỹ\nngành CS "));
        Assert.Equal(100, ChatSessionTitle.FromUser(new string('a', 100)).Length);
        Assert.Throws<ArgumentException>(() => ChatSessionTitle.FromUser("   "));
        Assert.Throws<ArgumentException>(() => ChatSessionTitle.FromUser(null));
        Assert.Throws<ArgumentException>(() => ChatSessionTitle.FromUser(new string('a', 101)));
    }

    [Fact]
    public async Task Rename_SavesTheCleanedName_WithoutMovingTheSessionInTheList()
    {
        var (sessions, messages) = Data();
        var service = new ChatHistoryService(new MemoryRepository(sessions, messages));
        var orderBefore = (await service.ListSessionsAsync(Me, null, 10)).Items.Select(s => s.Id).ToList();

        var saved = await service.RenameAsync(Me, Id(2), "  Ngành   CS ở Texas ");

        Assert.Equal("Ngành CS ở Texas", saved);
        var after = (await service.ListSessionsAsync(Me, null, 10)).Items;
        Assert.Equal("Ngành CS ở Texas", after.Single(s => s.Id == Id(2)).Title);
        Assert.Equal(orderBefore, after.Select(s => s.Id));   // đổi tên không phải một lần trao đổi: thứ tự giữ nguyên
    }

    [Fact]
    public async Task Rename_SomeoneElsesOrADeletedSession_IsNotFound_AndABadNameIsRejectedFirst()
    {
        var (sessions, messages) = Data();
        sessions.Single(s => s.Id == Id(2)).DeletedAt = At(10);
        var service = new ChatHistoryService(new MemoryRepository(sessions, messages));

        Assert.Null(await service.RenameAsync(Me, Id(4), "Lấy phiên người khác"));
        Assert.Null(await service.RenameAsync(Me, Id(2), "Phiên đã xóa"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RenameAsync(Me, Id(999), ""));
        Assert.Equal("Của người khác", sessions.Single(s => s.Id == Id(4)).Title);
    }

    // ---- xóa mềm

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task Delete_HidesTheSessionFromTheListAndFromReading_ButKeepsItsMessages()
    {
        var (sessions, messages) = Data();
        var now = new DateTimeOffset(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);
        var service = new ChatHistoryService(new MemoryRepository(sessions, messages), new FixedClock(now));

        Assert.True(await service.DeleteAsync(Me, Id(1)));

        Assert.DoesNotContain((await service.ListSessionsAsync(Me, null, 10)).Items, s => s.Id == Id(1));
        Assert.Null(await service.GetMessagesAsync(Me, Id(1), null, 50));          // không mở lại được
        Assert.Equal(now.UtcDateTime, sessions.Single(s => s.Id == Id(1)).DeletedAt);
        Assert.Equal(4, messages.Count(m => m.ChatSessionId == Id(1)));              // xóa mềm: tin nhắn còn nguyên
    }

    [Fact]
    public async Task Delete_SomeoneElsesSession_OrTheSameOneTwice_IsNotFound()
    {
        var (sessions, messages) = Data();
        var service = new ChatHistoryService(new MemoryRepository(sessions, messages));

        Assert.False(await service.DeleteAsync(Me, Id(4)));
        Assert.Null(sessions.Single(s => s.Id == Id(4)).DeletedAt);
        Assert.True(await service.DeleteAsync(Me, Id(1)));
        Assert.False(await service.DeleteAsync(Me, Id(1)));
    }

    [Fact]
    public void ActiveSession_IsOnlyTheOwnersAndNotDeleted_AndTranslatesToSql()
    {
        var sessions = new List<ChatSession>
        {
            Session(1, Me, At(1)),
            Session(2, Me, At(1)),
            Session(3, Other, At(1)),
        };
        sessions[1].DeletedAt = At(2);

        Assert.Single(ChatHistoryQuery.Active(sessions.AsQueryable(), Id(1), Me));
        Assert.Empty(ChatHistoryQuery.Active(sessions.AsQueryable(), Id(2), Me));   // đã xóa
        Assert.Empty(ChatHistoryQuery.Active(sessions.AsQueryable(), Id(3), Me));   // của người khác

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);
        var sql = ChatHistoryQuery.Active(db.ChatSessions, Id(1), Me).ToQueryString();
        Assert.Contains("deleted_at IS NULL", sql);
        Assert.Contains("user_id", sql);
    }

    // ---- controller: đổi tên, xóa

    [Fact]
    public async Task Controller_Rename_ReturnsTheSavedName_NotFound_OrBadRequest()
    {
        var ok = await ControllerFor(new StubHistory(), Me).Rename(Id(1), new RenameChatSessionRequest("Tên mới"));
        var missing = await ControllerFor(new StubHistory { RenameResult = null }, Me).Rename(Id(1), new RenameChatSessionRequest("Tên mới"));
        var bad = await ControllerFor(new StubHistory(fail: new ArgumentException("rỗng")), Me).Rename(Id(1), new RenameChatSessionRequest(""));
        var anonymous = await ControllerFor(new StubHistory(), null).Rename(Id(1), new RenameChatSessionRequest("x"));

        Assert.Equal("Tên mới", Assert.IsType<RenamedChatSessionDto>(Assert.IsType<OkObjectResult>(ok.Result).Value).Title);
        Assert.IsType<NotFoundResult>(missing.Result);
        Assert.IsType<BadRequestObjectResult>(bad.Result);
        Assert.IsType<UnauthorizedResult>(anonymous.Result);
    }

    [Fact]
    public async Task Controller_Delete_IsNoContent_OrNotFound()
    {
        Assert.IsType<NoContentResult>(await ControllerFor(new StubHistory(), Me).Delete(Id(1)));
        Assert.IsType<NotFoundResult>(await ControllerFor(new StubHistory { DeleteResult = false }, Me).Delete(Id(1)));
        Assert.IsType<UnauthorizedResult>(await ControllerFor(new StubHistory(), null).Delete(Id(1)));
    }
}
