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

    // Trường đưa vào lọc. TotalCostUsd = học phí + ăn ở + phí + chi khác.
    // HasIntlAid: có chính sách học bổng du học sinh thì true, chính sách "none" thì false, chưa có số thì null.
    public record FilterSchool(
        string Code,
        IReadOnlyList<string> Majors,
        string? State,
        string? Control,
        decimal? TotalCostUsd,
        bool? HasIntlAid,
        decimal? MinIelts = null,
        decimal? MinToefl = null,
        decimal? MinDuolingo = null);

    //Kết quả sau lọc
    public record FilterResult(
        FilterSchool School,
        FilterOutcome Outcome,
        IReadOnlyList<string> Flags,
        string? ExcludeReason = null,
        decimal? OverBudgetUsd = null);

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
    public static class HardFilter
    {
        public static IReadOnlyList<FilterResult> Apply(
            //Các tiêu chí lọc, danh sách các trường đưa vào lọc, dung sai ngân sách
            FilterCriteria criteria, IEnumerable<FilterSchool> schools, decimal budgetTolerance) =>
            schools.Select(s => Check(criteria, s, budgetTolerance)).ToList();

        public static FilterResult Check(FilterCriteria c, FilterSchool school, decimal budgetTolerance)
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
            var (englishFail, englishFlag) = CheckEnglish(c, school);
            if (englishFail) return Exclude(school, "ENGLISH");
            if (englishFlag is not null) flags.Add(englishFlag);

            // Trường k có chi phí -> gán cờ + giữ
            if (school.TotalCostUsd is not decimal cost)
            {
                flags.Add(FilterFlags.CostUnknown);
                return new FilterResult(school, FilterOutcome.Keep, flags);
            }

            // Nếu k có ngân sách -> gán cờ + giữ
            if(c.AnnualBudgetUsd is not decimal budget)
                return new FilterResult(school,FilterOutcome.Keep, flags);

            // Nếu ngân sách *1,1 >= cost ==> giữ 
            var ceiling = budget * (1+budgetTolerance);
            if (cost <= ceiling)
                return new FilterResult(school, FilterOutcome.Keep, flags);

            //Nếu ngân sách *1,1 < cost -> loại
            if (c.AidNeed == AidNeed.None) return Exclude(school, "BUDGET");

            //Nếu ngân sách *1,1 < cost  && trường k có học bổng -> loại
            if (school.HasIntlAid == false) return Exclude(school, "BUDGET_NO_AID");

            //Nếu ngân sách *1,1 < cost && trường k có dữ liệu học bổng -> cờ (k rõ học bổng + cần số tiền lớn)
            if (school.HasIntlAid is null) flags.Add(FilterFlags.AidUnknown);
            flags.Add(FilterFlags.NeedBigAid);
            return new FilterResult(school, FilterOutcome.AidBlock, flags, OverBudgetUsd: cost - budget);
        }

        // So từng bài với mức tối thiểu cùng loại. Một bài đạt là đủ.
        // Chỉ loại khi có cặp so được và trượt hết các cặp đó.
        private static (bool Fail, string? Flag) CheckEnglish(FilterCriteria c, FilterSchool s)
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
        //Hàm loại trường
        private static FilterResult Exclude(FilterSchool school, string reason) =>
            new(school, FilterOutcome.Excluded, [], reason);
    }
    }
