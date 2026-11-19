using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace StudyAbroad.Infrastructure.Import;

/// <summary>Một dòng dữ liệu: ô theo tên cột (chữ thường, đã trim). Ô trống không có trong Cells.</summary>
public record XlsxRow(int RowNumber, IReadOnlyDictionary<string, string> Cells)
{
    public string? Get(string header) => Cells.TryGetValue(header, out var value) ? value : null;
}

/// <summary>Một sheet đã đọc: tên, các tên cột ở dòng đầu (chữ thường), các dòng dữ liệu (đã bỏ dòng trống).</summary>
public record XlsxSheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<XlsxRow> Rows);

/// <summary>Một sheet để ghi: dòng tiêu đề và các dòng giá trị (null hoặc rỗng thì bỏ ô).</summary>
public record XlsxSheetData(string Name, IReadOnlyList<string> Headers, IReadOnlyList<string?[]> Rows);

/// <summary>
/// Đọc và ghi xlsx tối giản bằng ZipArchive + XDocument, không cần thư viện ngoài. Dùng chung cho mẫu SLM và kho tri thức.
/// Chỉ hiểu chuỗi dùng chung, chuỗi inline và giá trị thô của ô số; không đọc công thức, định dạng, ngày tháng.
/// </summary>
public static class XlsxPackage
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace DocRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    private const string WorkbookContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
    private const string WorksheetContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
    private const string RelationshipsContentType = "application/vnd.openxmlformats-package.relationships+xml";
    private const string WorkbookRelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
    private const string WorksheetRelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet";

    /// <summary>
    /// Đọc các sheet có tên (đã trim) thỏa includeSheet, theo thứ tự trong workbook. Dòng đầu tiên của sheet là tiêu đề;
    /// cột tiêu đề trống bị bỏ, dòng không có ô nào có chữ bị bỏ. File hỏng hoặc không phải xlsx thì ném InvalidDataException.
    /// </summary>
    public static IReadOnlyList<XlsxSheet> Read(Stream workbook, Func<string, bool> includeSheet, CancellationToken ct = default)
    {
        try
        {
            return ReadInternal(workbook, includeSheet, ct);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is XmlException or IOException or NotSupportedException or ArgumentException)
        {
            throw new InvalidDataException("File không phải xlsx hợp lệ hoặc đã bị hỏng.", ex);
        }
    }

    private static IReadOnlyList<XlsxSheet> ReadInternal(Stream workbook, Func<string, bool> includeSheet, CancellationToken ct)
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
                    targetsById[id] = target;
            }
        }

        var sharedStrings = new List<string>();
        var sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedEntry != null)
        {
            foreach (var si in LoadXml(sharedEntry).Descendants(Main + "si"))
                sharedStrings.Add(ConcatText(si));
        }

        var result = new List<XlsxSheet>();
        foreach (var sheet in workbookDoc.Descendants(Main + "sheet"))
        {
            ct.ThrowIfCancellationRequested();

            var name = ((string?)sheet.Attribute("name"))?.Trim();
            if (name == null || !includeSheet(name))
                continue;

            var relId = (string?)sheet.Attribute(DocRel + "id");
            if (relId == null || !targetsById.TryGetValue(relId, out var target))
                continue;

            var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            var sheetEntry = archive.GetEntry(path);
            if (sheetEntry == null)
                continue;

            result.Add(ReadSheet(name, LoadXml(sheetEntry), sharedStrings, ct));
        }
        return result;
    }

    private static XlsxSheet ReadSheet(string name, XDocument doc, List<string> sharedStrings, CancellationToken ct)
    {
        Dictionary<int, string>? headerByColumn = null;
        var rows = new List<XlsxRow>();
        var nextRowNumber = 0;

        foreach (var rowElement in doc.Descendants(Main + "row"))
        {
            ct.ThrowIfCancellationRequested();
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
                    cells[column] = text;
            }

            if (headerByColumn == null)
            {
                // Dòng đầu là tiêu đề. Tên cột trùng thì cột đầu tiên thắng
                headerByColumn = new Dictionary<int, string>();
                var seen = new HashSet<string>();
                foreach (var pair in cells)
                {
                    var header = pair.Value.ToLowerInvariant();
                    if (seen.Add(header))
                        headerByColumn[pair.Key] = header;
                }
                continue;
            }

            if (cells.Count == 0)
                continue;

            var values = new Dictionary<string, string>();
            foreach (var pair in cells)
            {
                if (headerByColumn.TryGetValue(pair.Key, out var header))
                    values[header] = pair.Value;
            }
            rows.Add(new XlsxRow(rowNumber, values));
        }

        return new XlsxSheet(name, headerByColumn?.Values.ToList() ?? [], rows);
    }

    private static string? CellText(XElement cell, List<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        switch (type)
        {
            case "s":
                var raw = (string?)cell.Element(Main + "v");
                if (int.TryParse(raw, out var index) && index >= 0 && index < sharedStrings.Count)
                    return sharedStrings[index];
                return null;
            case "inlineStr":
                var inline = cell.Element(Main + "is");
                return inline == null ? null : ConcatText(inline);
            default:
                // Ô số (id = 1) và ô t="str": lấy nguyên văn <v>
                return (string?)cell.Element(Main + "v");
        }
    }

    private static string ConcatText(XElement element)
    {
        var builder = new StringBuilder();
        foreach (var t in element.Descendants(Main + "t"))
        {
            if (t.Ancestors(Main + "rPh").Any())
                continue;
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
                break;
            index = index * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }
        return index - 1;
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    /// <summary>Ghi các sheet thành file xlsx (chuỗi inline, không style). Tối đa 26 cột (A–Z).</summary>
    public static byte[] Write(IReadOnlyList<XlsxSheetData> sheets)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypes(sheets.Count));
            WriteEntry(archive, "_rels/.rels", BuildPackageRelationships());
            WriteEntry(archive, "xl/workbook.xml", BuildWorkbook(sheets.Select(s => s.Name).ToList()));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships(sheets.Count));

            for (var index = 0; index < sheets.Count; index++)
                WriteEntry(archive, $"xl/worksheets/sheet{index + 1}.xml", Sheet(sheets[index].Headers, sheets[index].Rows));
        }
        return output.ToArray();
    }

    private static XDocument BuildContentTypes(int sheetCount)
    {
        var types = new XElement(ContentTypes + "Types",
            new XElement(ContentTypes + "Default",
                new XAttribute("Extension", "rels"), new XAttribute("ContentType", RelationshipsContentType)),
            new XElement(ContentTypes + "Default",
                new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
            new XElement(ContentTypes + "Override",
                new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", WorkbookContentType)));

        for (var index = 1; index <= sheetCount; index++)
        {
            types.Add(new XElement(ContentTypes + "Override",
                new XAttribute("PartName", $"/xl/worksheets/sheet{index}.xml"),
                new XAttribute("ContentType", WorksheetContentType)));
        }
        return new XDocument(types);
    }

    private static XDocument BuildPackageRelationships() =>
        new(new XElement(PkgRel + "Relationships",
            new XElement(PkgRel + "Relationship",
                new XAttribute("Id", "rId1"),
                new XAttribute("Type", WorkbookRelationshipType),
                new XAttribute("Target", "xl/workbook.xml"))));

    private static XDocument BuildWorkbook(IReadOnlyList<string> names)
    {
        var sheets = new XElement(Main + "sheets");
        for (var index = 0; index < names.Count; index++)
        {
            sheets.Add(new XElement(Main + "sheet",
                new XAttribute("name", names[index]),
                new XAttribute("sheetId", index + 1),
                new XAttribute(DocRel + "id", $"rId{index + 1}")));
        }

        var workbook = new XElement(Main + "workbook", new XAttribute(XNamespace.Xmlns + "r", DocRel.NamespaceName), sheets);
        return new XDocument(workbook);
    }

    private static XDocument BuildWorkbookRelationships(int sheetCount)
    {
        var relationships = new XElement(PkgRel + "Relationships");
        for (var index = 1; index <= sheetCount; index++)
        {
            relationships.Add(new XElement(PkgRel + "Relationship",
                new XAttribute("Id", $"rId{index}"),
                new XAttribute("Type", WorksheetRelationshipType),
                new XAttribute("Target", $"worksheets/sheet{index}.xml")));
        }
        return new XDocument(relationships);
    }

    private static XDocument Sheet(IReadOnlyList<string?> headers, IReadOnlyList<string?[]> rows)
    {
        var sheetData = new XElement(Main + "sheetData");
        sheetData.Add(BuildRow(1, headers));
        for (var index = 0; index < rows.Count; index++)
            sheetData.Add(BuildRow(index + 2, rows[index]));
        return new XDocument(new XElement(Main + "worksheet", sheetData));
    }

    private static XElement BuildRow(int rowNumber, IReadOnlyList<string?> values)
    {
        var row = new XElement(Main + "row", new XAttribute("r", rowNumber));
        for (var column = 0; column < values.Count; column++)
        {
            var text = values[column];
            if (string.IsNullOrEmpty(text))
                continue;

            var reference = $"{(char)('A' + column)}{rowNumber}";
            var textElement = new XElement(Main + "t", text);
            textElement.SetAttributeValue(XNamespace.Xml + "space", "preserve");
            row.Add(new XElement(Main + "c",
                new XAttribute("r", reference),
                new XAttribute("t", "inlineStr"),
                new XElement(Main + "is", textElement)));
        }
        return row;
    }

    private static void WriteEntry(ZipArchive archive, string path, XDocument document)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
        };
        using var writer = XmlWriter.Create(entryStream, settings);
        document.Save(writer);
    }
}