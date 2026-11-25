using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Application.Audit;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Import;
using StudyAbroad.Infrastructure.Persistence;
using StudyAbroad.Infrastructure.Persistence.Repositories;
using Xunit;

namespace StudyAbroad.UnitTests;

public class SlmSampleServiceTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private const string ValidIntents = "[{\"type\":\"knowledge\",\"span\":\"OPT là gì\",\"entities\":{}}]";
    private const string OtherIntents = "[{\"type\":\"knowledge\",\"span\":\"CPT là gì\",\"entities\":{}}]";

    private static SlmSampleService Svc(FakeSlmRepository repo, ISlmWorkbookReader? reader = null, FakeAuditLogs? audit = null) =>
        new(repo, reader ?? new FakeReader(), new SlmTemplateWriter(), audit ?? new FakeAuditLogs());

    private static SaveSlmSampleRequest ValidRequest(string question = "OPT là gì", string? source = null) =>
        new(SlmSampleTypes.Knowledge, question, null, null, ValidIntents, null, null, null, source);

    [Fact]
    public async Task CreateAsync_CreatesVersion1_DefaultsSplitAndSource_AndWritesAuditLog()
    {
        var repo = new FakeSlmRepository();
        var audit = new FakeAuditLogs();

        var created = await Svc(repo, audit: audit).CreateAsync(ValidRequest(), Actor);

        Assert.Equal((1, SlmSampleSplits.Train, SlmSampleSources.Manual, Actor), (created.Version, created.Split, created.Source, created.CreatedByUserId));
        var ver = Assert.Single(repo.Versions.Where(v => v.SampleId == created.Id));
        Assert.Equal((1, VersionActions.Create, Actor), (ver.Version, ver.Action, ver.ActorUserId));
        Assert.Contains(audit.Entries, a => a.Action == AuditLogConstants.SlmSampleCreate && a.ActorUserId == Actor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("not-json")]
    public async Task CreateAndUpdate_RejectEmptyOrInvalidIntents_PerUc92E1(string? intents)
    {
        var repo = new FakeSlmRepository();
        var existing = repo.AddSample();

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).CreateAsync(ValidRequest() with { Intents = intents }, Actor));
        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).UpdateAsync(existing.Id, ValidRequest() with { Intents = intents }, Actor));
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("MANUAL")]
    [InlineData("Manual")]
    [InlineData("excel")]
    [InlineData("EXCEL")]
    public async Task CreateAsync_RejectsReservedSourceCaseInsensitive_PerUc92E3(string source)
    {
        var repo = new FakeSlmRepository();

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).CreateAsync(ValidRequest(source: source), Actor));
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateCaseInsensitiveAndExtraWhitespace_AndAllowsAfterSoftDelete()
    {
        var repo = new FakeSlmRepository();
        var svc = Svc(repo);

        var first = await svc.CreateAsync(ValidRequest("Visa   F-1 là gì?"), Actor);

        var dup = await Assert.ThrowsAsync<SlmDuplicateSampleException>(() =>
            svc.CreateAsync(ValidRequest("  visa f-1   LÀ GÌ?  "), Actor));
        Assert.Equal(first.Id, dup.ExistingId);

        // Soft delete allows re-creating the same sample (UC-94)
        Assert.True(await svc.DeleteAsync(first.Id, Actor));
        var recreated = await svc.CreateAsync(ValidRequest("visa f-1 là gì?"), Actor);
        Assert.NotEqual(first.Id, recreated.Id);
    }

    [Fact]
    public async Task UpdateAsync_CreatesNewVersion_WritesAudit_AndRejectsDeletedSample()
    {
        var repo = new FakeSlmRepository();
        var audit = new FakeAuditLogs();
        var svc = Svc(repo, audit: audit);
        var created = await svc.CreateAsync(ValidRequest("Câu cũ"), Actor);

        var updated = await svc.UpdateAsync(created.Id, ValidRequest("Câu mới") with { Intents = OtherIntents, Split = SlmSampleSplits.Val }, Actor);

        Assert.NotNull(updated);
        Assert.Equal((2, "Câu mới", SlmSampleSplits.Val), (updated!.Version, updated.Question, updated.Split));
        Assert.Equal(2, repo.Versions.Count(v => v.SampleId == created.Id));
        Assert.Contains(audit.Entries, a => a.Action == AuditLogConstants.SlmSampleUpdate);

        Assert.True(await svc.DeleteAsync(created.Id, Actor));
        await Assert.ThrowsAsync<SlmSampleDeletedException>(() => svc.UpdateAsync(created.Id, ValidRequest("Sửa khi đã xóa"), Actor));
    }

    [Fact]
    public async Task RestoreVersionAsync_CreatesNewVersionWithOldContent_AndLogsAudit()
    {
        var repo = new FakeSlmRepository();
        var audit = new FakeAuditLogs();
        var svc = Svc(repo, audit: audit);
        var created = await svc.CreateAsync(ValidRequest("Bản 1", source: "Nguồn A"), Actor);
        await svc.UpdateAsync(created.Id, ValidRequest("Bản 2", source: "Nguồn B"), Actor);

        var restored = await svc.RestoreVersionAsync(created.Id, 1, Actor);

        Assert.NotNull(restored);
        Assert.Equal((3, "Bản 1", "Nguồn A"), (restored!.Version, restored.Question, restored.Source));
        var detail = await svc.GetDetailAsync(created.Id);
        Assert.Equal(3, detail!.Versions.Count);
        Assert.Equal((3, VersionActions.Restore, 1), (detail.Versions[0].Version, detail.Versions[0].Action, detail.Versions[0].RestoredFromVersion));
        Assert.Contains(audit.Entries, a => a.Action == AuditLogConstants.SlmSampleRestoreVersion);
    }

    [Fact]
    public async Task RestoreAndRestoreVersion_RunUpdateValidation_EmptyIntentsAndPersonalData()
    {
        var repo = new FakeSlmRepository();
        var svc = Svc(repo);
        var sample = repo.AddSample();
        repo.Versions[0].Intents = "[]";                       // phiên bản cũ có intents rỗng (UC-92 E1)
        await Assert.ThrowsAsync<ArgumentException>(() => svc.RestoreVersionAsync(sample.Id, 1, Actor));

        repo.Versions[0].Intents = ValidIntents;
        repo.Versions[0].Question = "Gọi em số 0912345678";    // có số điện thoại
        await Assert.ThrowsAsync<ArgumentException>(() => svc.RestoreVersionAsync(sample.Id, 1, Actor));

        sample.DeletedAt = DateTime.UtcNow;
        sample.Intents = "[]";
        await Assert.ThrowsAsync<ArgumentException>(() => svc.RestoreAsync(sample.Id, Actor));
        Assert.NotNull(sample.DeletedAt);
    }

    [Fact]
    public async Task DeleteAndRestoreAsync_SoftDeletes_FiltersByDeleted_ChecksDuplicateOnRestore_AndLogsAudit()
    {
        var repo = new FakeSlmRepository();
        var audit = new FakeAuditLogs();
        var svc = Svc(repo, audit: audit);
        var created = await svc.CreateAsync(ValidRequest("Câu cần xóa"), Actor);

        Assert.True(await svc.DeleteAsync(created.Id, Actor));
        Assert.Empty((await svc.GetPageAsync(new SlmSampleFilter(null, null, null, Deleted: false), 1, 20)).Items);
        Assert.Single((await svc.GetPageAsync(new SlmSampleFilter(null, null, null, Deleted: true), 1, 20)).Items);
        Assert.NotNull(await svc.GetDetailAsync(created.Id));
        Assert.Contains(audit.Entries, a => a.Action == AuditLogConstants.SlmSampleDelete);

        // If a duplicate was added while deleted, restore is blocked with 409
        var dup = await svc.CreateAsync(ValidRequest("câu   cần xóa"), Actor);
        await Assert.ThrowsAsync<SlmDuplicateSampleException>(() => svc.RestoreAsync(created.Id, Actor));

        // Remove the duplicate, now restore succeeds
        await svc.DeleteAsync(dup.Id, Actor);
        var restored = await svc.RestoreAsync(created.Id, Actor);
        Assert.NotNull(restored);
        Assert.Null(restored!.DeletedAt);
        Assert.Contains(audit.Entries, a => a.Action == AuditLogConstants.SlmSampleRestore);
    }

    [Fact]
    public async Task ImportAsync_RejectsEmptyIntents_IgnoresDeletedForDuplicateCheck_AndCollapsesWhitespace()
    {
        var repo = new FakeSlmRepository();
        var audit = new FakeAuditLogs();
        var live = repo.AddSample("Visa  F-1 là gì?");
        var deleted = repo.AddSample("Câu đã xóa");
        deleted.DeletedAt = DateTime.UtcNow;

        var rows = new[]
        {
            new SlmImportRow(SlmSampleTypes.Knowledge, 2, "visa f-1 là gì?", null, null, ValidIntents, null, "train", null), // dup of live
            new SlmImportRow(SlmSampleTypes.Knowledge, 3, "Câu đã xóa", null, null, ValidIntents, null, "train", null),      // allowed because deleted
            new SlmImportRow(SlmSampleTypes.Knowledge, 4, "Thiếu intent", null, null, "[]", null, "train", null),            // error (empty intents)
            new SlmImportRow(SlmSampleTypes.Knowledge, 5, "Câu hợp lệ", null, null, ValidIntents, null, "val", null),
            new SlmImportRow(SlmSampleTypes.Knowledge, 6, "câu   hợp lệ", null, null, ValidIntents, null, "val", null),      // dup within file
        };

        var result = await Svc(repo, new FakeReader(rows), audit).ImportAsync(new MemoryStream(), Actor);

        Assert.Equal((5, 2, 2), (result.TotalRows, result.Inserted, result.Duplicates));
        var err = Assert.Single(result.Errors);
        Assert.Equal(4, err.RowNumber);
        Assert.Contains(audit.Entries, a => a.Action == AuditLogConstants.SlmSampleImport);
        Assert.Equal(2, repo.Versions.Count(v => v.Action == VersionActions.Import));
    }

    [Fact]
    public async Task ExportAsync_ExportsAllNonDeletedSamples_MarksExportedAt_AndFailsWithoutCreatingWhenAuditFails()
    {
        var repo = new FakeSlmRepository();
        var audit = new FakeAuditLogs();
        var active1 = repo.AddSample("Câu 1");
        var active2 = repo.AddSample("Câu 2");
        var deleted = repo.AddSample("Câu đã xóa");
        deleted.DeletedAt = DateTime.UtcNow;

        var exported = await Svc(repo, audit: audit).ExportAsync("jsonl", Actor, "127.0.0.1", "agent");

        var lines = Encoding.UTF8.GetString(exported.Content).Trim().Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.NotNull(active1.ExportedAt);
        Assert.NotNull(active2.ExportedAt);
        Assert.Null(deleted.ExportedAt);
        Assert.Contains(audit.Entries, a => a.Action == AuditLogConstants.SlmSampleExport);

        // UC-96 E1: if audit log throws, ExportAsync throws and does not mark ExportedAt
        var active3 = repo.AddSample("Câu 3");
        var failingAudit = new FakeAuditLogs { ThrowOnAdd = new InvalidOperationException("audit db down") };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Svc(repo, audit: failingAudit).ExportAsync("xlsx", Actor, null, null));
        Assert.Null(active3.ExportedAt);
    }

    [Fact]
    public async Task GetSourcesAsync_IncludesSystemAndCustomSources()
    {
        var repo = new FakeSlmRepository();
        repo.AddSample("A").Source = "Website ĐSQ";
        repo.AddSample("B").Source = SlmSampleSources.Excel;

        var sources = await Svc(repo).GetSourcesAsync();

        Assert.Contains(SlmSampleSources.Manual, sources);
        Assert.Contains(SlmSampleSources.Excel, sources);
        Assert.Contains("Website ĐSQ", sources);
    }

    [Fact]
    public async Task GetSummaryAsync_CountsActiveAndDeletedSeparately()
    {
        var repo = new FakeSlmRepository();
        repo.AddSample("A");
        var del = repo.AddSample("B");
        del.DeletedAt = DateTime.UtcNow;

        var summary = await Svc(repo).GetSummaryAsync();

        Assert.Equal(1, summary.Total);
        Assert.Equal(1, summary.Deleted);
        Assert.Equal(1, summary.ByType[SlmSampleTypes.Knowledge]);
    }

    [Fact]
    public void RepositoryFilter_TranslatesToSql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);
        var filter = new SlmSampleFilter(SlmSampleTypes.Knowledge, SlmSampleSplits.Train, "visa", Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, "Website ĐSQ", Deleted: false);

        var sql = SlmSampleRepository.Filtered(db.SlmTrainingSamples, filter).ToQueryString();

        Assert.Contains("ILIKE", sql);
        Assert.Contains("sample_type", sql);
    }

    private sealed class FakeReader(params SlmImportRow[] rows) : ISlmWorkbookReader
    {
        public IReadOnlyList<SlmImportRow> Read(Stream workbook, CancellationToken ct = default) => rows;
    }
}

internal class FakeAuditLogs : IAuditLogRepository
{
    public List<AuditLog> Entries { get; } = [];
    public Exception? ThrowOnAdd { get; init; }

    public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ThrowOnAdd is not null)
            throw ThrowOnAdd;
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
    public List<SlmSampleVersion> Versions { get; } = [];
    public Dictionary<Guid, string> Names { get; } = [];

    private IEnumerable<SlmTrainingSample> Live => Items.Where(x => x.DeletedAt == null);

    public SlmTrainingSample AddSample(string question = "OPT là gì")
    {
        var sample = new SlmTrainingSample { SampleType = SlmSampleTypes.Knowledge, Question = question, Intents = ValidIntents, Version = 1 };
        Items.Add(sample);
        Versions.Add(new SlmSampleVersion
        {
            SampleId = sample.Id,
            Version = 1,
            Action = VersionActions.Create,
            SampleType = sample.SampleType,
            Question = sample.Question,
            Profile = sample.Profile,
            PendingQuestion = sample.PendingQuestion,
            Intents = sample.Intents,
            Context = sample.Context,
            Split = sample.Split,
            SourceReview = sample.SourceReview,
            Source = sample.Source,
        });
        return sample;
    }

    public Task<(IReadOnlyList<SlmSampleListRow> Rows, int Total)> PageAsync(
        SlmSampleFilter filter, int skip, int take, CancellationToken ct = default)
    {
        var source = filter.Deleted ? Items.Where(x => x.DeletedAt != null) : Live;
        var all = SlmSampleRepository.Filtered(source.AsQueryable(), filter).ToList();
        IReadOnlyList<SlmSampleListRow> rows = all.Skip(skip).Take(take)
            .Select(x => new SlmSampleListRow(x, x.CreatedByUserId is { } id ? Names.GetValueOrDefault(id) : null)).ToList();
        return Task.FromResult((rows, all.Count));
    }

    public Task<SlmTrainingSample?> FindAsync(Guid id, bool includeDeleted = false, CancellationToken ct = default) =>
        Task.FromResult((includeDeleted ? Items : Live).FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<SlmSampleVersionRow>> VersionsAsync(Guid sampleId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SlmSampleVersionRow>>(
            Versions.Where(v => v.SampleId == sampleId).OrderByDescending(v => v.Version).Select(v => new SlmSampleVersionRow(v, "Admin")).ToList());

    public Task<SlmSampleVersion?> FindVersionAsync(Guid sampleId, int version, CancellationToken ct = default) =>
        Task.FromResult(Versions.FirstOrDefault(v => v.SampleId == sampleId && v.Version == version));

    public Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(Names.Where(n => userIds.Contains(n.Key)).ToDictionary());

    public Task AddAsync(SlmTrainingSample sample, SlmSampleVersion initialVersion, CancellationToken ct = default)
    {
        Items.Add(sample);
        initialVersion.SampleId = sample.Id;
        Versions.Add(initialVersion);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IReadOnlyList<SlmTrainingSample> samples, IReadOnlyList<SlmSampleVersion> initialVersions, CancellationToken ct = default)
    {
        Items.AddRange(samples);
        Versions.AddRange(initialVersions);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(
        Guid id, Func<SlmTrainingSample, (bool Changed, SlmSampleVersion? NewVersion)> apply,
        bool includeDeleted = false, CancellationToken ct = default)
    {
        var sample = (includeDeleted ? Items : Live).FirstOrDefault(x => x.Id == id);
        if (sample is null)
            return Task.FromResult(false);
        var (changed, newVersion) = apply(sample);
        if (changed && newVersion is not null)
        {
            newVersion.SampleId = sample.Id;
            Versions.Add(newVersion);
        }
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<SlmSampleCountRow>> CountsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SlmSampleCountRow>>(Items
            .GroupBy(x => (x.SampleType, Deleted: x.DeletedAt != null))
            .Select(g => new SlmSampleCountRow(g.Key.SampleType, g.Key.Deleted, g.Count()))
            .ToList());

    public Task<IReadOnlyList<SlmSampleKeyRow>> ExistingKeysAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SlmSampleKeyRow>>(Live
            .Select(x => new SlmSampleKeyRow(x.Id, x.SampleType, x.Question, x.PendingQuestion, x.Profile, x.Context, x.Intents))
            .ToList());

    public Task<IReadOnlyList<SlmSampleKeyRow>> LiveKeysByTypeAsync(string sampleType, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SlmSampleKeyRow>>(Live
            .Where(x => x.SampleType == sampleType)
            .Select(x => new SlmSampleKeyRow(x.Id, x.SampleType, x.Question, x.PendingQuestion, x.Profile, x.Context, x.Intents))
            .ToList());

    public Task<IReadOnlyList<SlmSampleCreatorDto>> CreatorsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SlmSampleCreatorDto>>(Live
            .Where(x => x.CreatedByUserId is { } id && Names.ContainsKey(id))
            .Select(x => new SlmSampleCreatorDto(x.CreatedByUserId!.Value, Names[x.CreatedByUserId.Value])).Distinct().ToList());

    public Task<IReadOnlyList<string>> SourcesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(Live.Select(x => x.Source).Distinct().OrderBy(x => x).ToList());

    public Task<IReadOnlyList<SlmTrainingSample>> ExportableAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SlmTrainingSample>>(Live.ToList());

    public Task MarkExportedAsync(IReadOnlyList<Guid> ids, DateTime exportedAt, CancellationToken ct = default)
    {
        foreach (var sample in Items.Where(x => ids.Contains(x.Id)))
            sample.ExportedAt = exportedAt;
        return Task.CompletedTask;
    }
}
