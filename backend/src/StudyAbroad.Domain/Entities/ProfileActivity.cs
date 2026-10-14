using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Ngoại khóa, kinh nghiệm hoặc giải thưởng trong hồ sơ (backlog #3).</summary>
public class ProfileActivity : BaseEntity
{
    public Guid StudentProfileId { get; set; }
    /// <summary>extracurricular | experience | award</summary>
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Organization { get; set; }
    /// <summary>vai trò; luồng 6 bước lưu mã founder | leader | vice_leader | member (null với giải thưởng). Dữ liệu cũ có thể là chữ tự do</summary>
    public string? Role { get; set; }
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    /// <summary>[USAS-640] năm đạt giải (chỉ với Kind = award)</summary>
    public int? AwardYear { get; set; }
    /// <summary>Phạm vi / cấp độ: 1 Trường, 2 Quận/huyện, 3 Tỉnh/TP, 4 Quốc gia, 5 Quốc tế</summary>
    public int? ImpactLevel { get; set; }
    /// <summary>[USAS-364] Số tháng tham gia học sinh tự khai ở form; null thì tính từ StartDate/EndDate</summary>
    public int? DurationMonths { get; set; }
    /// <summary>[USAS-364] Đang tiếp tục tham gia</summary>
    public bool IsOngoing { get; set; }
}
