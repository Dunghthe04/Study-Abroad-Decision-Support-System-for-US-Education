using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Yêu cầu liên hệ người dùng gửi cho trung tâm (backlog #24).</summary>
public class Lead : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid StudyCenterId { get; set; }
    public Guid? ConsentId { get; set; }
    public string? StudyLevel { get; set; }
    public string? ContactPhone { get; set; }
    public string? Message { get; set; }
    /// <summary>Có chia sẻ nhóm nào ngoài contact không. Giữ cho UC-73 cũ; nguồn chính là <see cref="SharedFields"/>.</summary>
    public bool SharedProfile { get; set; }
    /// <summary>UC-42: phụ huynh gửi thay con thì là id phụ huynh; học sinh tự gửi thì bằng UserId. Dùng để đếm giới hạn gửi.</summary>
    public Guid? SubmittedByUserId { get; set; }
    /// <summary>UC-42: các key trong LeadSharedFields mà người gửi đồng ý chia sẻ.</summary>
    public List<string> SharedFields { get; set; } = [];
    /// <summary>UC-42: học sinh 16–17 tuổi đã tích "hiểu trung tâm sẽ nhận dữ liệu và liên hệ lại".</summary>
    public bool AcknowledgedCenterContact { get; set; }
    /// <summary>new | contacted | closed | spam</summary>
    public string Status { get; set; } = "new";
}
