namespace StudyAbroad.Domain.Constants;

/// <summary>
/// UC-42: các nhóm dữ liệu người gửi chọn chia sẻ cho trung tâm, lưu ở leads.shared_fields.
/// Trung tâm (UC-73) chỉ thấy các trường thuộc nhóm đã chọn.
/// </summary>
public static class LeadSharedFields
{
    /// <summary>Họ tên, email. Bắt buộc, trung tâm cần để liên hệ lại.</summary>
    public const string Contact = "contact";
    /// <summary>GPA, hệ học, lớp hiện tại.</summary>
    public const string Academic = "academic";
    /// <summary>Loại bài thi và điểm tiếng Anh, SAT, ACT.</summary>
    public const string Tests = "tests";
    /// <summary>Bậc học muốn học, ngành dự định.</summary>
    public const string Goals = "goals";
    /// <summary>Mức ngân sách, mức hoạt động ngoại khóa.</summary>
    public const string Budget = "budget";

    public static readonly IReadOnlyList<string> All = [Contact, Academic, Tests, Goals, Budget];
}
