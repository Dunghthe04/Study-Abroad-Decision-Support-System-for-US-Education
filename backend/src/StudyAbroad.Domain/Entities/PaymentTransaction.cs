using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Lịch sử giao dịch thanh toán qua cổng Payment Gateway (FE-19: UC-78, UC-79).</summary>
public class PaymentTransaction : BaseEntity
{
    /// <summary>[UC-80] Null khi tài khoản đã bị xóa hẳn: giao dịch giữ lại ẩn danh theo nghĩa vụ kế toán.</summary>
    public Guid? UserId { get; set; }
    public Guid? UserSubscriptionId { get; set; }
    public decimal AmountVnd { get; set; }
    /// <summary>vnpay | momo | stripe</summary>
    public string Gateway { get; set; } = string.Empty;
    public string? GatewayTransactionId { get; set; }
    /// <summary>pending | success | failed | refunded</summary>
    public string Status { get; set; } = "pending";
    public string? PayloadJson { get; set; }
    public DateTime? PaidAt { get; set; }
}
