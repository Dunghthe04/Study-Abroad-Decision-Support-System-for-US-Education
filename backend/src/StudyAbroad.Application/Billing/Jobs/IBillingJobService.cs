namespace StudyAbroad.Application.Billing.Jobs;

/// <summary>
/// Các bước của job nền thanh toán (kế hoạch billing v2, tác vụ 11), chạy theo thứ tự này mỗi lượt. Mỗi bước trả số mục đã xử lý.
/// Một mục lỗi chỉ được ghi log, không dừng các mục còn lại.
/// </summary>
public interface IBillingJobService
{
    /// <summary>Đối soát mọi đơn đang chờ với payOS: đã trả thì kích hoạt, hủy / hết hạn / thiếu tiền thì cập nhật, quá hạn cộng ân hạn thì hết hạn (P11, P14).</summary>
    Task<int> ReconcilePendingOrdersAsync(DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Kết thúc kỳ hết hạn và nhóm Gia đình không còn kỳ nào (R2); thông báo hết hạn / nhóm Gia đình kết thúc phát ra trong <see cref="IEntitlementService.EndExpiredCyclesAsync"/>.</summary>
    Task<int> EndExpiredCyclesAsync(DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Lời mời Gia đình quá 7 ngày thành expired (F13).</summary>
    Task<int> ExpireFamilyInvitesAsync(DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Nhắc gia hạn một lần trước ngày hết hạn (R8): email và thông báo cho chủ kỳ.</summary>
    Task<int> SendRenewalRemindersAsync(DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Thông báo dùng 80% và 100% hạn mức của kỳ, mỗi mốc một lần mỗi kỳ (T7, T8).</summary>
    Task<int> SendUsageNotificationsAsync(DateTime nowUtc, CancellationToken ct = default);
}
