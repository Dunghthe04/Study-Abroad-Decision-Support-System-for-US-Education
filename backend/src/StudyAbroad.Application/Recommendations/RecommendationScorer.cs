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

            //Sắp xếp theo luật , không phụ thuộc SAW
            var category = AdmissionCategorizer.Categorize(student.Gpa4, student.Sat, candidate.AvgGpa4, candidate.Sat25, candidate.Sat75, settings.GpaBand);

            //Chi phí trường
            var cost = candidate.TotalCostUsd;

            //Chuẩn hóa tiêu chí về [0 1]
            var fit = new FitBreakdown(
                Academic: new[] { GpaFit(student.Gpa4, candidate.AvgGpa4, settings.GpaBand), SatFit(student.Sat, candidate.Sat25, candidate.Sat75) }.Average(),
                Finance: FinanceFit(cost, student.AnnualBudgetUsd),
                English: EnglishFit(student, candidate),
                Extracurricular: ExtracurricularFit(student.ExtracurricularScore, candidate.AcceptanceRate));

            // SAW = trọng số x điểm tiêu chí
            var w = settings.Weights;
            var score = WeightedSum((fit.Academic, w.Academic), (fit.Finance, w.Finance), (fit.English, w.English), (fit.Extracurricular, w.Extracurricular));

            return new ScoredSchool(candidate, category,score is { } s ? Math.Round(s, 4): null,cost, CostUnknown: cost is null,fit);
        }

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

        //Ngân sách còn dư trừ chi phí, vượt ngân sách => 0
        private static decimal? FinanceFit(decimal? cost, decimal? budget)
        {
            if (cost == null || budget == null || budget <= 0) return null;
            return Clamp01((budget.Value-cost.Value)/budget.Value);
        }

        //Đạt yêu cầu => 1, chưa đạt => điểm/ yêu cầu, nếu có cả 2 lấy tốt hơn
        private static decimal? EnglishFit(StudentSnapshot s, SchoolCandidate c)
        {
            decimal? ielts = s.Ielts is { } i && c.MinIelts is { } mi && mi > 0 ? Math.Min(1m, i / mi) : null;
            decimal? toefl = s.Toefl is { } t && c.MinToefl is { } mt && mt > 0 ? Math.Min(1m, t / mt) : null;
            return new[] { ielts, toefl }.Max();
        }

        //Trường càng chọn lọc càng xét hồ sơ toàn diện → ngoại khóa có trọng lượng lớn hơn
        private static decimal? ExtracurricularFit(decimal? ecScore, decimal? acceptanceRate)
        {
            if (ecScore is null || acceptanceRate is null) return null;
            return Clamp01(ecScore.Value / 10m) * Clamp01(1m - acceptanceRate.Value);
        }
        //Tiêu chí thiếu dữ liệu thì bỏ, chia lại theo tổng trọng số của các tiêu chí còn lại
        private static decimal? WeightedSum(params (decimal? Value, decimal Weight)[] criteria)
        {
            var available = criteria.Where(c => c.Value is not null && c.Weight > 0).ToList();
            var totalWeight = available.Sum(c => c.Weight);
            if (totalWeight == 0) return null;
            return available.Sum(c => c.Value!.Value * c.Weight) / totalWeight;
        }



        private static decimal Clamp01(decimal x) => Math.Clamp(x, 0m, 1m);
        //Lọc danh sách trường theo ngành học mong muốn
        private static bool MatchesMajor(SchoolCandidate c, string? studentMajor)
        {
            //Không nhập trường or trường có trong danh sách ngành học của trường = true
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
