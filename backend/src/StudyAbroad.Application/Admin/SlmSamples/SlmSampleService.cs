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
    public const int MaxPreviewRows = 100;
    public const int MaxSourceLength = 256;
    public const string ExportAction = AuditLogConstants.SlmSampleExport;
    private const string EntityType = "slm_training_sample";
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

        var byType = new Dictionary<string, int>();
        foreach (var type in SlmSampleTypes.All)
            byType[type] = 0;

        var total = 0;
        var deleted = 0;
        foreach (var row in counts)
        {
            if (row.Deleted)
            {
                deleted += row.Count;
            }
            else
            {
                total += row.Count;
                byType[row.SampleType] = byType.GetValueOrDefault(row.SampleType) + row.Count;
            }
        }
        return new SlmSampleSummaryDto(total, deleted, byType);
    }

    public async Task<SlmSampleDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        (await GetDetailAsync(id, ct))?.Sample;

    public async Task<SlmSampleDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var sample = await repository.FindAsync(id, includeDeleted: true, ct);
        if (sample is null)
            return null;

        var userIds = new[] { sample.CreatedByUserId }.OfType<Guid>().Distinct().ToList();
        var names = userIds.Count == 0 ? new Dictionary<Guid, string>() : await repository.UserNamesAsync(userIds, ct);
        var versions = await repository.VersionsAsync(id, ct);

        return new SlmSampleDetailDto(
            ToDto(sample, NameOf(names, sample.CreatedByUserId)),
            versions.Select(ToVersionDto).ToList());
    }

    public Task<IReadOnlyList<SlmSampleCreatorDto>> GetCreatorsAsync(CancellationToken ct = default) =>
        repository.CreatorsAsync(ct);

    public async Task<IReadOnlyList<string>> GetSourcesAsync(CancellationToken ct = default)
    {
        var fromDb = await repository.SourcesAsync(ct);
        var result = new List<string>(SlmSampleSources.All);
        foreach (var source in fromDb)
        {
            if (!string.IsNullOrWhiteSpace(source) && !result.Contains(source, StringComparer.Ordinal))
                result.Add(source);
        }
        return result;
    }

    public async Task<SlmSampleDto> CreateAsync(
        SaveSlmSampleRequest request, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        var valid = Validate(request);
        CheckPersonalData(valid);
        CheckSourceNotReserved(valid.Source, null);
        await CheckDuplicateAsync(valid, null, ct);

        var sample = NewSample(valid, actorUserId);
        sample.Source = valid.Source ?? SlmSampleSources.Manual;
        var initialVersion = SnapshotOf(sample, VersionActions.Create, actorUserId);
        await repository.AddAsync(sample, initialVersion, ct);

        await WriteAuditAsync(
            AuditLogConstants.SlmSampleCreate, actorUserId, sample.Id.ToString(),
            new { sampleType = sample.SampleType, split = sample.Split, version = sample.Version },
            ipAddress, userAgent, ct);

        return ToDto(sample, null);
    }

    public async Task<SlmSampleDto?> UpdateAsync(
        Guid id, SaveSlmSampleRequest request, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        var valid = Validate(request);
        CheckPersonalData(valid);
        await CheckDuplicateAsync(valid, id, ct);

        List<string> changed = [];
        int newVersion = 0;

        var found = await repository.UpdateAsync(id, sample =>
        {
            if (sample.DeletedAt is not null)
                throw new SlmSampleDeletedException();

            CheckSourceNotReserved(valid.Source, sample.Source);
            changed = ChangedFields(sample, valid);
            if (changed.Count == 0)
                return (false, null);

            Apply(sample, valid);
            sample.Version++;
            newVersion = sample.Version;
            return (true, SnapshotOf(sample, VersionActions.Edit, actorUserId));
        }, includeDeleted: true, ct);

        if (!found)
            return null;

        if (changed.Count > 0)
        {
            await WriteAuditAsync(
                AuditLogConstants.SlmSampleUpdate, actorUserId, id.ToString(),
                new { version = newVersion, changedFields = changed },
                ipAddress, userAgent, ct);
        }

        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(
        Guid id, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        var found = await repository.UpdateAsync(id, sample =>
        {
            sample.DeletedAt = DateTime.UtcNow;
            sample.DeletedByUserId = actorUserId;
            return (true, null);
        }, includeDeleted: false, ct);

        if (!found)
            return false;

        await WriteAuditAsync(
            AuditLogConstants.SlmSampleDelete, actorUserId, id.ToString(),
            new { action = "soft_delete" },
            ipAddress, userAgent, ct);
        return true;
    }

    public async Task<SlmSampleDto?> RestoreAsync(
        Guid id, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        var existing = await repository.FindAsync(id, includeDeleted: true, ct);
        if (existing is null)
            return null;
        if (existing.DeletedAt is null)
            return await GetAsync(id, ct);

        var candidate = ValidateForRestore(
            existing.SampleType, existing.Question, existing.Profile, existing.PendingQuestion,
            existing.Intents, existing.Context, existing.Split, existing.SourceReview, existing.Source);
        await CheckDuplicateAsync(candidate, id, ct);

        var found = await repository.UpdateAsync(id, sample =>
        {
            if (sample.DeletedAt is null)
                return (false, null);
            sample.DeletedAt = null;
            sample.DeletedByUserId = null;
            return (true, null);
        }, includeDeleted: true, ct);

        if (!found)
            return null;

        await WriteAuditAsync(
            AuditLogConstants.SlmSampleRestore, actorUserId, id.ToString(),
            new { version = existing.Version },
            ipAddress, userAgent, ct);

        return await GetAsync(id, ct);
    }

    public async Task<SlmSampleDto?> RestoreVersionAsync(
        Guid id, int version, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        var existing = await repository.FindAsync(id, includeDeleted: true, ct);
        if (existing is null)
            return null;
        if (existing.DeletedAt is not null)
            throw new SlmSampleDeletedException();

        var target = await repository.FindVersionAsync(id, version, ct);
        if (target is null)
            return null;

        var candidate = ValidateForRestore(
            target.SampleType, target.Question, target.Profile, target.PendingQuestion,
            target.Intents, target.Context, target.Split, target.SourceReview, target.Source);
        await CheckDuplicateAsync(candidate, id, ct);

        var nextVersion = 0;
        var found = await repository.UpdateAsync(id, sample =>
        {
            if (sample.DeletedAt is not null)
                throw new SlmSampleDeletedException();

            sample.SampleType = target.SampleType;
            sample.Question = target.Question;
            sample.Profile = target.Profile;
            sample.PendingQuestion = target.PendingQuestion;
            sample.Intents = target.Intents;
            sample.Context = target.Context;
            sample.Split = target.Split;
            sample.SourceReview = target.SourceReview;
            sample.Source = target.Source;
            sample.Version++;
            nextVersion = sample.Version;
            return (true, SnapshotOf(sample, VersionActions.Restore, actorUserId, restoredFromVersion: version));
        }, includeDeleted: true, ct);

        if (!found)
            return null;

        await WriteAuditAsync(
            AuditLogConstants.SlmSampleRestoreVersion, actorUserId, id.ToString(),
            new { fromVersion = version, toVersion = nextVersion },
            ipAddress, userAgent, ct);

        return await GetAsync(id, ct);
    }

    public async Task<SlmImportResultDto> ImportAsync(
        Stream workbook, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        var rows = ReadRows(workbook, ct);

        var existingKeys = new HashSet<string>();
        foreach (var key in await repository.ExistingKeysAsync(ct))
            existingKeys.Add(BuildKey(key.SampleType, key.Question, key.PendingQuestion, key.Profile, key.Context, key.Intents));

        var toInsert = new List<SlmTrainingSample>();
        var versions = new List<SlmSampleVersion>();
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

            var sample = NewSample(valid, actorUserId);
            sample.Source = SlmSampleSources.Excel;
            toInsert.Add(sample);
            versions.Add(SnapshotOf(sample, VersionActions.Import, actorUserId));
        }

        if (toInsert.Count > 0)
            await repository.AddRangeAsync(toInsert, versions, ct);

        await WriteAuditAsync(
            AuditLogConstants.SlmSampleImport, actorUserId, null,
            new { totalRows = rows.Count, inserted = toInsert.Count, duplicates, errors = errors.Count },
            ipAddress, userAgent, ct);

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

    public async Task<SlmExportFile> ExportAsync(
        string? format, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        format = Clean(format)?.ToLowerInvariant();
        if (format is not ("jsonl" or "xlsx"))
            throw new ArgumentException("Định dạng xuất phải là jsonl hoặc xlsx.");

        var rows = await repository.ExportableAsync(ct);

        // UC-96 bước 2 & E1: ghi nhật ký truy cập TRƯỚC khi tạo tệp; không ghi được thì không tạo tệp và không đánh dấu đã xuất
        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = ExportAction,
            EntityType = EntityType,
            DetailJson = JsonSerializer.Serialize(new { format, count = rows.Count }),
            IpAddress = Clip(ipAddress, 64),
            UserAgent = Clip(userAgent, 512),
        }, ct);

        var stamp = DateTime.UtcNow;
        var fileName = $"slm-samples-{stamp:yyyyMMdd-HHmmss}.{format}";
        var file = format == "jsonl"
            ? new SlmExportFile(Encoding.UTF8.GetBytes(ToJsonl(rows)), "application/x-ndjson", fileName)
            : new SlmExportFile(templateWriter.BuildExport(rows), XlsxContentType, fileName);

        if (rows.Count > 0)
            await repository.MarkExportedAsync(rows.Select(r => r.Id).ToList(), stamp, ct);
        return file;
    }

    private Task WriteAuditAsync(
        string action, Guid actorUserId, string? entityId, object detail,
        string? ipAddress, string? userAgent, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityType = EntityType,
            EntityId = entityId,
            DetailJson = JsonSerializer.Serialize(detail),
            IpAddress = Clip(ipAddress, 64),
            UserAgent = Clip(userAgent, 512),
        }, ct);

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
                ["source"] = s.Source,
                ["input"] = input,
                ["output"] = new JsonObject
                {
                    ["intents"] = ParseNode(s.Intents) ?? new JsonArray(),
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

    // Khôi phục đưa nội dung cũ trở lại đang dùng: kiểm tra như khi sửa (intents không rỗng, không có dữ liệu cá nhân)
    private static ValidSample ValidateForRestore(
        string sampleType, string question, string? profile, string? pendingQuestion,
        string intents, string? context, string split, string? sourceReview, string? source)
    {
        var valid = Validate(new SaveSlmSampleRequest(sampleType, question, profile, pendingQuestion, intents, context, split, sourceReview, source));
        CheckPersonalData(valid);
        return valid;
    }

    private async Task CheckDuplicateAsync(ValidSample valid, Guid? selfId, CancellationToken ct)
    {
        var key = BuildKey(valid.SampleType, valid.Question, valid.PendingQuestion, valid.Profile, valid.Context, valid.Intents);
        foreach (var other in await repository.LiveKeysByTypeAsync(valid.SampleType, ct))
        {
            if (other.Id == selfId)
                continue;
            var otherKey = BuildKey(other.SampleType, other.Question, other.PendingQuestion, other.Profile, other.Context, other.Intents);
            if (otherKey == key)
                throw new SlmDuplicateSampleException(other.Id);
        }
    }

    private static void CheckSourceNotReserved(string? source, string? current)
    {
        if (source is not null
            && !string.Equals(source, current, StringComparison.Ordinal)
            && SlmSampleSources.All.Contains(source, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Nguồn \"{source}\" là giá trị của hệ thống, hãy nhập nguồn tham khảo khác.");
    }

    private static void CheckPersonalData(ValidSample valid)
    {
        if (SlmPersonalDataCheck.HasContact(valid.Question)
            || SlmPersonalDataCheck.HasContact(valid.PendingQuestion)
            || SlmPersonalDataCheck.HasContact(valid.Source))
            throw new ArgumentException(PersonalDataMessage);
    }

    public static bool IsReady(string? intents)
    {
        try
        {
            return JsonNode.Parse(intents ?? "[]") is JsonArray { Count: > 0 };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void Apply(SlmTrainingSample sample, ValidSample valid)
    {
        sample.SampleType = valid.SampleType;
        sample.Question = valid.Question;
        sample.Source = valid.Source ?? sample.Source;
        sample.Profile = valid.Profile;
        sample.PendingQuestion = valid.PendingQuestion;
        sample.Intents = valid.Intents;
        sample.Context = valid.Context;
        sample.Split = valid.Split;
        sample.SourceReview = valid.SourceReview;
    }

    private static SlmSampleVersion SnapshotOf(
        SlmTrainingSample sample, string action, Guid? actorUserId, int? restoredFromVersion = null) => new()
    {
        SampleId = sample.Id,
        Version = sample.Version,
        ActorUserId = actorUserId,
        Action = action,
        RestoredFromVersion = restoredFromVersion,
        SampleType = sample.SampleType,
        Question = sample.Question,
        Profile = sample.Profile,
        PendingQuestion = sample.PendingQuestion,
        Intents = sample.Intents,
        Context = sample.Context,
        Split = sample.Split,
        SourceReview = sample.SourceReview,
        Source = sample.Source,
    };

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
        Check("source", valid.Source is not null && sample.Source != valid.Source);
        Check("profile", Canonical(sample.Profile) != Canonical(valid.Profile));
        Check("pendingQuestion", (sample.PendingQuestion ?? "") != (valid.PendingQuestion ?? ""));
        Check("intents", Canonical(sample.Intents) != Canonical(valid.Intents));
        Check("context", Canonical(sample.Context) != Canonical(valid.Context));
        Check("split", sample.Split != valid.Split);
        Check("sourceReview", (sample.SourceReview ?? "") != (valid.SourceReview ?? ""));
        return changed;
    }

    private static SlmTrainingSample NewSample(ValidSample valid, Guid? actorUserId) => new()
    {
        SampleType = valid.SampleType,
        Question = valid.Question,
        Profile = valid.Profile,
        PendingQuestion = valid.PendingQuestion,
        Intents = valid.Intents,
        Context = valid.Context,
        Split = valid.Split,
        SourceReview = valid.SourceReview,
        Version = 1,
        CreatedByUserId = actorUserId,
    };

    private static SlmSampleFilter NormalizeFilter(SlmSampleFilter? filter)
    {
        var sampleType = Clean(filter?.SampleType);
        var split = Clean(filter?.Split);
        var search = Clean(filter?.Search);
        var source = Clean(filter?.Source);

        if (sampleType is not null && !SlmSampleTypes.All.Contains(sampleType))
            throw new ArgumentException($"Loại mẫu phải là một trong: {string.Join(", ", SlmSampleTypes.All)}.");
        if (split is not null && !SlmSampleSplits.All.Contains(split))
            throw new ArgumentException($"Tập phải là một trong: {string.Join(", ", SlmSampleSplits.All)}.");

        return new SlmSampleFilter(
            sampleType, split, search, filter?.CreatedByUserId, AsUtc(filter?.From), AsUtc(filter?.To), source,
            filter?.Deleted ?? false);
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
        string? Source);

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

        var intents = Clean(request.Intents)
            ?? throw new ArgumentException("Danh sách ý định (intents) không được để trống.");
        var intentsRoot = ParseJson(intents, "Intents");
        if (intentsRoot.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("Intents phải là một mảng JSON.");
        if (intentsRoot.GetArrayLength() == 0)
            throw new ArgumentException("Danh sách ý định (intents) không được để trống.");

        var profile = Clean(request.Profile);
        if (profile is not null)
            ParseJson(profile, "Profile");
        var context = Clean(request.Context);
        if (context is not null)
            ParseJson(context, "Context");

        var source = Clean(request.Source);
        if (source is not null && source.Length > MaxSourceLength)
            throw new ArgumentException($"Nguồn tối đa {MaxSourceLength} ký tự.");

        return new ValidSample(
            sampleType, question, profile, Clean(request.PendingQuestion), intents, context, split, sourceReview,
            source);
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

    /// <summary>Chuẩn hóa câu hỏi để so trùng theo UC-92: không phân biệt chữ hoa, chữ thường và khoảng trắng thừa.</summary>
    public static string NormalizeQuestion(string question) =>
        Regex.Replace(question.Trim(), @"\s+", " ").ToLowerInvariant();

    private static string BuildKey(
        string sampleType, string question, string? pendingQuestion, string? profile, string? context, string intents)
    {
        var parts = new[]
        {
            sampleType,
            NormalizeQuestion(question),
            pendingQuestion ?? "",
            Canonical(profile),
            Canonical(context),
            Canonical(intents),
        };
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
        sample.Context, sample.Split, sample.SourceReview, sample.CreatedAt, sample.UpdatedAt,
        sample.Source, sample.CreatedByUserId, creatorName, sample.ExportedAt, sample.Version, sample.DeletedAt);

    private static SlmSampleVersionDto ToVersionDto(SlmSampleVersionRow row) => new(
        row.Version.Id, row.Version.Version, row.Version.Action, row.Version.RestoredFromVersion,
        row.Version.ActorUserId, row.ActorName, row.Version.CreatedAt,
        row.Version.SampleType, row.Version.Question, row.Version.Profile, row.Version.PendingQuestion,
        row.Version.Intents, row.Version.Context, row.Version.Split, row.Version.SourceReview, row.Version.Source);
}
