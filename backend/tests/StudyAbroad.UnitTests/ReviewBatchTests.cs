using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.ReviewBatches;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;

namespace StudyAbroad.UnitTests;

// Bốc mẫu ngẫu nhiên có seed, chia nhóm theo skill, và tạo đợt duyệt mẫu (tab 2, bước 2b).
public class ReviewBatchTests
{
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");
    private static DateTime Day(int day, int hour = 12) => new(2026, 11, day, hour, 0, 0, DateTimeKind.Utc);

    private static List<(Guid, string)> Pool(string stratum, int from, int count) =>
        Enumerable.Range(from, count).Select(n => (Id(n), stratum)).ToList();

    // ================================================================= bộ bốc mẫu

    [Fact]
    public void Draw_WithTheSameSeedAndTheSameCandidates_GivesExactlyTheSameSample()
    {
        var candidates = Pool("scenario", 1, 40).Concat(Pool("knowledge", 100, 25)).ToList();

        var first = ReviewSampler.Draw(candidates, "abc123", 10);
        var second = ReviewSampler.Draw(candidates, "abc123", 10);

        Assert.Equal(first.Items, second.Items);
        Assert.Equal(first.Strata, second.Strata);
    }

    [Fact]
    public void Draw_DoesNotDependOnTheOrderOfTheInput()
    {
        var candidates = Pool("scenario", 1, 40).Concat(Pool("knowledge", 100, 25)).ToList();
        var shuffled = candidates.OrderByDescending(c => c.Item1).ToList();

        var a = ReviewSampler.Draw(candidates, "abc123", 10);
        var b = ReviewSampler.Draw(shuffled, "abc123", 10);

        Assert.Equal(a.Items, b.Items);
    }

    [Fact]
    public void Draw_WithADifferentSeed_PicksADifferentSample()
    {
        var candidates = Pool("scenario", 1, 60);

        var a = ReviewSampler.Draw(candidates, "seed-A", 10).Items.Select(i => i.MessageId).ToHashSet();
        var b = ReviewSampler.Draw(candidates, "seed-B", 10).Items.Select(i => i.MessageId).ToHashSet();

        Assert.False(a.SetEquals(b));
    }

    [Fact]
    public void Draw_TakesAtMostTheRequestedNumberPerStratum_FromTheCandidatesOnly_WithoutDuplicates()
    {
        var candidates = Pool("scenario", 1, 40).Concat(Pool("knowledge", 100, 25)).ToList();

        var sample = ReviewSampler.Draw(candidates, "s", 7);

        Assert.Equal(14, sample.Items.Count);
        Assert.Equal(7, sample.Items.Count(i => i.Stratum == "scenario"));
        Assert.Equal(7, sample.Items.Count(i => i.Stratum == "knowledge"));
        Assert.Equal(sample.Items.Count, sample.Items.Select(i => i.MessageId).Distinct().Count());
        var all = candidates.ToDictionary(c => c.Item1, c => c.Item2);
        Assert.All(sample.Items, i => Assert.Equal(all[i.MessageId], i.Stratum));
    }

    [Fact]
    public void Draw_AStratumSmallerThanTheRequest_IsTakenWhole()
    {
        var candidates = Pool("scenario", 1, 40).Concat(Pool("center_referral", 100, 3)).ToList();

        var sample = ReviewSampler.Draw(candidates, "s", 10);

        Assert.Equal(3, sample.Items.Count(i => i.Stratum == "center_referral"));
        Assert.Equal(new StratumCount("center_referral", 3, 3), sample.Strata.Single(s => s.Stratum == "center_referral"));
    }

    [Fact]
    public void Draw_ReportsPopulationAndPickedForEveryStratum_InNameOrder()
    {
        var candidates = Pool("scenario", 1, 40).Concat(Pool("knowledge", 100, 25)).Concat(Pool("unlabeled", 200, 2)).ToList();

        var sample = ReviewSampler.Draw(candidates, "s", 10);

        Assert.Equal(
            [new StratumCount("knowledge", 25, 10), new StratumCount("scenario", 40, 10), new StratumCount("unlabeled", 2, 2)],
            sample.Strata);
    }

    [Fact]
    public void Draw_NumbersTheItemsFromOneWithoutGaps_InReadingOrder()
    {
        var sample = ReviewSampler.Draw(Pool("scenario", 1, 30).Concat(Pool("knowledge", 100, 30)).ToList(), "s", 8);

        Assert.Equal(Enumerable.Range(1, 16), sample.Items.Select(i => i.Position));
    }

    [Fact]
    public void Draw_ReadingOrderMixesTheStrata_InsteadOfListingThemOneAfterAnother()
    {
        var sample = ReviewSampler.Draw(Pool("a", 1, 60).Concat(Pool("b", 100, 60)).ToList(), "s", 30);

        // Nếu xếp theo nhóm thì chỉ đổi nhóm một lần; xáo trộn thì đổi nhiều lần
        var switches = sample.Items.Zip(sample.Items.Skip(1), (x, y) => x.Stratum != y.Stratum).Count(changed => changed);
        Assert.True(switches > 5, $"Chỉ đổi nhóm {switches} lần");
    }

    [Fact]
    public void Draw_CountsAnIdThatAppearsTwiceOnlyOnce()
    {
        var sample = ReviewSampler.Draw([(Id(1), "a"), (Id(1), "a"), (Id(2), "a")], "s", 10);

        Assert.Equal(2, sample.Items.Count);
        Assert.Equal(2, sample.Strata.Single().Population);
    }

    [Fact]
    public void Draw_WithNoCandidates_IsEmpty()
    {
        var sample = ReviewSampler.Draw([], "s", 10);

        Assert.Empty(sample.Items);
        Assert.Empty(sample.Strata);
    }

    [Fact]
    public void Draw_IsFair_EveryCandidateHasAboutTheSameChanceOfBeingPicked()
    {
        // 10 câu trong một nhóm, mỗi lần bốc 5: qua 2.000 seed khác nhau, câu nào cũng phải được chọn khoảng một nửa số lần
        var candidates = Pool("a", 1, 10);
        var hits = candidates.ToDictionary(c => c.Item1, _ => 0);

        for (var seed = 0; seed < 2000; seed++)
            foreach (var item in ReviewSampler.Draw(candidates, $"seed-{seed}", 5).Items)
                hits[item.MessageId]++;

        Assert.All(hits.Values, count => Assert.InRange(count / 2000.0, 0.42, 0.58));
    }

    [Theory]
    [InlineData("", 5)]
    [InlineData("   ", 5)]
    [InlineData("s", 0)]
    [InlineData("s", -1)]
    public void Draw_WithABadSeedOrSize_Throws(string seed, int size) =>
        Assert.ThrowsAny<ArgumentException>(() => ReviewSampler.Draw(Pool("a", 1, 5), seed, size));

    [Fact]
    public void Strata_RoundTripThroughJson_AndBrokenJsonReadsAsEmpty()
    {
        var strata = new List<StratumCount> { new("scenario", 40, 10), new("unlabeled", 2, 2) };

        var json = ReviewSampler.StrataToJson(strata);

        Assert.Equal(strata, ReviewSampler.ReadStrata(json));
        Assert.Contains("\"population\":40", json);
        Assert.Empty(ReviewSampler.ReadStrata("hỏng"));
        Assert.Empty(ReviewSampler.ReadStrata(null));
        Assert.Empty(ReviewSampler.ReadStrata("   "));
    }

    // ================================================================= nhóm của một câu

    [Theory]
    [InlineData("{\"skills\":[\"scenario\"]}", "scenario")]
    [InlineData("{\"skills\":[\"scenario\",\"knowledge\"]}", "knowledge+scenario")]
    [InlineData("{\"skills\":[\"knowledge\",\"scenario\"]}", "knowledge+scenario")]   // thứ tự trong meta không tạo nhóm mới
    [InlineData("{\"skills\":[\"scenario\",\"scenario\",\" scenario \"]}", "scenario")]
    [InlineData("{\"skills\":[\"scenario\",1,null,\"\",\"  \"]}", "scenario")]
    public void Strata_AreTheSortedDistinctSkillsJoinedWithAPlus(string meta, string expected) =>
        Assert.Equal(expected, ReviewStrata.Of(meta));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("không phải JSON")]
    [InlineData("[1,2]")]
    [InlineData("{}")]
    [InlineData("{\"skills\":[]}")]
    [InlineData("{\"skills\":\"scenario\"}")]
    [InlineData("{\"skills\":[1,2]}")]
    public void Strata_WithoutUsableSkills_AreUnlabeled_NeverThrow(string? meta) =>
        Assert.Equal(ReviewStrata.Unlabeled, ReviewStrata.Of(meta));

    [Fact]
    public void Strata_AreClippedToTheColumnWidth()
    {
        var meta = "{\"skills\":[\"" + new string('a', 100) + "\"]}";

        Assert.Equal(ReviewStrata.MaxLength, ReviewStrata.Of(meta).Length);
    }

    // ================================================================= truy vấn các câu có thể bốc

    private static ChatMessage Msg(int n, string role, DateTime at, string? outcome = "answered", string? review = null, int? replyTo = 1) =>
        new()
        {
            Id = Id(n), ChatSessionId = Id(900), Role = role, CreatedAt = at, Outcome = outcome, ReviewStatus = review,
            ReplyToMessageId = replyTo is { } r ? Id(r) : null, MetaJson = "{\"skills\":[\"scenario\"]}", Content = "nội dung riêng tư",
        };

    [Fact]
    public void Candidates_AreOnlyAnsweredAssistantReplies_WithAQuestion_NotInTheQueue()
    {
        var messages = new[]
        {
            Msg(1, "assistant", Day(2)),                                        // được bốc
            Msg(2, "user", Day(2), outcome: null),                              // tin của học sinh
            Msg(3, "assistant", Day(2), outcome: "no_answer", review: "open"),  // thuộc hàng đợi
            Msg(4, "assistant", Day(2), outcome: "failed", review: "open"),
            Msg(5, "assistant", Day(2), review: "resolved"),                    // có trạng thái xử lý
            Msg(6, "assistant", Day(2), replyTo: null),                         // không có tin hỏi: không chấm được nhãn ý
            Msg(7, "system", Day(2), outcome: "failed", review: "open"),
            Msg(8, "assistant", Day(2), outcome: null),                         // tin cũ chưa có outcome
        }.AsQueryable();

        var ids = ReviewCandidateQuery.Candidates(messages, null, null).Select(c => c.MessageId).ToList();

        Assert.Equal([Id(1)], ids);
    }

    [Fact]
    public void Candidates_RespectTheDateRange_StartIncluded_EndExcluded()
    {
        var messages = new[]
        {
            Msg(1, "assistant", Day(1, 23)),
            Msg(2, "assistant", Day(2, 0)),    // đúng đầu khoảng: được tính
            Msg(3, "assistant", Day(3, 23)),
            Msg(4, "assistant", Day(4, 0)),    // đúng cuối khoảng: không tính
        }.AsQueryable();

        var ids = ReviewCandidateQuery.Candidates(messages, Day(2, 0), Day(4, 0)).Select(c => c.MessageId).ToList();

        Assert.Equal([Id(2), Id(3)], ids);
    }

    [Fact]
    public void Candidates_CarryOnlyTheIdAndTheMeta_NoChatContent()
    {
        var names = typeof(ReviewCandidate).GetProperties().Select(p => p.Name);

        Assert.Equal(["MessageId", "ReplyMetaJson"], names);
        Assert.DoesNotContain("nội dung riêng tư", JsonSerializer.Serialize(
            ReviewCandidateQuery.Candidates(new[] { Msg(1, "assistant", Day(2)) }.AsQueryable(), null, null).ToList()));
    }

    [Fact]
    public void Candidates_TranslateToSql_WithoutTheContentColumn()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check").UseSnakeCaseNamingConvention().Options;
        using var db = new AppDbContext(options);

        var sql = ReviewCandidateQuery.Candidates(db.ChatMessages, Day(1), Day(30)).ToQueryString();

        Assert.Contains("review_status", sql);
        Assert.Contains("meta_json", sql);
        Assert.DoesNotContain("content", sql);
    }

    // ================================================================= tạo đợt

    private sealed class FakeRepo : IReviewBatchRepository
    {
        public List<ReviewCandidate> Candidates { get; init; } = [];
        public HashSet<Guid> Admins { get; init; } = [];
        public (DateTime? From, DateTime? To)? AskedRange { get; private set; }
        public int Saves { get; private set; }
        public ReviewBatch? Batch { get; private set; }
        public IReadOnlyList<ReviewBatchItem> Items { get; private set; } = [];
        public IReadOnlyList<ReviewBatchReviewer> Reviewers { get; private set; } = [];
        public AuditLog? Audit { get; private set; }

        public Task<IReadOnlyList<ReviewCandidate>> ListCandidatesAsync(DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken ct = default)
        {
            AskedRange = (fromUtc, toUtcExclusive);
            return Task.FromResult<IReadOnlyList<ReviewCandidate>>(Candidates);
        }

        public Task<IReadOnlyList<Guid>> FindActiveAdminIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(userIds.Where(Admins.Contains).ToList());

        public Task AddAsync(
            ReviewBatch batch, IReadOnlyList<ReviewBatchItem> items, IReadOnlyList<ReviewBatchReviewer> reviewers, AuditLog audit,
            CancellationToken ct = default)
        {
            Saves++;
            (Batch, Items, Reviewers, Audit) = (batch, items, reviewers, audit);
            return Task.CompletedTask;
        }
    }

    private static readonly Guid Admin1 = Id(801);
    private static readonly Guid Admin2 = Id(802);

    private static List<ReviewCandidate> Messages(string skill, int from, int count) =>
        Enumerable.Range(from, count).Select(n => new ReviewCandidate(Id(n), "{\"skills\":[\"" + skill + "\"]}")).ToList();

    private static FakeRepo Repo(params int[] _) => new()
    {
        Candidates = [.. Messages("scenario", 1, 30), .. Messages("knowledge", 100, 20), new ReviewCandidate(Id(300), null)],
        Admins = [Admin1, Admin2],
    };

    private static CreateReviewBatchRequest Request(
        string? name = "Đợt 1", int size = 10, DateOnly? from = null, DateOnly? to = null, Guid[]? reviewers = null, string? seed = "abc123") =>
        new(name, size, from, to, reviewers ?? [Admin1], seed);

    private static ReviewBatchService Svc(FakeRepo repo) => new(repo);

    [Fact]
    public async Task Create_DrawsTheSample_AndSavesTheBatchItemsReviewersAndAuditTogether()
    {
        var repo = Repo();

        await Svc(repo).CreateAsync(Request(reviewers: [Admin1, Admin2]), Admin1, "10.0.0.5", "Mozilla");

        Assert.Equal(1, repo.Saves);   // một lần lưu cho cả bốn thứ
        var batch = repo.Batch!;
        Assert.Equal(("Đợt 1", "abc123", 10, Admin1), (batch.Name, batch.Seed, batch.SizePerStratum, batch.CreatedByUserId));
        Assert.Equal(21, repo.Items.Count);   // 10 scenario + 10 knowledge + 1 chưa gắn nhãn (nhóm chỉ có một câu)
        Assert.Equal(2, repo.Reviewers.Count);
        Assert.NotNull(repo.Audit);
    }

    [Fact]
    public async Task Create_TheSavedItemsAreTheSample_ForTheSavedBatch_WithStrataAndPositions()
    {
        var repo = Repo();

        var dto = await Svc(repo).CreateAsync(Request(size: 5), Admin1, null, null);

        // 5 scenario + 5 knowledge + 1 unlabeled (nhóm chỉ có một câu)
        Assert.Equal(11, dto.TotalItems);
        Assert.Equal(11, repo.Items.Count);
        Assert.All(repo.Items, i => Assert.Equal(repo.Batch!.Id, i.ReviewBatchId));
        Assert.Equal(Enumerable.Range(1, 11), repo.Items.Select(i => i.Position));
        Assert.Equal(
            [new StratumCount("knowledge", 20, 5), new StratumCount("scenario", 30, 5), new StratumCount("unlabeled", 1, 1)],
            dto.Strata);
        Assert.Equal(dto.Strata, ReviewSampler.ReadStrata(repo.Batch!.StrataJson));
        Assert.Equal(repo.Batch.Id, dto.Id);
    }

    [Fact]
    public async Task Create_WithTheSameSeed_GivesTheSameSampleTwice()
    {
        var a = Repo();
        var b = Repo();

        await Svc(a).CreateAsync(Request(seed: "fixed"), Admin1, null, null);
        await Svc(b).CreateAsync(Request(seed: "fixed"), Admin1, null, null);

        Assert.Equal(
            a.Items.Select(i => (i.ChatMessageId, i.Stratum, i.Position)),
            b.Items.Select(i => (i.ChatMessageId, i.Stratum, i.Position)));
    }

    [Fact]
    public async Task Create_WithoutASeed_GeneratesOne_AndStoresIt()
    {
        var repo = Repo();

        var dto = await Svc(repo).CreateAsync(Request(seed: "  "), Admin1, null, null);

        Assert.Matches("^[0-9a-f]{12}$", dto.Seed);
        Assert.Equal(dto.Seed, repo.Batch!.Seed);
    }

    [Fact]
    public async Task Create_PassesTheDateRangeAsUtcBounds_EndDayIncluded()
    {
        var repo = Repo();

        var dto = await Svc(repo).CreateAsync(Request(from: new DateOnly(2026, 11, 2), to: new DateOnly(2026, 11, 8)), Admin1, null, null);

        Assert.Equal((Day(2, 0), Day(9, 0)), repo.AskedRange);
        Assert.Equal((Day(2, 0), Day(9, 0)), (dto.SampledFromUtc, dto.SampledToUtcExclusive));
        Assert.Equal((Day(2, 0), Day(9, 0)), (repo.Batch!.SampledFromUtc, repo.Batch.SampledToUtcExclusive));
    }

    [Fact]
    public async Task Create_WithoutDates_HasNoLimit()
    {
        var repo = Repo();

        await Svc(repo).CreateAsync(Request(), Admin1, null, null);

        Assert.Equal((null, null), repo.AskedRange);
    }

    [Fact]
    public async Task Create_AssignsEachReviewerOnce()
    {
        var repo = Repo();

        var dto = await Svc(repo).CreateAsync(Request(reviewers: [Admin1, Admin2, Admin1, Guid.Empty]), Admin1, null, null);

        Assert.Equal([Admin1, Admin2], dto.ReviewerIds);
        Assert.Equal([Admin1, Admin2], repo.Reviewers.Select(r => r.UserId));
        Assert.All(repo.Reviewers, r => Assert.Equal(repo.Batch!.Id, r.ReviewBatchId));
    }

    [Fact]
    public async Task Create_WritesAnAuditEntry_WithTheParametersButNoChatContent()
    {
        var repo = Repo();

        var dto = await Svc(repo).CreateAsync(Request(size: 5), Admin1, "10.0.0.5", "Mozilla");

        var audit = repo.Audit!;
        Assert.Equal((Admin1, ReviewBatchService.CreateAction, "review_batch", dto.Id.ToString()), (audit.ActorUserId, audit.Action, audit.EntityType, audit.EntityId));
        Assert.Equal(("10.0.0.5", "Mozilla"), (audit.IpAddress, audit.UserAgent));
        using var detail = JsonDocument.Parse(audit.DetailJson!);
        Assert.Equal("abc123", detail.RootElement.GetProperty("seed").GetString());
        Assert.Equal(11, detail.RootElement.GetProperty("totalItems").GetInt32());
        Assert.DoesNotContain("nội dung", audit.DetailJson!);
    }

    [Fact]
    public async Task Create_ClipsTheAddressAndBrowserToTheAuditColumns()
    {
        var repo = Repo();

        await Svc(repo).CreateAsync(Request(), Admin1, new string('1', 200), new string('u', 900), default);

        Assert.Equal((64, 512), (repo.Audit!.IpAddress!.Length, repo.Audit.UserAgent!.Length));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithoutAName_Throws_AndSavesNothing(string? name)
    {
        var repo = Repo();

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).CreateAsync(Request(name: name), Admin1, null, null));

        Assert.Equal(0, repo.Saves);
    }

    [Fact]
    public async Task Create_WithATooLongNameOrSeed_Throws()
    {
        var repo = Repo();

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).CreateAsync(Request(name: new string('a', 121)), Admin1, null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).CreateAsync(Request(seed: new string('a', 65)), Admin1, null, null));
        Assert.Equal(0, repo.Saves);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(101)]
    public async Task Create_WithAnOutOfRangeSize_Throws_AndSavesNothing(int size)
    {
        var repo = Repo();

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).CreateAsync(Request(size: size), Admin1, null, null));

        Assert.Equal(0, repo.Saves);
    }

    [Fact]
    public async Task Create_WithTheStartAfterTheEnd_Throws()
    {
        var repo = Repo();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Svc(repo).CreateAsync(Request(from: new DateOnly(2026, 11, 9), to: new DateOnly(2026, 11, 2)), Admin1, null, null));

        Assert.Equal(0, repo.Saves);
    }

    [Fact]
    public async Task Create_WithNoReviewers_OrTooMany_Throws()
    {
        var repo = Repo();
        var tooMany = Enumerable.Range(0, ReviewBatchService.MaxReviewers + 1).Select(n => Id(810 + n)).ToArray();
        repo.Admins.UnionWith(tooMany);

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).CreateAsync(Request(reviewers: []), Admin1, null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).CreateAsync(Request(reviewers: tooMany), Admin1, null, null));
        Assert.Equal(0, repo.Saves);
    }

    [Fact]
    public async Task Create_WithAReviewerWhoIsNotAnActiveAdmin_Throws_AndSavesNothing()
    {
        var repo = Repo();

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).CreateAsync(Request(reviewers: [Admin1, Id(999)]), Admin1, null, null));

        Assert.Equal(0, repo.Saves);
    }

    [Fact]
    public async Task Create_WithNoAnswerToDraw_Throws_AndSavesNothing()
    {
        var repo = new FakeRepo { Candidates = [], Admins = [Admin1] };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).CreateAsync(Request(), Admin1, null, null));

        Assert.Contains("Không có câu", error.Message);
        Assert.Equal(0, repo.Saves);
    }

    [Fact]
    public async Task Create_WithoutAnActor_Throws_AndSavesNothing()
    {
        var repo = Repo();

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).CreateAsync(Request(), Guid.Empty, null, null));

        Assert.Equal(0, repo.Saves);
    }

    [Fact]
    public void TheResultDto_CarriesNoChatContent()
    {
        var names = typeof(ReviewBatchCreatedDto).GetProperties().Select(p => p.Name);

        Assert.DoesNotContain(names, n => n is "Content" or "Text" or "Question" or "Answer");
    }
}
