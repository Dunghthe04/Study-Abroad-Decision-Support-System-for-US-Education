using System.Globalization;

namespace StudyAbroad.Application.Recommendations
{
    //Lý do xếp nhóm + ưu điểm + nhược điểm của hồ sơ với một trường. Code dựng từ số liệu nên luôn đúng, không cần chờ AI
    public record ReasonParts(string CategoryReason, IReadOnlyList<string> Strengths, IReadOnlyList<string> Weaknesses);

    public static class ReasonBreakdown
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        public static ReasonParts Build(StudentSnapshot s, ScoredSchool r, decimal gpaBand)
        {
            var strengths = new List<string>();
            var weaknesses = new List<string>();
            var c = r.Candidate;

            //Học thuật: cùng ngưỡng với AdmissionCategorizer (GPA ± band, SAT mốc 25%/75%)
            if (s.Gpa4 is { } g && c.AvgGpa4 is { } avg)
            {
                var diff = g - avg;
                if (diff >= 0) strengths.Add($"GPA {Gpa(g)} {(diff == 0 ? "bằng" : "cao hơn")} GPA trung bình {Gpa(avg)} của trường");
                else weaknesses.Add($"GPA {Gpa(g)} thấp hơn GPA trung bình {Gpa(avg)} của trường {Gpa(-diff)} điểm");
            }
            if (s.Sat is { } sat && c.Sat25 is { } p25 && c.Sat75 is { } p75)
            {
                if (sat >= p75) strengths.Add($"SAT {sat} từ mốc 75% ({p75}) trở lên");
                else if (sat >= p25) strengths.Add($"SAT {sat} nằm trong khoảng 25–75% ({p25}–{p75})");
                else weaknesses.Add($"SAT {sat} dưới mốc 25% ({p25})");
            }

            //Tiếng Anh là điều kiện đầu vào
            switch (r.English)
            {
                case EnglishStatus.Met: strengths.Add($"Tiếng Anh đạt yêu cầu ({EnglishFacts(s, c)})"); break;
                case EnglishStatus.BelowMin: weaknesses.Add($"Tiếng Anh chưa đạt mức tối thiểu ({EnglishFacts(s, c)})"); break;
                case EnglishStatus.NoScore: weaknesses.Add("Chưa có điểm tiếng Anh (IELTS/TOEFL/Duolingo)"); break;
            }

            //Tài chính
            if (s.AnnualBudgetUsd is { } budget)
            {
                if (r.TotalCostUsd is { } cost)
                {
                    if (cost <= budget) strengths.Add($"Chi phí ${Money(cost)}/năm nằm trong ngân sách ${Money(budget)}");
                    else weaknesses.Add($"Chi phí ${Money(cost)}/năm vượt ngân sách ${Money(budget)} khoảng ${Money(cost - budget)}");
                }
                else weaknesses.Add("Trường chưa công bố chi phí, cần hỏi lại trường");
            }

            //Ngoại khóa không dùng để xếp nhóm, chỉ để xếp hạng trong nhóm
            if (s.ExtracurricularScore is { } ec)
            {
                if (ec >= 3) strengths.Add($"Ngoại khóa mạnh ({Score(ec)}/4)");
                else if (ec < 2) weaknesses.Add($"Ngoại khóa còn mỏng ({Score(ec)}/4)");
            }

            return new ReasonParts(CategoryReason(s, r, gpaBand), strengths, weaknesses);
        }

        //Vì sao trường thuộc nhóm này: nêu tiêu chí quyết định nhóm và quy tắc "lấy mức thấp hơn"
        private static string CategoryReason(StudentSnapshot s, ScoredSchool r, decimal band)
        {
            var c = r.Candidate;
            if (r.OpenAdmission) return "An toàn — trường tuyển sinh mở (open admission), không xét GPA/SAT đầu vào.";
            if (r.AdmissionCategory == AdmissionCategory.InsufficientData)
                return s.Gpa4 is null && s.Sat is null
                    ? "Chưa đủ dữ liệu — hồ sơ của bạn chưa có GPA thang 4 hoặc SAT để so với trường."
                    : "Chưa đủ dữ liệu — trường chưa công bố GPA trung bình hay khoảng SAT để so với hồ sơ.";

            var label = r.AdmissionCategory switch
            {
                AdmissionCategory.Reach => "Thử sức",
                AdmissionCategory.Match => "Vừa sức",
                _ => "An toàn",
            };
            var basis = AdmissionCategorizer.Basis(s.Gpa4, s.Sat, c.AvgGpa4, c.Sat25, c.Sat75, band);
            var facts = new List<string>();
            if (basis is "gpa" or "gpa_sat") facts.Add(GpaFact(s.Gpa4!.Value, c.AvgGpa4!.Value, band));
            if (basis is "sat" or "gpa_sat") facts.Add(SatFact(s.Sat!.Value, c.Sat25!.Value, c.Sat75!.Value));

            var text = $"{label} — vì {string.Join(" và ", facts)}.";
            var both = s.Gpa4 is not null && c.AvgGpa4 is not null && s.Sat is not null && c.Sat25 is not null && c.Sat75 is not null;
            return basis is "gpa" or "sat" && both
                ? text + " Hệ thống xét cả GPA và SAT, lấy mức thấp hơn để đánh giá thận trọng."
                : text;
        }

        private static string GpaFact(decimal g, decimal avg, decimal band)
        {
            var diff = g - avg;
            if (diff >= band) return $"GPA {Gpa(g)} cao hơn GPA trung bình {Gpa(avg)} từ {Gpa(band)} điểm trở lên";
            if (diff <= -band) return $"GPA {Gpa(g)} thấp hơn GPA trung bình {Gpa(avg)} từ {Gpa(band)} điểm trở lên";
            return $"GPA {Gpa(g)} chênh dưới {Gpa(band)} điểm so với GPA trung bình {Gpa(avg)}";
        }

        private static string SatFact(int sat, int p25, int p75) =>
            sat >= p75 ? $"SAT {sat} từ mốc 75% ({p75}) trở lên"
            : sat >= p25 ? $"SAT {sat} nằm trong khoảng 25–75% ({p25}–{p75})"
            : $"SAT {sat} dưới mốc 25% ({p25})";

        //Liệt kê điểm của học sinh so với mức tối thiểu cho từng bài thi trường công bố, ví dụ "IELTS 6.5 / tối thiểu 6.0"
        private static string EnglishFacts(StudentSnapshot s, SchoolCandidate c)
        {
            var facts = new List<string>();
            if (s.Ielts is { } i && c.MinIelts is { } mi) facts.Add($"IELTS {i.ToString("0.0", Us)} / tối thiểu {mi.ToString("0.0", Us)}");
            if (s.Toefl is { } t && c.MinToefl is { } mt) facts.Add($"TOEFL {t.ToString("0", Us)} / tối thiểu {mt.ToString("0", Us)}");
            if (s.Duolingo is { } d && c.MinDuolingo is { } md) facts.Add($"Duolingo {d.ToString("0", Us)} / tối thiểu {md.ToString("0", Us)}");
            return string.Join(", ", facts);
        }

        private static string Gpa(decimal v) => v.ToString("0.00", Us);
        private static string Score(decimal v) => v.ToString("0.##", Us);
        private static string Money(decimal v) => v.ToString("N0", Us);
    }
}
