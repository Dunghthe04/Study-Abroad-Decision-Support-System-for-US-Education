using StudyAbroad.Application.Admin.KnowledgeBase;

namespace StudyAbroad.Infrastructure.Import;

public class KnowledgeWorkbookReader : IKnowledgeWorkbookReader
{
    public const string SheetName = "knowledge_base";
    public static readonly string[] RequiredHeaders =
        ["id", "doc_type", "label", "question", "answer", "source", "source_url", "retrieved_at", "topic_code", "image_url", "note"];

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
                row.RowNumber, row.Get("id"), row.Get("doc_type"), row.Get("label"), row.Get("question"),
                row.Get("answer"), row.Get("source"), row.Get("source_url"), row.Get("retrieved_at"),
                row.Get("topic_code"), row.Get("image_url"), row.Get("note")))
            .ToList();
    }
}
