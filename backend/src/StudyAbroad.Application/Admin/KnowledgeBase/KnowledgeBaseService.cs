using System.Text;
using System.Text.RegularExpressions;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.KnowledgeBase;

public partial class KnowledgeBaseService(
    IKnowledgeBaseRepository repository) : IKnowledgeBaseService
{
    public const int MaxPageSize = 100;
    public const int MaxReviewIds = 500;
    public const int MaxNoteLength = 1000;
    public const int MaxQuestionLength = 500;
    public const int MaxAnswerLength = 4000;
    public const int MaxSourceLength = 2000;
    private const string PersonalDataMessage =
        "Câu hỏi hoặc câu trả lời có số điện thoại hoặc e-mail. Hãy bỏ thông tin cá nhân trước khi lưu.";
    private static readonly string[] Sorts = ["newest", "oldest", "entryNo", "updated"];

    public async Task<PagedResult<KnowledgeEntryDto>> GetPageAsync(
        KnowledgeEntryFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var cleanFilter = NormalizeFilter(filter);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (rows, total) = await repository.PageAsync(cleanFilter, (page - 1) * pageSize, pageSize, ct);
        var items = rows.Select(row => ToDto(row.Entry, row.CreatorName)).ToList();
        return new PagedResult<KnowledgeEntryDto>(items, page, pageSize, total);
    }

    public async Task<KnowledgeEntryDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        (await GetDetailAsync(id, ct))?.Entry;

    public async Task<KnowledgeEntryDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await repository.FindAsync(id, includeDeleted: true, ct);
        if (entry is null)
            return null;

        var userIds = new[] { entry.CreatedByUserId, entry.ReviewedByUserId }.OfType<Guid>().Distinct().ToList();
        var names = userIds.Count == 0 ? new Dictionary<Guid, string>() : await repository.UserNamesAsync(userIds, ct);
        var history = await repository.HistoryAsync(id, ct);

        return new KnowledgeEntryDetailDto(
            ToDto(entry, NameOf(names, entry.CreatedByUserId)),
            NameOf(names, entry.ReviewedByUserId),
            history.Select(row => new KnowledgeEntryHistoryDto(
                row.History.Id, row.History.Action, row.History.FromStatus, row.History.ToStatus, row.History.Note,
                string.IsNullOrEmpty(row.History.ChangedFields) ? [] : row.History.ChangedFields.Split(','),
                row.History.ActorUserId, row.ActorName, row.History.CreatedAt)).ToList());
    }

    public Task<IReadOnlyList<KnowledgeCreatorDto>> GetCreatorsAsync(CancellationToken ct = default) =>
        repository.CreatorsAsync(ct);

    public async Task<KnowledgeEntryDto> CreateAsync(SaveKnowledgeEntryRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var valid = Validate(request);
        await CheckDuplicateAsync(valid.Question, null, ct);

        var status = KnowledgeReviewStatuses.Draft;
        if (request.Submit)
        {
            EnsureReady(valid);
            status = KnowledgeReviewStatuses.Pending;
        }

        var entry = NewEntry(valid, status, KnowledgeOrigins.Manual, actorUserId);
        await repository.AddAsync(entry, History(KnowledgeHistoryActions.Create, actorUserId, null, status, entry.Id), ct);
        return ToDto(entry, null);
    }

    public async Task<KnowledgeEntryDto?> UpdateAsync(
        Guid id, SaveKnowledgeEntryRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var valid = Validate(request);
        await CheckDuplicateAsync(valid.Question, id, ct);

        var found = await repository.UpdateManyAsync([id], entry =>
        {
            var changed = ChangedFields(entry, valid);
            var from = entry.ReviewStatus;
            var to = from;
            // Khác SLM: đổi bất kỳ trường nào (kể cả nguồn) của mục đã duyệt cũng phải duyệt lại
            if (from == KnowledgeReviewStatuses.Approved && changed.Count > 0)
                to = KnowledgeReviewStatuses.Pending;
            else if (request.Submit && from is KnowledgeReviewStatuses.Draft or KnowledgeReviewStatuses.Rejected or KnowledgeReviewStatuses.NeedsFix)
                to = KnowledgeReviewStatuses.Pending;

            if (changed.Count == 0 && to == from)
                return null;
            if (to == KnowledgeReviewStatuses.Pending)
                EnsureReady(valid);

            entry.Label = valid.Label;
            entry.Levels = valid.Levels;
            entry.Question = valid.Question;
            entry.Answer = valid.Answer;
            entry.Source = valid.Source;
            if (to != from)
            {
                entry.ReviewStatus = to;
                entry.ReviewNote = null;
                entry.ReviewedByUserId = null;
                entry.ReviewedAt = null;
            }

            var action = changed.Count > 0 ? KnowledgeHistoryActions.Edit : KnowledgeHistoryActions.Submit;
            var history = History(action, actorUserId, from, to, entry.Id);
            history.ChangedFields = changed.Count > 0 ? string.Join(",", changed) : null;
            return history;
        }, ct: ct);

        return found == 0 ? null : await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var found = await repository.UpdateManyAsync([id], entry =>
        {
            entry.DeletedAt = DateTime.UtcNow;
            entry.DeletedByUserId = actorUserId;
            return History(KnowledgeHistoryActions.Delete, actorUserId, entry.ReviewStatus, null, entry.Id);
        }, ct: ct);
        return found > 0;
    }

    public async Task<KnowledgeEntryDto?> RestoreAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var deleted = await repository.FindAsync(id, includeDeleted: true, ct);
        if (deleted?.DeletedAt is null)
            return null;
        // Trong lúc mục này nằm thùng rác, có thể ai đó đã thêm lại đúng câu hỏi đó
        await CheckDuplicateAsync(deleted.Question, id, ct);

        await repository.UpdateManyAsync([id], entry =>
        {
            entry.DeletedAt = null;
            entry.DeletedByUserId = null;
            return History(KnowledgeHistoryActions.Restore, actorUserId, null, entry.ReviewStatus, entry.Id);
        }, includeDeleted: true, ct);
        return await GetAsync(id, ct);
    }

    public async Task<ReviewKnowledgeEntriesResultDto> ReviewAsync(
        ReviewKnowledgeEntriesRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var status = request?.Status?.Trim();
        var allowed = new[] { KnowledgeReviewStatuses.Approved, KnowledgeReviewStatuses.Rejected, KnowledgeReviewStatuses.NeedsFix };
        if (string.IsNullOrEmpty(status) || !allowed.Contains(status))
            throw new ArgumentException($"Trạng thái duyệt phải là một trong: {string.Join(", ", allowed)}.");

        var ids = request!.Ids?.Distinct().ToList() ?? [];
        if (ids.Count == 0)
            throw new ArgumentException("Cần chọn ít nhất một mục để duyệt.");
        if (ids.Count > MaxReviewIds)
            throw new ArgumentException($"Mỗi lần chỉ duyệt tối đa {MaxReviewIds} mục.");

        var note = Clean(request.Note);
        if (note is not null && note.Length > MaxNoteLength)
            throw new ArgumentException($"Ghi chú tối đa {MaxNoteLength} ký tự.");
        if (note is null && status != KnowledgeReviewStatuses.Approved)
            throw new ArgumentException("Cần ghi lý do khi từ chối hoặc yêu cầu sửa.");

        var contributed = (await repository.ContributorsAsync(ids, ct))
            .Where(x => x.ActorUserId == actorUserId)
            .Select(x => x.EntryId)
            .ToHashSet();

        int skippedOwn = 0, skippedNotReady = 0, skippedNotPending = 0, updated = 0;
        var now = DateTime.UtcNow;
        var found = await repository.UpdateManyAsync(ids, entry =>
        {
            if (entry.CreatedByUserId == actorUserId || contributed.Contains(entry.Id))
            {
                skippedOwn++;
                return null;
            }
            if (entry.ReviewStatus != KnowledgeReviewStatuses.Pending)
            {
                skippedNotPending++;
                return null;
            }
            if (status == KnowledgeReviewStatuses.Approved && !IsReady(entry.Label, entry.Levels, entry.Answer, entry.Source))
            {
                skippedNotReady++;
                return null;
            }

            var history = History(KnowledgeHistoryActions.Review, actorUserId, entry.ReviewStatus, status, entry.Id);
            history.Note = note;
            entry.ReviewStatus = status;
            entry.ReviewNote = note;
            entry.ReviewedByUserId = actorUserId;
            entry.ReviewedAt = now;
            updated++;
            return history;
        }, ct: ct);

        return new ReviewKnowledgeEntriesResultDto(updated, ids.Count - found, skippedOwn, skippedNotReady, skippedNotPending);
    }

    /// <summary>Đủ nội dung để chat dùng: có nhãn, ít nhất một bậc học, câu trả lời và nguồn. Giống check constraint ck_knowledge_base_entries_ready.</summary>
    public static bool IsReady(string? label, IReadOnlyCollection<string> levels, string? answer, string? source) =>
        label is not null && levels.Count > 0 && !string.IsNullOrWhiteSpace(answer) && !string.IsNullOrWhiteSpace(source);

    private static void EnsureReady(ValidEntry valid)
    {
        var missing = new List<string>();
        if (valid.Label is null) missing.Add("nhãn");
        if (valid.Levels.Count == 0) missing.Add("bậc học");
        if (valid.Answer is null) missing.Add("câu trả lời");
        if (valid.Source is null) missing.Add("nguồn");
        if (missing.Count > 0)
            throw new ArgumentException($"Cần nhập {string.Join(", ", missing)} trước khi gửi duyệt.");
    }

    private async Task CheckDuplicateAsync(string question, Guid? selfId, CancellationToken ct)
    {
        foreach (var other in await repository.SameQuestionAsync(question, includeDeleted: false, ct))
        {
            if (other.Id != selfId)
                throw new KnowledgeDuplicateEntryException(other.Id);
        }
    }

    private record ValidEntry(string? Label, List<string> Levels, string Question, string? Answer, string? Source);

    private static ValidEntry Validate(SaveKnowledgeEntryRequest? request)
    {
        if (request is null)
            throw new ArgumentException("Thiếu nội dung tri thức.");

        var question = CleanLine(request.Question)
            ?? throw new ArgumentException("Câu hỏi không được để trống.");
        if (question.Length > MaxQuestionLength)
            throw new ArgumentException($"Câu hỏi tối đa {MaxQuestionLength} ký tự.");

        var answer = CleanText(request.Answer);
        if (answer is not null && answer.Length > MaxAnswerLength)
            throw new ArgumentException($"Câu trả lời tối đa {MaxAnswerLength} ký tự.");

        var source = CleanText(request.Source);
        if (source is not null && source.Length > MaxSourceLength)
            throw new ArgumentException($"Nguồn tối đa {MaxSourceLength} ký tự.");

        var label = Clean(request.Label)?.ToUpperInvariant();
        if (label is not null && !KnowledgeLabels.All.Contains(label))
            throw new ArgumentException($"Nhãn \"{label}\" không có trong danh sách nhãn của kho tri thức.");

        // Nguồn không bị kiểm tra: URL trong trích dẫn là hợp lệ
        if (SlmPersonalDataCheck.HasContact(question) || SlmPersonalDataCheck.HasContact(answer))
            throw new ArgumentException(PersonalDataMessage);

        return new ValidEntry(label, NormalizeLevels(request.Levels), question, answer, source);
    }

    private static List<string> NormalizeLevels(IReadOnlyList<string>? codes)
    {
        var picked = new HashSet<string>();
        foreach (var raw in codes ?? [])
        {
            var code = Clean(raw)?.ToLowerInvariant();
            if (code is null)
                continue;
            if (!KnowledgeLevels.All.Contains(code))
                throw new ArgumentException($"Bậc học phải là một trong: {string.Join(", ", KnowledgeLevels.All)}.");
            picked.Add(code);
        }
        if (picked.Contains(KnowledgeLevels.Chung) && picked.Count > 1)
            throw new ArgumentException("Bậc học \"Chung\" không đi cùng bậc học khác.");
        // Thứ tự cố định để so sánh thay đổi và hiển thị ổn định
        return KnowledgeLevels.All.Where(picked.Contains).ToList();
    }

    private static List<string> ChangedFields(KnowledgeBaseEntry entry, ValidEntry valid)
    {
        var changed = new List<string>();
        if (entry.Label != valid.Label) changed.Add("label");
        if (!entry.Levels.SequenceEqual(valid.Levels)) changed.Add("levels");
        if (entry.Question != valid.Question) changed.Add("question");
        if ((entry.Answer ?? "") != (valid.Answer ?? "")) changed.Add("answer");
        if ((entry.Source ?? "") != (valid.Source ?? "")) changed.Add("source");
        return changed;
    }

    private static KnowledgeBaseEntry NewEntry(ValidEntry valid, string status, string origin, Guid? actorUserId) => new()
    {
        Label = valid.Label,
        Levels = valid.Levels,
        Question = valid.Question,
        Answer = valid.Answer,
        Source = valid.Source,
        Origin = origin,
        ReviewStatus = status,
        CreatedByUserId = actorUserId,
    };

    private static KnowledgeBaseEntryHistory History(string action, Guid? actor, string? from, string? to, Guid entryId) => new()
    {
        EntryId = entryId,
        ActorUserId = actor,
        Action = action,
        FromStatus = from,
        ToStatus = to,
    };

    private static KnowledgeEntryFilter NormalizeFilter(KnowledgeEntryFilter? filter)
    {
        var label = Clean(filter?.Label)?.ToUpperInvariant();
        var level = Clean(filter?.Level)?.ToLowerInvariant();
        var status = Clean(filter?.ReviewStatus);
        var origin = Clean(filter?.Origin);
        var sort = Clean(filter?.Sort);

        if (label is not null && !KnowledgeLabels.All.Contains(label))
            throw new ArgumentException("Nhãn không có trong danh sách nhãn của kho tri thức.");
        if (level is not null && !KnowledgeLevels.All.Contains(level))
            throw new ArgumentException($"Bậc học phải là một trong: {string.Join(", ", KnowledgeLevels.All)}.");
        if (status is not null && !KnowledgeReviewStatuses.All.Contains(status))
            throw new ArgumentException($"Trạng thái phải là một trong: {string.Join(", ", KnowledgeReviewStatuses.All)}.");
        if (origin is not null && !KnowledgeOrigins.All.Contains(origin))
            throw new ArgumentException($"Nguồn gốc phải là một trong: {string.Join(", ", KnowledgeOrigins.All)}.");
        if (sort is not null && !Sorts.Contains(sort))
            throw new ArgumentException($"Sắp xếp phải là một trong: {string.Join(", ", Sorts)}.");

        return new KnowledgeEntryFilter(
            Clean(filter?.Search), label, level, Clean(filter?.Source), status, origin,
            filter?.CreatedByUserId, filter?.Deleted ?? false, sort);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Một dòng: NFC (Excel có thể xuất tiếng Việt dạng NFD), bỏ khoảng trắng hai đầu, gộp khoảng trắng giữa.</summary>
    private static string? CleanLine(string? value) =>
        Clean(value) is { } text ? Regex.Replace(text.Normalize(NormalizationForm.FormC), @"\s+", " ") : null;

    /// <summary>Văn bản nhiều dòng: NFC, bỏ khoảng trắng hai đầu, CRLF thành LF, giữ nguyên xuống dòng.</summary>
    private static string? CleanText(string? value) =>
        Clean(value)?.Normalize(NormalizationForm.FormC).Replace("\r\n", "\n").Replace('\r', '\n');

    /// <summary>Khóa so trùng câu hỏi khi nhập file.</summary>
    private static string QuestionKey(string question) => (CleanLine(question) ?? "").ToLowerInvariant();

    private static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid? id) =>
        id is { } key && names.TryGetValue(key, out var name) ? name : null;

    private static KnowledgeEntryDto ToDto(KnowledgeBaseEntry entry, string? creatorName) => new(
        entry.Id, entry.EntryNo, entry.Label, entry.Levels, entry.Question, entry.Answer, entry.Source, entry.Origin,
        entry.ReviewStatus, entry.ReviewNote, entry.ReviewedByUserId, entry.ReviewedAt, entry.CreatedByUserId,
        creatorName, entry.CreatedAt, entry.UpdatedAt, entry.DeletedAt);
}