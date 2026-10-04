namespace StudyAbroad.Domain.Constants;

/// <summary>
/// [USAS-364] Các hằng số nghiệp vụ định nghĩa cho Hồ sơ tài chính và Hoạt động ngoại khóa.
/// </summary>
public static class ProfileConstants
{
    public static class FundingSources
    {
        public const string FamilySupport = "family_support";     // Gia đình tài trợ
        public const string PersonalSavings = "personal_savings"; // Tiết kiệm cá nhân
        public const string BankLoan = "bank_loan";               // Vay vốn ngân hàng du học
        public const string Scholarship = "scholarship";          // Trông đợi vào học bổng
        public const string Other = "other";                      // Khác

        public static readonly IReadOnlySet<string> All = new HashSet<string>
        {
            FamilySupport, PersonalSavings, BankLoan, Scholarship, Other
        };
    }

    public static class ImpactLevels
    {
        public const int School = 1;       // Cấp Trường / CLB
        public const int District = 2;     // Cấp Quận / Huyện
        public const int City = 3;         // Cấp Tỉnh / Thành phố
        public const int National = 4;     // Cấp Quốc gia
        public const int International = 5;// Cấp Quốc tế

        public static bool IsValid(int level) => level >= 1 && level <= 5;
    }

    public static class AchievementCategories
    {
        public const string Award = "award";                 // Giải thưởng học thuật / năng khiếu
        public const string Research = "research";           // Đề tài nghiên cứu khoa học
        public const string Internship = "internship";       // Thực tập / Dự án thực tế
        public const string Certification = "certification"; // Chứng chỉ chuyên môn
        public const string Other = "other";

        public static readonly IReadOnlySet<string> All = new HashSet<string>
        {
            Award, Research, Internship, Certification, Other
        };
    }

    public static class ValidationLimits
    {
        public const decimal MaxAnnualBudget = 10_000_000m; // 10 triệu USD/năm
        public const int MaxActivityNameLength = 150;
        public const int MaxRoleLength = 100;
        public const int MaxOrganizationLength = 150;
        public const int MaxDescriptionLength = 1000;
        public const int MaxTitleLength = 150;
    }
}
