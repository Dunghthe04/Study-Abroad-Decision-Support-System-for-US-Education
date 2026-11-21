using StudyAbroad.Application.Admin.KnowledgeBase;

namespace StudyAbroad.Infrastructure.Import;

public class KnowledgeWorkbookReader : IKnowledgeWorkbookReader
{
    public const string SheetName = "knowledge_base";
    public static readonly string[] RequiredHeaders = ["id", "label", "level", "question", "answer", "source"];

    public IReadOnlyList<KnowledgeImportRow> Read(Stream workbook, CancellationToken ct = default)
    {
        var sheet = XlsxPackage.Read(workbook, name => string.Equals(name, SheetName, StringComparison.OrdinalIgnoreCase), ct)
            .FirstOrDefault()
            ?? throw new InvalidDataException($"File không có sheet {SheetName}.");

        var missing = RequiredHeaders.Where(header => !sheet.Headers.Contains(header)).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException($"Sheet {SheetName} thiếu cột: {string.Join(", ", missing)}.");

        return sheet.Rows
            .Select(row => new KnowledgeImportRow(
                row.RowNumber, row.Get("id"), row.Get("label"), row.Get("level"),
                row.Get("question"), row.Get("answer"), row.Get("source")))
            .ToList();
    }
}