using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Recommendations
{
    //Đổi hồ sơ học sinh (luồng 6 bước) thành đầu vào của lọc cứng và lượt ①. Chỉ đổi định dạng, không chấm điểm.
    public static class RecommendationInputs
    {
        public const int MaxActivities = 10;   // advisor nhận tối đa 10 hoạt động, 10 giải
        public const int MaxActivityText = 500;
        public const int MaxAwardText = 300;
        private const string AwardKind = "award";   // ProfileActivity.Kind: extracurricular | experience | award

        //Dropdown ngành ở luồng 6 bước dùng tên ngành cụ thể; trường trong DB lưu 38 nhóm ngành CIP.
        //Bảng này đổi ngành cụ thể sang nhóm CIP để lọc. "Undecided" và ngành không có trong bảng thì không lọc theo ngành.
        private static readonly Dictionary<string, string> MajorToCip = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Computer Science"] = "Computer and information sciences",
            ["Data Science"] = "Computer and information sciences",
            ["Artificial Intelligence"] = "Computer and information sciences",
            ["Software Engineering"] = "Computer and information sciences",
            ["Information Technology"] = "Computer and information sciences",
            ["Cybersecurity"] = "Computer and information sciences",
            ["Computer Engineering"] = "Engineering",
            ["Mechanical Engineering"] = "Engineering",
            ["Electrical Engineering"] = "Engineering",
            ["Civil Engineering"] = "Engineering",
            ["Chemical Engineering"] = "Engineering",
            ["Biomedical Engineering"] = "Engineering",
            ["Aerospace Engineering"] = "Engineering",
            ["Industrial Engineering"] = "Engineering",
            ["Biological Sciences"] = "Biological/life sciences",
            ["Pre-Medicine"] = "Biological/life sciences",
            ["Chemistry"] = "Physical sciences",
            ["Physics"] = "Physical sciences",
            ["Mathematics"] = "Mathematics and statistics",
            ["Business Administration"] = "Business/marketing",
            ["Finance"] = "Business/marketing",
            ["Accounting"] = "Business/marketing",
            ["Marketing"] = "Business/marketing",
            ["Economics"] = "Social sciences",
            ["Psychology"] = "Psychology",
            ["Communication"] = "Communication/journalism",
            ["Architecture"] = "Architecture",
            ["Graphic Design"] = "Visual and performing arts",
            ["Nursing"] = "Health professions and related programs",
        };

        //null = không lọc theo ngành (chưa chọn, chưa quyết định, hoặc tên ngành chưa có trong bảng)
        public static string? ToCipMajor(string? major, IReadOnlySet<string> cipFamilies)
        {
            if (string.IsNullOrWhiteSpace(major)) return null;
            var name = major.Trim();
            if (cipFamilies.Contains(name)) return name;              // học sinh chọn thẳng nhóm CIP
            return MajorToCip.TryGetValue(name, out var cip) ? cip : null;
        }

        public static FilterCriteria ToCriteria(StudentProfile p, string? cipMajor) => new(
            Major: cipMajor,
            States: p.PreferredStates,
            Control: p.TargetSchoolType is "public" or "private" ? p.TargetSchoolType : null,   // "all" = không lọc
            AnnualBudgetUsd: p.BudgetMaxUsd,                                                   // lấy đầu trên khoảng ngân sách
            AidNeed: p.ScholarshipNeed switch
            {
                ProfileConstants.ScholarshipNeeds.Mandatory => AidNeed.Required,
                ProfileConstants.ScholarshipNeeds.Preferred => AidNeed.Preferred,
                _ => AidNeed.None
            },
            Ielts: p.Ielts,
            Toefl: p.Toefl,
            Duolingo: p.Duolingo);

        //Hoạt động + giải thưởng → đầu vào lượt ①. Không gửi tên, email, số điện thoại của học sinh.
        public static AiEcRequest ToEcRequest(IEnumerable<ProfileActivity> items, DateOnly today)
        {
            var list = items.ToList();
            var activities = list
                .Where(a => a.Kind != AwardKind)
                .Take(MaxActivities)
                .Select(a => new AiEcActivity(Describe(a, MaxActivityText), Level(a.ImpactLevel), Months(a, today), RoleCode(a.Role)))
                .ToList();
            var awards = list
                .Where(a => a.Kind == AwardKind)
                .Take(MaxActivities)
                .Select(a => new AiEcAward(Describe(a, MaxAwardText), Level(a.ImpactLevel)))
                .ToList();
            return new AiEcRequest(activities, awards);
        }

        //Ghép tên, vai trò, tổ chức, mô tả thành 1 đoạn để AI đọc vai trò và phạm vi
        private static string Describe(ProfileActivity a, int maxLength)
        {
            string?[] parts = [a.Title, a.Role is { Length: > 0 } role ? $"vai trò: {role}" : null, a.Organization, a.Description];
            var text = string.Join(". ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
            if (text.Length == 0) text = "Hoạt động không có mô tả";
            return text.Length <= maxLength ? text : text[..maxLength];
        }

        //Vai trò học sinh chọn ở form (founder | leader | vice_leader | member); advisor dùng khi AI lỗi. Chữ tự do của dữ liệu cũ thì bỏ.
        private static string? RoleCode(string? role) =>
            role is not null && ProfileConstants.ActivityRoles.All.Contains(role) ? role : null;

        //Phạm vi / cấp giải 1–5; thiếu thì coi là cấp trường (thấp nhất, không nâng hồ sơ)
        private static int Level(int? impact) => Math.Clamp(impact ?? 1, 1, 5);

        //Số tháng: học sinh tự khai, nếu không có thì tính từ ngày bắt đầu đến ngày kết thúc (đang tham gia thì đến hôm nay)
        private static int Months(ProfileActivity a, DateOnly today)
        {
            if (a.DurationMonths is int declared) return Math.Clamp(declared, 0, 120);
            if (a.StartDate is not DateOnly start) return 0;
            var end = a.EndDate ?? (a.IsOngoing ? today : start);
            var months = (end.Year - start.Year) * 12 + end.Month - start.Month;
            return Math.Clamp(months, 0, 120);
        }
    }
}
