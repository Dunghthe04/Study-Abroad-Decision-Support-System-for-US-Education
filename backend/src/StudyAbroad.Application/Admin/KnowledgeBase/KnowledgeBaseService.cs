using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.KnowledgeBase;

public partial class KnowledgeBaseService(
    IKnowledgeBaseRepository repository, IKnowledgeWorkbookReader workbookReader,
    IAdvisorClient advisor, IAuditLogRepository auditLogs, ILogger<KnowledgeBaseService> logger) : IKnowledgeBaseService
{
    public const int MaxPageSize = 100;
    public const int MaxQuestionLength = 500;
    public const int MaxAnswerLength = 8000;
    public const int MaxSourceLength = 2000;
    public const int MaxSourceUrlLength = 4000;
    public const int MaxTopicCodeLength = 64;
    public const int MaxNoteLength = 4000;
    public const int MaxEntryNo = 1_000_000;
    private const string EntityType = "knowledge_document";

    private static readonly string[] Sorts = ["newest", "oldest", "entryNo", "updated"];

    public async Task<PagedResult<KnowledgeEntryDto>> GetPageAsync(
        KnowledgeEntryFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var cleanFilter = NormalizeFilter(filter);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (rows, total) = await repository.PageAsync(cleanFilter, (page - 1) * pageSize, pageSize, ct);
        return new PagedResult<KnowledgeEntryDto>(rows.Select(d => ToDto(d)).ToList(), page, pageSize, total);
    }

    public async Task<KnowledgeEntryDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        await repository.FindAsync(id, includeDeleted: true, ct) is { } document ? ToDto(document) : null;

    public async Task<IReadOnlyList<KnowledgeEntryVersionDto>?> GetVersionsAsync(Guid id, CancellationToken ct = default)
    {
        if (await repository.FindAsync(id, includeDeleted: true, ct) is null)
            return null;
        var rows = await repository.VersionsAsync(id, ct);
        return rows.Select(ToVersionDto).ToList();
    }

    public async Task<KnowledgeEntryDto> CreateAsync(
        SaveKnowledgeEntryRequest request, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        var valid = Validate(request);
        int entryNo;
        if (request.EntryNo is { } given)
        {
            entryNo = CheckEntryNo(given);
        }
        else
        {
            var next = await repository.MaxEntryNoAsync(ct) + 1;
            if (next > MaxEntryNo)
                throw new ArgumentException("Mã số tự cấp vượt quá 1.000.000. Vui lòng tự nhập id hợp lệ (từ 1 đến 1.000.000).");
            entryNo = next;
        }

        if (await repository.FindIdByEntryNoAsync(entryNo, ct) is { } existing)
            throw new KnowledgeDuplicateEntryException(existing);

        var document = new KnowledgeDocument { EntryNo = entryNo, Version = 1 };
        Apply(document, valid);
        var initialVersion = SnapshotOf(document, VersionActions.Create, actorUserId);
        await repository.AddAsync(document, initialVersion, ct);

        await WriteAuditAsync(
            AuditLogConstants.KnowledgeBaseCreate, actorUserId, document.Id.ToString(),
            new { entryNo = document.EntryNo, docType = document.DocType, label = document.Label, version = document.Version },
            ipAddress, userAgent, ct);

        var (_, warning) = await StartEmbeddingAsync(ct);
        return ToDto(document, warning);
    }

    public async Task<KnowledgeEntryDto?> UpdateAsync(
        Guid id, SaveKnowledgeEntryRequest request, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        var valid = Validate(request);
        var questionChanged = false;
        var changed = false;
        var entryNo = 0;
        var newVersion = 0;

        var found = await repository.UpdateAsync(id, document =>
        {
            if (document.DeletedAt is not null)
                throw new KnowledgeEntryDeletedException();

            if (ValidDocument.From(document) == valid)
                return (false, null);

            questionChanged = document.Question != valid.Question;
            Apply(document, valid);
            document.Version++;
            document.UpdatedAt = DateTime.UtcNow;
            changed = true;
            entryNo = document.EntryNo;
            newVersion = document.Version;
            return (true, SnapshotOf(document, VersionActions.Edit, actorUserId));
        }, includeDeleted: true, ct);

        if (!found)
            return null;

        string? warning = null;
        if (changed)
        {
            await WriteAuditAsync(
                AuditLogConstants.KnowledgeBaseUpdate, actorUserId, id.ToString(),
                new { entryNo, version = newVersion, questionChanged },
                ipAddress, userAgent, ct);

            if (questionChanged)
            {
                await repository.ClearEmbeddingsAsync([id], ct);
                (_, warning) = await StartEmbeddingAsync(ct);
            }
        }

        var latest = await repository.FindAsync(id, includeDeleted: true, ct);
        return latest is null ? null : ToDto(latest, warning);
    }

    public async Task<bool> DeleteAsync(
        Guid id, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        var entryNo = 0;
        var found = await repository.UpdateAsync(id, document =>
        {
            if (document.DeletedAt is not null)
                return (false, null);
            document.DeletedAt = DateTime.UtcNow;
            document.DeletedByUserId = actorUserId;
            document.EmbeddingHash = null;
            document.IngestedAt = null;
            document.UpdatedAt = DateTime.UtcNow;
            entryNo = document.EntryNo;
            return (true, null);
        }, includeDeleted: false, ct);

        if (!found)
            return false;

        await repository.ClearEmbeddingsAsync([id], ct);
        await WriteAuditAsync(
            AuditLogConstants.KnowledgeBaseDelete, actorUserId, id.ToString(),
            new { entryNo, action = "soft_delete" },
            ipAddress, userAgent, ct);
        return true;
    }

    public async Task<KnowledgeEntryDto?> RestoreAsync(
        Guid id, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        var restored = false;
        var entryNo = 0;
        var version = 0;
        var found = await repository.UpdateAsync(id, document =>
        {
            entryNo = document.EntryNo;
            version = document.Version;
            if (document.DeletedAt is null)
                return (false, null);

            document.DeletedAt = null;
            document.DeletedByUserId = null;
            document.EmbeddingHash = null;
            document.IngestedAt = null;
            document.UpdatedAt = DateTime.UtcNow;
            restored = true;
            return (true, null);
        }, includeDeleted: true, ct);

        if (!found)
            return null;

        string? warning = null;
        if (restored)
        {
            await WriteAuditAsync(
                AuditLogConstants.KnowledgeBaseRestore, actorUserId, id.ToString(),
                new { entryNo, version },
                ipAddress, userAgent, ct);
            (_, warning) = await StartEmbeddingAsync(ct);
        }

        var latest = await repository.FindAsync(id, includeDeleted: true, ct);
        return latest is null ? null : ToDto(latest, warning);
    }

    public async Task<KnowledgeEntryDto?> RestoreVersionAsync(
        Guid id, int version, Guid actorUserId,
        string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        var existing = await repository.FindAsync(id, includeDeleted: true, ct);
        if (existing is null)
            return null;
        if (existing.DeletedAt is not null)
            throw new KnowledgeEntryDeletedException("Mục tri thức đã bị xóa. Hãy khôi phục mục trước khi khôi phục phiên bản cũ.");

        var target = await repository.FindVersionAsync(id, version, ct);
        if (target is null)
            return null;

        var targetValid = new ValidDocument(
            target.DocType, target.Label, target.Question, target.Answer,
            target.Source, target.SourceUrl, target.RetrievedAt, target.TopicCode, target.Note);

        var questionChanged = false;
        var entryNo = 0;
        var nextVersion = 0;

        var found = await repository.UpdateAsync(id, document =>
        {
            if (document.DeletedAt is not null)
                throw new KnowledgeEntryDeletedException("Mục tri thức đã bị xóa. Hãy khôi phục mục trước khi khôi phục phiên bản cũ.");

            questionChanged = document.Question != targetValid.Question;
            Apply(document, targetValid);
            document.Version++;
            document.UpdatedAt = DateTime.UtcNow;
            entryNo = document.EntryNo;
            nextVersion = document.Version;
            return (true, SnapshotOf(document, VersionActions.Restore, actorUserId, restoredFromVersion: version));
        }, includeDeleted: true, ct);

        if (!found)
            return null;

        await WriteAuditAsync(
            AuditLogConstants.KnowledgeBaseRestoreVersion, actorUserId, id.ToString(),
            new { entryNo, fromVersion = version, toVersion = nextVersion, questionChanged },
            ipAddress, userAgent, ct);

        string? warning = null;
        if (questionChanged)
        {
            await repository.ClearEmbeddingsAsync([id], ct);
            (_, warning) = await StartEmbeddingAsync(ct);
        }

        var latest = await repository.FindAsync(id, includeDeleted: true, ct);
        return latest is null ? null : ToDto(latest, warning);
    }

    public async Task<KnowledgeEmbedStatusDto> GetEmbedStatusAsync(CancellationToken ct = default) =>
        await AskAdvisorAsync(advisor.GetKnowledgeEmbedStatusAsync, ct);

    public async Task<KnowledgeEmbedStatusDto> StartEmbedAsync(CancellationToken ct = default) =>
        await AskAdvisorAsync(advisor.EmbedKnowledgeAsync, ct);

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

    private static string? Clip(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];

    private async Task<KnowledgeEmbedStatusDto> AskAdvisorAsync(Func<CancellationToken, Task<KnowledgeEmbedDto>> ask, CancellationToken ct)
    {
        try
        {
            var r = await ask(ct);
            return new KnowledgeEmbedStatusDto(true, r.Pending, r.Running, r.Done, r.Error, r.Started);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không hỏi được advisor về tiến độ tạo vector");
            return KnowledgeEmbedStatusDto.Unavailable;
        }
    }

    public const string EmbedWarning =
        "Đã lưu nhưng chưa tạo được vector (advisor chưa chạy hoặc lỗi). Chat chưa thấy các dòng mới cho tới khi vector được tạo.";

    /// <summary>
    /// Nhờ advisor tạo vector cho dòng thiếu vector, chạy nền. Gọi SAU khi đã lưu: advisor tắt hay lỗi thì dữ liệu vẫn nằm trong kho,
    /// chỉ trả cảnh báo. Người dùng hủy yêu cầu thì ném tiếp.
    /// </summary>
    private async Task<(bool Started, string? Warning)> StartEmbeddingAsync(CancellationToken ct)
    {
        try
        {
            var result = await advisor.EmbedKnowledgeAsync(ct);
            return (result.Started, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không nhờ được advisor tạo vector cho kho tri thức");
            return (false, EmbedWarning);
        }
    }

    private record ValidDocument(
        string DocType, string Label, string Question, string Answer, string? Source, string? SourceUrl,
        DateOnly RetrievedAt, string? TopicCode, string? Note)
    {
        public static ValidDocument From(KnowledgeDocument document) => new(
            document.DocType, document.Label, document.Question, document.Answer, document.Source, document.SourceUrl,
            document.RetrievedAt, document.TopicCode, document.Note);
    }

    private static ValidDocument Validate(SaveKnowledgeEntryRequest? request)
    {
        if (request is null)
            throw new ArgumentException("Thiếu nội dung tri thức.");

        var docType = Clean(request.DocType)?.ToLowerInvariant()
            ?? throw new ArgumentException("Cần chọn loại nội dung (doc_type).");
        if (!KnowledgeDocTypes.All.Contains(docType))
            throw new ArgumentException($"doc_type phải là một trong: {string.Join(", ", KnowledgeDocTypes.All)}.");

        var label = Clean(request.Label)?.ToUpperInvariant()
            ?? throw new ArgumentException("Cần chọn nhãn (label).");
        if (!KnowledgeLabels.All.Contains(label))
            throw new ArgumentException($"Nhãn \"{label}\" không có trong danh sách nhãn của kho tri thức.");

        var question = CleanLine(request.Question)
            ?? throw new ArgumentException("Câu hỏi (question) không được để trống.");
        if (question.Length > MaxQuestionLength)
            throw new ArgumentException($"Câu hỏi tối đa {MaxQuestionLength} ký tự.");

        var answer = CleanText(request.Answer)
            ?? throw new ArgumentException("Câu trả lời (answer) không được để trống.");
        if (answer.Length > MaxAnswerLength)
            throw new ArgumentException($"Câu trả lời tối đa {MaxAnswerLength} ký tự.");

        var source = CleanText(request.Source);
        var sourceUrl = CleanText(request.SourceUrl);
        if (source is null && sourceUrl is null)
            throw new ArgumentException("Cần ít nhất một trong hai: source (tên nguồn) hoặc source_url (link).");
        if (source is { Length: > MaxSourceLength })
            throw new ArgumentException($"source tối đa {MaxSourceLength} ký tự.");
        if (sourceUrl is { Length: > MaxSourceUrlLength })
            throw new ArgumentException($"source_url tối đa {MaxSourceUrlLength} ký tự.");
        if (sourceUrl is not null)
        {
            var urls = sourceUrl.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (urls.Length == 0 || urls.Any(part => !AdminEditHelpers.IsHttpUrl(part)))
                throw new ArgumentException("Mỗi link trong source_url phải bắt đầu bằng http:// hoặc https://.");
        }

        var retrievedAt = request.RetrievedAt
            ?? throw new ArgumentException("Cần nhập ngày thu thập (retrieved_at).");

        var topicCode = Clean(request.TopicCode);
        if (topicCode is { Length: > MaxTopicCodeLength })
            throw new ArgumentException($"topic_code tối đa {MaxTopicCodeLength} ký tự.");
        if (topicCode is null && KnowledgeDocTypes.IsProcedure(docType))
            throw new ArgumentException("Dòng thủ tục (proc_*) bắt buộc có topic_code.");

        var note = CleanText(request.Note);
        if (note is { Length: > MaxNoteLength })
            throw new ArgumentException($"note tối đa {MaxNoteLength} ký tự.");

        return new ValidDocument(docType, label, question, answer, source, sourceUrl, retrievedAt, topicCode, note);
    }

    private static int CheckEntryNo(int entryNo) =>
        entryNo is >= 1 and <= MaxEntryNo
            ? entryNo
            : throw new ArgumentException($"Mã số (id) phải là số nguyên từ 1 đến {MaxEntryNo}.");

    private static void Apply(KnowledgeDocument document, ValidDocument valid)
    {
        if (document.Question != valid.Question)
        {
            document.EmbeddingHash = null;
            document.IngestedAt = null;
        }
        document.DocType = valid.DocType;
        document.Label = valid.Label;
        document.Question = valid.Question;
        document.Answer = valid.Answer;
        document.Source = valid.Source;
        document.SourceUrl = valid.SourceUrl;
        document.RetrievedAt = valid.RetrievedAt;
        document.TopicCode = valid.TopicCode;
        document.Note = valid.Note;
    }

    private static KnowledgeDocumentVersion SnapshotOf(
        KnowledgeDocument document, string action, Guid? actorUserId, int? restoredFromVersion = null) => new()
    {
        DocumentId = document.Id,
        Version = document.Version,
        ActorUserId = actorUserId,
        Action = action,
        RestoredFromVersion = restoredFromVersion,
        DocType = document.DocType,
        Label = document.Label,
        Question = document.Question,
        Answer = document.Answer,
        Source = document.Source,
        SourceUrl = document.SourceUrl,
        RetrievedAt = document.RetrievedAt,
        TopicCode = document.TopicCode,
        Note = document.Note,
    };

    private static KnowledgeEntryFilter NormalizeFilter(KnowledgeEntryFilter? filter)
    {
        var docType = Clean(filter?.DocType)?.ToLowerInvariant();
        var label = Clean(filter?.Label)?.ToUpperInvariant();
        var sort = Clean(filter?.Sort);

        if (docType is not null && !KnowledgeDocTypes.All.Contains(docType))
            throw new ArgumentException($"doc_type phải là một trong: {string.Join(", ", KnowledgeDocTypes.All)}.");
        if (label is not null && !KnowledgeLabels.All.Contains(label))
            throw new ArgumentException("Nhãn không có trong danh sách nhãn của kho tri thức.");
        if (sort is not null && !Sorts.Contains(sort))
            throw new ArgumentException($"Sắp xếp phải là một trong: {string.Join(", ", Sorts)}.");

        return new KnowledgeEntryFilter(Clean(filter?.Search), docType, label, Clean(filter?.TopicCode), sort, filter?.Deleted ?? false);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? CleanLine(string? value) =>
        Clean(value) is { } text ? Regex.Replace(text.Normalize(NormalizationForm.FormC), @"\s+", " ") : null;

    private static string? CleanText(string? value) =>
        Clean(value)?.Normalize(NormalizationForm.FormC).Replace("\r\n", "\n").Replace('\r', '\n');

    private static KnowledgeEntryDto ToDto(KnowledgeDocument document, string? vectorWarning = null) => new(
        document.Id, document.EntryNo, document.DocType, document.Label, document.Question, document.Answer,
        document.Source, document.SourceUrl, document.RetrievedAt, document.TopicCode,
        document.Note, document.CreatedAt, document.UpdatedAt, document.Version, document.DeletedAt, vectorWarning);

    private static KnowledgeEntryVersionDto ToVersionDto(KnowledgeEntryVersionRow row) => new(
        row.Version.Id, row.Version.Version, row.Version.Action, row.Version.RestoredFromVersion,
        row.Version.ActorUserId, row.ActorName, row.Version.CreatedAt,
        row.Version.DocType, row.Version.Label, row.Version.Question, row.Version.Answer,
        row.Version.Source, row.Version.SourceUrl, row.Version.RetrievedAt, row.Version.TopicCode, row.Version.Note);
}
