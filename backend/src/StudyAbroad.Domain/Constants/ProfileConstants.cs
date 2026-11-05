namespace StudyAbroad.Domain.Constants;

/// <summary>
/// [USAS-364] Các hằng số nghiệp vụ định nghĩa cho Hồ sơ tài chính và Hoạt động ngoại khóa.
/// </summary>
public static class ProfileConstants
{
    /// <summary>[USAS-640] Khóa cấu hình toàn bộ metadata cho luồng nhập hồ sơ (không hardcode)</summary>
    public const string ProfileMetadataAppSettingKey = "profile.metadata";

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

    /// <summary>[USAS-640] Mã phạm vi hoạt động/giải thưởng ở luồng 6 bước, ánh xạ sang ImpactLevels 1..5</summary>
    public static class ImpactLevelCodes
    {
        public static readonly IReadOnlyDictionary<string, int> ToLevel = new Dictionary<string, int>
        {
            ["school"] = ImpactLevels.School,
            ["district"] = ImpactLevels.District,
            ["province"] = ImpactLevels.City,
            ["national"] = ImpactLevels.National,
            ["international"] = ImpactLevels.International,
        };

        public static string? FromLevel(int? level) =>
            ToLevel.FirstOrDefault(kv => kv.Value == level).Key;
    }

    /// <summary>[USAS-640] Vai trò trong hoạt động (R theo tài liệu tính điểm)</summary>
    public static class ActivityRoles
    {
        public const string Founder = "founder";
        public const string Leader = "leader";
        public const string ViceLeader = "vice_leader";
        public const string Member = "member";

        public static readonly IReadOnlySet<string> All = new HashSet<string> { Founder, Leader, ViceLeader, Member };
    }

    /// <summary>[USAS-640] Nhu cầu học bổng</summary>
    public static class ScholarshipNeeds
    {
        public const string Mandatory = "mandatory";
        public const string Preferred = "preferred";
        public const string Optional = "optional";

        public static readonly IReadOnlySet<string> All = new HashSet<string> { Mandatory, Preferred, Optional };

        public static bool NeedsScholarship(string? need) => need is Mandatory or Preferred;
    }

    /// <summary>Cách nhập GPA ở bước 2: GPA tổng hoặc tính từ điểm môn (học bạ)</summary>
    public static class GpaModes
    {
        public const string Overall = "overall";
        public const string Transcript = "transcript";

        public static readonly IReadOnlySet<string> All = new HashSet<string> { Overall, Transcript };
    }

    /// <summary>[USAS-640] Chứng chỉ tiếng Anh chọn ở bước 3</summary>
    public static class EnglishTestTypes
    {
        public const string Ielts = "ielts";
        public const string Toefl = "toefl";
        public const string Duolingo = "duolingo";
        public const string None = "none";

        public static readonly IReadOnlySet<string> All = new HashSet<string> { Ielts, Toefl, Duolingo, None };
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
