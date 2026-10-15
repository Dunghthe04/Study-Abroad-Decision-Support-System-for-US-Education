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
        decimal? AcceptanceRate = null, // 0–1, tỉ lệ nhận chung 
        decimal? MinIelts = null,
        decimal? MinToefl = null,
        decimal? MinDuolingo = null,
        string? City = null,
        string? Control = null,             // public | private
        string? Website = null,
        int? InternationalStudents = null,
        string? SatPolicy = null,            // required | optional | not_accepted
        bool? HasIntlAid = null,             // H6: có học bổng cho du học sinh không. null = chưa có dữ liệu
        // ---- Số liệu cho 3 lượt AI (đọc từ 16 cột migration AddRecommendationFormulaColumns) ----
        string? C7GpaImportance = null,      // C7: very_important | important | considered | not_considered
        string? C7TestImportance = null,     // C7 mức coi trọng SAT/ACT, cùng thang
        string? C7EcImportance = null,       // C7 mức coi trọng ngoại khóa, cùng thang
        decimal? SatSubmitPct = null,        // C9: 0–1, tỉ lệ sinh viên nộp SAT
        GpaDistribution? GpaDistribution = null,   // C11: phân phối GPA
        decimal? IntlAcceptanceRate = null,  // C1: 0–1, tỉ lệ nhận du học sinh
        decimal? IntlAidAvgUsd = null,       // H6: học bổng trung bình mỗi du học sinh (USD/năm)
        decimal? RetentionRate1Yr = null,    // B: 0–1, tỉ lệ giữ chân năm hai
        decimal? GradRate6Yr = null,        // B: 0–1, tỉ lệ tốt nghiệp 6 năm)             // H6: có học bổng cho du học sinh không. null = chưa có dữ liệu
        decimal? OtherExpensesUsd = null)    // G5: sách, đi lại, cá nhân (USD/năm)
    {
        // Tổng chi phí 1 năm = học phí + phí + ăn ở + chi khác (CDS G1 + G5)
        public decimal? TotalCostUsd =>
            TuitionUsd is null ? null : TuitionUsd + (LivingUsd ?? 0) + (FeesUsd ?? 0) + (OtherExpensesUsd ?? 0);
    }

    /// <summary>Phân phối GPA sinh viên trúng tuyển (CDS C11): tỉ lệ 0–1 trong từng khoảng. Null = trường không công bố.</summary>
    public record GpaDistribution(
        decimal? Pct40,          // đúng 4.0
        decimal? Pct375To399,
        decimal? Pct350To374,
        decimal? Pct325To349,
        decimal? Pct300To324,
        decimal? Pct250To299,
        decimal? PctBelow250);

}
