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
        var service = new SlmSampleService(new FakeSlmRepository(), new SlmWorkbookReader(), new SlmTemplateWriter(), new FakeAuditLogs());

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
    public void BuildExport_OneSheetPerTypeWithAnswerColumns_AndReadableByTheImporter()
    {
        var samples = new[]
        {
            new StudyAbroad.Domain.Entities.SlmTrainingSample
            {
                SampleType = SlmSampleTypes.Knowledge, Question = "visa F-1 cần gì", Answer = "Cần I-20", Topic = "visa",
                Intents = "[]", Split = SlmSampleSplits.Val,
            },
        };

        var bytes = new SlmTemplateWriter().BuildExport(samples);

        var row = Assert.Single(new SlmWorkbookReader().Read(new MemoryStream(bytes)));
        Assert.Equal((SlmSampleTypes.Knowledge, "visa F-1 cần gì", "val"), (row.SheetName, row.Question, row.Split));
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.Equal(SlmSampleTypes.All.Count, archive.Entries.Count(e => e.FullName.StartsWith("xl/worksheets/sheet")));
        using var reader = new StreamReader(archive.GetEntry("xl/worksheets/sheet4.xml")!.Open(), Encoding.UTF8);
        Assert.Contains("Cần I-20", reader.ReadToEnd());
    }
}
