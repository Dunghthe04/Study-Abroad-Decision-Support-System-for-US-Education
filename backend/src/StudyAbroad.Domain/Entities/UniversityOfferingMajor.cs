namespace StudyAbroad.Domain.Entities;

/// <summary>Ngành mà một offering của trường đào tạo.</summary>
public class UniversityOfferingMajor
{
    public Guid UniversityOfferingId { get; set; }
    public Guid MajorId { get; set; }
    /// <summary>ngành được tính STEM ở trường này (STEM OPT phụ thuộc mã CIP trường đăng ký)</summary>
    public bool IsStem { get; set; }
    /// <summary>true: lấy từ CDS mục J1 đã xác minh; false: từ College Scorecard (PCIP), chưa xác minh</summary>
    public bool IsVerified { get; set; }
}
