using System.Globalization;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.KnowledgeBase;

public partial class KnowledgeBaseService
{
    public const int MaxPreviewRows = 100;

    public Task<KnowledgeImportPreviewDto> PreviewAsync(Stream workbook, CancellationToken ct = default)
    {
        var rows = ReadRows(workbook, ct);

        var errors = new List<KnowledgeImportErrorDto>();
        var seen = new HashSet<int>();
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var (_, entryNo) = ValidateRow(row);
                if (!seen.Add(entryNo))
                    throw new ArgumentException($"id {entryNo} bị lặp trong file.");
            }
            catch (ArgumentException ex)
            {
                errors.Add(new KnowledgeImportErrorDto(row.RowNumber, ex.Message));
            }
        }

        return Task.FromResult(new KnowledgeImportPreviewDto(rows.Count, rows.Take(MaxPreviewRows).ToList(), errors));
    }

    public async Task<KnowledgeImportResultDto> ImportAsync(
        Stream workbook, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        var rows = ReadRows(workbook, ct);

        var errors = new List<KnowledgeImportErrorDto>();
        var valid = new List<(int RowNumber, int EntryNo, ValidDocument Document)>();
        var seen = new HashSet<int>();
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var (document, entryNo) = ValidateRow(row);
                if (!seen.Add(entryNo))
                    throw new ArgumentException($"id {entryNo} bị lặp trong file.");
                valid.Add((row.RowNumber, entryNo, document));
            }
            catch (ArgumentException ex)
            {
                errors.Add(new KnowledgeImportErrorDto(row.RowNumber, ex.Message));
            }
        }

        var existing = await repository.TrackedByEntryNosAsync(valid.Select(x => x.EntryNo).ToList(), ct);
        var added = new List<KnowledgeDocument>();
        var versions = new List<KnowledgeDocumentVersion>();
        var reembed = new List<Guid>();
        var unchanged = 0;
        var updated = 0;
        var processedValid = 0;

        foreach (var (rowNumber, entryNo, document) in valid)
        {
            if (!existing.TryGetValue(entryNo, out var current))
            {
                var created = new KnowledgeDocument { EntryNo = entryNo, Version = 1 };
                Apply(created, document);
                added.Add(created);
                versions.Add(SnapshotOf(created, VersionActions.Import, actorUserId));
                processedValid++;
            }
            else if (current.DeletedAt is not null)
            {
                // UC-117: id thuộc một mục đã xóa là lỗi của dòng đó; muốn dùng lại nội dung, khôi phục mục (UC-116).
                errors.Add(new KnowledgeImportErrorDto(rowNumber, $"id {entryNo} thuộc một mục đã xóa; hãy khôi phục mục đó (UC-116) trước khi cập nhật."));
            }
            else if (ValidDocument.From(current) == document)
            {
                unchanged++;
                processedValid++;
            }
            else
            {
                if (current.Question != document.Question)
                    reembed.Add(current.Id);
                Apply(current, document);
                current.Version++;
                current.UpdatedAt = DateTime.UtcNow;
                versions.Add(SnapshotOf(current, VersionActions.Import, actorUserId));
                updated++;
                processedValid++;
            }
        }

        if (added.Count > 0 || updated > 0)
            await repository.SaveImportAsync(added, versions, ct);
        if (reembed.Count > 0)
            await repository.ClearEmbeddingsAsync(reembed, ct);

        await WriteAuditAsync(
            AuditLogConstants.KnowledgeBaseImport, actorUserId, null,
            new { totalRows = rows.Count, created = added.Count, updated, unchanged, errors = errors.Count },
            ipAddress, userAgent, ct);

        // Gọi cả khi lần nhập này không đổi gì: dòng thiếu vector từ lần nhập trước (advisor tắt) cũng được tạo bù
        var (started, warning) = processedValid > 0 ? await StartEmbeddingAsync(ct) : (false, null);
        return new KnowledgeImportResultDto(rows.Count, added.Count, updated, unchanged, errors, started, warning);
    }

    private IReadOnlyList<KnowledgeImportRow> ReadRows(Stream workbook, CancellationToken ct)
    {
        var rows = workbookReader.Read(workbook, ct);
        if (rows.Count == 0)
            throw new InvalidDataException("File không có dòng dữ liệu nào bên dưới dòng tiêu đề.");
        return rows;
    }

    private static (ValidDocument Document, int EntryNo) ValidateRow(KnowledgeImportRow row)
    {
        var rawId = Clean(row.Id) ?? throw new ArgumentException("Cột id không được để trống.");
        if (!decimal.TryParse(rawId, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            || number != decimal.Truncate(number) || number < 1 || number > MaxEntryNo)
            throw new ArgumentException($"Cột id \"{rawId}\" phải là số nguyên từ 1 đến {MaxEntryNo}.");

        var document = Validate(new SaveKnowledgeEntryRequest(
            (int)number, row.DocType, row.Label, row.Question, row.Answer, row.Source, row.SourceUrl,
            ParseDate(row.RetrievedAt), row.TopicCode, row.Note));
        return (document, (int)number);
    }

    private static DateOnly? ParseDate(string? raw)
    {
        var text = Clean(raw);
        if (text is null)
            return null;
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 1 and < 100000)
            return DateOnly.FromDateTime(DateTime.FromOADate(serial));
        throw new ArgumentException($"retrieved_at \"{text}\" phải có dạng yyyy-mm-dd.");
    }
}
