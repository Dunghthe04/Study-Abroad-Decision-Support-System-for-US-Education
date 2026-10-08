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
        decimal? Toefl = null,
        decimal? Duolingo = null); 


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
        decimal? AcceptanceRate = null, // 0–1, chỉ để hiển thị thông tin trường, không dùng để chấm điểm
        decimal? MinIelts = null,
        decimal? MinToefl = null,
        decimal? MinDuolingo = null,
        string? City = null,
        string? Control = null,             // public | private
        string? Website = null,
        int? InternationalStudents = null,
        string? SatPolicy = null)            // required | optional | not_accepted)
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
        FitBreakdown Fit,
        EnglishStatus English = EnglishStatus.NoScore,   //để giao diện/giải thích ghi "cần IELTS ≥ 6.5"
        bool OpenAdmission = false);                      //để giải thích ghi "trường tuyển sinh mở");

    //Trạng thái điều kiện tiếng Anh của học sinh với trường
    public enum EnglishStatus { Met, BelowMin, NoScore, Unknown }
    //Met = đạt | BelowMin = chưa đạt mức tối thiểu | NoScore = HS chưa có điểm | Unknown = trường không công bố mức

}
