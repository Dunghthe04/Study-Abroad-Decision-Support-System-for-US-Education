using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Jobs;

/// <summary>Đơn đang chờ cần đối soát (chỉ mã và hạn, đọc không theo dõi: mọi thao tác ghi nạp lại đơn sau khi khóa dòng).</summary>
public sealed record PendingOrderRef(long OrderCode, DateTime ExpiresAt);

/// <summary>Một chủ kỳ và loại gói cần được kết thúc kỳ hết hạn (hoặc kết thúc nhóm Gia đình không còn kỳ).</summary>
public sealed record CycleOwnerRef(Guid OwnerUserId, string Kind);

/// <summary>
/// Truy vấn riêng của job nền thanh toán. Truy vấn chỉ đọc không theo dõi; <see cref="FindCyclesDueForReminderAsync"/> và <see cref="FindCycleAsync"/> trả
/// <see cref="UserSubscription"/> đang theo dõi (đặt dấu thời gian rồi lưu bằng <see cref="SaveAsync"/>).
/// </summary>
public interface IBillingJobRepository
{
    /// <summary>Tối đa <see cref="MaxOrdersPerTick"/> đơn mỗi lượt job (đơn dồn lại thì lượt sau làm tiếp).</summary>
    const int MaxOrdersPerTick = 200;

    /// <summary>Các đơn đang chờ thanh toán, hết hạn sớm nhất trước (đơn quá hạn được xử lý trước), tối đa <see cref="MaxOrdersPerTick"/>.</summary>
    Task<IReadOnlyList<PendingOrderRef>> ListPendingOrdersAsync(CancellationToken ct = default);

    /// <summary>
    /// Các đơn đã hết hạn mà ExpiresAt từ <paramref name="sinceUtc"/> trở lại (còn trong cửa sổ khách có thể trả muộn), hết hạn muộn nhất trước, tối đa <see cref="MaxOrdersPerTick"/>.
    /// Job hỏi lại payOS: PAID thì kích hoạt, còn lại giữ nguyên.
    /// </summary>
    Task<IReadOnlyList<PendingOrderRef>> ListRecentlyExpiredOrdersAsync(DateTime sinceUtc, CancellationToken ct = default);

    /// <summary>
    /// Các (chủ, loại) cần kết thúc kỳ: có kỳ active đã tới hạn (<c>ends_at &lt;= now</c>), hoặc nhóm Gia đình active mà chủ không còn kỳ Gia đình active nào (cần kết thúc nhóm).
    /// </summary>
    Task<IReadOnlyList<CycleOwnerRef>> FindDueCycleOwnersAsync(DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Kỳ active hết hạn trong (now, until] và chưa nhắc.</summary>
    Task<List<UserSubscription>> FindCyclesDueForReminderAsync(DateTime nowUtc, DateTime untilUtc, CancellationToken ct = default);

    /// <summary>Kỳ active có hạn mức &gt; 0 mà còn thiếu thông báo 80% hoặc 100%. Đọc KHÔNG theo dõi: kỳ nào cần ghi dấu thì nạp riêng bằng <see cref="FindCycleAsync"/>.</summary>
    Task<List<UserSubscription>> FindActiveCyclesAwaitingUsageNoticeAsync(CancellationToken ct = default);

    /// <summary>Một kỳ theo id, đang theo dõi (đặt dấu thời gian rồi lưu bằng <see cref="SaveAsync"/>). Null nếu kỳ đã mất.</summary>
    Task<UserSubscription?> FindCycleAsync(Guid cycleId, CancellationToken ct = default);

    /// <summary>Token tính phí (Claude) đã dùng của từng kỳ, theo cùng luật với <see cref="IBillingRepository.SumCycleTokensAsync"/>. Kỳ chưa dùng thì không có khóa.</summary>
    Task<IReadOnlyDictionary<Guid, long>> SumCycleTokensAsync(IReadOnlyCollection<Guid> cycleIds, CancellationToken ct = default);

    Task SaveAsync(CancellationToken ct = default);
}
