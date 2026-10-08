using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Recommendations
{
    //Kết quả chấm ngoại khóa lưu trong analysis_results.result_json; InputHash để biết hoạt động có đổi không
    public record ExtracurricularAnalysis(string InputHash, decimal Score, bool AiUsed, IReadOnlyList<ScoredActivity> Activities);

    /// <summary>Đổi hoạt động trong hồ sơ (profile_activities) thành dữ liệu gửi advisor.</summary>
    public static class ExtracurricularInputs
    {
        public const string AnalysisKind = "extracurricular";
        //Đổi công thức hoặc cách đọc thì tăng version: hash đổi theo, lần bấm sau tính lại hết
        public const string ModelVersion = "ec-v2";   // v2: cộng điểm thưởng giải thưởng

        //Loại hoạt động tính vào điểm ngoại khóa: hoạt động, kinh nghiệm tính bằng công thức q; giải thưởng cộng điểm thưởng
        public static readonly IReadOnlySet<string> CountedKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "extracurricular",
            "experience",
            "award",
        };

        public const int MaxActivities = 20;   // advisor nhận tối đa 20 hoạt động

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public static IReadOnlyList<ExtracurricularActivityInput> From(IEnumerable<ProfileActivity> activities, DateOnly today) =>
            activities
                .Where(a => CountedKinds.Contains(a.Kind))
                .OrderBy(a => a.StartDate ?? DateOnly.MaxValue).ThenBy(a => a.Id)   // thứ tự cố định để hash ổn định
                .Take(MaxActivities)
                .Select(a => IsAward(a)
                    // Giải thưởng: cột role đang chứa loại thành tích (award, research...), không phải vai trò
                    ? new ExtracurricularActivityInput(a.Id.ToString(), Name(a), null, Cut(a.Organization, 200),
                        Cut(a.Description, 1000), ImpactLevel: null, Months: null, Kind: "award")
                    : new ExtracurricularActivityInput(a.Id.ToString(), Name(a), Role(a), Cut(a.Organization, 200),
                        Cut(a.Description, 1000),
                        ImpactLevel: null,   // TODO: đổi thành a.ImpactLevel khi bảng profile_activities có cột impact_level
                        Months(a.StartDate, a.EndDate, today)))
                .ToList();

        //Thành tích loại "internship" (thực tập) là kinh nghiệm, tính như hoạt động; các loại còn lại là giải thưởng
        private static bool IsAward(ProfileActivity a) =>
            a.Kind.Equals("award", StringComparison.OrdinalIgnoreCase) && !string.Equals(a.Role, "internship", StringComparison.OrdinalIgnoreCase);

        private static string? Role(ProfileActivity a) =>
            string.Equals(a.Role, "internship", StringComparison.OrdinalIgnoreCase) ? "Thực tập sinh" : Cut(a.Role, 100);

        private static string Name(ProfileActivity a) => Cut(a.Title, 200) ?? "(không tên)";

        //Số tháng tham gia; chưa có ngày kết thúc = vẫn đang tham gia, tính đến hôm nay; không có ngày bắt đầu → null
        public static int? Months(DateOnly? start, DateOnly? end, DateOnly today)
        {
            if (start is not { } s) return null;
            var e = end ?? today;
            return Math.Max(0, (e.Year - s.Year) * 12 + e.Month - s.Month);
        }

        //Dấu vân tay của danh sách hoạt động: giống nhau thì dùng lại kết quả cũ, không gọi LLM
        public static string Hash(IReadOnlyList<ExtracurricularActivityInput> inputs)
        {
            var bytes = Encoding.UTF8.GetBytes(ModelVersion + JsonSerializer.Serialize(inputs, Json));
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        //Advisor giới hạn độ dài từng ô; cắt bớt thay vì để cả request bị từ chối
        private static string? Cut(string? text, int max)
        {
            var t = text?.Trim();
            return t is { Length: > 0 } ? (t.Length > max ? t[..max] : t) : null;
        }
    }
}
