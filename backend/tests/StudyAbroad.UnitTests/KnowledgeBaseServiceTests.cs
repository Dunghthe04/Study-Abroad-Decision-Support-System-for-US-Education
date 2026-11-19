using System.Text;
using StudyAbroad.Application.Admin.KnowledgeBase;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence.Repositories;
using Xunit;

namespace StudyAbroad.UnitTests;

public class KnowledgeBaseServiceTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Reviewer = Guid.NewGuid();

    private static KnowledgeBaseService Svc(FakeKnowledgeRepository repo, params KnowledgeImportRow[] rows) =>
        new(repo, new FakeKnowledgeReader(rows));

    private static SaveKnowledgeEntryRequest Full(string question = "OPT là gì?", bool submit = false) =>
        new("LABEL_VISA_F1", [KnowledgeLevels.DaiHoc], question, "OPT là thời gian thực tập sau tốt nghiệp.", "Hội đồng Chuyên gia DSS", submit);

    private static KnowledgeImportRow Row(int rowNumber, string? id, string question, string level = "Đại học; Thạc sĩ") =>
        new(rowNumber, id, "label_visa_f1", level, question, "Câu trả lời đủ dài.", "Nguồn\nMã: EXP-KB-1");

    [Fact]
    public async Task CreateAsync_DraftNeedsOnlyQuestion_AndNormalizesText()
    {
        var repo = new FakeKnowledgeRepository();
        var nfd = "Học   bổng là gì?".Normalize(NormalizationForm.FormD);

        var created = await Svc(repo).CreateAsync(new SaveKnowledgeEntryRequest(null, null, $"  {nfd} ", null, null), Actor);

        Assert.Equal(KnowledgeReviewStatuses.Draft, created.ReviewStatus);
        Assert.Equal("Học bổng là gì?", created.Question);
        Assert.True(created.Question.IsNormalized(NormalizationForm.FormC));
        Assert.Equal(Actor, Assert.Single(repo.Items).CreatedByUserId);
        Assert.Equal(KnowledgeHistoryActions.Create, Assert.Single(repo.History).Action);
    }

    [Fact]
    public async Task CreateAsync_SubmitWithoutAnswerLabelLevelsSource_IsRejected()
    {
        var repo = new FakeKnowledgeRepository();

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            Svc(repo).CreateAsync(new SaveKnowledgeEntryRequest(null, null, "OPT là gì?", null, null, Submit: true), Actor));

        Assert.Contains("nhãn", error.Message);
        Assert.Empty(repo.Items);
    }

    [Theory]
    [InlineData("LABEL_KHONG_CO", "dai_hoc")]
    [InlineData("LABEL_VISA_F1", "dai_hoc_2")]
    [InlineData("LABEL_VISA_F1", "chung,dai_hoc")]
    public async Task CreateAsync_RejectsUnknownLabelOrLevelAndChungWithOthers(string label, string levels)
    {
        var request = new SaveKnowledgeEntryRequest(label, levels.Split(','), "OPT là gì?", "Trả lời", "Nguồn");

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(new FakeKnowledgeRepository()).CreateAsync(request, Actor));
    }

    [Fact]
    public async Task CreateAsync_PhoneInAnswer_IsRejected_ButUrlInSourceIsFine()
    {
        var service = Svc(new FakeKnowledgeRepository());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            Full() with { Answer = "Gọi 0912 345 678 để được tư vấn." }, Actor));
        var ok = await service.CreateAsync(Full() with { Source = "U.S. Department of State\nURL: https://travel.state.gov" }, Actor);
        Assert.Contains("https://", ok.Source);
    }

    [Fact]
    public async Task CreateAsync_SameQuestionIgnoringCase_ThrowsWithExistingId()
    {
        var repo = new FakeKnowledgeRepository();
        var existing = repo.Add(KnowledgeReviewStatuses.Approved, "OPT là gì?");

        var error = await Assert.ThrowsAsync<KnowledgeDuplicateEntryException>(() =>
            Svc(repo).CreateAsync(Full("opt LÀ GÌ?"), Actor));

        Assert.Equal(existing.Id, error.ExistingId);
    }

    [Fact]
    public async Task UpdateAsync_ChangingSourceOfApprovedEntry_GoesBackToPending()
    {
        var repo = new FakeKnowledgeRepository();
        var approved = repo.Add(KnowledgeReviewStatuses.Approved);

        var updated = await Svc(repo).UpdateAsync(approved.Id, Full() with { Source = "Nguồn mới" }, Actor);

        Assert.Equal(KnowledgeReviewStatuses.Pending, updated!.ReviewStatus);
        Assert.Null(updated.ReviewedByUserId);
        var history = Assert.Single(repo.History);
        Assert.Equal(("edit", "source"), (history.Action, history.ChangedFields));
    }

    [Fact]
    public async Task UpdateAsync_SubmitDraftWithoutChanges_RecordsSubmit()
    {
        var repo = new FakeKnowledgeRepository();
        var draft = repo.Add(KnowledgeReviewStatuses.Draft);

        var updated = await Svc(repo).UpdateAsync(draft.Id, Full(submit: true), Actor);

        Assert.Equal(KnowledgeReviewStatuses.Pending, updated!.ReviewStatus);
        Assert.Equal(KnowledgeHistoryActions.Submit, Assert.Single(repo.History).Action);
    }

    [Fact]
    public async Task ReviewAsync_SkipsOwnNotPendingAndRequiresNoteForReject()
    {
        var repo = new FakeKnowledgeRepository();
        var mine = repo.Add(KnowledgeReviewStatuses.Pending, "Câu 1?", createdBy: Reviewer);
        var draft = repo.Add(KnowledgeReviewStatuses.Draft, "Câu 2?");
        var ok = repo.Add(KnowledgeReviewStatuses.Pending, "Câu 3?");
        var service = Svc(repo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewAsync(new ReviewKnowledgeEntriesRequest([ok.Id], KnowledgeReviewStatuses.Rejected, " "), Reviewer));
        var result = await service.ReviewAsync(
            new ReviewKnowledgeEntriesRequest([mine.Id, draft.Id, ok.Id, Guid.NewGuid()], KnowledgeReviewStatuses.Approved, null), Reviewer);

        Assert.Equal(new ReviewKnowledgeEntriesResultDto(1, 1, 1, 0, 1), result);
        Assert.Equal((KnowledgeReviewStatuses.Approved, Reviewer), (ok.ReviewStatus, ok.ReviewedByUserId));
    }

    [Fact]
    public async Task DeleteThenRestore_KeepsStatus_AndRestoreConflictsWithLiveDuplicate()
    {
        var repo = new FakeKnowledgeRepository();
        var entry = repo.Add(KnowledgeReviewStatuses.Approved, "Visa F-1 là gì?");
        var service = Svc(repo);

        Assert.True(await service.DeleteAsync(entry.Id, Actor));
        Assert.NotNull(entry.DeletedAt);
        var again = repo.Add(KnowledgeReviewStatuses.Draft, "Visa F-1 là gì?");
        await Assert.ThrowsAsync<KnowledgeDuplicateEntryException>(() => service.RestoreAsync(entry.Id, Actor));

        await service.DeleteAsync(again.Id, Actor);
        var restored = await service.RestoreAsync(entry.Id, Actor);

        Assert.Equal(KnowledgeReviewStatuses.Approved, restored!.ReviewStatus);
        Assert.Null(entry.DeletedAt);
        Assert.Null(await service.RestoreAsync(Guid.NewGuid(), Actor));
    }

    [Fact]
    public async Task ImportAsync_InsertsPending_CountsDuplicates_AndReportsIdCollisions()
    {
        var repo = new FakeKnowledgeRepository();
        repo.Add(KnowledgeReviewStatuses.Approved, "Câu đã có?", entryNo: 5);
        var service = Svc(repo,
            Row(2, "1.0", "Câu một?"),
            Row(3, null, "Câu hai?", level: "Chung"),
            Row(4, "5", "Câu đã có?"),          // cùng mã, cùng câu: trùng
            Row(5, "5", "Câu khác hẳn?"),       // cùng mã, khác câu: lỗi
            Row(6, null, "câu MỘT?"),           // trùng câu ở dòng trên
            Row(7, "x", "Câu ba?"),             // id sai
            Row(8, null, "Câu bốn?", level: "Chung; THPT"));

        var result = await service.ImportAsync(Stream.Null, Actor);

        Assert.Equal((7, 2, 2), (result.TotalRows, result.Inserted, result.Duplicates));
        Assert.Equal([5, 7, 8], result.Errors.Select(e => e.RowNumber));
        var inserted = repo.Items.Where(e => e.Origin == KnowledgeOrigins.Excel).ToList();
        Assert.All(inserted, e => Assert.Equal(KnowledgeReviewStatuses.Pending, e.ReviewStatus));
        Assert.Equal([1, 6], inserted.Select(e => e.EntryNo));    // dòng không id nhận số sau số lớn nhất (5)
        Assert.Equal([KnowledgeLevels.DaiHoc, KnowledgeLevels.ThacSi], inserted[0].Levels);
        Assert.Equal("LABEL_VISA_F1", inserted[0].Label);
    }

    [Fact]
    public async Task PreviewAsync_CapsRowsAt100_ReportsAllErrors_AndWritesNothing()
    {
        var repo = new FakeKnowledgeRepository();
        var rows = Enumerable.Range(2, 150).Select(n => Row(n, null, n % 50 == 0 ? "" : $"Câu {n}?")).ToArray();

        var preview = await Svc(repo, rows).PreviewAsync(Stream.Null);

        Assert.Equal((150, 100, 3), (preview.TotalRows, preview.Rows.Count, preview.Errors.Count));
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task CaptureUnansweredAsync_CollapsesSpaces_SkipsContactsAndDuplicatesIncludingDeleted()
    {
        var repo = new FakeKnowledgeRepository();
        var deleted = repo.Add(KnowledgeReviewStatuses.Draft, "Câu đã xóa?");
        deleted.DeletedAt = DateTime.UtcNow;
        var service = Svc(repo);

        Assert.True(await service.CaptureUnansweredAsync("  em   muốn hỏi  "));
        Assert.False(await service.CaptureUnansweredAsync("em muốn hỏi"));
        Assert.False(await service.CaptureUnansweredAsync("zalo em 0912345678"));
        Assert.False(await service.CaptureUnansweredAsync("câu đã xóa?"));

        var captured = repo.Items.Single(e => e.Origin == KnowledgeOrigins.ChatUnanswered);
        Assert.Equal(("em muốn hỏi", KnowledgeReviewStatuses.Draft, (Guid?)null), (captured.Question, captured.ReviewStatus, captured.CreatedByUserId));
    }
}

internal class FakeKnowledgeReader(params KnowledgeImportRow[] rows) : IKnowledgeWorkbookReader
{
    public IReadOnlyList<KnowledgeImportRow> Read(Stream workbook, CancellationToken ct = default) => rows;
}

internal class FakeKnowledgeRepository : IKnowledgeBaseRepository
{
    public List<KnowledgeBaseEntry> Items { get; } = [];
    public List<KnowledgeBaseEntryHistory> History { get; } = [];

    private IEnumerable<KnowledgeBaseEntry> Live => Items.Where(x => x.DeletedAt == null);

    /// <summary>Mục đủ nội dung ở trạng thái cho trước.</summary>
    public KnowledgeBaseEntry Add(string status, string question = "OPT là gì?", Guid? createdBy = null, int entryNo = 0)
    {
        var entry = new KnowledgeBaseEntry
        {
            EntryNo = entryNo == 0 ? Items.Count + 100 : entryNo,
            Label = "LABEL_VISA_F1",
            Levels = [KnowledgeLevels.DaiHoc],
            Question = question,
            Answer = "OPT là thời gian thực tập sau tốt nghiệp.",
            Source = "Hội đồng Chuyên gia DSS",
            ReviewStatus = status,
            CreatedByUserId = createdBy,
        };
        Items.Add(entry);
        return entry;
    }

    // Không dùng Search/Source trong test: EF.Functions.ILike chỉ chạy được trên database
    public Task<(IReadOnlyList<KnowledgeEntryListRow> Rows, int Total)> PageAsync(
        KnowledgeEntryFilter filter, int skip, int take, CancellationToken ct = default)
    {
        var source = filter.Deleted ? Items.Where(x => x.DeletedAt != null) : Live;
        var all = KnowledgeBaseRepository.Sorted(KnowledgeBaseRepository.Filtered(source.AsQueryable(), filter), filter.Sort).ToList();
        IReadOnlyList<KnowledgeEntryListRow> rows = all.Skip(skip).Take(take).Select(x => new KnowledgeEntryListRow(x, null)).ToList();
        return Task.FromResult((rows, all.Count));
    }

    public Task<KnowledgeBaseEntry?> FindAsync(Guid id, bool includeDeleted = false, CancellationToken ct = default) =>
        Task.FromResult((includeDeleted ? Items : Live).FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<KnowledgeEntryHistoryRow>> HistoryAsync(Guid entryId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeEntryHistoryRow>>(
            History.Where(h => h.EntryId == entryId).Select(h => new KnowledgeEntryHistoryRow(h, null)).ToList());

    public Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

    public Task AddAsync(KnowledgeBaseEntry entry, KnowledgeBaseEntryHistory history, CancellationToken ct = default)
    {
        Items.Add(entry);
        History.Add(history);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(
        IReadOnlyList<KnowledgeBaseEntry> entries, IReadOnlyList<KnowledgeBaseEntryHistory> history, CancellationToken ct = default)
    {
        Items.AddRange(entries);
        History.AddRange(history);
        return Task.CompletedTask;
    }

    public Task<int> UpdateManyAsync(
        IReadOnlyList<Guid> ids, Func<KnowledgeBaseEntry, KnowledgeBaseEntryHistory?> apply,
        bool includeDeleted = false, CancellationToken ct = default)
    {
        var matched = (includeDeleted ? Items : Live).Where(x => ids.Contains(x.Id)).ToList();
        foreach (var entry in matched)
        {
            if (apply(entry) is not { } history)
                continue;
            history.EntryId = entry.Id;
            History.Add(history);
        }
        return Task.FromResult(matched.Count);
    }

    public Task<IReadOnlyList<(Guid EntryId, Guid ActorUserId)>> ContributorsAsync(
        IReadOnlyList<Guid> entryIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<(Guid, Guid)>>(History
            .Where(h => entryIds.Contains(h.EntryId) && h.ActorUserId != null
                        && h.Action is KnowledgeHistoryActions.Create or KnowledgeHistoryActions.Edit or KnowledgeHistoryActions.Submit)
            .Select(h => (h.EntryId, h.ActorUserId!.Value)).Distinct().ToList());

    public Task<IReadOnlyList<KnowledgeKeyRow>> ExistingKeysAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeKeyRow>>(Items.Select(x => new KnowledgeKeyRow(x.EntryNo, x.Question)).ToList());

    public bool Fail { get; init; }

    public Task<IReadOnlyList<KnowledgeBaseEntry>> SameQuestionAsync(string question, bool includeDeleted, CancellationToken ct = default) =>
        Fail
            ? throw new InvalidOperationException("db down")
            : Task.FromResult<IReadOnlyList<KnowledgeBaseEntry>>(
                KnowledgeBaseRepository.SameQuestion((includeDeleted ? Items : Live).AsQueryable(), question).ToList());

    public Task<IReadOnlyList<KnowledgeCreatorDto>> CreatorsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeCreatorDto>>([]);
}
