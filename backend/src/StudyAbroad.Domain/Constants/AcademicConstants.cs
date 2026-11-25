namespace StudyAbroad.Domain.Constants;

/// <summary>
/// [USAS-365] Các hằng số định danh cho chức năng phân tích điểm học thuật và quy đổi GPA.
/// </summary>
public static class AcademicConstants
{
    // [USAS-365] Loại kết quả phân tích học thuật trong bảng analysis_results
    public const string KindGpa = "gpa";

    // [USAS-365] Phiên bản thuật toán phân tích GPA
    public const string DefaultModelVersion = "usas-gpa-v1.0";

    // [USAS-365] Khóa cấu hình bảng quy đổi thang điểm 10 trong bảng app_settings
    public const string GradeScale10AppSettingKey = "grade_scale.10";

    // [USAS-365] Nhóm môn học
    public const string GroupNaturalSciences = "natural_sciences";
    public const string GroupSocialSciences = "social_sciences";
    public const string GroupLanguages = "languages";
    public const string GroupOthers = "others";

    // [USAS-365] Đánh giá xu hướng học tập
    public const string TrendUpward = "upward";
    public const string TrendConsistent = "consistent";
    public const string TrendDownward = "downward";

    // [UC-18] Dòng lưu ý tham khảo bắt buộc hiển thị cho GPA thang 4 quy đổi
    public const string DefaultDisclaimer =
        "Điểm GPA thang 4.0 ở đây là quy đổi tham khảo, không thay cho đánh giá chính thức của trường đại học Mỹ.";
}
