using System.Globalization;
using System.Text;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.KnowledgeBase;

/// <summary>Xem trước và nhập file Excel. Hai lần tải lên độc lập (không giữ file giữa hai bước), giống trang mẫu SLM.</summary>
public partial class KnowledgeBaseService
{
    public const int MaxPreviewRows = 100;

    public Task<KnowledgeImportPreviewDto> PreviewAsync(Stream workbook, CancellationToken ct = default)
    {
        var rows = ReadRows(workbook, ct);

        var errors = new List<KnowledgeImportErrorDto>();
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                ValidateRow(row);
            }
            catch (ArgumentException ex)
            {
                errors.Add(new KnowledgeImportErrorDto(row.RowNumber, ex.Message));
            }
        }

        return Task.FromResult(new KnowledgeImportPreviewDto(rows.Count, rows.Take(MaxPreviewRows).ToList(), errors));
    }

    public async Task<KnowledgeImportResultDto> ImportAsync(Stream workbook, Guid actorUserId, CancellationToken ct = default)
    {
        var rows = ReadRows(workbook, ct);

        // Gồm cả mục đã xóa: nhập lại file cũ không hồi sinh mục đã bị xóa
        var questions = new HashSet<string>();
        var questionByNo = new Dictionary<int, string>();
        foreach (var key in await repository.ExistingKeysAsync(ct))
        {
            var question = QuestionKey(key.Question);
            questions.Add(question);
            questionByNo[key.EntryNo] = question;
        }

        var toInsert = new List<KnowledgeBaseEntry>();
        var errors = new List<KnowledgeImportErrorDto>();
        var duplicates = 0;

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();

            ValidEntry valid;
            int? entryNo;
            try
            {
                (valid, entryNo) = ValidateRow(row);
            }
            catch (ArgumentException ex)
            {
                errors.Add(new KnowledgeImportErrorDto(row.RowNumber, ex.Message));
                continue;
            }

            var question = QuestionKey(valid.Question);
            if (entryNo is { } no && questionByNo.TryGetValue(no, out var usedBy))
            {
                if (usedBy == question)
                    duplicates++;
                else
                    errors.Add(new KnowledgeImportErrorDto(row.RowNumber, $"Mã {no} đã dùng cho câu hỏi khác."));
                continue;
            }
            if (!questions.Add(question))
            {
                duplicates++;
                continue;
            }

            // File đã đủ nội dung nên vào thẳng pending; người khác duyệt theo lô
            var entry = NewEntry(valid, KnowledgeReviewStatuses.Pending, KnowledgeOrigins.Excel, actorUserId);
            if (entryNo is { } given)
            {
                entry.EntryNo = given;
                questionByNo[given] = question;
            }
            toInsert.Add(entry);
        }

        // Dòng không có id: cấp số tiếp sau số lớn nhất (trong DB và trong file), tránh đụng số file đã dùng
        var next = questionByNo.Keys.DefaultIfEmpty(0).Max();
        foreach (var entry in toInsert.Where(e => e.EntryNo == 0))
            entry.EntryNo = ++next;

        if (toInsert.Count > 0)
        {
            var history = toInsert
                .Select(e => History(KnowledgeHistoryActions.Create, actorUserId, null, e.ReviewStatus, e.Id))
                .ToList();
            await repository.AddRangeAsync(toInsert, history, ct);
        }

        return new KnowledgeImportResultDto(rows.Count, toInsert.Count, duplicates, errors);
    }

    private IReadOnlyList<KnowledgeImportRow> ReadRows(Stream workbook, CancellationToken ct)
    {
        var rows = workbookReader.Read(workbook, ct);
        if (rows.Count == 0)
            throw new InvalidDataException("File không có dòng dữ liệu nào bên dưới dòng tiêu đề.");
        return rows;
    }

    /// <summary>Một dòng file phải đủ nội dung như khi gửi duyệt. Cột level dùng tên tiếng Việt, nhiều bậc cách nhau bởi dấu ;.</summary>
    // Chặn id quá lớn để không làm sequence entry_no tràn int
    private const int MaxEntryNo = 1_000_000;

    private static (ValidEntry Entry, int? EntryNo) ValidateRow(KnowledgeImportRow row)
    {
        int? entryNo = null;
        if (Clean(row.Id) is { } rawId)
        {
            // Excel lưu id dạng số: "1" hoặc "1.0"
            if (!decimal.TryParse(rawId, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                || number < 1 || number > MaxEntryNo || number != decimal.Truncate(number))
                throw new ArgumentException($"Cột id \"{rawId}\" phải là số nguyên từ 1 đến {MaxEntryNo}.");
            entryNo = (int)number;
        }

        var levels = new List<string>();
        foreach (var part in (row.Level ?? "").Split(';'))
        {
            var name = Clean(part)?.Normalize(NormalizationForm.FormC);
            if (name is null)
                continue;
            var code = KnowledgeLevels.All.FirstOrDefault(c =>
                string.Equals(KnowledgeLevels.Names[c], name, StringComparison.OrdinalIgnoreCase));
            if (code is null)
                throw new ArgumentException(
                    $"Bậc học \"{name}\" không hợp lệ. Dùng: {string.Join(", ", KnowledgeLevels.Names.Values)}.");
            levels.Add(code);
        }

        var valid = Validate(new SaveKnowledgeEntryRequest(row.Label, levels, row.Question, row.Answer, row.Source, Submit: true));
        EnsureReady(valid);
        return (valid, entryNo);
    }
}