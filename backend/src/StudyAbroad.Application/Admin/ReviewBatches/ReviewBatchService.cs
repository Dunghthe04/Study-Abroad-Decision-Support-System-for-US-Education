using System.Security.Cryptography;
using System.Text.Json;
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
}

public class ReviewBatchService(IReviewBatchRepository repository) : IReviewBatchService
{
    public const int MaxNameLength = 120;
    public const int MaxSizePerStratum = 100;
    public const int MaxReviewers = 5;
    public const int MaxSeedLength = 64;
    public const string CreateAction = "review_batch.create";

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
        var reviewerIds = await ValidReviewersAsync(request.ReviewerIds, ct);

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

    // Phải có từ 1 đến MaxReviewers người, không trùng, và tất cả là Admin đang hoạt động
    private async Task<IReadOnlyList<Guid>> ValidReviewersAsync(IReadOnlyList<Guid>? requested, CancellationToken ct)
    {
        var ids = (requested ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0 || ids.Count > MaxReviewers)
            throw new ArgumentException($"Cần chọn từ 1 đến {MaxReviewers} người duyệt.");

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
