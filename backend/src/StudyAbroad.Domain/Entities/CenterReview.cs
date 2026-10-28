using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Đánh giá trung tâm tư vấn từ 1 đến 5 sao (UC-41, UC-45, UC-57).</summary>
public class CenterReview : BaseEntity
{
    public Guid StudyCenterId { get; set; }
    public Guid UserId { get; set; }
    public Guid? LeadId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsVerified { get; set; } = false;
    /// <summary>visible | hidden | removed</summary>
    public string Status { get; set; } = "visible";
}
