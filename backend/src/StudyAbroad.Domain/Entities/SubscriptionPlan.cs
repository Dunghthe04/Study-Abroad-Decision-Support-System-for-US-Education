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
    /// <summary>30 hoặc 90 ngày; null với gói free (làm mới đầu tháng).</summary>
    public int? DurationDays { get; set; }
    /// <summary>free | personal | family (PlanKinds). Đặt một lần lúc tạo.</summary>
    public string Kind { get; set; } = string.Empty;
    /// <summary>Hạn mức token mỗi kỳ (gói free: mỗi tháng). Kỳ đã mua giữ bản chụp riêng.</summary>
    public long TokenQuota { get; set; }
    /// <summary>Ước lượng số câu hỏi chỉ để hiển thị, không dùng để chặn.</summary>
    public int EstimatedQuestions { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>Đang bán (hiện trên trang gói).</summary>
    public bool IsActive { get; set; } = true;
}
