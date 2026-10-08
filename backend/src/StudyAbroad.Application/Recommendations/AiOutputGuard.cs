using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Recommendations
{
    //Câu trả lời của AI LLM: mã trường + giải thích (thứ tự AI trả về không được dùng)
    public record AiPick(string? Code, string? Reason);

    //Một trường Sau khi kiểm tra  : AiExplained = false ==> thay lời giải thích AI = đoạn sẵn
    public record GuardedPick(ScoredSchool School, string Reason, bool AiExplained);

    public class AiOutputGuard
    {
        //Giới hạn giải thích
        private const int MaxReasonLength = 600;

        //Số được phép dù ko có trong dữ liệu: mốc 24/75%, thang 4 (GPA, ngoại khóa), thang 10 (giữ cho kết quả cũ)
        private static readonly decimal[] Constants = [25m, 75m, 4m, 10m];

        //Tìm số trong câu
        private static readonly Regex NumberPattern = new(@"\d+(?:[.,]\d+)*", RegexOptions.Compiled);
        //Tìm %
        private static readonly Regex PercentPattern = new(@"(\d+(?:[.,]\d+)?)\s*%", RegexOptions.Compiled);

        //Nhận kết quả AI + danh sách trường CRM + thông tin học sinh ==> kiểm tra AI ==> tạo danh sách cuối
        //Thứ tự luôn là thứ tự SAW của CRM; AI chỉ đóng góp lời giải thích
        public static IReadOnlyList<GuardedPick> Apply(IReadOnlyList<AiPick> picks, IReadOnlyList<ScoredSchool> scored, StudentSnapshot student)
        {
            //1. Lời giải thích AI theo mã trường; mã trùng thì lấy lần đầu, mã lạ thì không bao giờ được dùng
            var reasons = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var pick in picks)
                if (pick.Code is { } code)
                    reasons.TryAdd(code.Trim(), pick.Reason);

            //2. Đi theo thứ tự CRM: giải thích AI đúng số liệu thì lấy, thiếu hoặc sai thì dùng câu soạn sẵn
            return scored.Select(school =>
                reasons.TryGetValue(school.Candidate.Code, out var reason) && IsGrounded(reason, student, school)
                    ? new GuardedPick(school, reason!.Trim(), AiExplained: true)
                    : new GuardedPick(school, ReasonTemplate.Build(student, school), AiExplained: false)).ToList();
        }

        /// <summary>Lời giải thích hợp lệ khi không rỗng, không quá dài, không nêu phần trăm đậu và mọi con số đều có trong dữ liệu.</summary>
        public static bool IsGrounded(string? reason, StudentSnapshot student, ScoredSchool school)
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > MaxReasonLength) return false;

            //Phần trăm chỉ được là mốc 25% / 75%, không được nêu tỷ lệ đậu
            foreach (Match m in PercentPattern.Matches(reason))
                if (Parse(m.Groups[1].Value) is not (25m or 75m)) return false;

            //Bỏ tên và mã trường trước khi tìm số (tên/mã có thể chứa chữ số)
            var text = reason.Replace(school.Candidate.Name, " ").Replace(school.Candidate.Code, " ");

            //Danh sách các số được xuất hiện
            var allowed = AllowedNumbers(student, school);

            //Duyệt kiểm tra từng số trong text
            foreach (Match m in NumberPattern.Matches(text))
                if (Parse(m.Value) is not { } n || !allowed.Contains(n)) return false;

            return true;

        }

        //Các con số LLM được phép dùng: của học sinh, của CHÍNH trường đó, và vài hằng số
        private static HashSet<decimal> AllowedNumbers(StudentSnapshot s, ScoredSchool r)
        {
            var c = r.Candidate;
            decimal?[] values =
            [
                s.Gpa4, s.Sat, s.AnnualBudgetUsd, s.Ielts, s.Toefl, s.Duolingo, s.ExtracurricularScore,
                c.AvgGpa4, c.Sat25, c.Sat75, c.TuitionUsd, c.LivingUsd, c.FeesUsd, r.TotalCostUsd,
                c.MinIelts, c.MinToefl, c.MinDuolingo,
            ];
            var set = values.Where(v => v is not null).Select(v => v!.Value).ToHashSet();
            set.UnionWith(Constants);
            return set;
        }

        //"57,168" / "57.168" → 57168;  "3.5" / "3,5" → 3.5;  không đọc được → null
        private static decimal? Parse(string token)
        {
            //Nếu là hàng nghìn => đổi thành ., còn nếu thập phân => ,
            var normalized = Regex.IsMatch(token, @"^\d{1,3}([.,]\d{3})+$")
                ? token.Replace(",", "").Replace(".", "")   // dấu phân cách hàng nghìn
                : token.Replace(",", ".");                    // dấu thập phân kiểu Việt Nam
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
        }

    }
}
