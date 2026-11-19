using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Import;

public class SlmWorkbookReader : ISlmWorkbookReader
{
    public IReadOnlyList<SlmImportRow> Read(Stream workbook, CancellationToken ct = default)
    {
        var sheets = XlsxPackage.Read(workbook, name => SlmSampleTypes.All.Contains(name), ct);
        if (sheets.Count == 0)
        {
            throw new InvalidDataException(
                $"File không có sheet nào đúng tên loại mẫu. Cần ít nhất một trong: {string.Join(", ", SlmSampleTypes.All)}.");
        }

        return sheets
            .SelectMany(sheet => sheet.Rows.Select(row => new SlmImportRow(
                sheet.Name,
                row.RowNumber,
                row.Get("question"),
                row.Get("profile"),
                row.Get("pending_question"),
                row.Get("intents"),
                row.Get("context"),
                row.Get("split"),
                row.Get("review"))))
            .ToList();
    }
}