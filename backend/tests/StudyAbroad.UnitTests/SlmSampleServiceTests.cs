using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Application.Audit;
using StudyAbroad.Infrastructure.Import;
using StudyAbroad.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace StudyAbroad.UnitTests;

public class SlmSampleServiceTests
{
    private static SlmSampleService Svc(ISlmSampleRepository repo, ISlmWorkbookReader reader, FakeAuditLogs? audit = null) =>
        new(repo, reader, new SlmTemplateWriter(), audit ?? new FakeAuditLogs());

    private static readonly Guid Actor = Guid.NewGuid();
    private const string ValidIntents = "[{\"type\":\"knowledge\",\"span\":\"OPT là gì\",\"entities\":{}}]";

    private static SaveSlmSampleRequest Request(
        string? type = SlmSampleTypes.Knowledge, string? question = "OPT là gì", string? intents = ValidIntents,
        string? profile = null, string? context = null, string? split = null) =>
        new(type, question, profile, null, intents, context, split, null);

    [Fact]
    public async Task CreateAsync_StartsAsDraftWithTrainSplitAndCreator()
    {
        var repo = new FakeSlmRepository();
        var service = Svc(repo, new FakeReader());

        var created = await service.CreateAsync(Request(question: "  OPT là gì  "), Actor);

        Assert.Equal(SlmReviewStatuses.Draft, created.ReviewStatus);
        Assert.Equal(SlmSampleSplits.Train, created.Split);
        Assert.Equal("OPT là gì", created.Question);
        Assert.Equal(Actor, Assert.Single(repo.Items).CreatedByUserId);
    }

    [Theory]
    [InlineData("unknown_type", "câu hỏi", ValidIntents)]
    [InlineData(SlmSampleTypes.Knowledge, "   ", ValidIntents)]
    [InlineData(SlmSampleTypes.Knowledge, "câu hỏi", "{\"a\":1}")]
    [InlineData(SlmSampleTypes.Knowledge, "câu hỏi", "[not json")]
    public async Task CreateAsync_RejectsInvalidInput(string type, string question, string? intents)
    {
        var repo = new FakeSlmRepository();
        var service = Svc(repo, new FakeReader());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(Request(type, question, intents), Actor));
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidProfileJsonAndSplit()
    {
        var service = Svc(new FakeSlmRepository(), new FakeReader());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Request(profile: "{broken"), Actor));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Request(split: "dev"), Actor));
    }

    [Fact]
    public async Task UpdateAsync_ChangingIntentsOfApprovedSampleReturnsToPending()
    {
        var repo = new FakeSlmRepository();
        var approved = repo.AddApproved();
        var service = Svc(repo, new FakeReader());

        var updated = await service.UpdateAsync(
            approved.Id, Request(intents: "[{\"type\":\"knowledge\",\"span\":\"khác\",\"entities\":{}}]"), Actor);

        Assert.Equal(SlmReviewStatuses.Pending, updated!.ReviewStatus);
        Assert.Null(updated.ReviewedByUserId);
        Assert.Null(updated.ReviewedAt);
        Assert.Null(updated.ReviewNote);
    }

    [Fact]
    public async Task UpdateAsync_ChangingOnlySplitKeepsApproval()
    {
        var repo = new FakeSlmRepository();
        var approved = repo.AddApproved();
        var service = Svc(repo, new FakeReader());

        var updated = await service.UpdateAsync(approved.Id, Request(split: SlmSampleSplits.Test), Actor);

        Assert.Equal(SlmReviewStatuses.Approved, updated!.ReviewStatus);
        Assert.Equal(SlmSampleSplits.Test, updated.Split);
    }

    [Fact]
    public async Task UpdateAsync_SameJsonWithDifferentKeyOrderAndSpacingKeepsApproval()
    {
        var repo = new FakeSlmRepository();
        var approved = repo.AddApproved();
        approved.Intents = "[{\"type\":\"knowledge\",\"span\":\"OPT là gì\",\"entities\":{\"a\":1,\"b\":2}}]";
        var service = Svc(repo, new FakeReader());

        var reordered = "[ { \"entities\": { \"b\": 2, \"a\": 1 }, \"span\": \"OPT là gì\", \"type\": \"knowledge\" } ]";
        var updated = await service.UpdateAsync(approved.Id, Request(intents: reordered), Actor);

        Assert.Equal(SlmReviewStatuses.Approved, updated!.ReviewStatus);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullWhenMissing()
    {
        var service = Svc(new FakeSlmRepository(), new FakeReader());

        Assert.Null(await service.UpdateAsync(Guid.NewGuid(), Request(), Actor));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseWhenMissing()
    {
        var repo = new FakeSlmRepository();
        var existing = repo.AddDraft();
        var service = Svc(repo, new FakeReader());

        Assert.True(await service.DeleteAsync(existing.Id, Actor));
        Assert.False(await service.DeleteAsync(existing.Id, Actor));
    }

    [Fact]
    public async Task ReviewAsync_UpdatesManyAndCountsMissing()
    {
        var repo = new FakeSlmRepository();
        var first = repo.AddPending();
        var second = repo.AddPending();
        var service = Svc(repo, new FakeReader());

        var result = await service.ReviewAsync(
            new ReviewSlmSamplesRequest([first.Id, second.Id, Guid.NewGuid(), first.Id], SlmReviewStatuses.Rejected, "  sai nhãn  "), Actor);

        Assert.Equal(2, result.Updated);
        Assert.Equal(1, result.NotFound);
        Assert.All(new[] { first, second }, x =>
        {
            Assert.Equal(SlmReviewStatuses.Rejected, x.ReviewStatus);
            Assert.Equal("sai nhãn", x.ReviewNote);
            Assert.Equal(Actor, x.ReviewedByUserId);
            Assert.NotNull(x.ReviewedAt);
        });
    }

    [Fact]
    public async Task ReviewAsync_RejectsBadRequests()
    {
        var service = Svc(new FakeSlmRepository(), new FakeReader());
        var tooMany = Enumerable.Range(0, SlmSampleService.MaxReviewIds + 1).Select(_ => Guid.NewGuid()).ToList();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewAsync(new ReviewSlmSamplesRequest([Guid.NewGuid()], "verified", null), Actor));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewAsync(new ReviewSlmSamplesRequest([], SlmReviewStatuses.Approved, null), Actor));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewAsync(new ReviewSlmSamplesRequest(tooMany, SlmReviewStatuses.Approved, null), Actor));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewAsync(new ReviewSlmSamplesRequest([Guid.NewGuid()], SlmReviewStatuses.Approved, new string('x', 1001)), Actor));
    }

    [Fact]
    public async Task ImportAsync_InsertsValidRowsAsDraftAndReportsErrorsAndDuplicates()
    {
        var repo = new FakeSlmRepository();
        repo.Items.Add(new SlmTrainingSample
        {
            SampleType = SlmSampleTypes.Knowledge, Question = "đã có", Intents = ValidIntents,
        });
        var reader = new FakeReader(
            new SlmImportRow(SlmSampleTypes.Knowledge, 2, "mới", null, null, ValidIntents, null, "val", "v10"),
            new SlmImportRow(SlmSampleTypes.Knowledge, 3, "đã có", null, null, "[ { \"entities\": {}, \"span\": \"OPT là gì\", \"type\": \"knowledge\" } ]", null, "train", null),
            new SlmImportRow(SlmSampleTypes.Knowledge, 4, "mới", null, null, ValidIntents, null, "val", "v10"),
            new SlmImportRow(SlmSampleTypes.Scenario, 5, "lỗi json", null, null, "[oops", null, "train", null),
            new SlmImportRow(SlmSampleTypes.Scenario, 6, "", null, null, ValidIntents, null, "train", null));
        var service = Svc(repo, reader);

        var result = await service.ImportAsync(new MemoryStream(), Actor);

        Assert.Equal(5, result.TotalRows);
        Assert.Equal(1, result.Inserted);
        Assert.Equal(2, result.Duplicates);
        Assert.Equal([5, 6], result.Errors.Select(e => e.RowNumber).ToArray());
        var inserted = repo.Items.Single(x => x.Question == "mới");
        Assert.Equal(SlmReviewStatuses.Draft, inserted.ReviewStatus);
        Assert.Equal("val", inserted.Split);
        Assert.Equal("v10", inserted.SourceReview);
        Assert.Equal(SlmSampleSources.Excel, inserted.Source);
        Assert.Equal(Actor, inserted.CreatedByUserId);
    }

    [Fact]
    public async Task ImportAsync_InsertsNothingWhenAllRowsFail()
    {
        var repo = new FakeSlmRepository();
        var reader = new FakeReader(new SlmImportRow(SlmSampleTypes.Knowledge, 2, null, null, null, ValidIntents, null, null, null));
        var service = Svc(repo, reader);

        var result = await service.ImportAsync(new MemoryStream(), Actor);

        Assert.Equal(0, result.Inserted);
        Assert.Single(result.Errors);
        Assert.Empty(repo.Items);
        Assert.Equal(0, repo.AddRangeCalls);
    }

    [Fact]
    public async Task GetSummaryAsync_ListsEveryStatusAndTypeEvenWhenZero()
    {
        var repo = new FakeSlmRepository();
        repo.AddApproved();
        repo.AddDraft();
        var service = Svc(repo, new FakeReader());

        var summary = await service.GetSummaryAsync();

        Assert.Equal(2, summary.Total);
        Assert.Equal(1, summary.ByStatus[SlmReviewStatuses.Approved]);
        Assert.Equal(0, summary.ByStatus[SlmReviewStatuses.Rejected]);
        Assert.Equal(2, summary.ByType[SlmSampleTypes.Knowledge]);
        Assert.Equal(0, summary.ByType[SlmSampleTypes.Scenario]);
    }

    [Fact]
    public async Task GetPageAsync_ClampsPagingAndRejectsUnknownFilter()
    {
        var repo = new FakeSlmRepository();
        for (var i = 0; i < 3; i++)
            repo.AddDraft();
        var service = Svc(repo, new FakeReader());

        var page = await service.GetPageAsync(new SlmSampleFilter(null, null, null, null), -4, 1000);

        Assert.Equal(1, page.Page);
        Assert.Equal(SlmSampleService.MaxPageSize, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetPageAsync(new SlmSampleFilter("nope", null, null, null), 1, 20));
    }

    [Fact]
    public async Task UpdateAsync_RejectsInvalidIntentsJsonAndDoesNotChangeData()
    {
        var repo = new FakeSlmRepository();
        var sample = repo.AddApproved();
        var originalQuestion = sample.Question;
        var service = Svc(repo, new FakeReader());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(sample.Id, Request(intents: "[oops"), Actor));

        Assert.Equal(originalQuestion, repo.Items.Single().Question);
        Assert.Equal(SlmReviewStatuses.Approved, repo.Items.Single().ReviewStatus);
    }

    [Fact]
    public async Task UpdateAsync_RejectsIntentsNotArrayAndDoesNotChangeData()
    {
        var repo = new FakeSlmRepository();
        var sample = repo.AddApproved();
        var originalStatus = sample.ReviewStatus;
        var service = Svc(repo, new FakeReader());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(sample.Id, Request(intents: "{\"a\":1}"), Actor));

        Assert.Equal(originalStatus, repo.Items.Single().ReviewStatus);
    }

    [Fact]
    public async Task ReviewAsync_ToApproved_SetsReviewerAndTimeAndNote()
    {
        var repo = new FakeSlmRepository();
        var sample = repo.AddPending();
        var service = Svc(repo, new FakeReader());

        var before = DateTime.UtcNow;
        await service.ReviewAsync(
            new ReviewSlmSamplesRequest([sample.Id], SlmReviewStatuses.Approved, "LGTM"), Actor);

        Assert.Equal(SlmReviewStatuses.Approved, sample.ReviewStatus);
        Assert.Equal("LGTM", sample.ReviewNote);
        Assert.Equal(Actor, sample.ReviewedByUserId);
        Assert.True(sample.ReviewedAt >= before);
    }

    [Fact]
    public async Task ReviewAsync_ToNeedsFix_SetsReviewerAndTimeAndNote()
    {
        var repo = new FakeSlmRepository();
        var sample = repo.AddPending();
        var service = Svc(repo, new FakeReader());

        var before = DateTime.UtcNow;
        await service.ReviewAsync(
            new ReviewSlmSamplesRequest([sample.Id], SlmReviewStatuses.NeedsFix, "cần sửa câu hỏi"), Actor);

        Assert.Equal(SlmReviewStatuses.NeedsFix, sample.ReviewStatus);
        Assert.Equal("cần sửa câu hỏi", sample.ReviewNote);
        Assert.Equal(Actor, sample.ReviewedByUserId);
        Assert.True(sample.ReviewedAt >= before);
    }

    [Fact]
    public async Task ReviewAsync_WhitespaceOnlyNote_BecomesNull()
    {
        var repo = new FakeSlmRepository();
        var sample = repo.AddPending();
        var service = Svc(repo, new FakeReader());

        await service.ReviewAsync(
            new ReviewSlmSamplesRequest([sample.Id], SlmReviewStatuses.Approved, "   "), Actor);

        Assert.Null(sample.ReviewNote);
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        var service = Svc(new FakeSlmRepository(), new FakeReader());

        Assert.Null(await service.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetPageAsync_PassesTrimmedFilters()
    {
        var repo = new FakeSlmRepository();
        repo.AddDraft();
        var service = Svc(repo, new FakeReader());

        var page = await service.GetPageAsync(
            new SlmSampleFilter("  " + SlmSampleTypes.Knowledge + "  ", null, null, null), 1, 10);

        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task ReviewAsync_NoteAtExactlyMaxLength_Passes()
    {
        var repo = new FakeSlmRepository();
        var sample = repo.AddPending();
        var service = Svc(repo, new FakeReader());
        var note = new string('x', SlmSampleService.MaxNoteLength);

        await service.ReviewAsync(
            new ReviewSlmSamplesRequest([sample.Id], SlmReviewStatuses.Approved, note), Actor);

        Assert.Equal(note, sample.ReviewNote);
    }

    [Fact]
    public async Task ReviewAsync_AtMaxReviewIds_Passes()
    {
        var repo = new FakeSlmRepository();
        for (var i = 0; i < SlmSampleService.MaxReviewIds; i++)
            repo.AddPending();
        var ids = repo.Items.Select(x => x.Id).ToList();
        var service = Svc(repo, new FakeReader());

        var result = await service.ReviewAsync(
            new ReviewSlmSamplesRequest(ids, SlmReviewStatuses.Approved, null), Actor);

        Assert.Equal(SlmSampleService.MaxReviewIds, result.Updated);
    }

    [Fact]
    public async Task PreviewAsync_ValidatesAllRowsCapsRowsAndWritesNothing()
    {
        var repo = new FakeSlmRepository();
        var rows = new List<SlmImportRow>();
        for (var i = 0; i < SlmSampleService.MaxPreviewRows + 20; i++)
            rows.Add(new SlmImportRow(SlmSampleTypes.Knowledge, i + 2, $"câu {i}", null, null, ValidIntents, null, "train", null));
        rows.Add(new SlmImportRow(SlmSampleTypes.Scenario, 500, "", null, null, ValidIntents, null, "train", null));
        rows.Add(new SlmImportRow(SlmSampleTypes.Scenario, 501, "lỗi json", null, null, "[oops", null, "train", null));
        var service = Svc(repo, new FakeReader(rows.ToArray()));

        var preview = await service.PreviewAsync(new MemoryStream());

        Assert.Equal(rows.Count, preview.TotalRows);
        Assert.Equal(SlmSampleService.MaxPreviewRows, preview.Rows.Count);
        Assert.Equal(2, preview.Rows[0].RowNumber);
        Assert.Equal([500, 501], preview.Errors.Select(e => e.RowNumber).ToArray());
        Assert.All(preview.Errors, e => Assert.Equal(SlmSampleTypes.Scenario, e.SheetName));
        Assert.All(preview.Errors, e => Assert.False(string.IsNullOrWhiteSpace(e.Message)));
        Assert.Empty(repo.Items);
        Assert.Equal(0, repo.AddRangeCalls);
        Assert.Equal(0, repo.AddCalls);
        Assert.Equal(0, repo.ExistingKeysCalls);
    }

    [Fact]
    public async Task PreviewAsync_ReportsErrorsBeyondTheCappedRows()
    {
        var rows = new List<SlmImportRow>();
        for (var i = 0; i < SlmSampleService.MaxPreviewRows; i++)
            rows.Add(new SlmImportRow(SlmSampleTypes.Knowledge, i + 2, $"câu {i}", null, null, ValidIntents, null, null, null));
        rows.Add(new SlmImportRow(SlmSampleTypes.Knowledge, 9999, "sai tập", null, null, ValidIntents, null, "dev", null));
        var service = Svc(new FakeSlmRepository(), new FakeReader(rows.ToArray()));

        var preview = await service.PreviewAsync(new MemoryStream());

        Assert.Equal(SlmSampleService.MaxPreviewRows, preview.Rows.Count);
        var error = Assert.Single(preview.Errors);
        Assert.Equal(9999, error.RowNumber);
    }

    [Fact]
    public async Task PreviewAsync_DoesNotCountDuplicatesAsErrors()
    {
        var repo = new FakeSlmRepository();
        repo.Items.Add(new SlmTrainingSample { SampleType = SlmSampleTypes.Knowledge, Question = "đã có", Intents = ValidIntents });
        var reader = new FakeReader(new SlmImportRow(SlmSampleTypes.Knowledge, 2, "đã có", null, null, ValidIntents, null, "train", null));
        var service = Svc(repo, reader);

        var preview = await service.PreviewAsync(new MemoryStream());

        Assert.Empty(preview.Errors);
        Assert.Single(preview.Rows);
    }

    [Fact]
    public async Task PreviewAsync_NoDataRows_ThrowsInvalidDataException()
    {
        var service = Svc(new FakeSlmRepository(), new FakeReader());

        await Assert.ThrowsAsync<InvalidDataException>(() => service.PreviewAsync(new MemoryStream()));
    }

    [Fact]
    public async Task PreviewAsync_PassesCancellationToReader()
    {
        var reader = new FakeReader(new SlmImportRow(SlmSampleTypes.Knowledge, 2, "câu", null, null, ValidIntents, null, null, null));
        var service = Svc(new FakeSlmRepository(), reader);
        using var cts = new CancellationTokenSource();

        await service.PreviewAsync(new MemoryStream(), cts.Token);

        Assert.Equal(cts.Token, reader.LastToken);
    }

    [Fact]
    public async Task PreviewAsync_CancelledToken_Throws()
    {
        var reader = new FakeReader(new SlmImportRow(SlmSampleTypes.Knowledge, 2, "câu", null, null, ValidIntents, null, null, null));
        var service = Svc(new FakeSlmRepository(), reader);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PreviewAsync(new MemoryStream(), cts.Token));
    }

    [Fact]
    public async Task ImportAsync_NoDataRows_ThrowsInvalidDataExceptionAndInsertsNothing()
    {
        var repo = new FakeSlmRepository();
        var service = Svc(repo, new FakeReader());

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(new MemoryStream(), Actor));
        Assert.Equal(0, repo.AddRangeCalls);
    }

    [Fact]
    public async Task ImportAsync_CanRunAgainAfterEarlierImport()
    {
        var repo = new FakeSlmRepository();
        var first = new FakeReader(new SlmImportRow(SlmSampleTypes.Knowledge, 2, "lần một", null, null, ValidIntents, null, null, null));
        var second = new FakeReader(new SlmImportRow(SlmSampleTypes.Knowledge, 2, "lần hai", null, null, ValidIntents, null, null, null));

        var firstResult = await Svc(repo, first).ImportAsync(new MemoryStream(), Actor);
        var secondResult = await Svc(repo, second).ImportAsync(new MemoryStream(), Actor);

        Assert.Equal(1, firstResult.Inserted);
        Assert.Equal(1, secondResult.Inserted);
        Assert.Equal(2, repo.Items.Count);
    }

    private static readonly Guid Other = Guid.NewGuid();

    private static SaveSlmSampleRequest QA(string question = "Visa F-1 cần gì?", string? answer = "Cần I-20 và hộ chiếu.", bool submit = false) =>
        new(SlmSampleTypes.Knowledge, question, null, null, null, null, null, null, answer, "visa", "Website ĐSQ", submit);

    [Fact]
    public async Task CreateAsync_QuestionAndAnswerDraft_StoresFieldsAndHistory()
    {
        var repo = new FakeSlmRepository();

        var created = await Svc(repo, new FakeReader()).CreateAsync(QA(), Actor);

        Assert.Equal((SlmReviewStatuses.Draft, "Cần I-20 và hộ chiếu.", "visa", "Website ĐSQ", "[]"),
            (created.ReviewStatus, created.Answer, created.Topic, created.Source, created.Intents));
        var history = Assert.Single(repo.History);
        Assert.Equal((SlmSampleHistoryActions.Create, Actor, SlmReviewStatuses.Draft), (history.Action, history.ActorUserId, history.ToStatus));
    }

    [Fact]
    public async Task CreateAsync_Submit_NeedsAnAnswerOrIntents()
    {
        var repo = new FakeSlmRepository();
        var service = Svc(repo, new FakeReader());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(QA(answer: null, submit: true), Actor));
        var draft = await service.CreateAsync(QA(answer: null), Actor);
        var pending = await service.CreateAsync(QA(question: "OPT là gì?", submit: true), Actor);

        Assert.Equal(SlmReviewStatuses.Draft, draft.ReviewStatus);
        Assert.Equal(SlmReviewStatuses.Pending, pending.ReviewStatus);
    }

    [Fact]
    public async Task CreateAsync_DuplicateIgnoringCaseAndSpaces_ThrowsWithExistingId_ButDeletedRowsDoNotBlock()
    {
        var repo = new FakeSlmRepository();
        var service = Svc(repo, new FakeReader());
        var first = await service.CreateAsync(QA(), Actor);

        var ex = await Assert.ThrowsAsync<SlmDuplicateSampleException>(() => service.CreateAsync(QA(question: "  visa f-1 CẦN gì?  "), Actor));
        Assert.Equal(first.Id, ex.ExistingId);

        await service.DeleteAsync(first.Id, Actor);
        await service.CreateAsync(QA(), Actor);
        Assert.Equal(2, repo.Items.Count);
    }

    [Theory]
    [InlineData("Số em là 0912 345 678, tư vấn giúp em", null)]
    [InlineData("Học bổng nào phù hợp?", "Liên hệ an.nguyen@gmail.com")]
    public async Task CreateAsync_PersonalData_IsRejected(string question, string? answer)
    {
        var repo = new FakeSlmRepository();

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo, new FakeReader()).CreateAsync(QA(question, answer), Actor));
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task UpdateAsync_SubmitDraft_GoesToPendingWithSubmitHistory_AndNoChangeWritesNothing()
    {
        var repo = new FakeSlmRepository();
        var service = Svc(repo, new FakeReader());
        var draft = await service.CreateAsync(QA(), Actor);

        await service.UpdateAsync(draft.Id, QA(), Actor);
        var submitted = await service.UpdateAsync(draft.Id, QA(submit: true), Actor);

        Assert.Equal(SlmReviewStatuses.Pending, submitted!.ReviewStatus);
        Assert.Equal([SlmSampleHistoryActions.Create, SlmSampleHistoryActions.Submit], repo.History.Select(h => h.Action));
    }

    [Fact]
    public async Task UpdateAsync_RecordsChangedFields()
    {
        var repo = new FakeSlmRepository();
        var service = Svc(repo, new FakeReader());
        var draft = await service.CreateAsync(QA(), Actor);

        await service.UpdateAsync(draft.Id, QA(answer: "Cần I-20, DS-160 và hộ chiếu."), Actor);

        var detail = await service.GetDetailAsync(draft.Id);
        var edit = detail!.History.Last();
        Assert.Equal(SlmSampleHistoryActions.Edit, edit.Action);
        Assert.Equal(["answer"], edit.ChangedFields);
    }

    [Fact]
    public async Task DeleteAsync_IsSoft_KeepsTheRowWithActor_AndHidesIt()
    {
        var repo = new FakeSlmRepository();
        var service = Svc(repo, new FakeReader());
        var sample = repo.AddDraft();

        Assert.True(await service.DeleteAsync(sample.Id, Actor));

        Assert.Equal(Actor, Assert.Single(repo.Items).DeletedByUserId);
        Assert.NotNull(sample.DeletedAt);
        Assert.Null(await service.GetAsync(sample.Id));
        Assert.Equal(0, (await service.GetPageAsync(new SlmSampleFilter(null, null, null, null), 1, 20)).TotalCount);
        Assert.Equal(SlmSampleHistoryActions.Delete, Assert.Single(repo.History).Action);
    }

    [Theory]
    [InlineData("gọi 0987654321", "Website ĐSQ")]
    [InlineData("visa", "hs.lop12@yahoo.com")]
    public async Task SaveAsync_PersonalDataInTopicOrSource_IsRejected(string topic, string source)
    {
        var repo = new FakeSlmRepository();
        var service = Svc(repo, new FakeReader());
        var request = QA() with { Topic = topic, Source = source };
        var existing = await service.CreateAsync(QA(question: "câu khác"), Actor);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request, Actor));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(existing.Id, request, Actor));
        Assert.Single(repo.Items);
    }

    [Theory]
    [InlineData(SlmSampleSources.Manual)]
    [InlineData(SlmSampleSources.Excel)]
    [InlineData(SlmSampleSources.ChatUnanswered)]
    public async Task SaveAsync_ReservedSource_CannotBeTypedButUnchangedValueCanBeResent(string reserved)
    {
        var repo = new FakeSlmRepository();
        var service = Svc(repo, new FakeReader());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(QA() with { Source = reserved }, Actor));

        var created = await service.CreateAsync(QA(), Actor);
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(created.Id, QA() with { Source = reserved }, Actor));
        repo.Items.Single().Source = reserved;
        Assert.NotNull(await service.UpdateAsync(created.Id, QA(answer: "đổi") with { Source = reserved }, Actor));
        Assert.Equal(reserved, repo.Items.Single().Source);
    }

    [Fact]
    public async Task ReviewAsync_SkipsSamplesTheReviewerCreatedOrEdited()
    {
        var repo = new FakeSlmRepository();
        var service = Svc(repo, new FakeReader());
        var own = await service.CreateAsync(QA(question: "câu của tôi", submit: true), Actor);
        var captured = repo.AddDraft();
        await service.UpdateAsync(captured.Id, QA(question: captured.Question, submit: true), Actor);
        var others = await service.CreateAsync(QA(question: "câu người khác", submit: true), Other);

        var result = await service.ReviewAsync(
            new ReviewSlmSamplesRequest([own.Id, captured.Id, others.Id], SlmReviewStatuses.Approved, null), Actor);

        Assert.Equal((1, 2, 0), (result.Updated, result.SkippedOwn, result.SkippedNotPending));
        Assert.Equal(SlmReviewStatuses.Approved, repo.Items.Single(x => x.Id == others.Id).ReviewStatus);
        Assert.Equal(SlmReviewStatuses.Pending, repo.Items.Single(x => x.Id == own.Id).ReviewStatus);
    }

    [Theory]
    [InlineData(SlmReviewStatuses.Approved)]
    [InlineData(SlmReviewStatuses.Rejected)]
    [InlineData(SlmReviewStatuses.NeedsFix)]
    public async Task ReviewAsync_OnlyPendingSamplesCanBeReviewed(string status)
    {
        var repo = new FakeSlmRepository();
        var draft = repo.AddDraft();
        var approved = repo.AddApproved();
        var rejected = repo.AddDraft();
        rejected.ReviewStatus = SlmReviewStatuses.Rejected;
        var needsFix = repo.AddDraft();
        needsFix.ReviewStatus = SlmReviewStatuses.NeedsFix;
        var pending = repo.AddPending();

        var result = await Svc(repo, new FakeReader()).ReviewAsync(
            new ReviewSlmSamplesRequest([draft.Id, approved.Id, rejected.Id, needsFix.Id, pending.Id], status, "ghi chú"), Actor);

        Assert.Equal((1, 4), (result.Updated, result.SkippedNotPending));
        Assert.Equal(status, pending.ReviewStatus);
        Assert.Equal(SlmReviewStatuses.Draft, draft.ReviewStatus);
        Assert.Equal(SlmReviewStatuses.Approved, approved.ReviewStatus);
        Assert.Equal(SlmReviewStatuses.Rejected, rejected.ReviewStatus);
        Assert.Equal(SlmReviewStatuses.NeedsFix, needsFix.ReviewStatus);
        Assert.Single(repo.History, h => h.Action == SlmSampleHistoryActions.Review);
    }

    [Theory]
    [InlineData(SlmReviewStatuses.Rejected)]
    [InlineData(SlmReviewStatuses.NeedsFix)]
    public async Task ReviewAsync_RejectOrNeedsFix_RequiresAReason(string status)
    {
        var repo = new FakeSlmRepository();
        var sample = repo.AddPending();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Svc(repo, new FakeReader()).ReviewAsync(new ReviewSlmSamplesRequest([sample.Id], status, "  "), Actor));
        Assert.Equal(SlmReviewStatuses.Pending, sample.ReviewStatus);
    }

    [Theory]
    [InlineData(SlmReviewStatuses.Draft)]
    [InlineData(SlmReviewStatuses.Pending)]
    public async Task ReviewAsync_CannotMoveToDraftOrPending(string status)
    {
        var repo = new FakeSlmRepository();
        var sample = repo.AddApproved();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Svc(repo, new FakeReader()).ReviewAsync(new ReviewSlmSamplesRequest([sample.Id], status, "x"), Actor));
    }

    [Fact]
    public async Task ReviewAsync_ApproveSkipsRowsWithoutAnswerOrIntents()
    {
        var repo = new FakeSlmRepository();
        var service = Svc(repo, new FakeReader());
        var ready = repo.AddPending();
        var empty = repo.AddPending();
        empty.Intents = "[]";

        var result = await service.ReviewAsync(new ReviewSlmSamplesRequest([ready.Id, empty.Id], SlmReviewStatuses.Approved, null), Actor);

        Assert.Equal((1, 1), (result.Updated, result.SkippedNotReady));
        Assert.Equal(SlmReviewStatuses.Pending, empty.ReviewStatus);
        var review = repo.History.Single(h => h.Action == SlmSampleHistoryActions.Review);
        Assert.Equal((SlmReviewStatuses.Pending, SlmReviewStatuses.Approved, Actor), (review.FromStatus, review.ToStatus, review.ActorUserId));
    }

    [Fact]
    public async Task CaptureUnansweredAsync_StoresOnlyTheQuestionAsAnonymousDraft()
    {
        var repo = new FakeSlmRepository();

        Assert.True(await Svc(repo, new FakeReader()).CaptureUnansweredAsync("  em muốn học   ngành hải dương học ở Hawaii  "));

        var sample = Assert.Single(repo.Items);
        Assert.Equal("em muốn học ngành hải dương học ở Hawaii", sample.Question);
        Assert.Equal((SlmReviewStatuses.Draft, SlmSampleSources.ChatUnanswered, "[]"), (sample.ReviewStatus, sample.Source, sample.Intents));
        Assert.Null(sample.CreatedByUserId);
        Assert.Null(sample.Answer);
        Assert.Null(sample.Profile);
        Assert.Null(sample.Context);
        var history = Assert.Single(repo.History);
        Assert.Equal((SlmSampleHistoryActions.Capture, (Guid?)null), (history.Action, history.ActorUserId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("gọi em số 0987654321 nha")]
    [InlineData("mail em: hs.lop12@yahoo.com")]
    [InlineData("xem giúp em https://example.org/ho-so")]
    [InlineData("facebook.com/nguyenvana của em")]
    [InlineData("CCCD em 001 203 004 567 có cần không")]
    public async Task CaptureUnansweredAsync_SkipsEmptyAndPersonalData(string question)
    {
        var repo = new FakeSlmRepository();

        Assert.False(await Svc(repo, new FakeReader()).CaptureUnansweredAsync(question));
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task CaptureUnansweredAsync_SkipsQuestionsOverTheLengthCap()
    {
        var repo = new FakeSlmRepository();
        var service = Svc(repo, new FakeReader());

        Assert.False(await service.CaptureUnansweredAsync(new string('a', SlmSampleService.MaxCapturedQuestionLength + 1)));
        Assert.True(await service.CaptureUnansweredAsync(new string('a', SlmSampleService.MaxCapturedQuestionLength)));
    }

    [Fact]
    public async Task CaptureUnansweredAsync_DedupesAgainstAllRowsIncludingDeleted()
    {
        var repo = new FakeSlmRepository();
        var service = Svc(repo, new FakeReader());
        await service.CaptureUnansweredAsync("Ngành nào dễ xin học bổng?");
        await service.DeleteAsync(repo.Items.Single().Id, Actor);

        Assert.False(await service.CaptureUnansweredAsync("ngành nào  dễ xin học bổng?"));
        Assert.Single(repo.Items);
    }

    [Fact]
    public async Task ExportAsync_Jsonl_HasOnlyApprovedLiveRows_AsParsedJson_AndIsLogged()
    {
        var repo = new FakeSlmRepository();
        var audit = new FakeAuditLogs();
        var approved = repo.AddApproved();
        approved.Answer = "OPT là giấy phép làm việc";
        var deleted = repo.AddApproved();
        deleted.Question = "đã xóa";
        deleted.DeletedAt = DateTime.UtcNow;
        repo.AddDraft();
        var service = Svc(repo, new FakeReader(), audit);

        var file = await service.ExportAsync("JSONL", Actor, "1.2.3.4", "test");

        var lines = System.Text.Encoding.UTF8.GetString(file.Content).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        using var line = System.Text.Json.JsonDocument.Parse(Assert.Single(lines));
        var root = line.RootElement;
        Assert.Equal(approved.Id.ToString(), root.GetProperty("id").GetString());
        Assert.Equal("OPT là gì", root.GetProperty("input").GetProperty("question").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Array, root.GetProperty("output").GetProperty("intents").ValueKind);
        Assert.Equal("OPT là giấy phép làm việc", root.GetProperty("output").GetProperty("answer").GetString());
        Assert.EndsWith(".jsonl", file.FileName);
        Assert.NotNull(approved.ExportedAt);
        Assert.Null(deleted.ExportedAt);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal((SlmSampleService.ExportAction, (Guid?)Actor, "1.2.3.4"), (entry.Action, entry.ActorUserId, entry.IpAddress));
        Assert.Contains("\"count\":1", entry.DetailJson);
    }

    [Fact]
    public async Task ExportAsync_XlsxWorks_EmptyExportIsStillLogged_AndUnknownFormatIsRejected()
    {
        var audit = new FakeAuditLogs();
        var service = Svc(new FakeSlmRepository(), new FakeReader(), audit);

        var file = await service.ExportAsync("xlsx", Actor, null, null);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExportAsync("csv", Actor, null, null));

        Assert.NotEmpty(file.Content);
        Assert.Single(audit.Entries);
    }

    [Fact]
    public async Task GetDetailAsync_HasCreatorNameAndHistoryOldestFirst()
    {
        var repo = new FakeSlmRepository();
        repo.Names[Actor] = "Mod A";
        var service = Svc(repo, new FakeReader());
        var created = await service.CreateAsync(QA(), Actor);
        await service.UpdateAsync(created.Id, QA(submit: true), Actor);

        var detail = await service.GetDetailAsync(created.Id);

        Assert.Equal("Mod A", detail!.Sample.CreatedByName);
        Assert.Equal([SlmSampleHistoryActions.Create, SlmSampleHistoryActions.Submit], detail.History.Select(h => h.Action));
        Assert.All(detail.History, h => Assert.Equal("Mod A", h.ActorName));
    }

    [Fact]
    public void RepositoryQueries_TranslateToSql()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<StudyAbroad.Infrastructure.Persistence.AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new StudyAbroad.Infrastructure.Persistence.AppDbContext(options);
        var filter = new SlmSampleFilter(SlmSampleTypes.Knowledge, null, SlmReviewStatuses.Pending, "visa",
            Actor, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), SlmSampleSources.ChatUnanswered);

        var page = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToQueryString(
            SlmSampleRepository.Filtered(db.SlmTrainingSamples, filter));
        var same = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToQueryString(
            SlmSampleRepository.SameQuestion(db.SlmTrainingSamples, " Visa "));

        var creators = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToQueryString(
            SlmSampleRepository.Creators(db.SlmTrainingSamples, db.Users));

        Assert.Contains("ORDER BY", creators);
        Assert.Contains("created_by_user_id", page);
        Assert.Contains("created_at", page);
        Assert.Contains("source", page);
        Assert.Contains("lower(", same);
    }
}

internal class FakeReader(params SlmImportRow[] rows) : ISlmWorkbookReader
{
    public CancellationToken LastToken { get; private set; }

    public IReadOnlyList<SlmImportRow> Read(Stream workbook, CancellationToken ct = default)
    {
        LastToken = ct;
        return rows;
    }
}

internal class FakeAuditLogs : IAuditLogRepository
{
    public List<AuditLog> Entries { get; } = [];

    public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}

internal class FakeSlmRepository : ISlmSampleRepository
{
    private const string ValidIntents = "[{\"type\":\"knowledge\",\"span\":\"OPT là gì\",\"entities\":{}}]";

    public List<SlmTrainingSample> Items { get; } = [];
    public List<SlmSampleHistory> History { get; } = [];
    public Dictionary<Guid, string> Names { get; } = [];
    public int AddRangeCalls { get; private set; }
    public int AddCalls { get; private set; }
    public int ExistingKeysCalls { get; private set; }

    private IEnumerable<SlmTrainingSample> Live => Items.Where(x => x.DeletedAt == null);

    public SlmTrainingSample AddDraft()
    {
        var sample = new SlmTrainingSample { SampleType = SlmSampleTypes.Knowledge, Question = "OPT là gì", Intents = ValidIntents };
        Items.Add(sample);
        return sample;
    }

    public SlmTrainingSample AddPending()
    {
        var sample = AddDraft();
        sample.ReviewStatus = SlmReviewStatuses.Pending;
        return sample;
    }

    public SlmTrainingSample AddApproved()
    {
        var sample = AddDraft();
        sample.ReviewStatus = SlmReviewStatuses.Approved;
        sample.ReviewedByUserId = Guid.NewGuid();
        sample.ReviewedAt = DateTime.UtcNow;
        sample.ReviewNote = "ok";
        return sample;
    }

    public Task<(IReadOnlyList<SlmSampleListRow> Rows, int Total)> PageAsync(
        SlmSampleFilter filter, int skip, int take, CancellationToken ct = default)
    {
        var all = SlmSampleRepository.Filtered(Live.AsQueryable(), filter).ToList();
        IReadOnlyList<SlmSampleListRow> rows = all.Skip(skip).Take(take)
            .Select(x => new SlmSampleListRow(x, x.CreatedByUserId is { } id ? Names.GetValueOrDefault(id) : null)).ToList();
        return Task.FromResult((rows, all.Count));
    }

    public Task<SlmTrainingSample?> FindAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Live.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<SlmSampleHistoryRow>> HistoryAsync(Guid sampleId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SlmSampleHistoryRow>>(History.Where(h => h.SampleId == sampleId)
            .Select(h => new SlmSampleHistoryRow(h, h.ActorUserId is { } id ? Names.GetValueOrDefault(id) : null)).ToList());

    public Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(Names.Where(n => userIds.Contains(n.Key)).ToDictionary());

    public Task AddAsync(SlmTrainingSample sample, SlmSampleHistory history, CancellationToken ct = default)
    {
        AddCalls++;
        Items.Add(sample);
        History.Add(history);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IReadOnlyList<SlmTrainingSample> samples, IReadOnlyList<SlmSampleHistory> history, CancellationToken ct = default)
    {
        AddRangeCalls++;
        Items.AddRange(samples);
        History.AddRange(history);
        return Task.CompletedTask;
    }

    public Task<int> UpdateManyAsync(IReadOnlyList<Guid> ids, Func<SlmTrainingSample, SlmSampleHistory?> apply, CancellationToken ct = default)
    {
        var matched = Live.Where(x => ids.Contains(x.Id)).ToList();
        foreach (var sample in matched)
        {
            if (apply(sample) is not { } history)
                continue;
            history.SampleId = sample.Id;
            History.Add(history);
        }
        return Task.FromResult(matched.Count);
    }

    public Task<IReadOnlyList<(Guid SampleId, Guid ActorUserId)>> ContributorsAsync(IReadOnlyList<Guid> sampleIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<(Guid, Guid)>>(History
            .Where(h => sampleIds.Contains(h.SampleId) && h.ActorUserId != null
                        && h.Action is SlmSampleHistoryActions.Create or SlmSampleHistoryActions.Edit or SlmSampleHistoryActions.Submit)
            .Select(h => (h.SampleId, h.ActorUserId!.Value)).Distinct().ToList());

    public Task<IReadOnlyList<SlmSampleCountRow>> CountsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SlmSampleCountRow>>(Live
            .GroupBy(x => (x.SampleType, x.ReviewStatus))
            .Select(g => new SlmSampleCountRow(g.Key.SampleType, g.Key.ReviewStatus, g.Count()))
            .ToList());

    public Task<IReadOnlyList<SlmSampleKeyRow>> ExistingKeysAsync(CancellationToken ct = default)
    {
        ExistingKeysCalls++;
        return Task.FromResult<IReadOnlyList<SlmSampleKeyRow>>(Items
            .Select(x => new SlmSampleKeyRow(x.SampleType, x.Question, x.PendingQuestion, x.Profile, x.Context, x.Intents))
            .ToList());
    }

    public bool Fail { get; init; }

    public Task<IReadOnlyList<SlmTrainingSample>> SameQuestionAsync(string question, bool includeDeleted, CancellationToken ct = default) =>
        Fail ? throw new InvalidOperationException("db down") : Task.FromResult<IReadOnlyList<SlmTrainingSample>>(
            SlmSampleRepository.SameQuestion((includeDeleted ? Items : Live).AsQueryable(), question).ToList());

    public Task<IReadOnlyList<SlmSampleCreatorDto>> CreatorsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SlmSampleCreatorDto>>(Live
            .Where(x => x.CreatedByUserId is { } id && Names.ContainsKey(id))
            .Select(x => new SlmSampleCreatorDto(x.CreatedByUserId!.Value, Names[x.CreatedByUserId.Value])).Distinct().ToList());

    public Task<IReadOnlyList<SlmTrainingSample>> ApprovedAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SlmTrainingSample>>(Live.Where(x => x.ReviewStatus == SlmReviewStatuses.Approved).ToList());

    public Task MarkExportedAsync(IReadOnlyList<Guid> ids, DateTime exportedAt, CancellationToken ct = default)
    {
        foreach (var sample in Items.Where(x => ids.Contains(x.Id)))
            sample.ExportedAt = exportedAt;
        return Task.CompletedTask;
    }
}
