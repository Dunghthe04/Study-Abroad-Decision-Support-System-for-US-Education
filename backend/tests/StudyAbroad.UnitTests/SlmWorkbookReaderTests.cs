using System.IO.Compression;
using System.Text;
using StudyAbroad.Infrastructure.Import;
using Xunit;

namespace StudyAbroad.UnitTests;

public class SlmWorkbookReaderTests
{
    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static MemoryStream BuildWorkbook()
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "xl/workbook.xml",
                $"<workbook xmlns=\"{Ns}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" +
                "<sheet name=\"README\" sheetId=\"1\" r:id=\"rId1\"/>" +
                "<sheet name=\"knowledge\" sheetId=\"2\" r:id=\"rId2\"/>" +
                "<sheet name=\"profile_review\" sheetId=\"3\" r:id=\"rId3\"/></sheets></workbook>");
            Add(zip, "xl/_rels/workbook.xml.rels",
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\"/>" +
                "<Relationship Id=\"rId2\" Target=\"worksheets/sheet2.xml\"/>" +
                "<Relationship Id=\"rId3\" Target=\"/xl/worksheets/sheet3.xml\"/></Relationships>");
            Add(zip, "xl/sharedStrings.xml",
                $"<sst xmlns=\"{Ns}\">" +
                "<si><t>Question</t></si><si><t>intents</t></si><si><t>Hỏi gì đó</t></si>" +
                "<si><r><t>[\"a\",</t></r><r><t>\"b\"]</t></r></si>" +
                "<si><t> SPLIT </t></si><si><t>train</t></si><si><t>context</t></si><si><t>review</t></si></sst>");
            Add(zip, "xl/worksheets/sheet1.xml",
                $"<worksheet xmlns=\"{Ns}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c></row>" +
                "<row r=\"2\"><c r=\"A2\" t=\"s\"><v>2</v></c></row></sheetData></worksheet>");
            Add(zip, "xl/worksheets/sheet2.xml",
                $"<worksheet xmlns=\"{Ns}\"><sheetData>" +
                "<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\" t=\"s\"><v>1</v></c><c r=\"C1\" t=\"s\"><v>4</v></c></row>" +
                "<row r=\"2\"><c r=\"A2\" t=\"s\"><v>2</v></c><c r=\"B2\" t=\"s\"><v>3</v></c><c r=\"C2\" t=\"s\"><v>5</v></c></row>" +
                "<row r=\"3\"><c r=\"A3\"/></row>" +
                "<row r=\"5\"><c r=\"A5\" t=\"inlineStr\"><is><t> inline </t></is></c><c r=\"B5\" t=\"str\"><v>[]</v></c></row>" +
                "</sheetData></worksheet>");
            Add(zip, "xl/worksheets/sheet3.xml",
                $"<worksheet xmlns=\"{Ns}\"><sheetData>" +
                "<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"C1\" t=\"s\"><v>6</v></c><c r=\"E1\" t=\"s\"><v>4</v></c><c r=\"F1\" t=\"s\"><v>7</v></c></row>" +
                "<row r=\"2\"><c r=\"A2\" t=\"s\"><v>2</v></c><c r=\"C2\" t=\"s\"><v>3</v></c><c r=\"E2\" t=\"s\"><v>5</v></c><c r=\"F2\" t=\"n\"><v>42</v></c></row>" +
                "</sheetData></worksheet>");
        }
        stream.Position = 0;
        return stream;
    }

    private static void Add(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    [Fact]
    public void Read_MatchesHeadersIgnoresReadmeAndKeepsRowNumbers()
    {
        var rows = new SlmWorkbookReader().Read(BuildWorkbook());

        Assert.DoesNotContain(rows, r => r.SheetName == "README");
        var knowledge = rows.Where(r => r.SheetName == "knowledge").ToList();
        Assert.Equal(new[] { 2, 5 }, knowledge.Select(r => r.RowNumber));
        Assert.Equal("Hỏi gì đó", knowledge[0].Question);
        Assert.Equal("[\"a\",\"b\"]", knowledge[0].Intents);
        Assert.Equal("train", knowledge[0].Split);
        Assert.Equal("inline", knowledge[1].Question);
        Assert.Equal("[]", knowledge[1].Intents);
        Assert.Null(knowledge[1].Split);
    }

    [Fact]
    public void Read_HandlesDifferentColumnOrder()
    {
        var rows = new SlmWorkbookReader().Read(BuildWorkbook());

        var row = Assert.Single(rows, r => r.SheetName == "profile_review");
        Assert.Equal(2, row.RowNumber);
        Assert.Equal("Hỏi gì đó", row.Question);
        Assert.Equal("[\"a\",\"b\"]", row.Context);
        Assert.Equal("train", row.Split);
        Assert.Equal("42", row.SourceReview);
        Assert.Null(row.Intents);
    }

    [Fact]
    public void Read_InvalidFile_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not a zip"));
        Assert.Throws<InvalidDataException>(() => new SlmWorkbookReader().Read(stream));
    }

    [Fact]
    public void Read_ZipWithoutWorkbook_ThrowsInvalidDataException()
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "other.xml", "<a/>");
        }
        stream.Position = 0;
        Assert.Throws<InvalidDataException>(() => new SlmWorkbookReader().Read(stream));
    }

    [Fact]
    public void Read_RealFile_WhenPresent()
    {
        const string path = "C:/Users/duncuti16/Downloads/du_lieu_slm.xlsx";
        if (!File.Exists(path))
        {
            return;
        }

        using var file = File.OpenRead(path);
        var rows = new SlmWorkbookReader().Read(file);

        Assert.Equal(2360, rows.Count(r => r.SheetName == "knowledge"));
        Assert.True(rows.Count > 6000);
    }
}
