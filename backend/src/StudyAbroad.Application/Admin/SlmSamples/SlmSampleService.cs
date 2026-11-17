using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.SlmSamples;

public class SlmSampleService(
    ISlmSampleRepository repository, ISlmWorkbookReader workbookReader,
    ISlmTemplateWriter templateWriter, IAuditLogRepository auditLogs) : ISlmSampleService
{
    public const int MaxPageSize = 100;
    public const int MaxReviewIds = 500;
    public const int MaxNoteLength = 1000;
    public const int MaxPreviewRows = 100;
    public const int MaxTopicLength = 128;
    public const int MaxSourceLength = 256;
    public const int MaxCapturedQuestionLength = 500;
    public const string ExportAction = "slm_sample.export";
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PersonalDataMessage = "Nội dung có số điện thoại hoặc e-mail. Hãy bỏ thông tin cá nhân trước khi lưu.";

    private static readonly JsonSerializerOptions ExportJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public async Task<PagedResult<SlmSampleDto>> GetPageAsync(
        SlmSampleFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var cleanFilter = NormalizeFilter(filter);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var skip = (page - 1) * pageSize;
        var (rows, total) = await repository.PageAsync(cleanFilter, skip, pageSize, ct);

        var items = rows.Select(row => ToDto(row.Sample, row.CreatorName)).ToList();
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

    public async Task<SlmSampleDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        (await GetDetailAsync(id, ct))?.Sample;

    public async Task<SlmSampleDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var sample = await repository.FindAsync(id, ct);
        if (sample is null)
            return null;

        var userIds = new[] { sample.CreatedByUserId, sample.ReviewedByUserId }.OfType<Guid>().Distinct().ToList();
        var names = userIds.Count == 0 ? new Dictionary<Guid, string>() : await repository.UserNamesAsync(userIds, ct);
        var history = await repository.HistoryAsync(id, ct);

        return new SlmSampleDetailDto(
            ToDto(sample, NameOf(names, sample.CreatedByUserId)),
            NameOf(names, sample.ReviewedByUserId),
            history.Select(row => new SlmSampleHistoryDto(
                row.History.Id, row.History.Action, row.History.FromStatus, row.History.ToStatus, row.History.Note,
                string.IsNullOrEmpty(row.History.ChangedFields) ? [] : row.History.ChangedFields.Split(','),
                row.History.ActorUserId, row.ActorName, row.History.CreatedAt)).ToList());
    }

    public Task<IReadOnlyList<SlmSampleCreatorDto>> GetCreatorsAsync(CancellationToken ct = default) =>
        repository.CreatorsAsync(ct);

    public async Task<SlmSampleDto> CreateAsync(SaveSlmSampleRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var valid = Validate(request);
        CheckPersonalData(valid);
        await CheckDuplicateAsync(valid, null, ct);

        var status = SlmReviewStatuses.Draft;
        if (request.Submit)
        {
            EnsureReady(valid.Answer, valid.Intents);
            status = SlmReviewStatuses.Pending;
        }

        var sample = NewSample(valid, status, actorUserId);
        sample.Source = valid.Source ?? SlmSampleSources.Manual;
        await repository.AddAsync(sample, History(SlmSampleHistoryActions.Create, actorUserId, null, status, sample.Id), ct);
        return ToDto(sample, null);
    }

    public async Task<SlmSampleDto?> UpdateAsync(
        Guid id, SaveSlmSampleRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var valid = Validate(request);
        CheckPersonalData(valid);
        await CheckDuplicateAsync(valid, id, ct);

        var found = await repository.UpdateManyAsync([id], sample =>
        {
            var changed = ChangedFields(sample, valid);
            var contentChanged = changed.Except(["split", "sourceReview", "source"]).Any();
            var from = sample.ReviewStatus;
            var to = from;
            if (from == SlmReviewStatuses.Approved && contentChanged)
                to = SlmReviewStatuses.Pending;
            else if (request.Submit && from is SlmReviewStatuses.Draft or SlmReviewStatuses.Rejected or SlmReviewStatuses.NeedsFix)
                to = SlmReviewStatuses.Pending;

            if (changed.Count == 0 && to == from)
                return null;
            if (to == SlmReviewStatuses.Pending)
                EnsureReady(valid.Answer, valid.Intents);

            sample.SampleType = valid.SampleType;
            sample.Question = valid.Question;
            sample.Answer = valid.Answer;
            sample.Topic = valid.Topic;
            sample.Source = valid.Source ?? sample.Source;
            sample.Profile = valid.Profile;
            sample.PendingQuestion = valid.PendingQuestion;
            sample.Intents = valid.Intents;
            sample.Context = valid.Context;
            sample.Split = valid.Split;
            sample.SourceReview = valid.SourceReview;

            if (to != from)
            {
                sample.ReviewStatus = to;
                sample.ReviewNote = null;
                sample.ReviewedByUserId = null;
                sample.ReviewedAt = null;
            }

            var action = changed.Count > 0 ? SlmSampleHistoryActions.Edit : SlmSampleHistoryActions.Submit;
            var history = History(action, actorUserId, from, to, sample.Id);
            history.ChangedFields = changed.Count > 0 ? string.Join(",", changed) : null;
            return history;
        }, ct);

        return found == 0 ? null : await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var found = await repository.UpdateManyAsync([id], sample =>
        {
            sample.DeletedAt = DateTime.UtcNow;
            sample.DeletedByUserId = actorUserId;
            return History(SlmSampleHistoryActions.Delete, actorUserId, sample.ReviewStatus, null, sample.Id);
        }, ct);
        return found > 0;
    }

    public async Task<ReviewSlmSamplesResultDto> ReviewAsync(
        ReviewSlmSamplesRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var status = request?.Status?.Trim();
        var allowed = new[] { SlmReviewStatuses.Approved, SlmReviewStatuses.Rejected, SlmReviewStatuses.NeedsFix };
        if (string.IsNullOrEmpty(status) || !allowed.Contains(status))
            throw new ArgumentException($"Trạng thái duyệt phải là một trong: {string.Join(", ", allowed)}.");

        var ids = request!.Ids?.Distinct().ToList() ?? new List<Guid>();
        if (ids.Count == 0)
            throw new ArgumentException("Cần chọn ít nhất một dòng để duyệt.");
        if (ids.Count > MaxReviewIds)
            throw new ArgumentException($"Mỗi lần chỉ duyệt tối đa {MaxReviewIds} dòng.");

        var note = Clean(request.Note);
        if (note is not null && note.Length > MaxNoteLength)
            throw new ArgumentException($"Ghi chú tối đa {MaxNoteLength} ký tự.");
        if (note is null && status != SlmReviewStatuses.Approved)
            throw new ArgumentException("Cần ghi lý do khi từ chối hoặc yêu cầu sửa.");

        var contributed = (await repository.ContributorsAsync(ids, ct))
            .Where(x => x.ActorUserId == actorUserId)
            .Select(x => x.SampleId)
            .ToHashSet();

        int skippedOwn = 0, skippedNotReady = 0, updated = 0;
        var now = DateTime.UtcNow;
        var found = await repository.UpdateManyAsync(ids, sample =>
        {
            if (sample.CreatedByUserId == actorUserId || contributed.Contains(sample.Id))
            {
                skippedOwn++;
                return null;
            }
            if (status == SlmReviewStatuses.Approved && !IsReady(sample.Answer, sample.Intents))
            {
                skippedNotReady++;
                return null;
            }

            var history = History(SlmSampleHistoryActions.Review, actorUserId, sample.ReviewStatus, status, sample.Id);
            history.Note = note;
            sample.ReviewStatus = status;
            sample.ReviewNote = note;
            sample.ReviewedByUserId = actorUserId;
            sample.ReviewedAt = now;
            updated++;
            return history;
        }, ct);

        return new ReviewSlmSamplesResultDto(updated, ids.Count - found, skippedOwn, skippedNotReady);
    }

    public async Task<SlmImportResultDto> ImportAsync(Stream workbook, Guid actorUserId, CancellationToken ct = default)
    {
        var rows = ReadRows(workbook, ct);

        var existingKeys = new HashSet<string>();
        foreach (var key in await repository.ExistingKeysAsync(ct))
            existingKeys.Add(BuildKey(key.SampleType, key.Question, key.PendingQuestion, key.Profile, key.Context, key.Intents));

        var toInsert = new List<SlmTrainingSample>();
        var errors = new List<SlmImportErrorDto>();
        var duplicates = 0;

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();

            ValidSample valid;
            try
            {
                valid = ValidateRow(row);
            }
            catch (ArgumentException ex)
            {
                errors.Add(new SlmImportErrorDto(row.SheetName, row.RowNumber, ex.Message));
                continue;
            }

            var key = BuildKey(valid.SampleType, valid.Question, valid.PendingQuestion, valid.Profile, valid.Context, valid.Intents);
            if (!existingKeys.Add(key))
            {
                duplicates++;
                continue;
            }

            var sample = NewSample(valid, SlmReviewStatuses.Draft, actorUserId);
            sample.Source = SlmSampleSources.Excel;
            toInsert.Add(sample);
        }

        if (toInsert.Count > 0)
        {
            var history = toInsert
                .Select(s => History(SlmSampleHistoryActions.Create, actorUserId, null, s.ReviewStatus, s.Id))
                .ToList();
            await repository.AddRangeAsync(toInsert, history, ct);
        }

        return new SlmImportResultDto(rows.Count, toInsert.Count, duplicates, errors);
    }

    public Task<SlmImportPreviewDto> PreviewAsync(Stream workbook, CancellationToken ct = default)
    {
        var rows = ReadRows(workbook, ct);

        var errors = new List<SlmImportErrorDto>();
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                ValidateRow(row);
            }
            catch (ArgumentException ex)
            {
                errors.Add(new SlmImportErrorDto(row.SheetName, row.RowNumber, ex.Message));
            }
        }

        var firstRows = rows.Take(MaxPreviewRows).ToList();
        return Task.FromResult(new SlmImportPreviewDto(rows.Count, firstRows, errors));
    }

    public async Task<bool> CaptureUnansweredAsync(string? question, CancellationToken ct = default)
    {
        var text = Clean(question) is { } q ? Regex.Replace(q, @"\s+", " ") : null;
        if (text is null || text.Length > MaxCapturedQuestionLength || SlmPersonalDataCheck.HasContactOrLink(text))
            return false;
        // ponytail: check-then-insert, two identical questions at the same instant can both land; add a unique index if that shows up
        if ((await repository.SameQuestionAsync(text, includeDeleted: true, ct)).Count > 0)
            return false;

        var sample = new SlmTrainingSample
        {
            SampleType = SlmSampleTypes.Knowledge,
            Question = text,
            Intents = "[]",
            Source = SlmSampleSources.ChatUnanswered,
            ReviewStatus = SlmReviewStatuses.Draft,
        };
        await repository.AddAsync(
            sample, History(SlmSampleHistoryActions.Capture, null, null, SlmReviewStatuses.Draft, sample.Id), ct);
        return true;
    }

    public async Task<SlmExportFile> ExportAsync(
        string? format, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        format = Clean(format)?.ToLowerInvariant();
        if (format is not ("jsonl" or "xlsx"))
            throw new ArgumentException("Định dạng xuất phải là jsonl hoặc xlsx.");

        var rows = await repository.ApprovedAsync(ct);
        var stamp = DateTime.UtcNow;
        var fileName = $"slm-samples-approved-{stamp:yyyyMMdd-HHmmss}.{format}";
        var file = format == "jsonl"
            ? new SlmExportFile(Encoding.UTF8.GetBytes(ToJsonl(rows)), "application/x-ndjson", fileName)
            : new SlmExportFile(templateWriter.BuildExport(rows), XlsxContentType, fileName);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = ExportAction,
            EntityType = "slm_training_sample",
            DetailJson = JsonSerializer.Serialize(new { format, count = rows.Count }),
            IpAddress = Clip(ipAddress, 64),
            UserAgent = Clip(userAgent, 512),
        }, ct);
        if (rows.Count > 0)
            await repository.MarkExportedAsync(rows.Select(r => r.Id).ToList(), stamp, ct);
        return file;
    }

    private static string ToJsonl(IReadOnlyList<SlmTrainingSample> rows)
    {
        var text = new StringBuilder();
        foreach (var s in rows)
        {
            var input = new JsonObject
            {
                ["question"] = s.Question,
                ["profile"] = ParseNode(s.Profile),
                ["pending_question"] = s.PendingQuestion,
            };
            if (ParseNode(s.Context) is { } context)
                input["context"] = context;
            var record = new JsonObject
            {
                ["id"] = s.Id.ToString(),
                ["type"] = s.SampleType,
                ["split"] = s.Split,
                ["topic"] = s.Topic,
                ["source"] = s.Source,
                ["input"] = input,
                ["output"] = new JsonObject
                {
                    ["intents"] = ParseNode(s.Intents) ?? new JsonArray(),
                    ["answer"] = s.Answer,
                },
            };
            text.Append(record.ToJsonString(ExportJson)).Append('\n');
        }
        return text.ToString();
    }

    private static JsonNode? ParseNode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return JsonValue.Create(json);
        }
    }

    private IReadOnlyList<SlmImportRow> ReadRows(Stream workbook, CancellationToken ct)
    {
        var rows = workbookReader.Read(workbook, ct);
        if (rows.Count == 0)
            throw new InvalidDataException("File không có dòng dữ liệu nào bên dưới dòng tiêu đề.");
        return rows;
    }

    private static ValidSample ValidateRow(SlmImportRow row)
    {
        var request = new SaveSlmSampleRequest(
            row.SheetName, row.Question, row.Profile, row.PendingQuestion, row.Intents, row.Context, row.Split, row.SourceReview);
        var valid = Validate(request);
        CheckPersonalData(valid);
        return valid;
    }

    private async Task CheckDuplicateAsync(ValidSample valid, Guid? selfId, CancellationToken ct)
    {
        var key = BuildKey(valid.SampleType, valid.Question.ToLowerInvariant(), valid.PendingQuestion, valid.Profile, valid.Context, valid.Intents);
        foreach (var other in await repository.SameQuestionAsync(valid.Question, includeDeleted: false, ct))
        {
            var otherKey = BuildKey(
                other.SampleType, other.Question.Trim().ToLowerInvariant(), other.PendingQuestion, other.Profile, other.Context, other.Intents);
            if (other.Id != selfId && otherKey == key)
                throw new SlmDuplicateSampleException(other.Id);
        }
    }

    private static void CheckPersonalData(ValidSample valid)
    {
        if (SlmPersonalDataCheck.HasContact(valid.Question)
            || SlmPersonalDataCheck.HasContact(valid.Answer)
            || SlmPersonalDataCheck.HasContact(valid.PendingQuestion))
            throw new ArgumentException(PersonalDataMessage);
    }

    private static void EnsureReady(string? answer, string intents)
    {
        if (!IsReady(answer, intents))
            throw new ArgumentException("Cần nhập câu trả lời (hoặc intents) trước khi gửi duyệt.");
    }

    public static bool IsReady(string? answer, string? intents)
    {
        if (!string.IsNullOrWhiteSpace(answer))
            return true;
        try
        {
            return JsonNode.Parse(intents ?? "[]") is JsonArray { Count: > 0 };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<string> ChangedFields(SlmTrainingSample sample, ValidSample valid)
    {
        var changed = new List<string>();
        void Check(string name, bool differs)
        {
            if (differs)
                changed.Add(name);
        }
        Check("sampleType", sample.SampleType != valid.SampleType);
        Check("question", sample.Question != valid.Question);
        Check("answer", (sample.Answer ?? "") != (valid.Answer ?? ""));
        Check("topic", (sample.Topic ?? "") != (valid.Topic ?? ""));
        Check("source", valid.Source is not null && sample.Source != valid.Source);
        Check("profile", Canonical(sample.Profile) != Canonical(valid.Profile));
        Check("pendingQuestion", (sample.PendingQuestion ?? "") != (valid.PendingQuestion ?? ""));
        Check("intents", Canonical(sample.Intents) != Canonical(valid.Intents));
        Check("context", Canonical(sample.Context) != Canonical(valid.Context));
        Check("split", sample.Split != valid.Split);
        Check("sourceReview", (sample.SourceReview ?? "") != (valid.SourceReview ?? ""));
        return changed;
    }

    private static SlmTrainingSample NewSample(ValidSample valid, string status, Guid? actorUserId) => new()
    {
        SampleType = valid.SampleType,
        Question = valid.Question,
        Answer = valid.Answer,
        Topic = valid.Topic,
        Profile = valid.Profile,
        PendingQuestion = valid.PendingQuestion,
        Intents = valid.Intents,
        Context = valid.Context,
        Split = valid.Split,
        SourceReview = valid.SourceReview,
        ReviewStatus = status,
        CreatedByUserId = actorUserId,
    };

    private static SlmSampleHistory History(string action, Guid? actor, string? from, string? to, Guid sampleId) => new()
    {
        SampleId = sampleId,
        ActorUserId = actor,
        Action = action,
        FromStatus = from,
        ToStatus = to,
    };

    private static SlmSampleFilter NormalizeFilter(SlmSampleFilter? filter)
    {
        var sampleType = Clean(filter?.SampleType);
        var split = Clean(filter?.Split);
        var status = Clean(filter?.ReviewStatus);
        var search = Clean(filter?.Search);
        var source = Clean(filter?.Source);

        if (sampleType is not null && !SlmSampleTypes.All.Contains(sampleType))
            throw new ArgumentException($"Loại mẫu phải là một trong: {string.Join(", ", SlmSampleTypes.All)}.");
        if (split is not null && !SlmSampleSplits.All.Contains(split))
            throw new ArgumentException($"Tập phải là một trong: {string.Join(", ", SlmSampleSplits.All)}.");
        if (status is not null && !SlmReviewStatuses.All.Contains(status))
            throw new ArgumentException($"Trạng thái phải là một trong: {string.Join(", ", SlmReviewStatuses.All)}.");

        return new SlmSampleFilter(
            sampleType, split, status, search, filter?.CreatedByUserId, AsUtc(filter?.From), AsUtc(filter?.To), source);
    }

    private static DateTime? AsUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } utc => utc,
        { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
        { } unspecified => DateTime.SpecifyKind(unspecified, DateTimeKind.Utc),
    };

    private record ValidSample(
        string SampleType, string Question, string? Profile, string? PendingQuestion,
        string Intents, string? Context, string Split, string? SourceReview,
        string? Answer, string? Topic, string? Source);

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

        var intents = Clean(request.Intents) ?? "[]";
        var intentsRoot = ParseJson(intents, "Intents");
        if (intentsRoot.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("Intents phải là một mảng JSON.");

        var profile = Clean(request.Profile);
        if (profile is not null)
            ParseJson(profile, "Profile");
        var context = Clean(request.Context);
        if (context is not null)
            ParseJson(context, "Context");

        var topic = Clean(request.Topic);
        if (topic is not null && topic.Length > MaxTopicLength)
            throw new ArgumentException($"Chủ đề tối đa {MaxTopicLength} ký tự.");
        var source = Clean(request.Source);
        if (source is not null && source.Length > MaxSourceLength)
            throw new ArgumentException($"Nguồn tối đa {MaxSourceLength} ký tự.");

        return new ValidSample(
            sampleType, question, profile, Clean(request.PendingQuestion), intents, context, split, sourceReview,
            Clean(request.Answer), topic, source);
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

    private static string? Clip(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];

    private static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid? id) =>
        id is { } key && names.TryGetValue(key, out var name) ? name : null;

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

    private static SlmSampleDto ToDto(SlmTrainingSample sample, string? creatorName) => new(
        sample.Id, sample.SampleType, sample.Question, sample.Profile, sample.PendingQuestion, sample.Intents,
        sample.Context, sample.Split, sample.SourceReview, sample.ReviewStatus, sample.ReviewNote,
        sample.ReviewedByUserId, sample.ReviewedAt, sample.CreatedAt, sample.UpdatedAt,
        sample.Answer, sample.Topic, sample.Source, sample.CreatedByUserId, creatorName, sample.ExportedAt);
}
