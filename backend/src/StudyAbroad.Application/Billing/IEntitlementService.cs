using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing;

/// <summary>
/// Quyền dùng của một tài khoản: tier (Free / USDSS Premium), pool trả cho lượt chat, tóm tắt gói. Mọi hàm đọc
/// đều áp dụng chuyển kỳ đến hạn trước (queued → active, hết hạn, gói Gia đình hết hiệu lực) nên đúng cả khi job chạy trễ.
/// </summary>
public interface IEntitlementService
{
    /// <summary>Áp dụng chuyển kỳ cho mọi làn người dùng chạm tới rồi tóm tắt. Dùng cho GET subscription.</summary>
    Task<SubscriptionSummaryDto> GetSummaryAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Chọn pool trả cho lượt chat kế tiếp (Personal → Family → Free). Áp dụng chuyển kỳ trước.</summary>
    Task<ChatCharge> ResolveChatChargeAsync(Guid userId, string role, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>True khi có kỳ trả phí còn trong hạn (Personal sở hữu, hoặc Gia đình là chủ / thành viên active). Nhân viên luôn true.</summary>
    Task<bool> IsPremiumAsync(Guid userId, string role, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Các gói đang bán, theo thứ tự hiển thị (GET plans).</summary>
    Task<IReadOnlyList<PlanDto>> GetPlansOnSaleAsync(CancellationToken ct = default);

    /// <summary>
    /// Đưa một làn (chủ, loại) tới nowUtc và trả kỳ active, kỳ queued, token đã dùng của kỳ active (thực thể đang theo dõi, đã lưu các chuyển kỳ).
    /// Làn family không còn kỳ nào thì nhóm active của chủ bị kết thúc (family_lapsed) như khi đọc quyền dùng.
    /// </summary>
    /// <param name="deferredNotifications">
    /// Khác null thì thông báo chuyển kỳ / nhóm kết thúc được xếp vào đây (hàm gửi) thay vì gửi ngay: người gọi đang trong transaction chưa commit
    /// (áp dụng thanh toán) gửi sau commit, chỉ cho lần thử đã commit. Null thì gửi ngay sau khi lưu.
    /// </param>
    Task<LaneState> AdvanceLaneAsync(
        Guid ownerUserId, string kind, DateTime nowUtc, CancellationToken ct = default, ICollection<Func<CancellationToken, Task>>? deferredNotifications = null);
}

/// <summary>Trạng thái một làn sau khi chuyển kỳ. Active / Queued là thực thể đang theo dõi (sửa được rồi lưu).</summary>
/// <param name="ActiveUsed">Token đã dùng của kỳ active (0 nếu không có).</param>
/// <param name="ActiveExhausted">Có kỳ active và đã dùng hết hạn mức (hạn mức 0 luôn hết).</param>
public sealed record LaneState(UserSubscription? Active, UserSubscription? Queued, long ActiveUsed, bool ActiveExhausted);
