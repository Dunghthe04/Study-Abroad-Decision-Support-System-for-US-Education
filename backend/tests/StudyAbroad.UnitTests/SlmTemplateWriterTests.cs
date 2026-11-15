using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Infrastructure.Import;
using Xunit;

namespace StudyAbroad.UnitTests;

public class SlmTemplateWriterTests
{
    private static readonly string[] ExpectedHeaders =
        ["question", "profile", "pending_question", "intents", "split", "context", "review"];

    [Fact]
    public void Build_ReadableBySlmWorkbookReaderWithOneRowPerSampleType()
    {
        var bytes = new SlmTemplateWriter().Build();

        var rows = new SlmWorkbookReader().Read(new MemoryStream(bytes));

        Assert.Equal(SlmSampleTypes.All.Count, rows.Count);
        foreach (var type in SlmSampleTypes.All)
        {
            var row = Assert.Single(rows, r => r.SheetName == type);
            Assert.Equal(2, row.RowNumber);
            Assert.Equal(SlmSampleSplits.Train, row.Split);
            Assert.False(string.IsNullOrWhiteSpace(row.Question));
        }
    }

    [Fact]
    public async Task Build_EveryExampleRowPassesImportValidation()
    {
        var bytes = new SlmTemplateWriter().Build();
        var service = new SlmSampleService(new NoopRepository(), new SlmWorkbookReader());

        var preview = await service.PreviewAsync(new MemoryStream(bytes));

        Assert.Equal(SlmSampleTypes.All.Count, preview.TotalRows);
        Assert.Empty(preview.Errors);
    }

    [Fact]
    public void Build_IntentTypeMatchesSheetAndSpanIsInsideQuestion()
    {
        var bytes = new SlmTemplateWriter().Build();

        var rows = new SlmWorkbookReader().Read(new MemoryStream(bytes));

        foreach (var row in rows)
        {
            using var intents = JsonDocument.Parse(row.Intents!);
            var intent = Assert.Single(intents.RootElement.EnumerateArray());
            Assert.Equal(row.SheetName, intent.GetProperty("type").GetString());
            Assert.Contains(intent.GetProperty("span").GetString()!, row.Question!);
            Assert.Equal(JsonValueKind.Object, intent.GetProperty("entities").ValueKind);
        }
    }

    [Fact]
    public void Build_SheetsAreNamedAfterSampleTypesWithExpectedHeaders()
    {
        var bytes = new SlmTemplateWriter().Build();

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var workbook = XDocument.Load(archive.GetEntry("xl/workbook.xml")!.Open());
        var names = workbook.Descendants(main + "sheet").Select(s => (string)s.Attribute("name")!).ToList();
        Assert.Equal(SlmSampleTypes.All, names);

        for (var index = 1; index <= names.Count; index++)
        {
            var sheet = XDocument.Load(archive.GetEntry($"xl/worksheets/sheet{index}.xml")!.Open());
            var headerRow = sheet.Descendants(main + "row").First();
            var headers = headerRow.Descendants(main + "t").Select(t => t.Value).ToArray();
            Assert.Equal(ExpectedHeaders, headers);
        }
    }

    [Fact]
    public void Build_EntriesAreUtf8WithoutBomAndKeepVietnameseText()
    {
        var bytes = new SlmTemplateWriter().Build();

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            using var stream = entry.Open();
            var buffer = new byte[3];
            var read = stream.Read(buffer, 0, 3);
            Assert.False(read == 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF, entry.FullName);
        }

        using var reader = new StreamReader(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open(), Encoding.UTF8);
        Assert.Contains("em học lớp 10 ạ", reader.ReadToEnd());
    }

    [Fact]
    public void Build_ContainsRequiredPackageParts()
    {
        var bytes = new SlmTemplateWriter().Build();

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var names = archive.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("[Content_Types].xml", names);
        Assert.Contains("_rels/.rels", names);
        Assert.Contains("xl/workbook.xml", names);
        Assert.Contains("xl/_rels/workbook.xml.rels", names);
        Assert.Equal(SlmSampleTypes.All.Count, names.Count(n => n.StartsWith("xl/worksheets/sheet")));
    }

    [Fact]
    public void SlmSampleBackend_HasNoJsonlExportLeft()
    {
        var root = FindBackendSourceRoot();
        var files = new[]
        {
            Path.Combine(root, "StudyAbroad.Api", "Controllers", "AdminSlmSamplesController.cs"),
            Path.Combine(root, "StudyAbroad.Application", "Admin", "SlmSamples", "ISlmSampleService.cs"),
            Path.Combine(root, "StudyAbroad.Application", "Admin", "SlmSamples", "ISlmSampleRepository.cs"),
            Path.Combine(root, "StudyAbroad.Application", "Admin", "SlmSamples", "SlmSampleService.cs"),
            Path.Combine(root, "StudyAbroad.Application", "Admin", "SlmSamples", "SlmSampleDtos.cs"),
            Path.Combine(root, "StudyAbroad.Infrastructure", "Persistence", "Repositories", "SlmSampleRepository.cs"),
        };

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("ExportJsonl", text);
            Assert.DoesNotContain("ApprovedAsync", text);
            Assert.DoesNotContain("SlmExportLine", text);
            Assert.DoesNotContain("x-ndjson", text);
            Assert.DoesNotContain("HttpGet(\"export\")", text);
        }
    }

    private static string FindBackendSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "StudyAbroad.Api");
            if (Directory.Exists(candidate))
                return Path.Combine(directory.FullName, "src");
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Không tìm thấy thư mục backend/src.");
    }

    private class NoopRepository : ISlmSampleRepository
    {
        public Task<(IReadOnlyList<StudyAbroad.Domain.Entities.SlmTrainingSample> Rows, int Total)> PageAsync(
            SlmSampleFilter filter, int skip, int take, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<StudyAbroad.Domain.Entities.SlmTrainingSample?> FindAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task AddAsync(StudyAbroad.Domain.Entities.SlmTrainingSample sample, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task AddRangeAsync(IReadOnlyList<StudyAbroad.Domain.Entities.SlmTrainingSample> samples, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> UpdateAsync(
            Guid id, Action<StudyAbroad.Domain.Entities.SlmTrainingSample> apply, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<int> ReviewAsync(
            IReadOnlyList<Guid> ids, string status, string? note, Guid reviewerId, DateTime reviewedAt, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SlmSampleCountRow>> CountsAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<SlmSampleKeyRow>> ExistingKeysAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
