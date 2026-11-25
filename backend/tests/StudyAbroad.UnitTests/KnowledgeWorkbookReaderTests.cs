using System.IO.Compression;
using System.Text;
using StudyAbroad.Application.Admin.KnowledgeBase;
using StudyAbroad.Infrastructure.Import;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace StudyAbroad.UnitTests;

public class KnowledgeWorkbookReaderTests
{
    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>
    /// Giống file thật: id là ô số, có cột tiêu đề trống và dòng trống ở cuối.
    /// withImageColumn: file cũ còn cột image_url (11 cột); false là file mới 10 cột.
    /// </summary>
    private static MemoryStream RealShapedWorkbook(string sheetName = "knowledge_base", bool withImageColumn = true, string? imageValue = null)
    {
        static string Inline(string cell, string text) => $"<c r=\"{cell}\" t=\"inlineStr\"><is><t>{text}</t></is></c>";
        var headerTail = withImageColumn
            ? Inline("J1", "image_url") + Inline("K1", "note") + "<c r=\"L1\"/>"
            : Inline("J1", "note") + "<c r=\"K1\"/>";
        var imageCell = withImageColumn && imageValue is not null ? Inline("J2", imageValue) : "";
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "xl/workbook.xml",
                $"<workbook xmlns=\"{Ns}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                $"<sheets><sheet name=\"{sheetName}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add(zip, "xl/_rels/workbook.xml.rels",
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Add(zip, "xl/worksheets/sheet1.xml",
                $"<worksheet xmlns=\"{Ns}\"><sheetData>" +
                "<row r=\"1\">" + Inline("A1", "id") + Inline("B1", "doc_type") + Inline("C1", "label") + Inline("D1", "Question") +
                Inline("E1", "answer") + Inline("F1", "source") + Inline("G1", "source_url") + Inline("H1", "retrieved_at") +
                Inline("I1", "topic_code") + headerTail + "</row>" +
                "<row r=\"2\"><c r=\"A2\"><v>1</v></c>" + Inline("B2", "faq") + Inline("C2", "LABEL_VISA_F1") +
                Inline("D2", "OPT là gì?") + Inline("E2", "Trả lời") + Inline("F2", "Nguồn\nMã: EXP-KB-1") +
                Inline("H2", "2026-11-01") + imageCell + "</row>" +
                "<row r=\"3\"><c r=\"A3\"/><c r=\"B3\"/></row>" +
                "</sheetData></worksheet>");
        }
        stream.Position = 0;
        return stream;
    }

    private static void Add(ZipArchive zip, string path, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    [Fact]
    public void Read_NumericIdHeadersCaseInsensitive_SkipsBlankRowsAndEmptyHeaders()
    {
        var row = Assert.Single(new KnowledgeWorkbookReader().Read(RealShapedWorkbook()));

        Assert.Equal(
            new KnowledgeImportRow(2, "1", "faq", "LABEL_VISA_F1", "OPT là gì?", "Trả lời", "Nguồn\nMã: EXP-KB-1", null, "2026-11-01", null, null),
            row);
    }

    [Fact]
    public void Read_NewTenColumnFile_WithoutImageUrl_IsAccepted()
    {
        var row = Assert.Single(new KnowledgeWorkbookReader().Read(RealShapedWorkbook(withImageColumn: false)));

        Assert.Equal("OPT là gì?", row.Question);
    }

    [Fact]
    public void Read_OldFileWithAnImageUrl_IgnoresTheColumn_BecauseTheKnowledgeBaseIsTextOnly()
    {
        var row = Assert.Single(new KnowledgeWorkbookReader().Read(RealShapedWorkbook(imageValue: "https://example.org/i20.png")));

        Assert.Equal("OPT là gì?", row.Question);
    }

    [Fact]
    public void Template_HasTheTenColumnsOfUc117_AndNoImageUrl()
    {
        var sheet = XlsxPackage.Read(new MemoryStream(new KnowledgeTemplateWriter().Build()), n => n == KnowledgeWorkbookReader.SheetName).Single();

        Assert.Equal(10, sheet.Headers.Count);
        Assert.DoesNotContain("image_url", sheet.Headers);
    }

    [Fact]
    public void Read_WithoutKnowledgeBaseSheet_Throws()
    {
        var error = Assert.Throws<InvalidDataException>(() => new KnowledgeWorkbookReader().Read(RealShapedWorkbook("Sheet1")));

        Assert.Contains("knowledge_base", error.Message);
    }

    [Fact]
    public void Read_NotAZip_ThrowsInvalidData()
    {
        Assert.Throws<InvalidDataException>(() => new KnowledgeWorkbookReader().Read(new MemoryStream([1, 2, 3])));
    }

    [Fact]
    public async Task Template_RoundTripsThroughReader_AndEveryExampleRowIsValid()
    {
        var template = new MemoryStream(new KnowledgeTemplateWriter().Build());
        var rows = new KnowledgeWorkbookReader().Read(template);
        template.Position = 0;

        var preview = await new KnowledgeBaseService(new FakeKnowledgeRepository(), new KnowledgeWorkbookReader(), new FakeKnowledgeAdvisor(), new FakeAuditLogs(), NullLogger<KnowledgeBaseService>.Instance).PreviewAsync(template);

        Assert.Equal(2, rows.Count);
        Assert.Empty(preview.Errors);
    }

    [Fact]
    public void XlsxPackage_WriteThenRead_KeepsMultilineText_AndSkipsBlankRows()
    {
        var bytes = XlsxPackage.Write([new XlsxSheetData("s", ["a", "b"], [["x", "dòng 1\ndòng 2"], [null, ""], ["y", null]])]);

        var sheet = Assert.Single(XlsxPackage.Read(new MemoryStream(bytes), _ => true));

        Assert.Equal(["a", "b"], sheet.Headers);
        Assert.Equal([2, 4], sheet.Rows.Select(r => r.RowNumber));
        Assert.Equal("dòng 1\ndòng 2", sheet.Rows[0].Get("b"));
        Assert.Null(sheet.Rows[1].Get("b"));
    }
}