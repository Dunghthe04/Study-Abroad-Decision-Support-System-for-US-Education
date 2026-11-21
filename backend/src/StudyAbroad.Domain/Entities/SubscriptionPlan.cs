using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Gói dịch vụ do admin quản lý (UC-77, UC-79). Đúng một gói loại free (chỉ số hạn mức tháng, không thời hạn).</summary>
public class SubscriptionPlan : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Giá, đồng nguyên. Gói free luôn 0.</summary>
    public decimal PriceVnd { get; set; } = 0;
    /// <summary>Thời hạn gói trả phí (1..3650 ngày); null với gói free (làm mới theo chu kỳ).</summary>
    public int? DurationDays { get; set; }
    /// <summary>
    /// Chu kỳ cấp lại token (ngày). Với gói free: null = làm mới ngày 1 hằng tháng (UTC+7), hoặc 1..365 ngày.
    /// Với gói personal/family: null hoặc bằng DurationDays = dùng chung cả kỳ; 1..DurationDays = làm mới mỗi N ngày tính từ StartsAt.
    /// </summary>
    public int? QuotaPeriodDays { get; set; }
    /// <summary>free | personal | family (PlanKinds). Đặt một lần lúc tạo.</summary>
    public string Kind { get; set; } = string.Empty;
    /// <summary>Hạn mức token mỗi chu kỳ cấp (gói free: mỗi tháng hoặc mỗi QuotaPeriodDays). Kỳ đã mua giữ bản chụp riêng.</summary>
    public long TokenQuota { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>Đang bán (hiện trên trang gói).</summary>
    public bool IsActive { get; set; } = true;
}
