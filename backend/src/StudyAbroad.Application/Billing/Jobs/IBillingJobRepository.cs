using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Jobs;

/// <summary>Đơn đang chờ cần đối soát (chỉ mã và hạn, đọc không theo dõi: mọi thao tác ghi nạp lại đơn sau khi khóa dòng).</summary>
public sealed record PendingOrderRef(long OrderCode, DateTime ExpiresAt);

/// <summary>Một làn (chủ, loại) cần được đưa tới thời điểm hiện tại.</summary>
public sealed record LaneRef(Guid OwnerUserId, string Kind);

/// <summary>
/// Truy vấn riêng của job nền thanh toán. Các hàm trả <see cref="UserSubscription"/> cho thực thể đang theo dõi
/// (đặt dấu thời gian rồi lưu bằng <see cref="SaveAsync"/>).
/// </summary>
public interface IBillingJobRepository
{
    /// <summary>Mọi đơn đang chờ thanh toán, cũ nhất trước.</summary>
    Task<IReadOnlyList<PendingOrderRef>> ListPendingOrdersAsync(CancellationToken ct = default);

    /// <summary>
    /// Các làn cần chuyển kỳ: có kỳ active đã tới hạn (<c>ends_at &lt;= now</c>), hoặc có kỳ queued (có thể bắt đầu khi kỳ cũ dùng hết),
    /// hoặc nhóm Gia đình active mà làn family không còn kỳ active (cần kết thúc nhóm).
    /// </summary>
    Task<IReadOnlyList<LaneRef>> FindDueLanesAsync(DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Kỳ active hết hạn trong (now, until], chưa nhắc, và làn không có kỳ queued.</summary>
    Task<List<UserSubscription>> FindCyclesDueForReminderAsync(DateTime nowUtc, DateTime untilUtc, CancellationToken ct = default);

    /// <summary>Kỳ active có hạn mức &gt; 0 mà còn thiếu thông báo 80% hoặc 100%.</summary>
    Task<List<UserSubscription>> FindActiveCyclesAwaitingUsageNoticeAsync(CancellationToken ct = default);

    /// <summary>Token tính phí (Claude) đã dùng của từng kỳ, theo cùng luật với <see cref="IBillingRepository.SumCycleTokensAsync"/>. Kỳ chưa dùng thì không có khóa.</summary>
    Task<IReadOnlyDictionary<Guid, long>> SumCycleTokensAsync(IReadOnlyCollection<Guid> cycleIds, CancellationToken ct = default);

    Task SaveAsync(CancellationToken ct = default);
}
