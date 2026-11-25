using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StudyAbroad.Application.Admin.KnowledgeBase;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence.Repositories;
using Xunit;

namespace StudyAbroad.UnitTests;

public class KnowledgeBaseServiceTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    private static KnowledgeBaseService Svc(FakeKnowledgeRepository repo, params KnowledgeImportRow[] rows) =>
        Svc(repo, new FakeKnowledgeAdvisor(), new FakeAuditLogs(), rows);

    private static KnowledgeBaseService Svc(FakeKnowledgeRepository repo, FakeKnowledgeAdvisor advisor, params KnowledgeImportRow[] rows) =>
        Svc(repo, advisor, new FakeAuditLogs(), rows);

    private static KnowledgeBaseService Svc(
        FakeKnowledgeRepository repo, FakeKnowledgeAdvisor advisor, FakeAuditLogs audit, params KnowledgeImportRow[] rows) =>
        new(repo, new FakeKnowledgeReader(rows), advisor, audit, NullLogger<KnowledgeBaseService>.Instance);

    private static SaveKnowledgeEntryRequest Full(string question = "OPT là gì?", int? entryNo = null) =>
        new(entryNo, "faq", "LABEL_VISA_F1", question, "OPT là thời gian thực tập sau tốt nghiệp.", "Study in the States", null,
            new DateOnly(2026, 11, 1), null, null);

    private static KnowledgeImportRow Row(int rowNumber, string? id, string question = "OPT là gì?") =>
        new(rowNumber, id, "faq", "label_visa_f1", question, "Câu trả lời.", null, "https://studyinthestates.dhs.gov",
            "2026-11-01", null, null);

    [Fact]
    public async Task CreateAsync_WithoutEntryNo_TakesMaxPlusOne_CreatesVersion1_AndWritesAuditLog()
    {
        var repo = new FakeKnowledgeRepository();
        var audit = new FakeAuditLogs();
        repo.Add(entryNo: 41);
        var nfd = "Học   bổng là gì?".Normalize(NormalizationForm.FormD);

        var created = await Svc(repo, new FakeKnowledgeAdvisor(), audit).CreateAsync(Full(nfd), Actor);

        Assert.Equal(42, created.EntryNo);
        Assert.Equal(1, created.Version);
        Assert.Equal("Học bổng là gì?", created.Question);
        Assert.True(created.Question.IsNormalized(NormalizationForm.FormC));
        Assert.Equal(2, repo.Items.Count);
        var ver = Assert.Single(repo.Versions.Where(v => v.DocumentId == created.Id));
        Assert.Equal((1, VersionActions.Create, Actor), (ver.Version, ver.Action, ver.ActorUserId));
        Assert.Contains(audit.Entries, a => a.Action == AuditLogConstants.KnowledgeBaseCreate && a.ActorUserId == Actor);
    }

    [Fact]
    public async Task CreateAsync_AutoAssignExceedsMaxEntryNo_ThrowsArgumentException()
    {
        var repo = new FakeKnowledgeRepository();
        repo.Add(entryNo: KnowledgeBaseService.MaxEntryNo);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).CreateAsync(Full(), Actor));

        Assert.Contains("1.000.000", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_UsedEntryNo_IncludingSoftDeleted_ThrowsDuplicateWithExistingId()
    {
        var repo = new FakeKnowledgeRepository();
        var existing = repo.Add(entryNo: 7);
        existing.DeletedAt = DateTime.UtcNow;

        var error = await Assert.ThrowsAsync<KnowledgeDuplicateEntryException>(() => Svc(repo).CreateAsync(Full(entryNo: 7), Actor));

        Assert.Equal(existing.Id, error.ExistingId);
    }

    [Fact]
    public async Task CreateAsync_ConcurrentUniqueViolationOnSave_ThrowsDuplicate()
    {
        var repo = new FakeKnowledgeRepository { ThrowDuplicateOnAdd = true };

        await Assert.ThrowsAsync<KnowledgeDuplicateEntryException>(() => Svc(repo).CreateAsync(Full(entryNo: 9), Actor));
    }

    [Fact]
    public async Task CreateAsync_AllowsFreeTextNoteIncludingPendingPrefix_PerUc114()
    {
        var repo = new FakeKnowledgeRepository();
        var request = Full() with { Note = "CHỜ DUYỆT: ghi chú nội bộ tự do" };

        var created = await Svc(repo).CreateAsync(request, Actor);

        Assert.Equal("CHỜ DUYỆT: ghi chú nội bộ tự do", created.Note);
    }

    [Theory]
    [InlineData("doc_type", "bang")]
    [InlineData("label", "LABEL_KHONG_CO")]
    [InlineData("question", " ")]
    [InlineData("answer", null)]
    [InlineData("retrieved_at", null)]
    [InlineData("source", null)]
    [InlineData("topic_code", "proc")]
    [InlineData("source_url", "javascript:alert(1)")]
    [InlineData("source_url", "https://ok.com;javascript:x")]
    [InlineData("source_url", ";;;")]
    public async Task CreateAsync_RejectsInvalidFields(string field, string? value)
    {
        var request = field switch
        {
            "doc_type" => Full() with { DocType = value },
            "label" => Full() with { Label = value },
            "question" => Full() with { Question = value },
            "answer" => Full() with { Answer = value },
            "retrieved_at" => Full() with { RetrievedAt = null },
            "source" => Full() with { Source = null, SourceUrl = null },
            "source_url" => Full() with { SourceUrl = value },
            _ => Full() with { DocType = "proc_steps", TopicCode = null },
        };

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(new FakeKnowledgeRepository()).CreateAsync(request, Actor));
    }

    [Fact]
    public async Task UpdateAsync_ChangedQuestion_CreatesNewVersionClearsEmbeddingKeepsEntryNoAndLogsAudit()
    {
        var repo = new FakeKnowledgeRepository();
        var audit = new FakeAuditLogs();
        var existing = repo.Add(entryNo: 5);
        existing.EmbeddingHash = "abc";
        existing.IngestedAt = DateTime.UtcNow;

        var updated = await Svc(repo, new FakeKnowledgeAdvisor(), audit).UpdateAsync(existing.Id, Full("OPT kéo dài bao lâu?", entryNo: 99), Actor);

        Assert.Equal(5, updated!.EntryNo);
        Assert.Equal(2, updated.Version);
        Assert.Null(existing.EmbeddingHash);
        Assert.Null(existing.IngestedAt);
        Assert.Equal([existing.Id], repo.ClearedEmbeddings);
        Assert.Contains(repo.Versions, v => v.DocumentId == existing.Id && v.Version == 2 && v.Action == VersionActions.Edit);
        Assert.Contains(audit.Entries, a => a.Action == AuditLogConstants.KnowledgeBaseUpdate);
    }

    [Fact]
    public async Task UpdateAsync_DeletedEntry_ThrowsKnowledgeEntryDeletedException()
    {
        var repo = new FakeKnowledgeRepository();
        var existing = repo.Add(entryNo: 5);
        existing.DeletedAt = DateTime.UtcNow;

        await Assert.ThrowsAsync<KnowledgeEntryDeletedException>(() => Svc(repo).UpdateAsync(existing.Id, Full("Mới?"), Actor));
    }

    [Fact]
    public async Task UpdateAsync_SameQuestion_KeepsEmbedding_AndUnknownIdReturnsNull()
    {
        var repo = new FakeKnowledgeRepository();
        var existing = repo.Add();
        existing.EmbeddingHash = "abc";

        await Svc(repo).UpdateAsync(existing.Id, Full(existing.Question) with { Answer = "Câu trả lời mới." }, Actor);

        Assert.Equal("abc", existing.EmbeddingHash);
        Assert.Empty(repo.ClearedEmbeddings);
        Assert.Null(await Svc(repo).UpdateAsync(Guid.NewGuid(), Full(), Actor));
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes_ClearsEmbedding_KeepsIdReserved_AndRestoreReEmbeds()
    {
        var repo = new FakeKnowledgeRepository();
        var audit = new FakeAuditLogs();
        var advisor = new FakeKnowledgeAdvisor();
        var existing = repo.Add(entryNo: 10);
        var svc = Svc(repo, advisor, audit);

        Assert.True(await svc.DeleteAsync(existing.Id, Actor));
        Assert.False(await svc.DeleteAsync(existing.Id, Actor));
        Assert.NotNull(existing.DeletedAt);
        Assert.Equal(Actor, existing.DeletedByUserId);
        Assert.Equal([existing.Id], repo.ClearedEmbeddings);
        Assert.Contains(audit.Entries, a => a.Action == AuditLogConstants.KnowledgeBaseDelete);

        // Deleted entry appears only in deleted filter, and is readable by GetAsync
        Assert.Empty((await svc.GetPageAsync(new KnowledgeEntryFilter(), 1, 20)).Items);
        Assert.Single((await svc.GetPageAsync(new KnowledgeEntryFilter(Deleted: true), 1, 20)).Items);
        Assert.NotNull(await svc.GetAsync(existing.Id));

        // Restore clears DeletedAt, triggers embedding, logs audit
        var restored = await svc.RestoreAsync(existing.Id, Actor);
        Assert.NotNull(restored);
        Assert.Null(restored!.DeletedAt);
        Assert.Equal(1, advisor.Calls);
        Assert.Contains(audit.Entries, a => a.Action == AuditLogConstants.KnowledgeBaseRestore);
    }

    [Fact]
    public async Task RestoreVersionAsync_CreatesNewVersionWithOldContent_ReEmbedsWhenQuestionChanges_AndLogsAudit()
    {
        var repo = new FakeKnowledgeRepository();
        var audit = new FakeAuditLogs();
        var advisor = new FakeKnowledgeAdvisor();
        var svc = Svc(repo, advisor, audit);

        var created = await svc.CreateAsync(Full("Câu gốc?", entryNo: 1), Actor);
        await svc.UpdateAsync(created.Id, Full("Câu đã sửa?") with { Answer = "Đáp án mới." }, Actor);
        advisor.ResetCalls();
        repo.ClearedEmbeddings.Clear();

        var restored = await svc.RestoreVersionAsync(created.Id, 1, Actor);

        Assert.NotNull(restored);
        Assert.Equal(3, restored!.Version);
        Assert.Equal("Câu gốc?", restored.Question);
        Assert.Equal("OPT là thời gian thực tập sau tốt nghiệp.", restored.Answer);
        Assert.Equal([created.Id], repo.ClearedEmbeddings);
        Assert.Equal(1, advisor.Calls);
        var versions = await svc.GetVersionsAsync(created.Id);
        Assert.Equal(3, versions!.Count);
        Assert.Equal((3, VersionActions.Restore, 1), (versions[0].Version, versions[0].Action, versions[0].RestoredFromVersion));
        Assert.Contains(audit.Entries, a => a.Action == AuditLogConstants.KnowledgeBaseRestoreVersion);
    }

    [Fact]
    public async Task GetPageAsync_FiltersByDocTypeLabelAndTopic_AndRejectsUnknownValues()
    {
        var repo = new FakeKnowledgeRepository();
        repo.Add(entryNo: 1);
        var proc = repo.Add(entryNo: 2);
        proc.DocType = "proc_steps";
        proc.TopicCode = "I-20";

        var page = await Svc(repo).GetPageAsync(new KnowledgeEntryFilter(DocType: "proc_steps", TopicCode: "I-20"), 1, 20);

        Assert.Equal(proc.Id, Assert.Single(page.Items).Id);
        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).GetPageAsync(new KnowledgeEntryFilter(DocType: "x"), 1, 20));
        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).GetPageAsync(new KnowledgeEntryFilter(Sort: "random"), 1, 20));
    }

    [Fact]
    public async Task ImportAsync_UpsertsByEntryNo_CreatesVersions_CountsCreatedUpdatedUnchanged_AndRejectsDeletedId()
    {
        var repo = new FakeKnowledgeRepository();
        var audit = new FakeAuditLogs();
        var same = repo.Add(entryNo: 1, question: "OPT là gì?");
        same.Label = "LABEL_VISA_F1";
        same.Answer = "Câu trả lời.";
        same.Source = null;
        same.SourceUrl = "https://studyinthestates.dhs.gov";
        same.Note = null;
        var changed = repo.Add(entryNo: 2, question: "Câu cũ?");
        changed.EmbeddingHash = "abc";
        var deleted = repo.Add(entryNo: 4, question: "Đã xóa?");
        deleted.DeletedAt = DateTime.UtcNow;

        var rows = new[] { Row(2, "1"), Row(3, "2", "Câu mới?"), Row(4, "3", "Dòng thêm?"), Row(5, "4", "Thử ghi đè đã xóa?") };

        var result = await Svc(repo, new FakeKnowledgeAdvisor(), audit, rows).ImportAsync(new MemoryStream(), Actor);

        Assert.Equal((4, 1, 1, 1), (result.TotalRows, result.Created, result.Updated, result.Unchanged));
        var err = Assert.Single(result.Errors);
        Assert.Equal(5, err.RowNumber);
        Assert.Equal(2, changed.Version);
        Assert.Equal("Câu mới?", changed.Question);
        Assert.Null(changed.EmbeddingHash);
        Assert.Equal([changed.Id], repo.ClearedEmbeddings);
        Assert.Contains(audit.Entries, a => a.Action == AuditLogConstants.KnowledgeBaseImport);
    }

    [Fact]
    public async Task ImportAsync_BadRowsAreReportedWithRowNumber_AndDuplicateIdsInFileAreSkipped()
    {
        var repo = new FakeKnowledgeRepository();
        var rows = new[]
        {
            Row(2, "1"),
            Row(3, "1", "Trùng id?"),
            Row(4, null),
            Row(5, "abc"),
            Row(6, "7") with { RetrievedAt = "01/11/2026" },
            Row(7, "8") with { DocType = "proc_steps" },
        };

        var result = await Svc(repo, rows).ImportAsync(new MemoryStream(), Actor);
        var preview = await Svc(repo, rows).PreviewAsync(new MemoryStream());

        Assert.Equal(1, result.Created);
        Assert.Equal([3, 4, 5, 6, 7], result.Errors.Select(e => e.RowNumber));
        Assert.Equal(result.Errors.Select(e => e.RowNumber), preview.Errors.Select(e => e.RowNumber));
        Assert.Single(repo.Items);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("oldest")]
    [InlineData("entryNo")]
    [InlineData("updated")]
    public void RepositoryQueries_TranslateToSql(string? sort)
    {
        var options = new DbContextOptionsBuilder<StudyAbroad.Infrastructure.Persistence.AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new StudyAbroad.Infrastructure.Persistence.AppDbContext(options);
        var filter = new KnowledgeEntryFilter("100%_", "proc_steps", "LABEL_VISA_F1", "I-20", sort);

        var sql = KnowledgeBaseRepository.Sorted(KnowledgeBaseRepository.Filtered(db.KnowledgeDocuments, filter), sort).ToQueryString();

        Assert.Contains("ILIKE", sql);
        Assert.Contains("ORDER BY", sql);
    }

    [Fact]
    public async Task ImportAsync_TreatsPendingPrefixNoteAsNormalNote()
    {
        var repo = new FakeKnowledgeRepository();
        var rows = new[]
        {
            Row(2, "1") with { Note = "CHỜ DUYỆT: ghi chú thường" },
            Row(3, "2", "Viết thường?") with { Note = "  chờ duyệt: nội dung bình thường" },
            Row(4, "3", "Thiếu nguồn?") with { Note = "CHỜ DUYỆT: thiếu nguồn", Source = null, SourceUrl = null },
            Row(5, "abc") with { Note = "CHỜ DUYỆT" },
        };

        var preview = await Svc(repo, rows).PreviewAsync(new MemoryStream());
        var result = await Svc(repo, rows).ImportAsync(new MemoryStream(), Actor);

        Assert.Equal((4, 2), (result.TotalRows, result.Created));
        Assert.Equal([4, 5], result.Errors.Select(e => e.RowNumber));
        Assert.Equal([4, 5], preview.Errors.Select(e => e.RowNumber));
        Assert.Equal([1, 2], repo.Items.Select(x => x.EntryNo));
        Assert.Equal("CHỜ DUYỆT: ghi chú thường", repo.Items.Single(x => x.EntryNo == 1).Note);
    }

    // --- tạo vector sau khi lưu: chat chỉ thấy dòng đã có vector ---

    [Fact]
    public async Task ImportAsync_AsksAdvisorForVectorsOnce_AfterTheRowsAreSaved()
    {
        var repo = new FakeKnowledgeRepository();
        var advisor = new FakeKnowledgeAdvisor { OnCall = () => repo.Items.Count };
        var rows = new[] { Row(2, "1"), Row(3, "2", "Câu hai?") };

        var result = await Svc(repo, advisor, rows).ImportAsync(new MemoryStream(), Actor);

        Assert.Equal(1, advisor.Calls);
        Assert.Equal(2, advisor.RowsSavedWhenCalled);
        Assert.True(result.VectorsStarted);
        Assert.Null(result.VectorWarning);
    }

    [Fact]
    public async Task ImportAsync_WhenAdvisorIsDown_StillSavesRowsAndOnlyWarns()
    {
        var repo = new FakeKnowledgeRepository();
        var advisor = new FakeKnowledgeAdvisor { Error = new HttpRequestException("advisor tắt") };

        var result = await Svc(repo, advisor, Row(2, "1")).ImportAsync(new MemoryStream(), Actor);

        Assert.Equal(1, result.Created);
        Assert.Single(repo.Items);
        Assert.False(result.VectorsStarted);
        Assert.Equal(KnowledgeBaseService.EmbedWarning, result.VectorWarning);
    }

    [Fact]
    public async Task CreateAndUpdate_WhenAdvisorIsDown_StillSavesAndReturnsVectorWarning()
    {
        var repo = new FakeKnowledgeRepository();
        var advisor = new FakeKnowledgeAdvisor { Error = new HttpRequestException("advisor tắt") };
        var svc = Svc(repo, advisor);

        var created = await svc.CreateAsync(Full(), Actor);
        Assert.Equal(KnowledgeBaseService.EmbedWarning, created.VectorWarning);

        var updated = await svc.UpdateAsync(created.Id, Full("Câu hỏi mới?"), Actor);
        Assert.Equal(KnowledgeBaseService.EmbedWarning, updated!.VectorWarning);
    }

    [Fact]
    public async Task UpdateAsync_AsksAdvisorOnlyWhenTheQuestionChanged()
    {
        var repo = new FakeKnowledgeRepository();
        var document = repo.Add();
        var advisor = new FakeKnowledgeAdvisor();
        var svc = Svc(repo, advisor);

        await svc.UpdateAsync(document.Id, Full(question: document.Question) with { Answer = "Đổi câu trả lời" }, Actor);
        Assert.Equal(0, advisor.Calls);

        await svc.UpdateAsync(document.Id, Full(question: "Câu hỏi mới?"), Actor);
        Assert.Equal(1, advisor.Calls);
    }

    [Fact]
    public async Task GetEmbedStatus_MapsTheAdvisorAnswer_WithoutStartingAnything()
    {
        var advisor = new FakeKnowledgeAdvisor { Status = new KnowledgeEmbedDto(494, true, 12, null, false) };

        var status = await Svc(new FakeKnowledgeRepository(), advisor).GetEmbedStatusAsync();

        Assert.Equal(new KnowledgeEmbedStatusDto(true, 494, true, 12, null, false), status);
        Assert.Equal(1, advisor.StatusCalls);
        Assert.Equal(0, advisor.Calls);
    }

    [Fact]
    public async Task GetEmbedStatus_WhenAdvisorIsDown_ReportsUnavailableInsteadOfThrowing()
    {
        var advisor = new FakeKnowledgeAdvisor { Error = new HttpRequestException("advisor tắt") };

        var status = await Svc(new FakeKnowledgeRepository(), advisor).GetEmbedStatusAsync();

        Assert.False(status.Available);
    }

    [Fact]
    public async Task ImportAsync_EmptySheet_Throws()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Svc(new FakeKnowledgeRepository()).ImportAsync(new MemoryStream(), Actor));
    }
}

internal sealed class FakeKnowledgeAdvisor : IAdvisorClient
{
    public int Calls { get; private set; }
    public Exception? Error { get; init; }
    public Func<int>? OnCall { get; init; }
    public int RowsSavedWhenCalled { get; private set; }
    public KnowledgeEmbedDto Status { get; init; } = new(494, true, 12, null, false);
    public int StatusCalls { get; private set; }
    public void ResetCalls() => Calls = 0;

    public Task<KnowledgeEmbedDto> GetKnowledgeEmbedStatusAsync(CancellationToken ct = default)
    {
        StatusCalls++;
        if (Error is not null)
            throw Error;
        return Task.FromResult(Status);
    }

    public Task<KnowledgeEmbedDto> EmbedKnowledgeAsync(CancellationToken ct = default)
    {
        Calls++;
        if (OnCall is not null)
            RowsSavedWhenCalled = OnCall();
        if (Error is not null)
            throw Error;
        return Task.FromResult(new KnowledgeEmbedDto(2, true, 0, null, true));
    }

    public Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ExplainResponseDto> ExplainAsync(ExplainRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<GoalNoteResponseDto> WriteGoalNoteAsync(GoalNoteRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ScenarioNoteResponseDto> WriteScenarioNoteAsync(ScenarioNoteRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CompareNoteResponseDto> WriteCompareNoteAsync(CompareNoteRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<RecommendNoteResponseDto> WriteRecommendNoteAsync(RecommendNoteRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();
}

internal class FakeKnowledgeReader(params KnowledgeImportRow[] rows) : IKnowledgeWorkbookReader
{
    public IReadOnlyList<KnowledgeImportRow> Read(Stream workbook, CancellationToken ct = default) => rows;
}

internal class FakeKnowledgeRepository : IKnowledgeBaseRepository
{
    public List<KnowledgeDocument> Items { get; } = [];
    public List<KnowledgeDocumentVersion> Versions { get; } = [];
    public List<Guid> ClearedEmbeddings { get; } = [];
    public bool ThrowDuplicateOnAdd { get; init; }

    public KnowledgeDocument Add(string question = "OPT là gì?", int entryNo = 0)
    {
        var document = new KnowledgeDocument
        {
            EntryNo = entryNo == 0 ? Items.Count + 100 : entryNo,
            DocType = "faq",
            Label = "LABEL_VISA_F1",
            Question = question,
            Answer = "OPT là thời gian thực tập sau tốt nghiệp.",
            Source = "Study in the States",
            RetrievedAt = new DateOnly(2026, 11, 1),
            Version = 1,
        };
        Items.Add(document);
        Versions.Add(new KnowledgeDocumentVersion
        {
            DocumentId = document.Id,
            Version = 1,
            Action = VersionActions.Create,
            DocType = document.DocType,
            Label = document.Label,
            Question = document.Question,
            Answer = document.Answer,
            Source = document.Source,
            SourceUrl = document.SourceUrl,
            RetrievedAt = document.RetrievedAt,
            TopicCode = document.TopicCode,
            Note = document.Note,
        });
        return document;
    }

    public Task<(IReadOnlyList<KnowledgeDocument> Rows, int Total)> PageAsync(
        KnowledgeEntryFilter filter, int skip, int take, CancellationToken ct = default)
    {
        var source = filter.Deleted ? Items.Where(x => x.DeletedAt != null) : Items.Where(x => x.DeletedAt == null);
        var all = KnowledgeBaseRepository.Sorted(KnowledgeBaseRepository.Filtered(source.AsQueryable(), filter), filter.Sort).ToList();
        return Task.FromResult(((IReadOnlyList<KnowledgeDocument>)all.Skip(skip).Take(take).ToList(), all.Count));
    }

    public Task<KnowledgeDocument?> FindAsync(Guid id, bool includeDeleted = true, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id && (includeDeleted || x.DeletedAt == null)));

    public Task<IReadOnlyList<KnowledgeEntryVersionRow>> VersionsAsync(Guid documentId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeEntryVersionRow>>(
            Versions.Where(v => v.DocumentId == documentId).OrderByDescending(v => v.Version).Select(v => new KnowledgeEntryVersionRow(v, "Admin")).ToList());

    public Task<KnowledgeDocumentVersion?> FindVersionAsync(Guid documentId, int version, CancellationToken ct = default) =>
        Task.FromResult(Versions.FirstOrDefault(v => v.DocumentId == documentId && v.Version == version));

    public Task<int> MaxEntryNoAsync(CancellationToken ct = default) =>
        Task.FromResult(Items.Select(x => x.EntryNo).DefaultIfEmpty(0).Max());

    public Task<Guid?> FindIdByEntryNoAsync(int entryNo, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(x => x.EntryNo == entryNo)?.Id);

    public Task AddAsync(KnowledgeDocument document, KnowledgeDocumentVersion initialVersion, CancellationToken ct = default)
    {
        if (ThrowDuplicateOnAdd)
            throw new KnowledgeDuplicateEntryException(Guid.NewGuid());
        Items.Add(document);
        initialVersion.DocumentId = document.Id;
        Versions.Add(initialVersion);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(
        Guid id, Func<KnowledgeDocument, (bool Changed, KnowledgeDocumentVersion? NewVersion)> apply,
        bool includeDeleted = true, CancellationToken ct = default)
    {
        var document = Items.FirstOrDefault(x => x.Id == id && (includeDeleted || x.DeletedAt == null));
        if (document is null)
            return Task.FromResult(false);
        var (changed, ver) = apply(document);
        if (changed && ver is not null)
        {
            ver.DocumentId = document.Id;
            Versions.Add(ver);
        }
        return Task.FromResult(true);
    }

    public Task<IReadOnlyDictionary<int, KnowledgeDocument>> TrackedByEntryNosAsync(
        IReadOnlyCollection<int> entryNos, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<int, KnowledgeDocument>>(
            Items.Where(x => entryNos.Contains(x.EntryNo)).ToDictionary(x => x.EntryNo));

    public Task SaveImportAsync(
        IReadOnlyList<KnowledgeDocument> added, IReadOnlyList<KnowledgeDocumentVersion> versions,
        CancellationToken ct = default)
    {
        Items.AddRange(added);
        Versions.AddRange(versions);
        return Task.CompletedTask;
    }

    public Task ClearEmbeddingsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        ClearedEmbeddings.AddRange(ids);
        return Task.CompletedTask;
    }
}
