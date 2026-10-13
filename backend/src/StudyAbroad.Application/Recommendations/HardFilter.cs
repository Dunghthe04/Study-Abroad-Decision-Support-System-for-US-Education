using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Recommendations
{
    // Mức cần học bổng
    public enum AidNeed
    {
        None,
        Preferred,
        Required
    }

    //Kết quả lọc của 1 trường
    public enum FilterOutcome
    {
        Keep, // giữ
        AidBlock, // vượt ngân sách nhưng giữ ở khối riêng "cần học bổng lớn"
        Excluded // loại
    }

    // Tiêu chí lọc từ form
    public record FilterCriteria(
        string? Major,
        IReadOnlyList<string> States,
        string? Control, // public/ private/ ko lọc
        decimal? AnnualBudgetUsd,
        AidNeed AidNeed,
        decimal? Ielts = null,
        decimal? Toefl = null,
        decimal? Duolingo = null);

    public record FilterResult(
        SchoolCandidate School,
        FilterOutcome Outcome,
        IReadOnlyList<string> Flags,
        string? ExcludeReason = null,   // chỉ có khi bị loại: MAJOR, STATE, CONTROL, ENGLISH, BUDGET, BUDGET_NO_AID
        decimal? OverBudgetUsd = null); // chỉ có ở khối học bổng: chi phí − ngân sách

    //Mã cờ: thiếu dữ liệu thì giữ trường và gắn cờ, không loại
    public static class FilterFlags
    {
        public const string MajorUnknown = "MAJOR_UNKNOWN";
        public const string ControlUnknown = "CONTROL_UNKNOWN";
        public const string CostUnknown = "COST_UNKNOWN";
        public const string NeedBigAid = "NEED_BIG_AID";
        public const string AidUnknown = "AID_UNKNOWN";
        public const string EnglishNoScore = "ENGLISH_NO_SCORE";
        public const string EnglishMinUnknown = "ENGLISH_MIN_UNKNOWN";
        public const string EnglishNotComparable = "ENGLISH_NOT_COMPARABLE";
    }

    //Tầng 1: lọc cứng. Không chấm điểm, mỗi trường ra: giữ / khối học bổng / loại
    public static class HardFilter
    {
        public static IReadOnlyList<FilterResult> Apply(FilterCriteria criteria, IEnumerable<SchoolCandidate> schools, decimal budgetTolerance)
        {
            return schools.Select(s => Check(criteria, s, budgetTolerance)).ToList();
        }

        public static FilterResult Check(FilterCriteria c, SchoolCandidate school, decimal budgetTolerance)
        {
            var flags = new List<string>();

            //1. Lọc ngành học , trường không có ngành = loại
            if (!string.IsNullOrWhiteSpace(c.Major))
            {
                if (school.Majors.Count == 0) flags.Add(FilterFlags.MajorUnknown);
                else if (!school.Majors.Contains(c.Major, StringComparer.OrdinalIgnoreCase)) return Exclude(school, "MAJOR");
            }

            //2. Lọc bang
            if (c.States.Count > 0 && (school.State is null || !c.States.Contains(school.State, StringComparer.OrdinalIgnoreCase)))
                return Exclude(school, "STATE");

            //3. Lọc trường công/ tư
            if (!string.IsNullOrWhiteSpace(c.Control))
            {
                if (school.Control is null) flags.Add(FilterFlags.ControlUnknown);
                else if (!string.Equals(school.Control, c.Control, StringComparison.OrdinalIgnoreCase)) return Exclude(school, "CONTROL");
            }

            // 4. Tiếng Anh: chỉ lọc, không cộng điểm
            var (englishFail, englishFlag) = CheckEnglish(c, school);
            if (englishFail) return Exclude(school, "ENGLISH");
            if (englishFlag is not null) flags.Add(englishFlag);

            //5. Ngân sách + mức học bổng
            var cost = school.TotalCostUsd;
            if (cost is null)
            {
                flags.Add(FilterFlags.CostUnknown);
                return new FilterResult(school, FilterOutcome.Keep, flags);
            }

            //Nếu giá trị kp decimal
            if (c.AnnualBudgetUsd is not decimal budget) return new FilterResult(school, FilterOutcome.Keep, flags);

            var ceiling = budget * (1 + budgetTolerance); // trần = ngân sách x1,1
            //nếu học phí < ngân sách cho phép
            if (cost <= ceiling) return new FilterResult(school, FilterOutcome.Keep, flags);

            //nếu vượt trần
            if (c.AidNeed == AidNeed.None) return Exclude(school, "BUDGET");
            if (school.HasIntlAid == false) return Exclude(school, "BUDGET_NO_AID");
            if (school.HasIntlAid is null) flags.Add(FilterFlags.AidUnknown);
            flags.Add(FilterFlags.NeedBigAid);
            return new FilterResult(school, FilterOutcome.AidBlock, flags, OverBudgetUsd: cost - budget);
        }

        //So từng bài cùng loại (IELTS với IELTS...). Đạt 1 bài là đủ.
        //Chỉ loại khi có ít nhất 1 cặp so được và trượt hết các cặp đó.
        private static (bool Fail, string? Flag) CheckEnglish(FilterCriteria c, SchoolCandidate s)
        {
            (decimal? Student, decimal? Min)[] pairs =
            [
                (c.Ielts, s.MinIelts),
                (c.Toefl, s.MinToefl),
                (c.Duolingo, s.MinDuolingo)
            ];

            if (pairs.All(p => p.Student is null)) return (false, FilterFlags.EnglishNoScore);
            if (pairs.All(p => p.Min is null)) return (false, FilterFlags.EnglishMinUnknown);

            var comparable = pairs.Where(p => p.Student is not null && p.Min is not null).ToList();
            if (comparable.Count == 0) return (false, FilterFlags.EnglishNotComparable);

            return comparable.Any(p => p.Student >= p.Min) ? (false, null) : (true, null);
        }

        private static FilterResult Exclude(SchoolCandidate s, string reason) =>
            new(s, FilterOutcome.Excluded, [], reason);
    }
}
