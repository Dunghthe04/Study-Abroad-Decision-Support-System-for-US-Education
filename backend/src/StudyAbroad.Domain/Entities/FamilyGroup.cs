using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Nhóm gói Gia đình của một chủ gói, sống qua các kỳ của làn family (UC-126..131). Mỗi chủ tối đa 1 nhóm active.</summary>
public class FamilyGroup : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    /// <summary>active | ended (FamilyGroupStatuses)</summary>
    public string Status { get; set; } = string.Empty;
    public DateTime? EndedAt { get; set; }
    public string? EndedReason { get; set; }
}
