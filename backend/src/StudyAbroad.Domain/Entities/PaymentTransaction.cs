using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Đơn hàng thanh toán qua payOS (UC-78, UC-112, UC-132..134). Gói là bản chụp lúc đặt.</summary>
public class PaymentTransaction : BaseEntity
{
    /// <summary>[UC-80] Null khi tài khoản đã bị xóa hẳn: giao dịch giữ lại ẩn danh theo nghĩa vụ kế toán.</summary>
    public Guid? UserId { get; set; }
    /// <summary>Kỳ được kích hoạt bởi đơn này, gán lúc thanh toán.</summary>
    public Guid? UserSubscriptionId { get; set; }
    public decimal AmountVnd { get; set; }
    /// <summary>payos</summary>
    public string Gateway { get; set; } = string.Empty;
    /// <summary>paymentLinkId của payOS.</summary>
    public string? GatewayTransactionId { get; set; }
    /// <summary>pending | paid | mismatch | expired | cancelled (OrderStatuses)</summary>
    public string Status { get; set; } = "pending";
    public string? PayloadJson { get; set; }
    public DateTime? PaidAt { get; set; }
    /// <summary>Mã đơn gửi payOS = Unix ms, duy nhất.</summary>
    public long OrderCode { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    public string PlanKind { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public int PlanDurationDays { get; set; }
    /// <summary>Bản chụp chu kỳ cấp lại token (ngày) từ gói lúc tạo đơn.</summary>
    public int? PlanQuotaPeriodDays { get; set; }
    public long PlanTokenQuota { get; set; }
    /// <summary>Nội dung chuyển khoản (USAS + 5 số cuối mã đơn).</summary>
    public string Description { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string? AdminNote { get; set; }
    public Guid? HandledByUserId { get; set; }
}
