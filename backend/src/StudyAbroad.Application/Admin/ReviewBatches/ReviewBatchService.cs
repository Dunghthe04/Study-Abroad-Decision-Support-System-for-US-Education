using System.Security.Cryptography;
using System.Text.Json;
using StudyAbroad.Application.Admin.ChatReview;
using StudyAbroad.Application.Audit;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.ReviewBatches;

public interface IReviewBatchService
{
    /// <summary>
    /// Tạo đợt duyệt mẫu: bốc ngẫu nhiên (có seed, chia nhóm theo skill) từ các câu đã trả lời được trong khoảng ngày, lưu cố định danh sách câu
    /// và người được giao. Ghi nhật ký (review_batch.create) cùng lúc, không chứa nội dung chat. Tham số sai (tên, số câu, khoảng ngày, người
    /// duyệt không phải Admin đang hoạt động, không có câu nào để bốc) thì ném ArgumentException và không lưu gì.
    /// </summary>
    Task<ReviewBatchCreatedDto> CreateAsync(
        CreateReviewBatchRequest request, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default);

    /// <summary>Các đợt người dùng đã tạo hoặc được giao duyệt, mới nhất trước. Chỉ số đo, không có nội dung chat.</summary>
    Task<IReadOnlyList<ReviewBatchListItemDto>> ListAsync(Guid actorUserId, CancellationToken ct = default);

    /// <summary>Các câu của một đợt (chưa có nội dung). Null nếu đợt không có hoặc người này không được giao duyệt (không phân biệt hai trường hợp).</summary>
    Task<ReviewItemListDto?> GetItemsAsync(Guid batchId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>
    /// Một câu của đợt CÓ NỘI DUNG chat, kèm kết luận của chính người xem. Chỉ người được giao duyệt đợt mới xem được (người khác nhận null).
    /// Ghi nhật ký người xem (audit_logs) TRƯỚC khi trả nội dung; nếu ghi nhật ký lỗi thì ném lỗi chứ không trả nội dung.
    /// Nhật ký không chứa nội dung chat.
    /// </summary>
    Task<ReviewItemDetailDto?> GetItemDetailAsync(
        Guid batchId, Guid itemId, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default);

    /// <summary>
    /// Lưu kết luận của người duyệt cho một câu (ghi đè nếu đã chấm). Null nếu người này không được giao đợt hoặc câu không thuộc đợt.
    /// Kết luận sai quy tắc thì ném ArgumentException và không lưu gì.
    /// </summary>
    Task<ReviewVerdictDto?> SaveVerdictAsync(
        Guid batchId, Guid itemId, SaveReviewVerdictRequest request, Guid actorUserId, CancellationToken ct = default);

    /// <summary>
    /// Báo cáo của một đợt: tỷ lệ sai từng nhóm và chung (có trọng số), nguyên nhân, nhãn đúng. Chỉ là số tổng hợp, không có nội dung chat.
    /// Null nếu đợt không có hoặc người gọi không phải người tạo cũng không được giao duyệt (không phân biệt hai trường hợp).
    /// </summary>
    Task<ReviewReportDto?> GetReportAsync(Guid batchId, Guid actorUserId, CancellationToken ct = default);
}

public class ReviewBatchService(IReviewBatchRepository repository, IAuditLogRepository auditLogs) : IReviewBatchService
{
    public const int MaxNameLength = 120;
    public const int MaxSizePerStratum = 100;
    public const int MaxReviewers = 5;
    public const int MaxSeedLength = 64;
    public const int ContextMessages = 6;
    public const string CreateAction = "review_batch.create";
    public const string ViewItemAction = "review_batch.view_item";

    public async Task<ReviewBatchCreatedDto> CreateAsync(
        CreateReviewBatchRequest request, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("Thiếu người tạo: không ghi được nhật ký nên không tạo đợt.");

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
            throw new ArgumentException($"Tên đợt phải có từ 1 đến {MaxNameLength} ký tự.");
        if (request.SizePerStratum is < 1 or > MaxSizePerStratum)
            throw new ArgumentException($"Số câu mỗi nhóm phải từ 1 đến {MaxSizePerStratum}.");
        var seed = request.Seed?.Trim();
        if (seed is { Length: > MaxSeedLength })
            throw new ArgumentException($"Seed tối đa {MaxSeedLength} ký tự.");
        if (string.IsNullOrEmpty(seed))
            seed = RandomNumberGenerator.GetHexString(12, lowercase: true);   // tự sinh và lưu lại, nên vẫn lặp lại được sau này

        var (fromUtc, toUtcExclusive) = Range(request.From, request.To);
        var reviewerIds = await ValidReviewersAsync(request.ReviewerIds, actorUserId, ct);

        var candidates = await repository.ListCandidatesAsync(fromUtc, toUtcExclusive, ct);
        var sample = ReviewSampler.Draw(
            candidates.Select(c => (c.MessageId, ReviewStrata.Of(c.ReplyMetaJson))), seed, request.SizePerStratum);
        if (sample.Items.Count == 0)
            throw new ArgumentException("Không có câu trả lời nào trong khoảng này để bốc mẫu.");

        var batch = new ReviewBatch
        {
            Name = name,
            CreatedByUserId = actorUserId,
            Seed = seed,
            SizePerStratum = request.SizePerStratum,
            SampledFromUtc = fromUtc,
            SampledToUtcExclusive = toUtcExclusive,
            StrataJson = ReviewSampler.StrataToJson(sample.Strata),
        };
        var items = sample.Items
            .Select(i => new ReviewBatchItem { ReviewBatchId = batch.Id, ChatMessageId = i.MessageId, Stratum = i.Stratum, Position = i.Position })
            .ToList();
        var reviewers = reviewerIds.Select(id => new ReviewBatchReviewer { ReviewBatchId = batch.Id, UserId = id }).ToList();
        var audit = new AuditLog
        {
            ActorUserId = actorUserId,
            Action = CreateAction,
            EntityType = "review_batch",
            EntityId = batch.Id.ToString(),
            // Chỉ tham số của đợt, không có nội dung chat
            DetailJson = JsonSerializer.Serialize(new
            {
                seed, sizePerStratum = request.SizePerStratum, totalItems = items.Count, reviewers = reviewers.Count,
            }),
            IpAddress = Clip(ipAddress, 64),
            UserAgent = Clip(userAgent, 512),
        };

        await repository.AddAsync(batch, items, reviewers, audit, ct);

        return new ReviewBatchCreatedDto(
            batch.Id, batch.Name, batch.Seed, batch.SizePerStratum, fromUtc, toUtcExclusive, items.Count, sample.Strata, reviewerIds,
            batch.CreatedAt);
    }

    public async Task<IReadOnlyList<ReviewBatchListItemDto>> ListAsync(Guid actorUserId, CancellationToken ct = default)
    {
        var rows = await repository.ListForUserAsync(actorUserId, ct);
        return rows.Select(r => new ReviewBatchListItemDto(
                r.Id, r.Name, r.Seed, r.CreatedAt, r.SizePerStratum, r.SampledFromUtc, r.SampledToUtcExclusive, r.TotalItems,
                ReviewSampler.ReadStrata(r.StrataJson), r.IsCreator, r.IsReviewer,
                r.Reviewers.FirstOrDefault(p => p.UserId == actorUserId)?.Graded ?? 0,
                r.Reviewers.Select(p => new ReviewerProgressDto(p.UserId, p.FullName, p.Graded)).ToList()))
            .ToList();
    }

    public async Task<ReviewItemListDto?> GetItemsAsync(Guid batchId, Guid actorUserId, CancellationToken ct = default)
    {
        var rows = await repository.ListItemsAsync(batchId, actorUserId, ct);
        if (rows is null)
            return null;

        var items = rows.Items.Select(i => new ReviewItemRowDto(i.ItemId, i.Position, i.Stratum, i.GradedByMe)).ToList();
        return new ReviewItemListDto(rows.BatchId, rows.BatchName, items.Count, items.Count(i => i.GradedByMe), items);
    }

    public async Task<ReviewItemDetailDto?> GetItemDetailAsync(
        Guid batchId, Guid itemId, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("Thiếu người xem: không ghi được nhật ký nên không trả nội dung.");

        var rows = await repository.FindItemDetailAsync(batchId, itemId, actorUserId, ContextMessages, ct);
        if (rows is null)
            return null;   // không được giao, không thuộc đợt hoặc tin gốc đã mất: không có gì để xem và cũng không ghi nhật ký

        // Ghi nhật ký TRƯỚC khi trả nội dung. Không bắt lỗi: ghi lỗi thì ném lên, nội dung không rời khỏi hàm này
        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = ViewItemAction,
            EntityType = "chat_message",
            EntityId = rows.Reply.MessageId.ToString(),
            // Chỉ mã, tuyệt đối không có nội dung chat (không nhân bản dữ liệu cá nhân sang bảng nhật ký)
            DetailJson = JsonSerializer.Serialize(new { batchId, itemId }),
            IpAddress = Clip(ipAddress, 64),
            UserAgent = Clip(userAgent, 512),
        }, ct);

        return new ReviewItemDetailDto(
            rows.ItemId, rows.Position, rows.Stratum,
            rows.Question is { } question
                ? new ChatReviewQuestionDto(question.MessageId, question.CreatedAt, question.Content, ChatReviewMetaReader.ReadModelDetail(question.MetaJson))
                : null,
            new ChatReviewMessageDto(rows.Reply.MessageId, rows.Reply.Role, rows.Reply.CreatedAt, rows.Reply.Content),
            rows.Context
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.MessageId)
                .Select(c => new ChatReviewMessageDto(c.MessageId, c.Role, c.CreatedAt, c.Content))
                .ToList(),
            rows.MyVerdict is { } verdict ? ToDto(verdict) : null);
    }

    public async Task<ReviewVerdictDto?> SaveVerdictAsync(
        Guid batchId, Guid itemId, SaveReviewVerdictRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("Thiếu người chấm.");

        var label = request.LabelVerdict?.Trim();
        var answer = request.AnswerVerdict?.Trim();
        var correctIntent = string.IsNullOrWhiteSpace(request.CorrectIntent) ? null : request.CorrectIntent.Trim();
        var cause = string.IsNullOrWhiteSpace(request.Cause) ? null : request.Cause.Trim();

        if (label is null || !ReviewLabelVerdicts.All.Contains(label))
            throw new ArgumentException($"Nhãn phải là một trong: {string.Join(", ", ReviewLabelVerdicts.All)}.");
        if (answer is null || !ReviewAnswerVerdicts.All.Contains(answer))
            throw new ArgumentException($"Câu trả lời phải là một trong: {string.Join(", ", ReviewAnswerVerdicts.All)}.");

        // Nhãn đúng chỉ có nghĩa khi nhãn SLM gán là sai
        if (correctIntent is not null)
        {
            if (label != ReviewLabelVerdicts.Wrong)
                throw new ArgumentException("Chỉ chọn nhãn đúng khi nhãn SLM gán là sai.");
            if (!SlmIntentTypes.All.Contains(correctIntent))
                throw new ArgumentException($"Nhãn đúng phải là một trong: {string.Join(", ", SlmIntentTypes.All)}.");
        }

        // Nguyên nhân bắt buộc khi câu trả lời sai (để thống kê được vì sao sai) và không có nghĩa trong các trường hợp khác
        if (answer == ReviewAnswerVerdicts.Wrong)
        {
            if (cause is null || !ReviewCauses.All.Contains(cause))
                throw new ArgumentException($"Chọn nguyên nhân khi câu trả lời sai: {string.Join(", ", ReviewCauses.All)}.");
        }
        else if (cause is not null)
        {
            throw new ArgumentException("Chỉ chọn nguyên nhân khi câu trả lời sai.");
        }

        var saved = await repository.UpsertVerdictAsync(batchId, itemId, actorUserId, label, correctIntent, answer, cause, ct);
        return saved is null ? null : ToDto(saved);
    }

    public async Task<ReviewReportDto?> GetReportAsync(Guid batchId, Guid actorUserId, CancellationToken ct = default)
    {
        var rows = await repository.GetReportRowsAsync(batchId, actorUserId, ct);
        if (rows is null)
            return null;

        // Mỗi câu một kết luận: kết luận chấm sớm nhất (hòa thì theo mã người chấm cho ổn định), để câu nhiều người chấm không bị đếm hai lần
        var graded = rows.Verdicts
            .GroupBy(v => v.ItemId)
            .Select(g => g.OrderBy(v => v.CreatedAt).ThenBy(v => v.ReviewerUserId).First())
            .Select(v => new ReviewGradedItem(v.Stratum, v.LabelVerdict, v.CorrectIntent, v.AnswerVerdict, v.Cause))
            .ToList();

        var report = ReviewReportCalculator.Compute(ReviewSampler.ReadStrata(rows.StrataJson), graded);
        return new ReviewReportDto(rows.BatchId, rows.BatchName, rows.Seed, rows.TotalItems, graded.Count, rows.ReviewerCount, report);
    }

    private static ReviewVerdictDto ToDto(ReviewVerdict v) =>
        new(v.LabelVerdict, v.CorrectIntent, v.AnswerVerdict, v.Cause, v.UpdatedAt ?? v.CreatedAt);

    // Không chọn ai thì người tạo đợt tự duyệt (trường hợp thường gặp: một người làm hết). Có chọn thì không quá MaxReviewers người, không trùng.
    // Mọi người duyệt (kể cả người tạo) phải là Admin đang hoạt động
    private async Task<IReadOnlyList<Guid>> ValidReviewersAsync(IReadOnlyList<Guid>? requested, Guid actorUserId, CancellationToken ct)
    {
        var ids = (requested ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            ids = [actorUserId];
        if (ids.Count > MaxReviewers)
            throw new ArgumentException($"Tối đa {MaxReviewers} người duyệt.");

        var admins = (await repository.FindActiveAdminIdsAsync(ids, ct)).ToHashSet();
        if (ids.Any(id => !admins.Contains(id)))
            throw new ArgumentException("Người duyệt phải là tài khoản Admin đang hoạt động.");
        return ids;
    }

    // Khoảng ngày tính cả hai đầu theo UTC; bỏ trống đầu nào thì không giới hạn đầu đó
    private static (DateTime? FromUtc, DateTime? ToUtcExclusive) Range(DateOnly? from, DateOnly? to)
    {
        if (from is { } start && to is { } end && start > end)
            throw new ArgumentException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        return (
            from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            to?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }

    private static string? Clip(string? text, int max) =>
        string.IsNullOrEmpty(text) ? null : text.Length <= max ? text : text[..max];
}
