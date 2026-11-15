using System.Text.Json;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

public class SlmSampleServiceTests
{
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
        var service = new SlmSampleService(repo, new FakeReader());

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
    [InlineData(SlmSampleTypes.Knowledge, "câu hỏi", null)]
    public async Task CreateAsync_RejectsInvalidInput(string type, string question, string? intents)
    {
        var repo = new FakeSlmRepository();
        var service = new SlmSampleService(repo, new FakeReader());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(Request(type, question, intents), Actor));
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidProfileJsonAndSplit()
    {
        var service = new SlmSampleService(new FakeSlmRepository(), new FakeReader());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Request(profile: "{broken"), Actor));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Request(split: "dev"), Actor));
    }

    [Fact]
    public async Task UpdateAsync_ChangingIntentsResetsApprovalToDraft()
    {
        var repo = new FakeSlmRepository();
        var approved = repo.AddApproved();
        var service = new SlmSampleService(repo, new FakeReader());

        var updated = await service.UpdateAsync(
            approved.Id, Request(intents: "[{\"type\":\"knowledge\",\"span\":\"khác\",\"entities\":{}}]"), Actor);

        Assert.Equal(SlmReviewStatuses.Draft, updated!.ReviewStatus);
        Assert.Null(updated.ReviewedByUserId);
        Assert.Null(updated.ReviewedAt);
        Assert.Null(updated.ReviewNote);
    }

    [Fact]
    public async Task UpdateAsync_ChangingOnlySplitKeepsApproval()
    {
        var repo = new FakeSlmRepository();
        var approved = repo.AddApproved();
        var service = new SlmSampleService(repo, new FakeReader());

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
        var service = new SlmSampleService(repo, new FakeReader());

        var reordered = "[ { \"entities\": { \"b\": 2, \"a\": 1 }, \"span\": \"OPT là gì\", \"type\": \"knowledge\" } ]";
        var updated = await service.UpdateAsync(approved.Id, Request(intents: reordered), Actor);

        Assert.Equal(SlmReviewStatuses.Approved, updated!.ReviewStatus);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullWhenMissing()
    {
        var service = new SlmSampleService(new FakeSlmRepository(), new FakeReader());

        Assert.Null(await service.UpdateAsync(Guid.NewGuid(), Request(), Actor));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseWhenMissing()
    {
        var repo = new FakeSlmRepository();
        var existing = repo.AddDraft();
        var service = new SlmSampleService(repo, new FakeReader());

        Assert.True(await service.DeleteAsync(existing.Id));
        Assert.False(await service.DeleteAsync(existing.Id));
    }

    [Fact]
    public async Task ReviewAsync_UpdatesManyAndCountsMissing()
    {
        var repo = new FakeSlmRepository();
        var first = repo.AddDraft();
        var second = repo.AddDraft();
        var service = new SlmSampleService(repo, new FakeReader());

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
        var service = new SlmSampleService(new FakeSlmRepository(), new FakeReader());
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
        var service = new SlmSampleService(repo, reader);

        var result = await service.ImportAsync(new MemoryStream(), Actor);

        Assert.Equal(5, result.TotalRows);
        Assert.Equal(1, result.Inserted);
        Assert.Equal(2, result.Duplicates);
        Assert.Equal([5, 6], result.Errors.Select(e => e.RowNumber).ToArray());
        var inserted = repo.Items.Single(x => x.Question == "mới");
        Assert.Equal(SlmReviewStatuses.Draft, inserted.ReviewStatus);
        Assert.Equal("val", inserted.Split);
        Assert.Equal("v10", inserted.SourceReview);
        Assert.Equal(Actor, inserted.CreatedByUserId);
    }

    [Fact]
    public async Task ImportAsync_InsertsNothingWhenAllRowsFail()
    {
        var repo = new FakeSlmRepository();
        var reader = new FakeReader(new SlmImportRow(SlmSampleTypes.Knowledge, 2, null, null, null, ValidIntents, null, null, null));
        var service = new SlmSampleService(repo, reader);

        var result = await service.ImportAsync(new MemoryStream(), Actor);

        Assert.Equal(0, result.Inserted);
        Assert.Single(result.Errors);
        Assert.Empty(repo.Items);
        Assert.Equal(0, repo.AddRangeCalls);
    }

    [Fact]
    public async Task ExportJsonlAsync_WritesOnlyApprovedRowsAsJsonLines()
    {
        var repo = new FakeSlmRepository();
        var approved = repo.AddApproved();
        approved.Question = "học phí MIT";
        approved.Profile = "{\"gpa\":\"8\"}";
        repo.AddDraft();
        var service = new SlmSampleService(repo, new FakeReader());

        var jsonl = await service.ExportJsonlAsync(null);

        var lines = jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var line = Assert.Single(lines);
        using var doc = JsonDocument.Parse(line);
        Assert.Equal("học phí MIT", doc.RootElement.GetProperty("question").GetString());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("intents").ValueKind);
        Assert.Equal("8", doc.RootElement.GetProperty("profile").GetProperty("gpa").GetString());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("context").ValueKind);
    }

    [Fact]
    public async Task ExportJsonlAsync_FiltersBySplitAndRejectsUnknownSplit()
    {
        var repo = new FakeSlmRepository();
        repo.AddApproved().Split = SlmSampleSplits.Val;
        repo.AddApproved().Split = SlmSampleSplits.Train;
        var service = new SlmSampleService(repo, new FakeReader());

        var onlyVal = await service.ExportJsonlAsync(SlmSampleSplits.Val);

        Assert.Single(onlyVal.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExportJsonlAsync("dev"));
    }

    [Fact]
    public async Task GetSummaryAsync_ListsEveryStatusAndTypeEvenWhenZero()
    {
        var repo = new FakeSlmRepository();
        repo.AddApproved();
        repo.AddDraft();
        var service = new SlmSampleService(repo, new FakeReader());

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
        var service = new SlmSampleService(repo, new FakeReader());

        var page = await service.GetPageAsync(new SlmSampleFilter(null, null, null, null), -4, 1000);

        Assert.Equal(1, page.Page);
        Assert.Equal(SlmSampleService.MaxPageSize, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetPageAsync(new SlmSampleFilter("nope", null, null, null), 1, 20));
    }

    private class FakeReader(params SlmImportRow[] rows) : ISlmWorkbookReader
    {
        public IReadOnlyList<SlmImportRow> Read(Stream workbook) => rows;
    }

    private class FakeSlmRepository : ISlmSampleRepository
    {
        public List<SlmTrainingSample> Items { get; } = [];
        public int AddRangeCalls { get; private set; }

        public SlmTrainingSample AddDraft()
        {
            var sample = new SlmTrainingSample { SampleType = SlmSampleTypes.Knowledge, Question = "OPT là gì", Intents = ValidIntents };
            Items.Add(sample);
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

        public Task<(IReadOnlyList<SlmTrainingSample> Rows, int Total)> PageAsync(
            SlmSampleFilter filter, int skip, int take, CancellationToken ct = default)
        {
            var rows = Items.Skip(skip).Take(take).ToList();
            return Task.FromResult<(IReadOnlyList<SlmTrainingSample>, int)>((rows, Items.Count));
        }

        public Task<SlmTrainingSample?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

        public Task AddAsync(SlmTrainingSample sample, CancellationToken ct = default)
        {
            Items.Add(sample);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IReadOnlyList<SlmTrainingSample> samples, CancellationToken ct = default)
        {
            AddRangeCalls++;
            Items.AddRange(samples);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(Guid id, Action<SlmTrainingSample> apply, CancellationToken ct = default)
        {
            var sample = Items.FirstOrDefault(x => x.Id == id);
            if (sample is null)
                return Task.FromResult(false);
            apply(sample);
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.RemoveAll(x => x.Id == id) > 0);

        public Task<int> ReviewAsync(
            IReadOnlyList<Guid> ids, string status, string? note, Guid reviewerId, DateTime reviewedAt, CancellationToken ct = default)
        {
            var matched = Items.Where(x => ids.Contains(x.Id)).ToList();
            foreach (var sample in matched)
            {
                sample.ReviewStatus = status;
                sample.ReviewNote = note;
                sample.ReviewedByUserId = reviewerId;
                sample.ReviewedAt = reviewedAt;
            }
            return Task.FromResult(matched.Count);
        }

        public Task<IReadOnlyList<SlmSampleCountRow>> CountsAsync(CancellationToken ct = default)
        {
            IReadOnlyList<SlmSampleCountRow> counts = Items
                .GroupBy(x => (x.SampleType, x.ReviewStatus))
                .Select(g => new SlmSampleCountRow(g.Key.SampleType, g.Key.ReviewStatus, g.Count()))
                .ToList();
            return Task.FromResult(counts);
        }

        public Task<IReadOnlyList<SlmSampleKeyRow>> ExistingKeysAsync(CancellationToken ct = default)
        {
            IReadOnlyList<SlmSampleKeyRow> keys = Items
                .Select(x => new SlmSampleKeyRow(x.SampleType, x.Question, x.PendingQuestion, x.Profile, x.Context, x.Intents))
                .ToList();
            return Task.FromResult(keys);
        }

        public Task<IReadOnlyList<SlmTrainingSample>> ApprovedAsync(string? split, CancellationToken ct = default)
        {
            IReadOnlyList<SlmTrainingSample> approved = Items
                .Where(x => x.ReviewStatus == SlmReviewStatuses.Approved && (split is null || x.Split == split))
                .ToList();
            return Task.FromResult(approved);
        }
    }
}
