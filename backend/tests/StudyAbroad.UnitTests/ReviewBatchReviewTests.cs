using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.ChatReview;
using StudyAbroad.Application.Admin.ReviewBatches;
using StudyAbroad.Application.Audit;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;

namespace StudyAbroad.UnitTests;

// Xem danh sách đợt, đọc từng câu (có nhật ký) và lưu kết luận; controller (tab 2, bước 2c).
public class ReviewBatchReviewTests
{
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");
    private static DateTime Day(int day, int hour = 12) => new(2026, 11, day, hour, 0, 0, DateTimeKind.Utc);

    private static readonly Guid Me = Id(801);
    private static readonly Guid Other = Id(802);
    private static readonly Guid Batch = Id(50);
    private static readonly Guid Item = Id(60);
    private const string Secret = "GPA em 3.6 muốn học Marine Biology ở Hawaii";

    // ================================================================= bản giả

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

    private sealed class FakeRepo : IReviewBatchRepository
    {
        public List<ReviewBatchListRow> BatchRows { get; init; } = [];
        public ReviewItemsRows? ItemsRows { get; init; }
        public ReviewItemDetailRows? Detail { get; init; }
        public ReviewVerdict? Saved { get; init; }
        public int DetailQueries { get; private set; }
        public int UpsertCalls { get; private set; }
        public (Guid Batch, Guid Item, Guid Reviewer, string Label, string? Intent, string Answer, string? Cause)? Upserted { get; private set; }
        public (Guid Batch, Guid Item, Guid Reviewer, int Context)? AskedDetail { get; private set; }

        public Task<IReadOnlyList<ReviewBatchListRow>> ListForUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ReviewBatchListRow>>(BatchRows);

        public Task<ReviewItemsRows?> ListItemsAsync(Guid batchId, Guid reviewerUserId, CancellationToken ct = default) =>
            Task.FromResult(ItemsRows);

        public Task<ReviewItemDetailRows?> FindItemDetailAsync(Guid batchId, Guid itemId, Guid reviewerUserId, int contextCount, CancellationToken ct = default)
        {
            DetailQueries++;
            AskedDetail = (batchId, itemId, reviewerUserId, contextCount);
            return Task.FromResult(Detail);
        }

        public Task<ReviewVerdict?> UpsertVerdictAsync(
            Guid batchId, Guid itemId, Guid reviewerUserId, string labelVerdict, string? correctIntent, string answerVerdict, string? cause,
            CancellationToken ct = default)
        {
            UpsertCalls++;
            Upserted = (batchId, itemId, reviewerUserId, labelVerdict, correctIntent, answerVerdict, cause);
            return Task.FromResult(Saved);
        }

        // Không dùng ở nhóm test này
        public Task<IReadOnlyList<ReviewCandidate>> ListCandidatesAsync(DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> FindActiveAdminIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task AddAsync(ReviewBatch batch, IReadOnlyList<ReviewBatchItem> items, IReadOnlyList<ReviewBatchReviewer> reviewers, AuditLog audit, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static ReviewBatchService Svc(FakeRepo repo, FakeAudit? audit = null) => new(repo, audit ?? new FakeAudit());

    private static ReviewItemDetailRows DetailRows(ReviewVerdict? mine = null, bool withQuestion = true)
    {
        var reply = new ChatReviewReplyRow(Id(202), Id(101), Id(7), Day(2, 12), "assistant", "answered", "", "Câu trả lời " + Secret, null, Id(201));
        var question = withQuestion
            ? new ChatReviewQuestionRow(
                Id(201), Day(2, 11), "Câu hỏi " + Secret,
                "{\"v\":1,\"slm\":{\"modelIntents\":[{\"type\":\"knowledge\",\"span\":\"" + Secret + "\",\"entities\":{}}],\"intents\":[\"knowledge\"],\"confidence\":1,\"fallback\":false}}")
            : null;
        // Cố ý đưa ngữ cảnh KHÔNG theo thứ tự thời gian: dịch vụ phải sắp lại
        var context = new List<ChatReviewContextRow>
        {
            new(Id(199), "assistant", Day(2, 10), "tin sau"),
            new(Id(198), "user", Day(2, 9), "tin trước"),
        };
        return new ReviewItemDetailRows(Item, 3, "scenario", reply, question, context, mine);
    }

    // ================================================================= danh sách đợt

    [Fact]
    public async Task List_MapsTheBatches_WithMyOwnProgress_AndTheStrata()
    {
        var strata = ReviewSampler.StrataToJson([new StratumCount("scenario", 40, 10)]);
        var repo = new FakeRepo
        {
            BatchRows =
            [
                new ReviewBatchListRow(
                    Batch, "Đợt 1", "abc123", Day(3), 10, Day(1), Day(9), strata, 21, true, true,
                    [new ReviewerProgressRow(Me, "Admin A", 7), new ReviewerProgressRow(Other, "Admin B", 12)]),
            ],
        };

        var list = await Svc(repo).ListAsync(Me);

        var batch = Assert.Single(list);
        Assert.Equal((Batch, "Đợt 1", "abc123", 21, true, true), (batch.Id, batch.Name, batch.Seed, batch.TotalItems, batch.IsCreator, batch.IsReviewer));
        Assert.Equal(7, batch.MyGraded);   // của chính người xem, không phải của người kia
        Assert.Equal([new StratumCount("scenario", 40, 10)], batch.Strata);
        Assert.Equal([("Admin A", 7), ("Admin B", 12)], batch.Reviewers.Select(r => (r.FullName, r.Graded)));
    }

    [Fact]
    public async Task List_ForACreatorWhoIsNotAReviewer_HasNoGradedAndIsNotAReviewer()
    {
        var repo = new FakeRepo
        {
            BatchRows =
            [
                new ReviewBatchListRow(Batch, "Đợt 1", "s", Day(3), 10, null, null, "[]", 5, true, false, [new ReviewerProgressRow(Other, "Admin B", 3)]),
            ],
        };

        var batch = Assert.Single(await Svc(repo).ListAsync(Me));

        Assert.Equal((true, false, 0), (batch.IsCreator, batch.IsReviewer, batch.MyGraded));
    }

    [Fact]
    public async Task List_WithBrokenStrataJson_StillListsTheBatch()
    {
        var repo = new FakeRepo { BatchRows = [new ReviewBatchListRow(Batch, "Đợt 1", "s", Day(3), 10, null, null, "hỏng", 5, true, true, [])] };

        var batch = Assert.Single(await Svc(repo).ListAsync(Me));

        Assert.Empty(batch.Strata);
    }

    [Fact]
    public void TheListDtos_CarryNoChatContent_NorWhoAskedTheQuestion()
    {
        var names = new[] { typeof(ReviewBatchListItemDto), typeof(ReviewItemListDto), typeof(ReviewItemRowDto), typeof(ReviewerProgressDto) }
            .SelectMany(t => t.GetProperties().Select(p => p.Name));

        Assert.DoesNotContain(names, n => n is "Content" or "Text" or "Question" or "Answer" or "Reply" or "ChatMessageId" or "SessionId");
    }

    // ================================================================= các câu của đợt

    [Fact]
    public async Task Items_AreMapped_WithTheCountOfMyGradedOnes()
    {
        var repo = new FakeRepo
        {
            ItemsRows = new ReviewItemsRows(Batch, "Đợt 1",
            [
                new ReviewItemRow(Id(61), 1, "scenario", true),
                new ReviewItemRow(Id(62), 2, "knowledge", false),
                new ReviewItemRow(Id(63), 3, "scenario", true),
            ]),
        };

        var items = (await Svc(repo).GetItemsAsync(Batch, Me))!;

        Assert.Equal((Batch, "Đợt 1", 3, 2), (items.BatchId, items.BatchName, items.TotalItems, items.MyGraded));
        Assert.Equal([1, 2, 3], items.Items.Select(i => i.Position));
        Assert.Equal([true, false, true], items.Items.Select(i => i.GradedByMe));
    }

    [Fact]
    public async Task Items_OfABatchTheUserIsNotAssignedTo_AreNull()
    {
        Assert.Null(await Svc(new FakeRepo { ItemsRows = null }).GetItemsAsync(Batch, Me));
    }

    // ================================================================= xem một câu (có nhật ký)

    [Fact]
    public async Task Detail_WritesTheAuditBeforeReturning_WithIdsOnly_NoChatContent()
    {
        var audit = new FakeAudit();
        var repo = new FakeRepo { Detail = DetailRows() };

        var detail = await Svc(repo, audit).GetItemDetailAsync(Batch, Item, Me, "10.0.0.5", "Mozilla");

        Assert.NotNull(detail);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal((Me, ReviewBatchService.ViewItemAction, "chat_message", Id(202).ToString()), (entry.ActorUserId, entry.Action, entry.EntityType, entry.EntityId));
        Assert.Equal(("10.0.0.5", "Mozilla"), (entry.IpAddress, entry.UserAgent));
        Assert.DoesNotContain("Hawaii", entry.DetailJson!);
        Assert.DoesNotContain("Câu hỏi", entry.DetailJson!);
        using var json = JsonDocument.Parse(entry.DetailJson!);
        Assert.Equal(Batch, json.RootElement.GetProperty("batchId").GetGuid());
        Assert.Equal(Item, json.RootElement.GetProperty("itemId").GetGuid());
    }

    [Fact]
    public async Task Detail_WhenTheAuditCannotBeWritten_Throws_AndNoContentLeaves()
    {
        var repo = new FakeRepo { Detail = DetailRows() };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Svc(repo, new FakeAudit { Fail = true }).GetItemDetailAsync(Batch, Item, Me, null, null));
    }

    [Fact]
    public async Task Detail_OfAnItemTheUserCannotSee_IsNull_AndWritesNoAudit()
    {
        var audit = new FakeAudit();

        var detail = await Svc(new FakeRepo { Detail = null }, audit).GetItemDetailAsync(Batch, Item, Me, null, null);

        Assert.Null(detail);
        Assert.Empty(audit.Entries);   // không có gì được xem thì không có gì để ghi
    }

    [Fact]
    public async Task Detail_WithoutAnActor_Throws_AndQueriesNothing()
    {
        var repo = new FakeRepo { Detail = DetailRows() };

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).GetItemDetailAsync(Batch, Item, Guid.Empty, null, null));

        Assert.Equal(0, repo.DetailQueries);
    }

    [Fact]
    public async Task Detail_AsksTheRepositoryForTheRightItemAndReviewer_WithSixContextMessages()
    {
        var repo = new FakeRepo { Detail = DetailRows() };

        await Svc(repo).GetItemDetailAsync(Batch, Item, Me, null, null);

        Assert.Equal((Batch, Item, Me, 6), repo.AskedDetail);
    }

    [Fact]
    public async Task Detail_ReturnsTheContentAndTheModelLabel_ContextInTimeOrder_AndNoWhoIsTheStudent()
    {
        var repo = new FakeRepo { Detail = DetailRows() };

        var detail = (await Svc(repo).GetItemDetailAsync(Batch, Item, Me, null, null))!;

        Assert.Equal((Item, 3, "scenario"), (detail.ItemId, detail.Position, detail.Stratum));
        Assert.Contains("Hawaii", detail.Question!.Content);
        Assert.Contains("Hawaii", detail.Reply.Content);
        Assert.Equal(1.0, detail.Question.Model!.Confidence);
        Assert.Equal(["knowledge"], detail.Question.Model.ModelIntents.Select(i => i.Type));
        Assert.Equal(["tin trước", "tin sau"], detail.Context.Select(c => c.Content));
        var names = typeof(ReviewItemDetailDto).GetProperties().Select(p => p.Name)
            .Concat(typeof(ChatReviewMessageDto).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain(names, n => n is "UserId" or "SessionId" or "OwnerUserId");
    }

    [Fact]
    public async Task Detail_WithoutAQuestion_HasNoQuestion_ButStillTheReply()
    {
        var repo = new FakeRepo { Detail = DetailRows(withQuestion: false) };

        var detail = (await Svc(repo).GetItemDetailAsync(Batch, Item, Me, null, null))!;

        Assert.Null(detail.Question);
        Assert.Contains("Hawaii", detail.Reply.Content);
    }

    [Fact]
    public async Task Detail_CarriesOnlyMyOwnVerdict_AndNullIfIHaveNotGradedIt()
    {
        var mine = new ReviewVerdict { LabelVerdict = "wrong", CorrectIntent = "scenario", AnswerVerdict = "wrong", Cause = "hallucination", CreatedAt = Day(3), UpdatedAt = Day(4) };

        var graded = (await Svc(new FakeRepo { Detail = DetailRows(mine) }).GetItemDetailAsync(Batch, Item, Me, null, null))!;
        var notYet = (await Svc(new FakeRepo { Detail = DetailRows() }).GetItemDetailAsync(Batch, Item, Me, null, null))!;

        Assert.Equal(("wrong", "scenario", "wrong", "hallucination", Day(4)), (graded.MyVerdict!.LabelVerdict, graded.MyVerdict.CorrectIntent, graded.MyVerdict.AnswerVerdict, graded.MyVerdict.Cause, graded.MyVerdict.UpdatedAt));
        Assert.Null(notYet.MyVerdict);
    }

    [Fact]
    public async Task Detail_ClipsTheAddressAndBrowserToTheAuditColumns()
    {
        var audit = new FakeAudit();

        await Svc(new FakeRepo { Detail = DetailRows() }, audit).GetItemDetailAsync(Batch, Item, Me, new string('1', 200), new string('u', 900));

        Assert.Equal((64, 512), (audit.Entries.Single().IpAddress!.Length, audit.Entries.Single().UserAgent!.Length));
    }

    // ================================================================= lưu kết luận

    private static ReviewVerdict SavedVerdict(string label = "correct", string? intent = null, string answer = "correct", string? cause = null) =>
        new() { LabelVerdict = label, CorrectIntent = intent, AnswerVerdict = answer, Cause = cause, CreatedAt = Day(3) };

    [Theory]
    [InlineData("correct", null, "correct", null)]
    [InlineData("correct", null, "out_of_scope", null)]
    [InlineData("correct", null, "missing_data", null)]
    [InlineData("wrong", null, "correct", null)]                          // nhãn sai nhưng không chắc nhãn đúng là gì
    [InlineData("wrong", "scenario", "wrong", "misread_intent")]
    [InlineData("wrong", "knowledge", "wrong", "hallucination")]
    [InlineData("correct", null, "wrong", "missing_knowledge")]
    [InlineData("correct", null, "wrong", "technical")]
    public async Task SaveVerdict_AcceptsTheValidCombinations_AndPassesThemToTheRepository(string label, string? intent, string answer, string? cause)
    {
        var repo = new FakeRepo { Saved = SavedVerdict(label, intent, answer, cause) };

        var saved = await Svc(repo).SaveVerdictAsync(Batch, Item, new SaveReviewVerdictRequest(label, intent, answer, cause), Me);

        Assert.NotNull(saved);
        Assert.Equal((Batch, Item, Me, label, intent, answer, cause), repo.Upserted);
        Assert.Equal((label, intent, answer, cause), (saved.LabelVerdict, saved.CorrectIntent, saved.AnswerVerdict, saved.Cause));
    }

    [Theory]
    [InlineData(null, null, "correct", null)]
    [InlineData("", null, "correct", null)]
    [InlineData("maybe", null, "correct", null)]                          // nhãn lạ
    [InlineData("correct", null, null, null)]
    [InlineData("correct", null, "maybe", null)]                          // đánh giá lạ
    [InlineData("correct", "scenario", "correct", null)]                  // nhãn đúng khi nhãn SLM đã đúng
    [InlineData("wrong", "không_có_loại_này", "correct", null)]           // nhãn đúng không thuộc sáu loại ý
    [InlineData("correct", null, "wrong", null)]                          // sai mà không nói vì sao
    [InlineData("correct", null, "wrong", "lạ")]                          // nguyên nhân lạ
    [InlineData("correct", null, "correct", "hallucination")]             // nguyên nhân khi câu trả lời đúng
    [InlineData("correct", null, "missing_data", "technical")]
    public async Task SaveVerdict_RejectsTheInvalidCombinations_AndSavesNothing(string? label, string? intent, string? answer, string? cause)
    {
        var repo = new FakeRepo { Saved = SavedVerdict() };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Svc(repo).SaveVerdictAsync(Batch, Item, new SaveReviewVerdictRequest(label, intent, answer, cause), Me));

        Assert.Equal(0, repo.UpsertCalls);
    }

    [Fact]
    public async Task SaveVerdict_TrimsTheValues_AndTreatsBlankOptionalOnesAsNone()
    {
        var repo = new FakeRepo { Saved = SavedVerdict() };

        await Svc(repo).SaveVerdictAsync(Batch, Item, new SaveReviewVerdictRequest(" correct ", "   ", " correct", ""), Me);

        Assert.Equal((Batch, Item, Me, "correct", null, "correct", null), repo.Upserted);
    }

    [Fact]
    public async Task SaveVerdict_OfAnItemTheUserCannotGrade_IsNull()
    {
        var repo = new FakeRepo { Saved = null };

        var saved = await Svc(repo).SaveVerdictAsync(Batch, Item, new SaveReviewVerdictRequest("correct", null, "correct", null), Me);

        Assert.Null(saved);
    }

    [Fact]
    public async Task SaveVerdict_WithoutAnActor_Throws_AndSavesNothing()
    {
        var repo = new FakeRepo { Saved = SavedVerdict() };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Svc(repo).SaveVerdictAsync(Batch, Item, new SaveReviewVerdictRequest("correct", null, "correct", null), Guid.Empty));

        Assert.Equal(0, repo.UpsertCalls);
    }

    [Fact]
    public async Task SaveVerdict_ReportsTheUpdateTimeIfItWasEdited_ElseTheCreationTime()
    {
        var edited = SavedVerdict();
        edited.UpdatedAt = Day(5);

        var a = await Svc(new FakeRepo { Saved = edited }).SaveVerdictAsync(Batch, Item, new SaveReviewVerdictRequest("correct", null, "correct", null), Me);
        var b = await Svc(new FakeRepo { Saved = SavedVerdict() }).SaveVerdictAsync(Batch, Item, new SaveReviewVerdictRequest("correct", null, "correct", null), Me);

        Assert.Equal((Day(5), Day(3)), (a!.UpdatedAt, b!.UpdatedAt));
    }

    [Fact]
    public async Task EverythingTheServiceAccepts_IsAlsoAcceptedByTheDatabaseChecks()
    {
        // Ràng buộc kiểm trong database (ReviewVerdictConfiguration), viết lại bằng C#
        static bool DatabaseAccepts(string label, string? intent, string answer, string? cause) =>
            ReviewLabelVerdicts.All.Contains(label)
            && ReviewAnswerVerdicts.All.Contains(answer)
            && (cause is null || ReviewCauses.All.Contains(cause))
            && (intent is null || label == "wrong")
            && (cause is null || answer == "wrong");

        string?[] labels = [.. ReviewLabelVerdicts.All, "lạ", null];
        string?[] intents = [.. SlmIntentTypes.All, "lạ", null];
        string?[] answers = [.. ReviewAnswerVerdicts.All, "lạ", null];
        string?[] causes = [.. ReviewCauses.All, "lạ", null];
        var checkedCount = 0;
        var accepted = 0;

        foreach (var label in labels)
        foreach (var intent in intents)
        foreach (var answer in answers)
        foreach (var cause in causes)
        {
            var repo = new FakeRepo { Saved = SavedVerdict() };
            var service = Svc(repo);
            var request = new SaveReviewVerdictRequest(label, intent, answer, cause);
            var serviceAccepts = true;
            try
            {
                await service.SaveVerdictAsync(Batch, Item, request, Me);
            }
            catch (ArgumentException)
            {
                serviceAccepts = false;
            }

            checkedCount++;
            if (serviceAccepts)
            {
                accepted++;
                Assert.True(DatabaseAccepts(label!, intent, answer!, cause), $"Dịch vụ nhận nhưng database sẽ từ chối: {label}/{intent}/{answer}/{cause}");
            }
        }

        Assert.True(checkedCount > 1000);
        Assert.True(accepted > 0);   // không phải dịch vụ từ chối hết nên "đúng" một cách tầm thường
    }

    // ================================================================= truy vấn nội dung

    private static ChatMessage Msg(int n, string role, string? outcome, string? review = null, int? replyTo = 1) => new()
    {
        Id = Id(n), ChatSessionId = Id(900), Role = role, CreatedAt = Day(2), Outcome = outcome, ReviewStatus = review,
        ReplyToMessageId = replyTo is { } r ? Id(r) : null, Content = "nội dung riêng tư",
    };

    [Fact]
    public void AnsweredReply_SelectsOnlyAnAnsweredAssistantMessage()
    {
        var messages = new[]
        {
            Msg(1, "assistant", "answered"),                          // được
            Msg(2, "user", null),                                    // tin của học sinh
            Msg(3, "assistant", "no_answer", review: "open"),        // thuộc hàng đợi (có đường riêng, có nhật ký riêng)
            Msg(4, "system", "failed", review: "open"),              // dòng lỗi
        }.AsQueryable();
        var sessions = new[] { new ChatSession { Id = Id(900), UserId = Id(7) } }.AsQueryable();

        Assert.Single(ReviewItemQuery.AnsweredReply(messages, sessions, Id(1)));
        Assert.Empty(ReviewItemQuery.AnsweredReply(messages, sessions, Id(2)));
        Assert.Empty(ReviewItemQuery.AnsweredReply(messages, sessions, Id(3)));
        Assert.Empty(ReviewItemQuery.AnsweredReply(messages, sessions, Id(4)));
        Assert.Empty(ReviewItemQuery.AnsweredReply(messages, sessions, Id(999)));
    }

    [Fact]
    public void AnsweredReply_TranslatesToSql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check").UseSnakeCaseNamingConvention().Options;
        using var db = new AppDbContext(options);

        var sql = ReviewItemQuery.AnsweredReply(db.ChatMessages, db.ChatSessions, Id(1)).ToQueryString();

        Assert.Contains("outcome", sql);
        Assert.Contains("JOIN", sql);
    }

    // ================================================================= controller

    private static AdminReviewBatchesController ControllerFor(IReviewBatchService service, Guid? actor, string? ip = "10.0.0.5", string? userAgent = "Mozilla")
    {
        var http = new DefaultHttpContext();
        if (actor is { } id)
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"));
        if (ip is not null)
            http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
        if (userAgent is not null)
            http.Request.Headers.UserAgent = userAgent;
        return new AdminReviewBatchesController(service) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    private sealed class FakeService : IReviewBatchService
    {
        public ReviewBatchCreatedDto? Created { get; init; }
        public Exception? Error { get; init; }
        public ReviewItemListDto? Items { get; init; }
        public ReviewItemDetailDto? Detail { get; init; }
        public ReviewVerdictDto? Verdict { get; init; }
        public Guid? AskedActor { get; private set; }
        public (Guid Batch, Guid Item, Guid Actor, string? Ip, string? Ua)? AskedDetail { get; private set; }

        public Task<ReviewBatchCreatedDto> CreateAsync(CreateReviewBatchRequest request, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default)
        {
            AskedActor = actorUserId;
            if (Error is not null)
                throw Error;
            return Task.FromResult(Created!);
        }

        public Task<IReadOnlyList<ReviewBatchListItemDto>> ListAsync(Guid actorUserId, CancellationToken ct = default)
        {
            AskedActor = actorUserId;
            return Task.FromResult<IReadOnlyList<ReviewBatchListItemDto>>([]);
        }

        public Task<ReviewItemListDto?> GetItemsAsync(Guid batchId, Guid actorUserId, CancellationToken ct = default)
        {
            AskedActor = actorUserId;
            return Task.FromResult(Items);
        }

        public Task<ReviewItemDetailDto?> GetItemDetailAsync(Guid batchId, Guid itemId, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default)
        {
            AskedDetail = (batchId, itemId, actorUserId, ipAddress, userAgent);
            return Task.FromResult(Detail);
        }

        public Task<ReviewVerdictDto?> SaveVerdictAsync(Guid batchId, Guid itemId, SaveReviewVerdictRequest request, Guid actorUserId, CancellationToken ct = default)
        {
            AskedActor = actorUserId;
            if (Error is not null)
                throw Error;
            return Task.FromResult(Verdict);
        }
    }

    private static readonly CreateReviewBatchRequest SampleRequest = new("Đợt 1", 10, null, null, [Me], null);
    private static readonly ReviewBatchCreatedDto SampleCreated = new(Batch, "Đợt 1", "abc", 10, null, null, 5, [], [Me], Day(3));

    [Fact]
    public async Task Controller_Create_Is201WithTheBatch_AndPassesTheActor()
    {
        var service = new FakeService { Created = SampleCreated };

        var result = await ControllerFor(service, Me).Create(SampleRequest, default);

        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal((201, SampleCreated), (response.StatusCode, response.Value));
        Assert.Equal(Me, service.AskedActor);
    }

    [Fact]
    public async Task Controller_Create_WithBadArguments_Is400WithTheReason()
    {
        var controller = ControllerFor(new FakeService { Error = new ArgumentException("Tên đợt sai") }, Me);

        var result = await controller.Create(SampleRequest, default);

        Assert.Contains("Tên đợt sai", Assert.IsType<BadRequestObjectResult>(result.Result).Value!.ToString());
    }

    [Fact]
    public async Task Controller_EveryAction_WithoutALoggedInUser_Is401_AndTheServiceIsNotAsked()
    {
        var service = new FakeService { Created = SampleCreated, Items = new ReviewItemListDto(Batch, "x", 0, 0, []) };
        var controller = ControllerFor(service, actor: null);

        Assert.IsType<UnauthorizedResult>((await controller.Create(SampleRequest, default)).Result);
        Assert.IsType<UnauthorizedResult>((await controller.List(default)).Result);
        Assert.IsType<UnauthorizedResult>((await controller.GetItems(Batch, default)).Result);
        Assert.IsType<UnauthorizedResult>((await controller.GetItem(Batch, Item, default)).Result);
        Assert.IsType<UnauthorizedResult>((await controller.SaveVerdict(Batch, Item, new SaveReviewVerdictRequest("correct", null, "correct", null), default)).Result);
        Assert.Null(service.AskedActor);
        Assert.Null(service.AskedDetail);
    }

    [Fact]
    public async Task Controller_List_PassesTheActor()
    {
        var service = new FakeService();

        var result = await ControllerFor(service, Me).List(default);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(Me, service.AskedActor);
    }

    [Fact]
    public async Task Controller_Items_Are404WhenNotAssigned_AndOkOtherwise()
    {
        var listed = new ReviewItemListDto(Batch, "Đợt 1", 0, 0, []);

        Assert.IsType<NotFoundResult>((await ControllerFor(new FakeService(), Me).GetItems(Batch, default)).Result);
        Assert.Same(listed, Assert.IsType<OkObjectResult>((await ControllerFor(new FakeService { Items = listed }, Me).GetItems(Batch, default)).Result).Value);
    }

    [Fact]
    public async Task Controller_Item_PassesTheActorAddressAndBrowserToTheAudit_And404WhenHidden()
    {
        var detail = new ReviewItemDetailDto(Item, 1, "scenario", null, new ChatReviewMessageDto(Id(202), "assistant", Day(2), "x"), [], null);
        var service = new FakeService { Detail = detail };

        var ok = await ControllerFor(service, Me).GetItem(Batch, Item, default);
        var hidden = await ControllerFor(new FakeService(), Me).GetItem(Batch, Item, default);

        Assert.Same(detail, Assert.IsType<OkObjectResult>(ok.Result).Value);
        Assert.Equal((Batch, Item, Me, "10.0.0.5", "Mozilla"), service.AskedDetail);
        Assert.IsType<NotFoundResult>(hidden.Result);
    }

    [Fact]
    public async Task Controller_SaveVerdict_Is200_400OnBadRules_And404WhenNotAllowed()
    {
        var verdict = new ReviewVerdictDto("correct", null, "correct", null, Day(3));
        var request = new SaveReviewVerdictRequest("correct", null, "correct", null);

        var ok = await ControllerFor(new FakeService { Verdict = verdict }, Me).SaveVerdict(Batch, Item, request, default);
        var bad = await ControllerFor(new FakeService { Error = new ArgumentException("Sai quy tắc") }, Me).SaveVerdict(Batch, Item, request, default);
        var hidden = await ControllerFor(new FakeService(), Me).SaveVerdict(Batch, Item, request, default);

        Assert.Same(verdict, Assert.IsType<OkObjectResult>(ok.Result).Value);
        Assert.Contains("Sai quy tắc", Assert.IsType<BadRequestObjectResult>(bad.Result).Value!.ToString());
        Assert.IsType<NotFoundResult>(hidden.Result);
    }

    [Fact]
    public void Controller_IsForAdminsOnly_WithTheExpectedRoutes()
    {
        var type = typeof(AdminReviewBatchesController);

        Assert.Equal(UserRoles.Admin, type.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("api/v1/admin/review-batches", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.NotNull(type.GetMethod(nameof(AdminReviewBatchesController.Create))!.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(type.GetMethod(nameof(AdminReviewBatchesController.List))!.GetCustomAttribute<HttpGetAttribute>());
        Assert.Equal("{batchId:guid}/items", type.GetMethod(nameof(AdminReviewBatchesController.GetItems))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("{batchId:guid}/items/{itemId:guid}", type.GetMethod(nameof(AdminReviewBatchesController.GetItem))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("{batchId:guid}/items/{itemId:guid}/verdict", type.GetMethod(nameof(AdminReviewBatchesController.SaveVerdict))!.GetCustomAttribute<HttpPutAttribute>()!.Template);
    }
}
