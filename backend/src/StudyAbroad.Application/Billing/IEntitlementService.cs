using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing;

/// <summary>
/// Quyền dùng của một tài khoản: tier (Free / USDSS Premium), kỳ trả cho lượt chat, tóm tắt gói. Mọi hàm đọc
/// đều kết thúc kỳ đến hạn trước (và gói Gia đình hết kỳ) nên đúng cả khi job chạy trễ.
/// </summary>
public interface IEntitlementService
{
    /// <summary>Kết thúc kỳ đến hạn mà người dùng chạm tới rồi tóm tắt. Dùng cho GET subscription.</summary>
    Task<SubscriptionSummaryDto> GetSummaryAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Chọn kỳ trả cho lượt chat kế tiếp: trong các kỳ dùng được còn token ở cửa sổ hiện tại, kỳ hết hạn sớm nhất; không có thì hạn mức Free.</summary>
    Task<ChatCharge> ResolveChatChargeAsync(Guid userId, string role, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>True khi có kỳ trả phí còn trong hạn (kỳ Personal của mình, hoặc kỳ Gia đình của mình / của nhóm mình là thành viên active). Nhân viên luôn true.</summary>
    Task<bool> IsPremiumAsync(Guid userId, string role, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Các kỳ đã hết hạn của chính người dùng (tối đa 100), hết hạn gần nhất trước (GET subscription/expired).</summary>
    Task<IReadOnlyList<ExpiredCycleDto>> GetExpiredCyclesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Các gói đang bán, theo thứ tự hiển thị (GET plans).</summary>
    Task<IReadOnlyList<PlanDto>> GetPlansOnSaleAsync(CancellationToken ct = default);

    /// <summary>
    /// Kết thúc các kỳ active đã tới hạn (<c>ends_at &lt;= now</c>, ended / expired) của một chủ ở một loại gói rồi trả các kỳ còn lại (thực thể đang theo dõi) kèm token đã dùng.
    /// Loại family mà không còn kỳ nào thì nhóm active của chủ bị kết thúc (family_lapsed) như khi đọc quyền dùng.
    /// </summary>
    /// <param name="deferredNotifications">
    /// Khác null thì thông báo hết hạn / nhóm kết thúc được xếp vào đây (hàm gửi) thay vì gửi ngay: người gọi đang trong transaction chưa commit
    /// (áp dụng thanh toán) gửi sau commit, chỉ cho lần thử đã commit. Null thì gửi ngay sau khi lưu.
    /// </param>
    Task<IReadOnlyList<ActiveCycle>> EndExpiredCyclesAsync(
        Guid ownerUserId, string kind, DateTime nowUtc, CancellationToken ct = default, ICollection<Func<CancellationToken, Task>>? deferredNotifications = null);
}

/// <summary>Một kỳ còn chạy và token đã dùng trong cửa sổ hạn mức hiện tại. Hạn mức 0 luôn hết.</summary>
public sealed record ActiveCycle(UserSubscription Cycle, long Used)
{
    public bool Exhausted => CycleRules.IsExhausted(Used, Cycle.TokenQuota);
    public bool HasTokens => !Exhausted;
}
