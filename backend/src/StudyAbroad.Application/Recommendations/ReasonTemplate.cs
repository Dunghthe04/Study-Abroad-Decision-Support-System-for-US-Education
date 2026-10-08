using System.Globalization;

namespace StudyAbroad.Application.Recommendations
{
    /// <summary>
    /// Giải thích dựng từ số liệu có sẵn. Dùng khi chưa có AI, AI lỗi, hoặc AI viết sai số.
    /// Chỉ in số có trong dữ liệu → luôn đúng tiêu chí "mọi con số khớp dữ liệu".
    /// </summary>
    public static class ReasonTemplate
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        public static string Build(StudentSnapshot s, ScoredSchool r)
        {
            var parts = new List<string> { Academic(s, r) };

            if (r.English == EnglishStatus.BelowMin)
                parts.Add($"Chưa đạt điều kiện tiếng Anh tối thiểu của trường ({EnglishMinimums(r.Candidate)}).");

            parts.Add((r.TotalCostUsd, s.AnnualBudgetUsd) switch
            {
                ({ } t, { } b) => $"Chi phí ước tính khoảng ${Money(t)}/năm, ngân sách của bạn ${Money(b)}/năm.",
                ({ } t, null) => $"Chi phí ước tính khoảng ${Money(t)}/năm.",
                _ => "Chưa có dữ liệu chi phí, hãy kiểm tra trên website của trường.",
            });

            return string.Join(" ", parts);
        }

        //Câu về học thuật: so GPA, SAT với số liệu của trường → nhóm
        private static string Academic(StudentSnapshot s, ScoredSchool r)
        {
            if (r.OpenAdmission) return "Trường tuyển sinh mở (open admission) → nhóm An toàn (Safety).";

            var c = r.Candidate;
            var facts = new List<string>();
            if (s.Gpa4 is { } g && c.AvgGpa4 is { } avg)
                facts.Add($"GPA của bạn {g.ToString("0.00", Us)} so với GPA trung bình của trường {avg.ToString("0.00", Us)}");
            if (s.Sat is { } sat && c.Sat25 is { } p25 && c.Sat75 is { } p75)
                facts.Add($"SAT {sat} so với khoảng {p25}–{p75} của sinh viên trúng tuyển");

            return r.AdmissionCategory switch
            {
                AdmissionCategory.Safety => $"{string.Join("; ", facts)} → nhóm An toàn (Safety).",
                AdmissionCategory.Match => $"{string.Join("; ", facts)} → nhóm Vừa sức (Match).",
                AdmissionCategory.Reach => $"{string.Join("; ", facts)} → nhóm Thử thách (Reach).",
                _ => "Trường chưa công bố đủ GPA/SAT để xếp nhóm.",
            };
        }

        //Liệt kê mức tiếng Anh tối thiểu trường công bố, ví dụ "IELTS ≥ 6.5, TOEFL ≥ 80"
        private static string EnglishMinimums(SchoolCandidate c)
        {
            var mins = new List<string>();
            if (c.MinIelts is { } i) mins.Add($"IELTS ≥ {i.ToString("0.0", Us)}");
            if (c.MinToefl is { } t) mins.Add($"TOEFL ≥ {t.ToString("0", Us)}");
            if (c.MinDuolingo is { } d) mins.Add($"Duolingo ≥ {d.ToString("0", Us)}");
            return string.Join(", ", mins);
        }

        private static string Money(decimal v) => v.ToString("N0", Us);   // 50000 → "50,000"

        public static string CategoryCode(AdmissionCategory category) => category switch
        {
            AdmissionCategory.Reach => "reach",
            AdmissionCategory.Match => "match",
            AdmissionCategory.Safety => "safety",
            _ => "insufficient_data",
        };

        public static string EnglishCode(EnglishStatus status) => status switch
        {
            EnglishStatus.Met => "met",
            EnglishStatus.BelowMin => "below_min",
            EnglishStatus.Unknown => "unknown",
            _ => "no_score",
        };
    }
}
