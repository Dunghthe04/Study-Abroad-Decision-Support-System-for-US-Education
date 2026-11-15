using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// Tài liệu đính kèm hồ sơ xác minh trung tâm tư vấn (BR-C5: chỉ moderator xem, ghi audit, bảo mật).
/// </summary>
public class CenterVerificationDocument : BaseEntity
{
    public Guid RequestId { get; set; }
    public Guid CenterId { get; set; }

    /// <summary>
    /// business_registration | power_of_attorney | partnership_contract | consulting_license
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = "application/pdf";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

