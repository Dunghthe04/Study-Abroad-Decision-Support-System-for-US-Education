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
}
