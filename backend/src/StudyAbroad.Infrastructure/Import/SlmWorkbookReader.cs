using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Import;

public class SlmWorkbookReader : ISlmWorkbookReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace DocRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    public IReadOnlyList<SlmImportRow> Read(Stream workbook)
    {
        try
        {
            return ReadInternal(workbook);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or NotSupportedException or ArgumentException)
        {
            throw new InvalidDataException("File không phải xlsx hợp lệ hoặc đã bị hỏng.", ex);
        }
    }

    private static IReadOnlyList<SlmImportRow> ReadInternal(Stream workbook)
    {
        using var archive = new ZipArchive(workbook, ZipArchiveMode.Read, leaveOpen: true);

        var workbookEntry = archive.GetEntry("xl/workbook.xml")
            ?? throw new InvalidDataException("File không phải xlsx hợp lệ (thiếu xl/workbook.xml).");
        var workbookDoc = LoadXml(workbookEntry);

        var targetsById = new Dictionary<string, string>();
        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (relsEntry != null)
        {
            foreach (var rel in LoadXml(relsEntry).Descendants(PkgRel + "Relationship"))
            {
                var id = (string?)rel.Attribute("Id");
                var target = (string?)rel.Attribute("Target");
                if (id != null && target != null)
                {
                    targetsById[id] = target;
                }
            }
        }

        var sharedStrings = new List<string>();
        var sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedEntry != null)
        {
            foreach (var si in LoadXml(sharedEntry).Descendants(Main + "si"))
            {
                sharedStrings.Add(ConcatText(si));
            }
        }

        var result = new List<SlmImportRow>();
        foreach (var sheet in workbookDoc.Descendants(Main + "sheet"))
        {
            var name = ((string?)sheet.Attribute("name"))?.Trim();
            if (name == null || !SlmSampleTypes.All.Contains(name))
            {
                continue;
            }

            var relId = (string?)sheet.Attribute(DocRel + "id");
            if (relId == null || !targetsById.TryGetValue(relId, out var target))
            {
                continue;
            }

            var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            var sheetEntry = archive.GetEntry(path);
            if (sheetEntry == null)
            {
                continue;
            }

            ReadSheet(name, LoadXml(sheetEntry), sharedStrings, result);
        }

        return result;
    }

    private static void ReadSheet(string sheetName, XDocument doc, List<string> sharedStrings, List<SlmImportRow> output)
    {
        Dictionary<string, int>? columnByHeader = null;
        var nextRowNumber = 0;

        foreach (var rowElement in doc.Descendants(Main + "row"))
        {
            nextRowNumber++;
            var rowNumber = (int?)rowElement.Attribute("r") ?? nextRowNumber;
            nextRowNumber = rowNumber;

            var cells = new Dictionary<int, string>();
            var fallbackColumn = 0;
            foreach (var cell in rowElement.Elements(Main + "c"))
            {
                var reference = (string?)cell.Attribute("r");
                var column = reference != null ? ColumnIndex(reference) : fallbackColumn;
                fallbackColumn = column + 1;

                var text = CellText(cell, sharedStrings)?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    cells[column] = text;
                }
            }

            if (columnByHeader == null)
            {
                columnByHeader = new Dictionary<string, int>();
                foreach (var pair in cells)
                {
                    columnByHeader.TryAdd(pair.Value.ToLowerInvariant(), pair.Key);
                }
                continue;
            }

            if (cells.Count == 0)
            {
                continue;
            }

            string? Get(string header) =>
                columnByHeader.TryGetValue(header, out var index) && cells.TryGetValue(index, out var value) ? value : null;

            output.Add(new SlmImportRow(
                sheetName,
                rowNumber,
                Get("question"),
                Get("profile"),
                Get("pending_question"),
                Get("intents"),
                Get("context"),
                Get("split"),
                Get("review")));
        }
    }

    private static string? CellText(XElement cell, List<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        switch (type)
        {
            case "s":
                var raw = (string?)cell.Element(Main + "v");
                if (int.TryParse(raw, out var index) && index >= 0 && index < sharedStrings.Count)
                {
                    return sharedStrings[index];
                }
                return null;
            case "inlineStr":
                var inline = cell.Element(Main + "is");
                return inline == null ? null : ConcatText(inline);
            default:
                return (string?)cell.Element(Main + "v");
        }
    }

    private static string ConcatText(XElement element)
    {
        var builder = new StringBuilder();
        foreach (var t in element.Descendants(Main + "t"))
        {
            if (t.Ancestors(Main + "rPh").Any())
            {
                continue;
            }
            builder.Append(t.Value);
        }
        return builder.ToString();
    }

    private static int ColumnIndex(string reference)
    {
        var index = 0;
        foreach (var ch in reference)
        {
            if (!char.IsAsciiLetter(ch))
            {
                break;
            }
            index = index * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }
        return index - 1;
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }
}
