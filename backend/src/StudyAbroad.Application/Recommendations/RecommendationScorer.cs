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
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.Candidate.Code, StringComparer.Ordinal)
                .Take(settings.MaxResults)
                .ToList();

        }

        public static ScoredSchool Score(StudentSnapshot student, SchoolCandidate candidate, RecommendSettings settings)
        {

            //Phân loại Reach, Match, Safety dựa trên GPA và SAT
            var category = AdmissionCategorizer.Categorize(student.Gpa4, student.Sat, candidate.AvgGpa4, candidate.Sat25, candidate.Sat75, settings.GpaBand);

            var cost = candidate.TotalCostUsd;

            //Tính điểm tài chính
            var finance = cost is { } co && student.AnnualBudgetUsd is { } b && b > 0
                ? Math.Clamp((b - co) / b, -1m, 1m) : 0m;// giới hạn điểm tài chính trong khoảng [-1, 1]

            //Tính điểm ngoại khóa, chỉ cộng cho Reach/Match (nơi hồ sơ tổng thể tạo khác biệt)
            var extracurricular = category is AdmissionCategory.Reach or AdmissionCategory.Match && student.ExtracurricularScore is { } e
                                ? Math.Clamp(e / 10m, 0m, 1m) : 0m;// giới hạn điểm ngoại khóa trong khoảng [0, 1]

            var score = BaseScore(category) + settings.FinanceWeight * finance + settings.ExtracurricularWeight * extracurricular;

            return new ScoredSchool(candidate, category, Math.Round(score, 4),cost, CostUnknown: cost is null);
        }

        //Sắp xếp các trường trong nhóm, điểm cao trước bằng điểm theo mã trường
        private static IEnumerable<ScoredSchool> Ranked(IEnumerable<ScoredSchool> schools)
        {
            return schools.OrderByDescending(s=> s.Score).ThenBy(s => s.Candidate.Code, StringComparer.Ordinal);
        }

        //Lấy số lượng trường tối đa theo từng loại (reach, match, safety) trong gợi ý trường
        public static int LimitFor(AdmissionCategory category, RecommendSettings settings) => category switch
        {
            AdmissionCategory.Reach => settings.PerCategory.Reach,
            AdmissionCategory.Match => settings.PerCategory.Match,
            AdmissionCategory.Safety => settings.PerCategory.Safety,
            _ => settings.MaxResults,
        };
        //Quy đổi AdmissionCategory thành điểm cơ bản để tính tổng điểm
        public static decimal BaseScore(AdmissionCategory category) => category switch
        {
            AdmissionCategory.Reach => 1m,
            AdmissionCategory.Match => 2m,
            AdmissionCategory.Safety => 3m,
            _ => 0m,

        };

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
    }
}
