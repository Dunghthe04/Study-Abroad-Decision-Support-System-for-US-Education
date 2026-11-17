using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Tin nhắn trao đổi trong yêu cầu liên hệ trung tâm tư vấn (UC-44, UC-58, UC-74).</summary>
public class LeadMessage : BaseEntity
{
    public Guid LeadId { get; set; }
    public Guid SenderUserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsFlagged { get; set; } = false;
    public string? FlagReason { get; set; }
    /// <summary>visible | removed</summary>
    public string Status { get; set; } = "visible";
    public Guid? RemovedByUserId { get; set; }

    /// <summary>[UC-58] Người báo cáo tin vi phạm (học sinh / phụ huynh của lead). Báo lần đầu được giữ, báo lại không ghi đè.</summary>
    public Guid? ReportedByUserId { get; set; }
    public DateTime? ReportedAt { get; set; }
    public string? ReportReason { get; set; }

    /// <summary>
    /// [UC-58] Kết quả Admin xử lý báo cáo: removed (gỡ tin) | warned (cảnh cáo người gửi) | locked (khóa tài khoản người gửi).
    /// Null là chưa xử lý. Bác bỏ báo cáo thì xóa hẳn thông tin báo cáo nên không có giá trị riêng.
    /// </summary>
    public string? ReportResolution { get; set; }
    public DateTime? ReportResolvedAt { get; set; }

    /// <summary>Ảnh đính kèm (URL https của nhà cung cấp lưu trữ). Có thể kèm hoặc không kèm văn bản.</summary>
    public string? AttachmentUrl { get; set; }
    public string? AttachmentPublicId { get; set; }
}
