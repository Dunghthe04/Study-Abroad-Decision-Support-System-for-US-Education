using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Sự đồng ý của người dùng: xử lý dữ liệu, chia sẻ hồ sơ cho trung tâm, nội quy diễn đàn (backlog #17, #24, #27).</summary>
public class Consent : BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>privacy | share_with_center | forum_rules</summary>
    public string Purpose { get; set; } = string.Empty;
    public bool Granted { get; set; }
    public string? PolicyVersion { get; set; }
}
