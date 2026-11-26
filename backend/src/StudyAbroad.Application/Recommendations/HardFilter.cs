using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Recommendations
{
    // Mức cần học bổng ở form. Bắt buộc và Nên có cho cùng tập trường, chỉ khác câu chữ ở màn kết quả.
    public enum AidNeed
    {
        None,       // Không cần
        Preferred,  // Nên có
        Required    // Bắt buộc
    }

    public enum FilterOutcome
    {
        Keep,      // trong trần ngân sách
        AidBlock,  // vượt trần nhưng có học bổng cho du học sinh
        Excluded   // loại
    }

    //Tiêu chí lọc của sinh viên
    public record FilterCriteria(
        string? Major,
        IReadOnlyList<string> States,
        string? Control,
        decimal? AnnualBudgetUsd,
        AidNeed AidNeed,
        decimal? Ielts = null,
        decimal? Toefl = null,
        decimal? Duolingo = null);

    // Trường đưa vào lọc. TotalCostUsd = AnnualCost (học phí + ăn ở + phí + chi khác + bảo hiểm y tế).
    // HasIntlAid: có chính sách học bổng du học sinh thì true, chính sách "none" thì false, chưa có số thì null.
    // TuitionUsd, IntlAidAvgUsd: để trừ hỗ trợ tài chính trung bình khi chi phí vượt ngưỡng (UC-22 4a).
    public record FilterSchool(
        string Code,
        IReadOnlyList<string> Majors,
        string? State,
        string? Control,
        decimal? TotalCostUsd,
        bool? HasIntlAid,
        decimal? MinIelts = null,
        decimal? MinToefl = null,
        decimal? MinDuolingo = null,
        decimal? TuitionUsd = null,
        decimal? IntlAidAvgUsd = null);

    // Cấu hình cổng tiếng Anh (B4). Mọi con số chỉnh được, không ghi cứng trong hàm tính.
    public record EnglishOptions(
        decimal ToeflPointsPerBand = 15m,       // 15 điểm TOEFL iBT tương đương 1 band
        decimal DuolingoPointsPerBand = 25m,    // 25 điểm Duolingo tương đương 1 band
        decimal ExcludeFromShortfall = 1.0m);   // thiếu từ chừng này band trở lên thì loại

    //Kết quả sau lọc. EnglishShortfall: mức thiếu tiếng Anh theo band (0 là đạt), null nếu không so được.
    public record FilterResult(
        FilterSchool School,
        FilterOutcome Outcome,
        IReadOnlyList<string> Flags,
        string? ExcludeReason = null,
        decimal? OverBudgetUsd = null,
        decimal? EnglishShortfall = null);

    public static class FilterFlags
    {
        public const string MajorUnknown = "MAJOR_UNKNOWN";
        public const string ControlUnknown = "CONTROL_UNKNOWN";
        public const string CostUnknown = "COST_UNKNOWN";
        public const string NeedBigAid = "NEED_BIG_AID";
        public const string EnglishMinUnknown = "ENGLISH_MIN_UNKNOWN";
        public const string EnglishNotComparable = "ENGLISH_NOT_COMPARABLE";
        public const string EnglishNear = "ENGLISH_NEAR";   // thiếu dưới 1 band: giữ, cần đạt mức tối thiểu trước hạn nộp
    }

    // Lý do loại do tiếng Anh (FilterResult.ExcludeReason)
    public static class EnglishExclude
    {
        public const string Short = "ENGLISH";            // thiếu từ 1 band trở lên so với mức tối thiểu của trường
        public const string NoCertificate = "ENGLISH_NO_CERT";   // học sinh chưa có chứng chỉ tiếng Anh nào
    }
    public static class HardFilter
    {
        public static IReadOnlyList<FilterResult> Apply(
            //Các tiêu chí lọc, danh sách các trường đưa vào lọc, dung sai ngân sách
            FilterCriteria criteria, IEnumerable<FilterSchool> schools, decimal budgetTolerance, EnglishOptions? english = null) =>
            schools.Select(s => Check(criteria, s, budgetTolerance, english)).ToList();

        public static FilterResult Check(FilterCriteria c, FilterSchool school, decimal budgetTolerance, EnglishOptions? english = null)
        {
            var flags = new List<string>();
            // Ngành: học sinh chưa chọn thì bỏ qua. Trường không khai ngành thì giữ, gắn cờ.
            if (!string.IsNullOrWhiteSpace(c.Major))
            {
                //Sinh viên chọn ngành - mà trường ko có ngành nào
                if (school.Majors.Count == 0) flags.Add(FilterFlags.MajorUnknown);
                //Nếu trường ko có ngành trùng -> loại
                else if (!school.Majors.Contains(c.Major, StringComparer.OrdinalIgnoreCase))
                    return Exclude(school, "MAJOR");
            }

            // Bang: đã chọn mà trường không có bang, hoặc bang nằm ngoài danh sách, thì loại.
            if (c.States.Count > 0 && (school.State is null || !c.States.Contains(school.State, StringComparer.OrdinalIgnoreCase)))
                return Exclude(school, "STATE");

            // Công lập / tư thục. "Không lọc" thì truyền Control = null.
            if (!string.IsNullOrWhiteSpace(c.Control))
            {
                if (school.Control is null) flags.Add(FilterFlags.ControlUnknown);
                else if (!string.Equals(school.Control, c.Control, StringComparison.OrdinalIgnoreCase))
                    return Exclude(school, "CONTROL");
            }
            // Tiếng Anh (B4): học sinh chưa có chứng chỉ, hoặc thiếu từ 1 band so với mức tối thiểu của trường thì loại;
            // thiếu dưới 1 band thì giữ và gắn cờ. Điểm tiếng Anh còn tính vào fit score.
            var gate = CheckEnglish(c, school, english ?? new EnglishOptions());
            if (gate.ExcludeReason is not null) return Exclude(school, gate.ExcludeReason, gate.Shortfall);
            if (gate.Flag is not null) flags.Add(gate.Flag);

            // Trường k có chi phí -> gán cờ + giữ
            if (school.TotalCostUsd is not decimal cost)
            {
                flags.Add(FilterFlags.CostUnknown);
                return new FilterResult(school, FilterOutcome.Keep, flags, EnglishShortfall: gate.Shortfall);
            }

            // Nếu k có ngân sách -> gán cờ + giữ
            if(c.AnnualBudgetUsd is not decimal budget)
                return new FilterResult(school,FilterOutcome.Keep, flags, EnglishShortfall: gate.Shortfall);

            // Nếu ngân sách *1,1 >= cost ==> giữ
            var ceiling = budget * (1+budgetTolerance);
            if (cost <= ceiling)
                return new FilterResult(school, FilterOutcome.Keep, flags, EnglishShortfall: gate.Shortfall);

            // Vượt ngưỡng và không cần học bổng (4c) -> loại với lý do ngân sách
            if (c.AidNeed == AidNeed.None) return Exclude(school, "BUDGET");

            // Vượt ngưỡng và cần học bổng (4a): trừ mức hỗ trợ trung bình cho du học sinh. Không có số dùng được -> loại với lý do ngân sách
            if (UsableAid(school) is not decimal aid) return Exclude(school, "BUDGET_NO_AID");

            // Chi phí sau hỗ trợ vẫn vượt ngưỡng -> loại; không vượt -> khối "Cần học bổng lớn"
            if (cost - aid > ceiling) return Exclude(school, "BUDGET");
            flags.Add(FilterFlags.NeedBigAid);
            return new FilterResult(school, FilterOutcome.AidBlock, flags, OverBudgetUsd: cost - budget, EnglishShortfall: gate.Shortfall);
        }

        // Mức hỗ trợ trung bình cho du học sinh dùng để trừ (UC-22 4a). Không dùng được (trả null) khi: trường không có học bổng cho du học sinh,
        // chưa có dữ liệu, không có số tiền, hoặc số tiền lớn hơn học phí (dữ liệu bất thường, coi như không có dữ liệu).
        private static decimal? UsableAid(FilterSchool school)
        {
            if (school.HasIntlAid != true) return null;
            if (school.IntlAidAvgUsd is not decimal avg || avg <= 0) return null;
            if (school.TuitionUsd is decimal tuition && avg > tuition) return null;
            return avg;
        }

        // Kết quả cổng tiếng Anh: mức thiếu (band), cờ gắn thêm, hoặc lý do loại.
        private readonly record struct EnglishCheck(decimal? Shortfall, string? Flag, string? ExcludeReason);

        // So từng bài thi học sinh có với mức tối thiểu CÙNG BÀI của trường, đổi sang band. Một bài đạt là đủ nên lấy bài ít thiếu nhất.
        //   IELTS: thiếu bao nhiêu band; TOEFL, Duolingo: số điểm thiếu chia số điểm tương đương 1 band.
        //   Mức thiếu 0 = đạt · dưới ExcludeFromShortfall = giữ kèm cờ · từ ExcludeFromShortfall trở lên = loại.
        private static EnglishCheck CheckEnglish(FilterCriteria c, FilterSchool s, EnglishOptions o)
        {
            // Học sinh chưa có chứng chỉ nào: chưa nộp được trường nào
            if (c.Ielts is null && c.Toefl is null && c.Duolingo is null)
                return new EnglishCheck(null, null, EnglishExclude.NoCertificate);
            // Trường không công bố mức tối thiểu nào: không đủ căn cứ để loại, giữ và gắn cờ
            if (s.MinIelts is null && s.MinToefl is null && s.MinDuolingo is null)
                return new EnglishCheck(null, FilterFlags.EnglishMinUnknown, null);

            var gaps = new List<decimal>();
            if (c.Ielts is decimal ielts && s.MinIelts is decimal minIelts)
                gaps.Add(minIelts - ielts);
            if (c.Toefl is decimal toefl && s.MinToefl is decimal minToefl)
                gaps.Add((minToefl - toefl) / o.ToeflPointsPerBand);
            if (c.Duolingo is decimal duolingo && s.MinDuolingo is decimal minDuolingo)
                gaps.Add((minDuolingo - duolingo) / o.DuolingoPointsPerBand);

            // Có điểm và trường có mức tối thiểu nhưng không cùng loại bài thi: chưa so được, giữ và gắn cờ
            if (gaps.Count == 0)
                return new EnglishCheck(null, FilterFlags.EnglishNotComparable, null);

            var shortfall = Math.Max(0m, gaps.Min());
            if (shortfall == 0m) return new EnglishCheck(0m, null, null);
            return shortfall >= o.ExcludeFromShortfall
                ? new EnglishCheck(shortfall, null, EnglishExclude.Short)
                : new EnglishCheck(shortfall, FilterFlags.EnglishNear, null);
        }

        //Hàm loại trường
        private static FilterResult Exclude(FilterSchool school, string reason, decimal? englishShortfall = null) =>
            new(school, FilterOutcome.Excluded, [], reason, EnglishShortfall: englishShortfall);
    }
    }
