using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Sự đồng ý của người dùng: xử lý dữ liệu, chia sẻ hồ sơ cho trung tâm, nội quy diễn đàn (backlog #17, #24, #27).</summary>
public class Consent : BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>privacy | share_with_center | forum_rules | child_data_processing</summary>
    public string Purpose { get; set; } = string.Empty;
    public bool Granted { get; set; }
    public string? PolicyVersion { get; set; }
    /// <summary>Người bấm đồng ý khi khác chủ thể dữ liệu (phụ huynh đồng ý thay con). Null = chủ thể tự đồng ý.</summary>
    public Guid? GrantedByUserId { get; set; }
    /// <summary>IP lúc đồng ý, để kiểm chứng sự đồng ý (Nghị định 356/2025 Điều 6).</summary>
    public string? GrantedIp { get; set; }
}
