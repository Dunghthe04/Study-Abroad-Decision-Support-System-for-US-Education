using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Recommendations
{
    public static class RecommendationScorer
    {
        public static IReadOnlyList<ScoredSchool> Recommend(
            StudentSnapshot student, IReadOnlyList<SchoolCandidate> candidates, RecommendSettings settings)
        {
            // lấy danh sách trường phù hợp với ngành học, bang ưu tiên, chi phí
            var scored = candidates
                .Where(c => MatchesMajor(c, student.Major))
                .Where(c => MatchState(c, student.PreferredStates))
                .Where(c => WithinBudget(c, student.AnnualBudgetUsd, settings.BudgetTolerance))
                .Select(c => Score(student,c, settings) ).ToList();

            //Lấy tối đa số lượng trường theo nhóm Reach, Match, Safety
            return scored.GroupBy(s => s.AdmissionCategory)
                .SelectMany(g => Ranked(g).Take(LimitFor(g.Key, settings)))
                .OrderBy(s => s.AdmissionCategory)
                .ThenByDescending(s => s.Score)
                .ThenBy(s => s.Candidate.Code, StringComparer.Ordinal)
                .Take(settings.MaxResults)
                .ToList();

        }

        public static ScoredSchool Score(StudentSnapshot student, SchoolCandidate candidate, RecommendSettings settings)
        {
            var neutral = settings.MissingValue;

            //1. Nhóm học thuật theo luật
            var category = AdmissionCategorizer.Categorize(student.Gpa4, student.Sat, candidate.AvgGpa4, candidate.Sat25, candidate.Sat75, settings.GpaBand);

            var openAdmission = (category == AdmissionCategory.InsufficientData && settings.OpenAdmissionLevels.Contains(student.StudyLevel, StringComparer.OrdinalIgnoreCase));

            if (openAdmission)
                category = AdmissionCategory.Safety;

            //2. Chuẩn hóa từng tiêu chí về [0, 1]; tiếng Anh là một phần của học thuật
            var cost = candidate.TotalCostUsd;
            var (english, englishStatus) = English(student, candidate);
            var academic = AcademicFit(student, candidate, english, settings);
            var finance = FinanceFit(cost, student.AnnualBudgetUsd, neutral);
            var extracurricular = ExtracurricularFit(student.ExtracurricularScore);
            var fit = new FitBreakdown(academic, finance, english, extracurricular);

            //3. SAW 3 tiêu chí: một bộ trọng số chung cho mọi trường (trọng số chứng minh bằng AHP)
            var w = settings.Weights;
            var score = WeightedSum((academic, w.Academic), (finance, w.Finance), (extracurricular, w.Extracurricular));

            return new ScoredSchool(candidate, category, score is { } s ? Math.Round(s, 4) : null, cost, CostUnknown: cost is null, fit, englishStatus, openAdmission);
        }
        //Học thuật = trung bình có trọng số của GPA fit, SAT fit, tiếng Anh fit; chỉ tính phần cả HS lẫn trường có số liệu.
        //HS không có GPA, SAT lẫn điểm tiếng Anh → null (bỏ tiêu chí); HS có điểm nhưng trường không có số liệu nào → trung tính
        private static decimal? AcademicFit(StudentSnapshot s, SchoolCandidate c, decimal? english, RecommendSettings settings)
        {
            if (s.Gpa4 is null && s.Sat is null && !HasEnglishScore(s)) return null;
            var p = settings.AcademicParts;
            return WeightedSum(
                (GpaFit(s.Gpa4, c.AvgGpa4, settings.GpaBand), p.Gpa),
                (SatFit(s.Sat, c.Sat25, c.Sat75), p.Sat),
                (english, p.English)) ?? settings.MissingValue;
        }

        private static bool HasEnglishScore(StudentSnapshot s) => s.Ielts is not null || s.Toefl is not null || s.Duolingo is not null;
        //GPA = avg - band => 0 ,gpa = avg => 0,5 , gpa > avg =>1
        private static decimal? GpaFit(decimal? gpa, decimal? avg, decimal band)
        {
            if (avg == null || gpa == null) return null;
            if (band <= 0) return gpa >= avg ? 1m : 0m;
            return Clamp01((gpa.Value - avg.Value + band) / (2 * band));
        }

        //Sat bằng mốc 25% => 0, bằng mốc 0,75 => 1
        private static decimal? SatFit(decimal? sat, decimal? sat25, decimal? sat75)
        {
            if (sat is null || sat25 is null || sat75 is null) return null;
            if (sat75 <= sat25) return sat >= sat75 ? 1m : 0m;
            return Clamp01((decimal)(sat.Value - sat25.Value) / (sat75.Value - sat25.Value));
        }

        //Phần ngân sách còn dư sau khi trừ chi phí; vượt ngân sách → 0.
        //HS chưa nhập ngân sách → null (bỏ tiêu chí); trường thiếu chi phí → trung tính
        private static decimal? FinanceFit(decimal? cost, decimal? budget, decimal neutral)
        {
            if (budget is null || budget <= 0) return null;
            if (cost is null) return neutral;
            return Clamp01((budget.Value - cost.Value) / budget.Value);
        }

        //Điểm ngoại khóa thang 0–4 → [0, 1]; chưa chấm → null (bỏ tiêu chí)
        private static decimal? ExtracurricularFit(decimal? score) =>
            score is { } s ? Clamp01(s / 4m) : null;

        //Tiếng Anh là điều kiện đầu vào: đạt mức tối thiểu = 1, chưa đạt = điểm/mức. Có nhiều bài thi thì lấy bài tốt nhất.
        //Trường không công bố mức → null: không tính vào học thuật, chỉ báo trạng thái Unknown
        private static (decimal? Fit, EnglishStatus status) English(StudentSnapshot s, SchoolCandidate c)
        {
            if (!HasEnglishScore(s)) return (null, EnglishStatus.NoScore);

            var ratios = new[] {Ratio(s.Ielts,c.MinIelts),
                Ratio(s.Toefl, c.MinToefl),
                Ratio(s.Duolingo, c.MinDuolingo)
            }.Where(r => r is not null).ToList();

            
            if(ratios.Count == 0) return (null, EnglishStatus.Unknown);

            var best = ratios.Max();
            return (best,best >= 1m? EnglishStatus.Met : EnglishStatus.BelowMin);
        }

        private static decimal? Ratio(decimal? score, decimal? min)=>
            score is { } s && min is { } m && m>0 ? Math.Min(1m, s/m) : null;

        //Tiêu chí thiếu dữ liệu thì bỏ, chia lại theo tổng trọng số của các tiêu chí còn lại
        private static decimal? WeightedSum(params (decimal? Value, decimal Weight)[] criteria)
        {
            var available = criteria.Where(c => c.Value is not null && c.Weight > 0).ToList();
            var totalWeight = available.Sum(c => c.Weight);
            if (totalWeight == 0) return null;
            return available.Sum(c => c.Value!.Value * c.Weight) / totalWeight;
        }



        private static decimal Clamp01(decimal x) => Math.Clamp(x, 0m, 1m);

        //Lọc theo ngành: tên ngành đã chuẩn hóa từ trước (dropdown ở form, LLM ở chat) nên chỉ cần so trùng tên
        private static bool MatchesMajor(SchoolCandidate c, string? studentMajor)
        {
            return string.IsNullOrWhiteSpace(studentMajor) || c.Majors.Any(m => string.Equals(studentMajor.Trim(), m.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        //Lọc danh sách trường theo bang mong muốn
        private static bool MatchState(SchoolCandidate c, IReadOnlyList<string> preferredStates)
        {
            return preferredStates.Count==0 || (c.State != null && preferredStates.Contains(c.State, StringComparer.OrdinalIgnoreCase));
        }

        //Lọc danh sách trường theo chi phí, cho phép vượt quá tolerance% chi phí
        private static bool WithinBudget(SchoolCandidate c, decimal? studentBudget, decimal tolerance)
        {
            return studentBudget is null
                || c.TotalCostUsd is null
                || c.TotalCostUsd <= studentBudget * (1 + tolerance);// Cho phép vượt quá tolerance% chi phí
        }
        //Sắp xếp các trường trong nhóm, điểm cao trước bằng điểm theo mã trường
        private static IEnumerable<ScoredSchool> Ranked(IEnumerable<ScoredSchool> schools)
        {
            return schools.OrderByDescending(s => s.Score).ThenBy(s => s.Candidate.Code, StringComparer.Ordinal);
        }


        //Lấy số lượng trường tối đa theo từng loại (reach, match, safety) trong gợi ý trường
        public static int LimitFor(AdmissionCategory category, RecommendSettings settings) => category switch
        {
            AdmissionCategory.Reach => settings.PerCategory.Reach,
            AdmissionCategory.Match => settings.PerCategory.Match,
            AdmissionCategory.Safety => settings.PerCategory.Safety,
            _ => settings.MaxResults,
        };
    }
}
