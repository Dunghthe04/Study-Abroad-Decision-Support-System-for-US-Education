using System.IO.Compression;
using StudyAbroad.Infrastructure.Import;

namespace StudyAbroad.UnitTests;

public class XlsxPackageTests
{
    [Fact]
    public void Write_ThenRead_RoundTripsRowsAndSkipsBlankRows()
    {
        var bytes = XlsxPackage.Write([
            new XlsxSheetData("s", ["id", "question"], [["1", "a?"], [null, null], ["2", "b?"]]),
        ]);

        var sheet = Assert.Single(XlsxPackage.Read(new MemoryStream(bytes), _ => true));

        Assert.Equal(["id", "question"], sheet.Headers);
        Assert.Equal(2, sheet.Rows.Count);
        Assert.Equal("b?", sheet.Rows[1].Get("question"));
    }

    [Fact]
    public void Read_NotAZip_ThrowsInvalidData()
    {
        Assert.Throws<InvalidDataException>(() =>
            XlsxPackage.Read(new MemoryStream("not a zip"u8.ToArray()), _ => true));
    }

    [Fact]
    public void Read_EntryBiggerThanLimitAfterUnzip_ThrowsInvalidData()
    {
        // 51 MB khoảng trắng nén còn vài chục KB, đúng kiểu zip bomb
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = archive.CreateEntry("xl/workbook.xml").Open();
            var chunk = new byte[1024 * 1024];
            Array.Fill(chunk, (byte)' ');
            for (var i = 0; i < 51; i++)
                writer.Write(chunk, 0, chunk.Length);
        }
        output.Position = 0;

        Assert.Throws<InvalidDataException>(() => XlsxPackage.Read(output, _ => true));
    }
}
