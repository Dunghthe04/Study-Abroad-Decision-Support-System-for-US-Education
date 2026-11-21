using System.Reflection;
using StudyAbroad.Application.Admin.AiQuality;
using StudyAbroad.Application.Audit;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// UC-90, cổng nội dung chat: luật dưới 16 tuổi, che thông tin, nhật ký ghi TRƯỚC khi lấy nội dung, phân trang chỉ trên lượt được xem.
public class AiQualitySampleTests
{
    private static readonly Guid Admin = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
    private static readonly TimeProvider Now = new FixedClock(new DateTimeOffset(2026, 11, 20, 15, 0, 0, TimeSpan.Zero));
    private static readonly DateOnly D1 = new(2026, 11, 1);
    private static readonly DateOnly D20 = new(2026, 11, 20);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");
    private static DateTime At(int day, int hour = 12) => new(2026, 11, day, hour, 0, 0, DateTimeKind.Utc);

    // ---- cổng: tuổi tại thời điểm của lượt chat

    private static AiQualityCandidate Candidate(DateOnly? birth, DateTime at, params string?[] versions) =>
        new(Id(1), at, birth, versions);

    [Fact]
    public void Adults_AndAccountsWithoutADateOfBirth_CanBeRead()
    {
        Assert.True(AiQualityContentGate.CanReadContent(Candidate(new DateOnly(2000, 1, 1), At(5))));
        Assert.True(AiQualityContentGate.CanReadContent(Candidate(null, At(5))));
    }

    [Fact]
    public void AMinor_IsHidden_EvenWithTheParentsConsentToTheOlderPolicy()
    {
        var birth = new DateOnly(2012, 6, 1);   // 14 tuổi

        Assert.False(AiQualityContentGate.CanReadContent(Candidate(birth, At(5))));                                  // chưa có đồng ý nào
        Assert.False(AiQualityContentGate.CanReadContent(Candidate(birth, At(5), ConsentPurposes.ChildDataPolicyVersion)));   // bản hiện hành chưa nêu mục đích này
        Assert.False(AiQualityContentGate.CanReadContent(Candidate(birth, At(5), "2026-10", null)));
    }

    [Fact]
    public void AMinor_CanBeRead_OnlyWhenTheParentAgreedToAPolicyThatNamesThePurpose()
    {
        var birth = new DateOnly(2012, 6, 1);

        Assert.True(AiQualityContentGate.CanReadContent(Candidate(birth, At(5), ConsentPurposes.QualityReviewMinPolicyVersion)));
        Assert.True(AiQualityContentGate.CanReadContent(Candidate(birth, At(5), "2026-10", "2027-01")));   // bản mới hơn cũng được
    }

    [Fact]
    public void TheCurrentPolicyIsStillBelowTheBarForReading_SoNoMinorsChatIsShownYet()
    {
        // Cho đến khi chính sách được bổ sung và ChildDataPolicyVersion được tăng, mọi hội thoại của trẻ đều ẩn
        Assert.True(string.CompareOrdinal(ConsentPurposes.ChildDataPolicyVersion, ConsentPurposes.QualityReviewMinPolicyVersion) < 0);
    }

    [Fact]
    public void TheAgeIsTakenAtTheTimeOfTheTurn_NotToday()
    {
        var birth = new DateOnly(2010, 6, 1);   // tròn 16 tuổi ngày 1/6/2026

        Assert.False(AiQualityContentGate.CanReadContent(Candidate(birth, new DateTime(2026, 5, 31, 10, 0, 0, DateTimeKind.Utc))));   // còn 15 tuổi
        Assert.True(AiQualityContentGate.CanReadContent(Candidate(birth, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc))));     // vừa tròn 16
        // Lượt lúc 14 tuổi vẫn ẩn dù hôm nay (2026-11-20) học sinh đã 16: lúc đó cơ sở xử lý là sự đồng ý của phụ huynh
        Assert.False(AiQualityContentGate.CanReadContent(Candidate(birth, new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc))));
    }

    [Fact]
    public void TheAgeUsesVietnamDates_LikeTheRestOfTheProject()
    {
        var birth = new DateOnly(2010, 6, 1);   // tròn 16 tuổi ngày 1/6/2026 (giờ Việt Nam)

        // 20:00 UTC ngày 31/5 đã là 03:00 ngày 1/6 ở Việt Nam: vừa tròn 16
        Assert.True(AiQualityContentGate.CanReadContent(Candidate(birth, new DateTime(2026, 5, 31, 20, 0, 0, DateTimeKind.Utc))));
        Assert.False(AiQualityContentGate.CanReadContent(Candidate(birth, new DateTime(2026, 5, 31, 16, 0, 0, DateTimeKind.Utc))));
    }

    [Fact]
    public void ALeapDayBirthday_IsCountedLikeAgePolicyDoes()
    {
        var birth = new DateOnly(2008, 2, 29);

        Assert.False(AiQualityContentGate.CanReadContent(Candidate(birth, new DateTime(2024, 2, 28, 5, 0, 0, DateTimeKind.Utc))));   // chưa 16
        Assert.True(AiQualityContentGate.CanReadContent(Candidate(birth, new DateTime(2024, 2, 29, 5, 0, 0, DateTimeKind.Utc))));    // đủ 16
    }

    [Fact]
    public void AnAccountOperatedByTheParent_IsNotSubjectToTheUnder16Rule_LikeTheRestOfTheProject()
    {
        var child = new AiQualityCandidate(Id(1), At(5), new DateOnly(2014, 1, 1), [], OwnerIsParentOperated: true);

        Assert.True(AiQualityContentGate.CanReadContent(child));
    }

    // ---- dịch vụ

    private sealed class FakeRepository(IReadOnlyList<AiQualityCandidate> candidates, IReadOnlyList<AiQualitySampleRow>? rows = null)
        : IAiQualitySampleRepository
    {
        public List<Guid> RowsAsked { get; } = [];
        public int RowsCalls { get; private set; }
        public string? OutcomeAsked { get; private set; }

        public Task<IReadOnlyList<AiQualityCandidate>> CandidatesAsync(DateTime fromUtc, DateTime toUtcExclusive, string outcome, CancellationToken ct = default)
        {
            OutcomeAsked = outcome;
            return Task.FromResult(candidates);
        }

        public Task<IReadOnlyList<AiQualitySampleRow>> RowsAsync(IReadOnlyCollection<Guid> messageIds, int contextMessages, CancellationToken ct = default)
        {
            RowsCalls++;
            RowsAsked.AddRange(messageIds);
            return Task.FromResult<IReadOnlyList<AiQualitySampleRow>>((rows ?? []).Where(r => messageIds.Contains(r.MessageId)).ToList());
        }
    }

    private sealed class FakeAudit(bool fail = false) : IAuditLogRepository
    {
        public List<AuditLog> Entries { get; } = [];

        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
        {
            if (fail)
                throw new InvalidOperationException("không ghi được nhật ký");
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private static AiQualityCandidate Adult(int n, int day = 5) => new(Id(n), At(day), new DateOnly(2000, 1, 1), []);
    private static AiQualityCandidate Minor(int n, int day = 5) => new(Id(n), At(day), new DateOnly(2012, 6, 1), []);

    private static AiQualitySampleRow Row(
        int n, string? question = "câu hỏi", string answer = "câu trả lời", string? owner = null, string? meta = null,
        params AiQualitySampleMessage[] context) =>
        new(Id(n), At(5), "no_answer", meta, question, answer, context, owner);

    private static Task<AiQualitySamplePage> Page(
        AiQualitySampleService service, string outcome = "no_answer", int page = 1, int size = 10, Guid? actor = null) =>
        service.GetPageAsync(actor ?? Admin, D1, D20, outcome, page, size, "1.2.3.4", "agent", default);

    [Fact]
    public async Task OnlyTheProblemOutcomesCanBeRead_TheAnsweredGroupGoesToTheReviewFeature()
    {
        var service = new AiQualitySampleService(new FakeRepository([]), new FakeAudit(), Now);

        foreach (var ok in new[] { "no_answer", "refused", "failed" })
            await Page(service, ok);
        await Assert.ThrowsAsync<ArgumentException>(() => Page(service, "answered"));
        await Assert.ThrowsAsync<ArgumentException>(() => Page(service, "whatever"));
    }

    [Fact]
    public async Task TheTextIsMasked_TheQuestionTheAnswerAndTheContext_IncludingTheOwnersName()
    {
        var row = Row(1, question: "Em Trần Văn Minh, sđt 0912345678, hỏi về Whitman", answer: "Chào Minh, mình chưa có thông tin.",
            owner: "Trần Văn Minh", context: [new AiQualitySampleMessage("user", "email minh@gmail.com")]);
        var service = new AiQualitySampleService(new FakeRepository([Adult(1)], [row]), new FakeAudit(), Now);

        var item = Assert.Single((await Page(service)).Items);

        Assert.Equal("Em [HỌ TÊN], sđt [SĐT], hỏi về Whitman", item.Question);
        Assert.Equal("Chào [HỌ TÊN], mình chưa có thông tin.", item.Answer);
        Assert.Equal("email [EMAIL]", Assert.Single(item.Context).Content);
    }

    [Fact]
    public void TheResultCarriesNoIdentifiers_NoGuidAnywhereInTheShapeTheAdminReceives()
    {
        static bool HasGuid(Type type) => type.GetProperties().Any(p =>
            p.PropertyType == typeof(Guid) || p.PropertyType == typeof(Guid?)
            || (p.PropertyType.IsGenericType && p.PropertyType.GetGenericArguments().Any(a => a == typeof(Guid))));

        Assert.False(HasGuid(typeof(AiQualitySamplePage)));
        Assert.False(HasGuid(typeof(AiQualitySampleDto)));
        Assert.False(HasGuid(typeof(AiQualitySampleMessage)));
    }

    [Fact]
    public async Task ASampleWhoseSlmFellBack_IsLabelledAsSuch_NotAsOutOfScope()
    {
        var row = Row(1, meta: "{\"turnMs\":3500,\"skills\":[\"out_of_scope\"]}") with { QuestionMetaJson = "{\"slm\":{\"fallback\":true}}" };
        var service = new AiQualitySampleService(new FakeRepository([Adult(1)], [row]), new FakeAudit(), Now);

        var item = Assert.Single((await Page(service)).Items);

        Assert.Equal(AiQualityMetaReader.SlmFallbackFeature, item.Feature);
    }

    [Fact]
    public async Task FeatureAndTurnTime_AreReadFromTheRepliesMeta()
    {
        var row = Row(1, meta: "{\"turnMs\":3100,\"skills\":[\"knowledge\"]}");
        var service = new AiQualitySampleService(new FakeRepository([Adult(1)], [row]), new FakeAudit(), Now);

        var item = Assert.Single((await Page(service)).Items);

        Assert.Equal(("knowledge", 3100L, "no_answer"), (item.Feature, item.TurnMs, item.Outcome));
    }

    // ---- dưới 16 tuổi

    [Fact]
    public async Task AMinorsChat_IsCountedAsHidden_AndItsContentIsNeverLoaded()
    {
        var repo = new FakeRepository([Adult(1), Minor(2), Adult(3)], [Row(1), Row(2, question: "BÍ MẬT CỦA TRẺ"), Row(3)]);
        var service = new AiQualitySampleService(repo, new FakeAudit(), Now);

        var page = await Page(service);

        Assert.Equal((2, 1), (page.TotalCount, page.HiddenCount));
        Assert.Equal([Id(1), Id(3)], repo.RowsAsked);   // lượt của trẻ không được hỏi nội dung
        Assert.DoesNotContain(page.Items, i => i.Question == "BÍ MẬT CỦA TRẺ");
    }

    [Fact]
    public async Task APageOfHiddenTurnsOnly_ReturnsNoItems_NoAuditEntryAndNoContentLoaded()
    {
        var repo = new FakeRepository([Minor(1), Minor(2)], [Row(1), Row(2)]);
        var audit = new FakeAudit();

        var page = await Page(new AiQualitySampleService(repo, audit, Now));

        Assert.Empty(page.Items);
        Assert.Equal((0, 2), (page.TotalCount, page.HiddenCount));
        Assert.Equal(0, repo.RowsCalls);
        Assert.Empty(audit.Entries);   // không có nội dung rời đi nên không có gì để ghi
    }

    [Fact]
    public async Task Paging_CountsOnlyTheTurnsThatCanBeRead_SoPagesAreNeverShort()
    {
        // 5 lượt xem được xen lẫn 2 lượt của trẻ, mới nhất trước
        var candidates = new[] { Adult(1), Minor(2), Adult(3), Adult(4), Minor(5), Adult(6), Adult(7) };
        var rows = Enumerable.Range(1, 7).Select(n => Row(n, question: $"q{n}")).ToList();
        var service = new AiQualitySampleService(new FakeRepository(candidates, rows), new FakeAudit(), Now);

        var first = await Page(service, page: 1, size: 2);
        var third = await Page(service, page: 3, size: 2);

        Assert.Equal(["q1", "q3"], first.Items.Select(i => i.Question));
        Assert.Equal(["q7"], third.Items.Select(i => i.Question));
        Assert.Equal((5, 2), (first.TotalCount, first.HiddenCount));
    }

    // ---- nhật ký người xem

    [Fact]
    public async Task TheAuditEntryIsWrittenBeforeTheContentIsLoaded_AndAFailureStopsEverything()
    {
        var repo = new FakeRepository([Adult(1)], [Row(1)]);
        var service = new AiQualitySampleService(repo, new FakeAudit(fail: true), Now);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Page(service));

        Assert.Equal(0, repo.RowsCalls);   // nhật ký lỗi: nội dung chưa hề được tải
    }

    [Fact]
    public async Task TheAuditEntry_HoldsOnlyCodesAndCounts_NeverTheChatText()
    {
        var audit = new FakeAudit();
        var repo = new FakeRepository([Adult(1), Minor(2)], [Row(1, question: "NỘI DUNG CHAT BÍ MẬT", answer: "TRẢ LỜI BÍ MẬT")]);

        await Page(new AiQualitySampleService(repo, audit, Now), "failed");

        var entry = Assert.Single(audit.Entries);
        Assert.Equal((Admin, AiQualitySampleService.ViewSamplesAction, "chat_message"), (entry.ActorUserId, entry.Action, entry.EntityType));
        Assert.Equal(("1.2.3.4", "agent"), (entry.IpAddress, entry.UserAgent));
        Assert.Contains("\"outcome\":\"failed\"", entry.DetailJson);
        Assert.Contains("\"count\":1", entry.DetailJson);
        Assert.Contains("\"hiddenCount\":1", entry.DetailJson);
        Assert.Contains(Id(1).ToString(), entry.DetailJson);
        Assert.DoesNotContain("BÍ MẬT", entry.DetailJson);
        Assert.DoesNotContain(Id(2).ToString(), entry.DetailJson);   // lượt bị ẩn không nằm trong nhật ký như lượt đã xem
    }

    [Fact]
    public async Task WithoutAViewer_NothingIsReturned()
    {
        var service = new AiQualitySampleService(new FakeRepository([Adult(1)], [Row(1)]), new FakeAudit(), Now);

        await Assert.ThrowsAsync<ArgumentException>(() => Page(service, actor: Guid.Empty));
    }

    // ---- cỡ trang và khoảng ngày

    [Fact]
    public async Task PageSizeIsClamped_AndThePageNumberStartsAtOne()
    {
        var candidates = Enumerable.Range(1, 30).Select(n => Adult(n)).ToList();
        var rows = Enumerable.Range(1, 30).Select(n => Row(n)).ToList();
        var service = new AiQualitySampleService(new FakeRepository(candidates, rows), new FakeAudit(), Now);

        var big = await Page(service, size: 500);
        var small = await Page(service, page: -3, size: 0);

        Assert.Equal((AiQualitySampleService.MaxPageSize, AiQualitySampleService.MaxPageSize), (big.Items.Count, big.PageSize));
        Assert.Equal((1, 1, 1), (small.Items.Count, small.PageSize, small.Page));
    }

    [Fact]
    public async Task ABadDateRange_IsRejected()
    {
        var service = new AiQualitySampleService(new FakeRepository([]), new FakeAudit(), Now);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetPageAsync(Admin, D20, D1, "failed", 1, 10, null, null, default));
    }
}
