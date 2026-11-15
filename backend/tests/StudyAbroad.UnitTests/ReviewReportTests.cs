using System.Reflection;
using StudyAbroad.Application.Admin.ReviewBatches;
using StudyAbroad.Application.Audit;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Báo cáo một đợt: ghép kết luận từ kho dữ liệu với bộ tính (tab 3, bước 3b).
public class ReviewReportTests
{
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");
    private static DateTime Day(int day, int hour = 12) => new(2026, 11, day, hour, 0, 0, DateTimeKind.Utc);

    private static readonly Guid Me = Id(801);
    private static readonly Guid Other = Id(802);
    private static readonly Guid Batch = Id(50);

    private sealed class ReportRepo(ReviewReportRows? rows) : IReviewBatchRepository
    {
        public (Guid Batch, Guid User)? Asked { get; private set; }

        public Task<ReviewReportRows?> GetReportRowsAsync(Guid batchId, Guid userId, CancellationToken ct = default)
        {
            Asked = (batchId, userId);
            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<ReviewCandidate>> ListCandidatesAsync(DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> FindActiveAdminIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(ReviewBatch batch, IReadOnlyList<ReviewBatchItem> items, IReadOnlyList<ReviewBatchReviewer> reviewers, AuditLog audit, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReviewBatchListRow>> ListForUserAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ReviewItemsRows?> ListItemsAsync(Guid batchId, Guid reviewerUserId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ReviewItemDetailRows?> FindItemDetailAsync(Guid batchId, Guid itemId, Guid reviewerUserId, int contextCount, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ReviewVerdict?> UpsertVerdictAsync(
            Guid batchId, Guid itemId, Guid reviewerUserId, string labelVerdict, string? correctIntent, string answerVerdict, string? cause,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoAudit : IAuditLogRepository
    {
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private static ReviewBatchService Svc(ReviewReportRows? rows) => new(new ReportRepo(rows), new NoAudit());

    private static ReviewVerdictRow Row(
        int item, string stratum, string answer, string? cause = null, string label = "correct", string? intent = null,
        Guid? reviewer = null, DateTime? at = null) =>
        new(Id(item), stratum, reviewer ?? Me, label, intent, answer, cause, at ?? Day(3));

    private static ReviewReportRows Rows(IReadOnlyList<ReviewVerdictRow> verdicts, int totalItems = 4, int reviewers = 1, string? strata = null) =>
        new(Batch, "Đợt 1", "abc123", strata ?? ReviewSampler.StrataToJson([new StratumCount("scenario", 40, 2), new StratumCount("knowledge", 60, 2)]),
            totalItems, reviewers, verdicts);

    [Fact]
    public async Task Report_IsNull_WhenTheRepositoryHidesTheBatch_AndAsksWithTheCaller()
    {
        var repo = new ReportRepo(null);

        var report = await new ReviewBatchService(repo, new NoAudit()).GetReportAsync(Batch, Me);

        Assert.Null(report);
        Assert.Equal((Batch, Me), repo.Asked);
    }

    [Fact]
    public async Task Report_ComputesWeightedRates_FromTheStoredStrata()
    {
        var rows = Rows(
        [
            Row(1, "scenario", "wrong", "hallucination"), Row(2, "scenario", "correct"),
            Row(3, "knowledge", "correct"), Row(4, "knowledge", "correct"),
        ]);

        var report = (await Svc(rows).GetReportAsync(Batch, Me))!;

        // scenario 50% sai (40 câu), knowledge 0% (60 câu): (40x0.5 + 60x0) / 100 = 20%
        Assert.Equal(0.2, report.Report.AnswerWrong!.Rate, 6);
        Assert.Equal((Batch, "Đợt 1", "abc123", 4, 4, 1), (report.BatchId, report.BatchName, report.Seed, report.TotalItems, report.ItemsGraded, report.ReviewerCount));
        Assert.Equal([new CauseCount("hallucination", 1)], report.Report.Causes);
    }

    [Fact]
    public async Task Report_CountsAnItemOnce_UsingTheEarliestVerdict_WhenSeveralReviewersGradedIt()
    {
        var rows = Rows(
        [
            Row(1, "scenario", "wrong", "technical", reviewer: Other, at: Day(5)),   // chấm sau
            Row(1, "scenario", "correct", reviewer: Me, at: Day(3)),                  // chấm sớm nhất: câu này được tính là đúng
            Row(2, "scenario", "correct", reviewer: Other, at: Day(4)),
        ], totalItems: 2, reviewers: 2);

        var report = (await Svc(rows).GetReportAsync(Batch, Me))!;

        Assert.Equal(2, report.ItemsGraded);   // hai câu, không phải ba kết luận
        Assert.Equal(0, report.Report.AnswerWrong!.Count);
        Assert.Equal(2, report.Report.AnswerWrong.Total);
        Assert.Empty(report.Report.Causes);
    }

    [Fact]
    public async Task Report_WhenNothingIsGradedYet_HasNoRatesButStillShowsTheStrata()
    {
        var report = (await Svc(Rows([])).GetReportAsync(Batch, Me))!;

        Assert.Equal(0, report.ItemsGraded);
        Assert.Null(report.Report.AnswerWrong);
        Assert.Equal(100, report.Report.Population);
        Assert.Equal(2, report.Report.Strata.Count);
    }

    [Fact]
    public async Task Report_WithBrokenStrataJson_StillWorks()
    {
        var rows = Rows([Row(1, "scenario", "correct"), Row(2, "scenario", "wrong", "technical")], totalItems: 2, strata: "không phải json");

        var report = (await Svc(rows).GetReportAsync(Batch, Me))!;

        Assert.Equal(0.5, report.Report.AnswerWrong!.Rate, 6);
    }

    [Fact]
    public void Report_Dtos_CarryNoChatContentAndNoReviewerIdentity()
    {
        // Báo cáo chỉ gồm số đếm và mã nhóm: không có trường nào mang nội dung chat, và không nêu người chấm nào chấm thế nào
        var names = new[] { typeof(ReviewReportDto), typeof(ReviewReport), typeof(StratumReport), typeof(RateEstimate), typeof(AnswerCounts), typeof(CauseCount), typeof(CorrectIntentCount) }
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain(names, n => n.Contains("Content", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Message", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("User", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Session", StringComparison.OrdinalIgnoreCase));
    }
}
