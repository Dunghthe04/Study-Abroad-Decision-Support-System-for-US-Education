using System.Text;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.KnowledgeBase;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence.Repositories;
using Xunit;

namespace StudyAbroad.UnitTests;

public class KnowledgeBaseServiceTests
{
    private static KnowledgeBaseService Svc(FakeKnowledgeRepository repo, params KnowledgeImportRow[] rows) =>
        new(repo, new FakeKnowledgeReader(rows));

    private static SaveKnowledgeEntryRequest Full(string question = "OPT là gì?", int? entryNo = null) =>
        new(entryNo, "faq", "LABEL_VISA_F1", question, "OPT là thời gian thực tập sau tốt nghiệp.", "Study in the States", null,
            new DateOnly(2026, 11, 1), null, null, null);

    private static KnowledgeImportRow Row(int rowNumber, string? id, string question = "OPT là gì?") =>
        new(rowNumber, id, "faq", "label_visa_f1", question, "Câu trả lời.", null, "https://studyinthestates.dhs.gov",
            "2026-11-01", null, null, null);

    [Fact]
    public async Task CreateAsync_WithoutEntryNo_TakesMaxPlusOne_AndNormalizesText()
    {
        var repo = new FakeKnowledgeRepository();
        repo.Add(entryNo: 41);
        var nfd = "Học   bổng là gì?".Normalize(NormalizationForm.FormD);

        var created = await Svc(repo).CreateAsync(Full(nfd));

        Assert.Equal(42, created.EntryNo);
        Assert.Equal("Học bổng là gì?", created.Question);
        Assert.True(created.Question.IsNormalized(NormalizationForm.FormC));
        Assert.Equal(2, repo.Items.Count);
    }

    [Fact]
    public async Task CreateAsync_UsedEntryNo_ThrowsDuplicateWithExistingId()
    {
        var repo = new FakeKnowledgeRepository();
        var existing = repo.Add(entryNo: 7);

        var error = await Assert.ThrowsAsync<KnowledgeDuplicateEntryException>(() => Svc(repo).CreateAsync(Full(entryNo: 7)));

        Assert.Equal(existing.Id, error.ExistingId);
    }

    [Theory]
    [InlineData("doc_type", "bang")]
    [InlineData("label", "LABEL_KHONG_CO")]
    [InlineData("question", " ")]
    [InlineData("answer", null)]
    [InlineData("retrieved_at", null)]
    [InlineData("source", null)]
    [InlineData("topic_code", "proc")]
    [InlineData("image_url", "ảnh.png")]
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
            "topic_code" => Full() with { DocType = "proc_steps", TopicCode = null },
            _ => Full() with { ImageUrl = value },
        };

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(new FakeKnowledgeRepository()).CreateAsync(request));
    }

    [Fact]
    public async Task UpdateAsync_ChangedQuestion_ClearsEmbeddingAndKeepsEntryNo()
    {
        var repo = new FakeKnowledgeRepository();
        var existing = repo.Add(entryNo: 5);
        existing.EmbeddingHash = "abc";
        existing.IngestedAt = DateTime.UtcNow;

        var updated = await Svc(repo).UpdateAsync(existing.Id, Full("OPT kéo dài bao lâu?", entryNo: 99));

        Assert.Equal(5, updated!.EntryNo);
        Assert.Null(existing.EmbeddingHash);
        Assert.Null(existing.IngestedAt);
        Assert.Equal([existing.Id], repo.ClearedEmbeddings);
    }

    [Fact]
    public async Task UpdateAsync_SameQuestion_KeepsEmbedding_AndUnknownIdReturnsNull()
    {
        var repo = new FakeKnowledgeRepository();
        var existing = repo.Add();
        existing.EmbeddingHash = "abc";

        await Svc(repo).UpdateAsync(existing.Id, Full(existing.Question) with { Answer = "Câu trả lời mới." });

        Assert.Equal("abc", existing.EmbeddingHash);
        Assert.Empty(repo.ClearedEmbeddings);
        Assert.Null(await Svc(repo).UpdateAsync(Guid.NewGuid(), Full()));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheRow_AndFalseWhenMissing()
    {
        var repo = new FakeKnowledgeRepository();
        var existing = repo.Add();

        Assert.True(await Svc(repo).DeleteAsync(existing.Id));
        Assert.False(await Svc(repo).DeleteAsync(existing.Id));
        Assert.Empty(repo.Items);
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
    public async Task ImportAsync_UpsertsByEntryNo_CountsCreatedUpdatedUnchanged()
    {
        var repo = new FakeKnowledgeRepository();
        var same = repo.Add(entryNo: 1, question: "OPT là gì?");
        same.Label = "LABEL_VISA_F1";
        same.Answer = "Câu trả lời.";
        same.Source = null;
        same.SourceUrl = "https://studyinthestates.dhs.gov";
        same.Note = null;
        var changed = repo.Add(entryNo: 2, question: "Câu cũ?");
        changed.EmbeddingHash = "abc";
        var rows = new[] { Row(2, "1"), Row(3, "2", "Câu mới?"), Row(4, "3", "Dòng thêm?") };

        var result = await Svc(repo, rows).ImportAsync(new MemoryStream());

        Assert.Equal((3, 1, 1, 1), (result.TotalRows, result.Created, result.Updated, result.Unchanged));
        Assert.Empty(result.Errors);
        Assert.Equal([1, 2, 3], repo.Items.Select(x => x.EntryNo).Order());
        Assert.Equal("Câu mới?", changed.Question);
        Assert.Null(changed.EmbeddingHash);
        Assert.Equal([changed.Id], repo.ClearedEmbeddings);
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

        var result = await Svc(repo, rows).ImportAsync(new MemoryStream());
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
    public async Task ImportAsync_SkipsPendingRows_WithoutValidatingOrSaving_AndCountsThem()
    {
        var repo = new FakeKnowledgeRepository();
        var rows = new[]
        {
            Row(2, "1"),
            Row(3, "2", "Dòng đỏ?") with { Note = "CHỜ DUYỆT: chưa có nguồn thật", Source = null, SourceUrl = null },   // thiếu nguồn mà không báo lỗi
            Row(4, "3", "Viết thường?") with { Note = "  chờ duyệt: đã đổi nội dung" },
            Row(5, "abc") with { Note = "CHỜ DUYỆT" },                                                                  // id sai cũng chỉ bị bỏ qua
        };

        var result = await Svc(repo, rows).ImportAsync(new MemoryStream());
        var preview = await Svc(repo, rows).PreviewAsync(new MemoryStream());

        Assert.Equal((4, 1, 3), (result.TotalRows, result.Created, result.Pending));
        Assert.Empty(result.Errors);
        Assert.Equal([1], repo.Items.Select(x => x.EntryNo));
        Assert.Equal(3, preview.Pending);
        Assert.Empty(preview.Errors);
    }

    [Theory]
    [InlineData("CHỜ DUYỆT: thiếu nguồn", true)]
    [InlineData("chờ duyệt", true)]
    [InlineData("Đã duyệt, trước đây ghi CHỜ DUYỆT", false)]   // chỉ tính khi nằm ở đầu note
    [InlineData("Ghi chú thường", false)]
    [InlineData(null, false)]
    public void IsPending_OnlyWhenNoteStartsWithPendingMark(string? note, bool expected) =>
        Assert.Equal(expected, KnowledgeBaseService.IsPending(note));

    [Fact]
    public async Task CreateAsync_RejectsPendingNote_SoSingleEditCannotBypassTheRule()
    {
        var repo = new FakeKnowledgeRepository();
        var request = Full() with { Note = "CHỜ DUYỆT: chưa rà" };

        await Assert.ThrowsAsync<ArgumentException>(() => Svc(repo).CreateAsync(request));

        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task ImportAsync_EmptySheet_Throws()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Svc(new FakeKnowledgeRepository()).ImportAsync(new MemoryStream()));
    }
}

internal class FakeKnowledgeReader(params KnowledgeImportRow[] rows) : IKnowledgeWorkbookReader
{
    public IReadOnlyList<KnowledgeImportRow> Read(Stream workbook, CancellationToken ct = default) => rows;
}

internal class FakeKnowledgeRepository : IKnowledgeBaseRepository
{
    public List<KnowledgeDocument> Items { get; } = [];
    public List<Guid> ClearedEmbeddings { get; } = [];

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
        };
        Items.Add(document);
        return document;
    }

    public Task<(IReadOnlyList<KnowledgeDocument> Rows, int Total)> PageAsync(
        KnowledgeEntryFilter filter, int skip, int take, CancellationToken ct = default)
    {
        var all = KnowledgeBaseRepository.Sorted(KnowledgeBaseRepository.Filtered(Items.AsQueryable(), filter), filter.Sort).ToList();
        return Task.FromResult(((IReadOnlyList<KnowledgeDocument>)all.Skip(skip).Take(take).ToList(), all.Count));
    }

    public Task<KnowledgeDocument?> FindAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task<int> MaxEntryNoAsync(CancellationToken ct = default) =>
        Task.FromResult(Items.Select(x => x.EntryNo).DefaultIfEmpty(0).Max());

    public Task<Guid?> FindIdByEntryNoAsync(int entryNo, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(x => x.EntryNo == entryNo)?.Id);

    public Task AddAsync(KnowledgeDocument document, CancellationToken ct = default)
    {
        Items.Add(document);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(Guid id, Action<KnowledgeDocument> apply, CancellationToken ct = default)
    {
        var document = Items.FirstOrDefault(x => x.Id == id);
        if (document is not null)
            apply(document);
        return Task.FromResult(document is not null);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.RemoveAll(x => x.Id == id) > 0);

    public Task<IReadOnlyDictionary<int, KnowledgeDocument>> TrackedByEntryNosAsync(
        IReadOnlyCollection<int> entryNos, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<int, KnowledgeDocument>>(
            Items.Where(x => entryNos.Contains(x.EntryNo)).ToDictionary(x => x.EntryNo));

    public Task SaveImportAsync(IReadOnlyList<KnowledgeDocument> added, CancellationToken ct = default)
    {
        Items.AddRange(added);
        return Task.CompletedTask;
    }

    public Task ClearEmbeddingsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        ClearedEmbeddings.AddRange(ids);
        return Task.CompletedTask;
    }
}
