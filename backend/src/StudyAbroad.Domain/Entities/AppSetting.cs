using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Cấu hình admin chỉnh được, lưu JSON: trọng số gợi ý (#6), bảng quy đổi điểm (#13), trọng số môn theo ngành (#15), luật kiểm duyệt (#28).</summary>
public class AppSetting : BaseEntity
{
    /// <summary>recommend.weights | grade_scale.10 | major_weights | moderation.rules</summary>
    public string Key { get; set; } = string.Empty;
    public string ValueJson { get; set; } = "{}";
    public string? Description { get; set; }
}
