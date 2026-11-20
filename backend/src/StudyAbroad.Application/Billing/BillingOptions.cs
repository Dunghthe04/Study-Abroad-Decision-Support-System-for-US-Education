namespace StudyAbroad.Application.Billing;

/// <summary>Cấu hình thanh toán và gói (appsettings "Billing"). Máy chủ kiểm thử rút ngắn các giá trị này.</summary>
public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>Đơn chờ thanh toán hết hạn sau bấy nhiêu phút.</summary>
    public int OrderExpiryMinutes { get; set; } = 30;

    /// <summary>Chu kỳ job nền (đối soát đơn, chuyển kỳ, nhắc hạn).</summary>
    public int JobIntervalSeconds { get; set; } = 60;

    /// <summary>Nhắc gia hạn trước ngày hết hạn bấy nhiêu ngày.</summary>
    public int ReminderDays { get; set; } = 7;

    /// <summary>Đơn chờ chỉ bị job coi là hết hạn sau ExpiresAt cộng bấy nhiêu phút (chờ webhook tới muộn).</summary>
    public int PendingGraceMinutes { get; set; } = 2;
}
