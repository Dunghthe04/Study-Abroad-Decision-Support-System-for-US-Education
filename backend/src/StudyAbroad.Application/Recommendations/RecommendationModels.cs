using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Recommendations
{
    /// <summary>Hồ sơ học sinh đã chuẩn hóa để chấm điểm (GPA đã quy về thang 4).</summary>
    public record StudentSnapshot(
        string StudyLevel,
        string? Major,
        decimal? Gpa4,
        int? Sat,
        decimal? AnnualBudgetUsd,
        IReadOnlyList<string> PreferredStates,
        decimal? ExtracurricularScore,
        decimal? Ielts = null,
        decimal? Toefl = null); 


    public record SchoolCandidate(
        Guid UniversityId,
        Guid OfferingId,
        string Code,
        string Name,
        string? State,
        IReadOnlyList<string> Majors,
        decimal? AvgGpa4,
        int? Sat25,
        int? Sat75,
        decimal? TuitionUsd,
        decimal? LivingUsd,
        decimal? FeesUsd,
        decimal? AcceptanceRate = null, // 0–1, chỉ dùng nội bộ, không hiển thị
        decimal? MinIelts = null,
        decimal? MinToefl = null)
    {
        public decimal? TotalCostUsd =>
            TuitionUsd is null ? null : TuitionUsd + (LivingUsd ?? 0) + (FeesUsd ?? 0);
    }

    //Điểm từng tiêu chí, 0-1,nếu ko có= null
    public record FitBreakdown(decimal? Academic, decimal? Finance, decimal? English,decimal? Extracurricular);

    public record ScoredSchool(
        SchoolCandidate Candidate,
        AdmissionCategory AdmissionCategory,
        decimal? Score,
        decimal? TotalCostUsd,
        bool CostUnknown,
        FitBreakdown Fit);

}
