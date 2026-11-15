using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// Yêu cầu xác minh pháp lý của trung tâm tư vấn (UC-71 mới / UC-59).
/// </summary>
public class CenterVerificationRequest : BaseEntity
{
    public Guid CenterId { get; set; }
    public Guid RequestedByUserId { get; set; }

    /// <summary>Tên pháp lý theo Giấy chứng nhận đăng ký doanh nghiệp.</summary>
    public string LegalName { get; set; } = string.Empty;

    /// <summary>Mã số doanh nghiệp / Mã số thuế (duy nhất theo BR-C4).</summary>
    public string TaxCode { get; set; } = string.Empty;

    /// <summary>Địa chỉ trụ sở chính theo ĐKKD.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Họ tên người đại diện theo pháp luật.</summary>
    public string LegalRepresentative { get; set; } = string.Empty;

    /// <summary>Website hoặc Fanpage chính thức (tùy chọn).</summary>
    public string? Website { get; set; }

    /// <summary>pending_review | approved | rejected | changes_requested | suspended</summary>
    public string Status { get; set; } = "pending_review";

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Lý do từ chối hoặc yêu cầu bổ sung nếu không đạt duyệt.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Ghi chú nội bộ của Moderator khi kiểm tra.</summary>
    public string? ReviewNotes { get; set; }

    // --- Checklist đối chiếu 5 điều kiện (BR-C3) ---
    /// <summary>Đã đối chiếu khớp tên & MST trên Cổng ĐKDN quốc gia dangkykinhdoanh.gov.vn (Moderator).</summary>
    public bool IsNationalRegistryMatched { get; set; }

    /// <summary>Doanh nghiệp đang ở trạng thái hoạt động bình thường trên Cổng ĐKDN (Moderator).</summary>
    public bool IsBusinessOperating { get; set; }

    /// <summary>Người đăng ký là người đại diện pháp luật hoặc có giấy ủy quyền hợp lệ (Moderator).</summary>
    public bool IsRepresentativeValid { get; set; }

    /// <summary>Danh sách tài liệu đính kèm minh chứng.</summary>
    public List<CenterVerificationDocument> Documents { get; set; } = [];
}

