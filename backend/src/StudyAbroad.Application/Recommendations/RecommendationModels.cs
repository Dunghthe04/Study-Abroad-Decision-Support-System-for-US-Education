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
        decimal? ExtracurricularScore = null,
        decimal? Ielts = null,
        decimal? Toefl = null,
        decimal? Duolingo = null,
        decimal? BudgetMinUsd = null,
        decimal? BudgetMaxUsd = null); 


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
        string? SatPolicy = null,            // required | optional | not_accepted
        bool? HasIntlAid = null)             // H6: có học bổng cho du học sinh không. null = chưa có dữ liệu
    {
        public decimal? TotalCostUsd =>
            TuitionUsd is null ? null : TuitionUsd + (LivingUsd ?? 0) + (FeesUsd ?? 0);
    }

}
