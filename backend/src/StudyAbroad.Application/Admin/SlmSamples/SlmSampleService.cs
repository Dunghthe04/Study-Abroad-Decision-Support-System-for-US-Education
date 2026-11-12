using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.SlmSamples;

public class SlmSampleService(ISlmSampleRepository repository, ISlmWorkbookReader workbookReader) : ISlmSampleService
{
    public const int MaxPageSize = 100;
    public const int MaxReviewIds = 500;
    public const int MaxNoteLength = 1000;

    public async Task<PagedResult<SlmSampleDto>> GetPageAsync(
        SlmSampleFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var cleanFilter = NormalizeFilter(filter);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var skip = (page - 1) * pageSize;
        var (rows, total) = await repository.PageAsync(cleanFilter, skip, pageSize, ct);

        var items = new List<SlmSampleDto>();
        foreach (var row in rows)
            items.Add(ToDto(row));
        return new PagedResult<SlmSampleDto>(items, page, pageSize, total);
    }

    public async Task<SlmSampleSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var counts = await repository.CountsAsync(ct);

        var byStatus = new Dictionary<string, int>();
        foreach (var status in SlmReviewStatuses.All)
            byStatus[status] = 0;
        var byType = new Dictionary<string, int>();
        foreach (var type in SlmSampleTypes.All)
            byType[type] = 0;

        var total = 0;
        foreach (var row in counts)
        {
            total += row.Count;
            byStatus[row.ReviewStatus] = byStatus.GetValueOrDefault(row.ReviewStatus) + row.Count;
            byType[row.SampleType] = byType.GetValueOrDefault(row.SampleType) + row.Count;
        }
        return new SlmSampleSummaryDto(total, byStatus, byType);
    }

    public async Task<SlmSampleDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var sample = await repository.FindAsync(id, ct);
        return sample is null ? null : ToDto(sample);
    }

    public async Task<SlmSampleDto> CreateAsync(SaveSlmSampleRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var valid = Validate(request);

        var sample = new SlmTrainingSample
        {
            SampleType = valid.SampleType,
            Question = valid.Question,
            Profile = valid.Profile,
            PendingQuestion = valid.PendingQuestion,
            Intents = valid.Intents,
            Context = valid.Context,
            Split = valid.Split,
            SourceReview = valid.SourceReview,
            ReviewStatus = SlmReviewStatuses.Draft,
            CreatedByUserId = actorUserId,
        };
        await repository.AddAsync(sample, ct);
        return ToDto(sample);
    }

    public async Task<SlmSampleDto?> UpdateAsync(
        Guid id, SaveSlmSampleRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var valid = Validate(request);

        var updated = await repository.UpdateAsync(id, sample =>
        {
            var labelChanged =
                sample.SampleType != valid.SampleType
                || sample.Question != valid.Question
                || Canonical(sample.Profile) != Canonical(valid.Profile)
                || sample.PendingQuestion != valid.PendingQuestion
                || Canonical(sample.Intents) != Canonical(valid.Intents)
                || Canonical(sample.Context) != Canonical(valid.Context);

            sample.SampleType = valid.SampleType;
            sample.Question = valid.Question;
            sample.Profile = valid.Profile;
            sample.PendingQuestion = valid.PendingQuestion;
            sample.Intents = valid.Intents;
            sample.Context = valid.Context;
            sample.Split = valid.Split;
            sample.SourceReview = valid.SourceReview;

            if (labelChanged)
            {
                sample.ReviewStatus = SlmReviewStatuses.Draft;
                sample.ReviewNote = null;
                sample.ReviewedByUserId = null;
                sample.ReviewedAt = null;
            }
        }, ct);

        if (!updated)
            return null;
        return await GetAsync(id, ct);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) =>
        repository.DeleteAsync(id, ct);

    public async Task<ReviewSlmSamplesResultDto> ReviewAsync(
        ReviewSlmSamplesRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var status = request?.Status?.Trim();
        if (string.IsNullOrEmpty(status) || !SlmReviewStatuses.All.Contains(status))
            throw new ArgumentException($"Trạng thái phải là một trong: {string.Join(", ", SlmReviewStatuses.All)}.");

        var ids = request!.Ids?.Distinct().ToList() ?? new List<Guid>();
        if (ids.Count == 0)
            throw new ArgumentException("Cần chọn ít nhất một dòng để duyệt.");
        if (ids.Count > MaxReviewIds)
            throw new ArgumentException($"Mỗi lần chỉ duyệt tối đa {MaxReviewIds} dòng.");

        var note = request.Note?.Trim();
        if (string.IsNullOrEmpty(note))
            note = null;
        if (note is not null && note.Length > MaxNoteLength)
            throw new ArgumentException($"Ghi chú tối đa {MaxNoteLength} ký tự.");

        var updated = await repository.ReviewAsync(ids, status, note, actorUserId, DateTime.UtcNow, ct);
        return new ReviewSlmSamplesResultDto(updated, ids.Count - updated);
    }

    public async Task<SlmImportResultDto> ImportAsync(Stream workbook, Guid actorUserId, CancellationToken ct = default)
    {
        var rows = workbookReader.Read(workbook);

        var existingKeys = new HashSet<string>();
        foreach (var key in await repository.ExistingKeysAsync(ct))
            existingKeys.Add(BuildKey(key.SampleType, key.Question, key.PendingQuestion, key.Profile, key.Context, key.Intents));

        var toInsert = new List<SlmTrainingSample>();
        var errors = new List<SlmImportErrorDto>();
        var duplicates = 0;

        foreach (var row in rows)
        {
            var request = new SaveSlmSampleRequest(
                row.SheetName, row.Question, row.Profile, row.PendingQuestion, row.Intents, row.Context, row.Split, row.SourceReview);

            SaveSlmSampleRequest valid;
            try
            {
                var checkedRow = Validate(request);
                valid = new SaveSlmSampleRequest(
                    checkedRow.SampleType, checkedRow.Question, checkedRow.Profile, checkedRow.PendingQuestion,
                    checkedRow.Intents, checkedRow.Context, checkedRow.Split, checkedRow.SourceReview);
            }
            catch (ArgumentException ex)
            {
                errors.Add(new SlmImportErrorDto(row.SheetName, row.RowNumber, ex.Message));
                continue;
            }

            var key = BuildKey(valid.SampleType!, valid.Question!, valid.PendingQuestion, valid.Profile, valid.Context, valid.Intents!);
            if (!existingKeys.Add(key))
            {
                duplicates++;
                continue;
            }

            toInsert.Add(new SlmTrainingSample
            {
                SampleType = valid.SampleType!,
                Question = valid.Question!,
                Profile = valid.Profile,
                PendingQuestion = valid.PendingQuestion,
                Intents = valid.Intents!,
                Context = valid.Context,
                Split = valid.Split!,
                SourceReview = valid.SourceReview,
                ReviewStatus = SlmReviewStatuses.Draft,
                CreatedByUserId = actorUserId,
            });
        }

        if (toInsert.Count > 0)
            await repository.AddRangeAsync(toInsert, ct);

        return new SlmImportResultDto(rows.Count, toInsert.Count, duplicates, errors);
    }

    public async Task<string> ExportJsonlAsync(string? split, CancellationToken ct = default)
    {
        var cleanSplit = string.IsNullOrWhiteSpace(split) ? null : split.Trim();
        if (cleanSplit is not null && !SlmSampleSplits.All.Contains(cleanSplit))
            throw new ArgumentException($"Tập phải là một trong: {string.Join(", ", SlmSampleSplits.All)}.");

        var samples = await repository.ApprovedAsync(cleanSplit, ct);
        var builder = new StringBuilder();
        foreach (var sample in samples)
        {
            var line = new Dictionary<string, object?>
            {
                ["type"] = sample.SampleType,
                ["split"] = sample.Split,
                ["question"] = sample.Question,
                ["profile"] = ParseJsonOrNull(sample.Profile),
                ["pending_question"] = sample.PendingQuestion,
                ["context"] = ParseJsonOrNull(sample.Context),
                ["intents"] = ParseJsonOrNull(sample.Intents),
            };
            builder.Append(JsonSerializer.Serialize(line, ExportOptions));
            builder.Append('\n');
        }
        return builder.ToString();
    }

    private static readonly JsonSerializerOptions ExportOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static object? ParseJsonOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static SlmSampleFilter NormalizeFilter(SlmSampleFilter? filter)
    {
        var sampleType = Clean(filter?.SampleType);
        var split = Clean(filter?.Split);
        var status = Clean(filter?.ReviewStatus);
        var search = Clean(filter?.Search);

        if (sampleType is not null && !SlmSampleTypes.All.Contains(sampleType))
            throw new ArgumentException($"Loại mẫu phải là một trong: {string.Join(", ", SlmSampleTypes.All)}.");
        if (split is not null && !SlmSampleSplits.All.Contains(split))
            throw new ArgumentException($"Tập phải là một trong: {string.Join(", ", SlmSampleSplits.All)}.");
        if (status is not null && !SlmReviewStatuses.All.Contains(status))
            throw new ArgumentException($"Trạng thái phải là một trong: {string.Join(", ", SlmReviewStatuses.All)}.");

        return new SlmSampleFilter(sampleType, split, status, search);
    }

    private record ValidSample(
        string SampleType, string Question, string? Profile, string? PendingQuestion,
        string Intents, string? Context, string Split, string? SourceReview);

    private static ValidSample Validate(SaveSlmSampleRequest? request)
    {
        if (request is null)
            throw new ArgumentException("Thiếu nội dung dòng dữ liệu.");

        var sampleType = Clean(request.SampleType);
        if (sampleType is null || !SlmSampleTypes.All.Contains(sampleType))
            throw new ArgumentException($"Loại mẫu phải là một trong: {string.Join(", ", SlmSampleTypes.All)}.");

        var question = Clean(request.Question);
        if (question is null)
            throw new ArgumentException("Câu hỏi không được để trống.");

        var split = Clean(request.Split) ?? SlmSampleSplits.Train;
        if (!SlmSampleSplits.All.Contains(split))
            throw new ArgumentException($"Tập phải là một trong: {string.Join(", ", SlmSampleSplits.All)}.");

        var sourceReview = Clean(request.SourceReview);
        if (sourceReview is not null && sourceReview.Length > 16)
            throw new ArgumentException("Nhãn rà soát tối đa 16 ký tự.");

        var intents = Clean(request.Intents);
        if (intents is null)
            throw new ArgumentException("Intents không được để trống.");
        var intentsRoot = ParseJson(intents, "Intents");
        if (intentsRoot.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("Intents phải là một mảng JSON.");

        var profile = Clean(request.Profile);
        if (profile is not null)
            ParseJson(profile, "Profile");
        var context = Clean(request.Context);
        if (context is not null)
            ParseJson(context, "Context");

        return new ValidSample(
            sampleType, question, profile, Clean(request.PendingQuestion), intents, context, split, sourceReview);
    }

    private static JsonElement ParseJson(string json, string fieldName)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException)
        {
            throw new ArgumentException($"{fieldName} không phải JSON hợp lệ.");
        }
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Trim();
    }

    private static string BuildKey(
        string sampleType, string question, string? pendingQuestion, string? profile, string? context, string intents)
    {
        var parts = new[] { sampleType, question, pendingQuestion ?? "", Canonical(profile), Canonical(context), Canonical(intents) };
        return string.Join("\u001f", parts);
    }

    private static string Canonical(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "";
        try
        {
            var node = JsonNode.Parse(json);
            return SortKeys(node)?.ToJsonString() ?? "null";
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static JsonNode? SortKeys(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var sorted = new JsonObject();
            foreach (var pair in obj.OrderBy(p => p.Key, StringComparer.Ordinal).ToList())
                sorted[pair.Key] = SortKeys(pair.Value?.DeepClone());
            return sorted;
        }
        if (node is JsonArray array)
        {
            var copy = new JsonArray();
            foreach (var item in array)
                copy.Add(SortKeys(item?.DeepClone()));
            return copy;
        }
        return node?.DeepClone();
    }

    private static SlmSampleDto ToDto(SlmTrainingSample sample) => new(
        sample.Id, sample.SampleType, sample.Question, sample.Profile, sample.PendingQuestion, sample.Intents,
        sample.Context, sample.Split, sample.SourceReview, sample.ReviewStatus, sample.ReviewNote,
        sample.ReviewedByUserId, sample.ReviewedAt, sample.CreatedAt, sample.UpdatedAt);
}
