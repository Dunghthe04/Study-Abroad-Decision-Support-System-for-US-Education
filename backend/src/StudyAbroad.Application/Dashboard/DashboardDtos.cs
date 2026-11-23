using System;
using System.Collections.Generic;

namespace StudyAbroad.Application.Dashboard;

// ============================================================================
// 1. CHỈ SỐ HỌC SINH
// ============================================================================

/// <summary>
/// KPI 1: Số trường mỗi nhóm của lần gợi ý mới nhất (UC-87).
/// </summary>
public record RecommendationKpiDto(
    int Reach,
    int Match,
    int Safety,
    int Aid,
    int Total,
    DateTime? RunAt,
    string Explanation,
    bool HasRun
);

/// <summary>
/// KPI 2: Tiến độ hoàn thiện hồ sơ (USAS-640 Unified Profile Flow).
/// </summary>
public record ProfileProgressKpiDto(
    int CompletedSteps,
    int TotalSteps,
    bool HasCommittedProfile,
    string Explanation
);

/// <summary>
/// Một trường mục tiêu có hạn nộp sắp tới.
/// </summary>
public record UpcomingDeadlineItemDto(
    Guid TargetSchoolId,
    Guid UniversityId,
    string UniversityCode,
    string UniversityName,
    string? Category,
    string? ApplicationRound,
    DateTime? Deadline,
    int? DaysRemaining
);

/// <summary>
/// KPI 3: Các hạn nộp sắp tới của danh sách trường mục tiêu (UC-32, UC-33).
/// </summary>
public record UpcomingDeadlinesKpiDto(
    IReadOnlyList<UpcomingDeadlineItemDto> Upcoming,
    int UndecidedRoundCount,
    int TotalTargetSchools,
    string Explanation
);

/// <summary>
/// Một mục thông tin đánh giá độ đầy đủ dữ liệu hồ sơ.
/// </summary>
public record DataCompletenessItemDto(
    string Key,
    string Label,
    bool IsFilled
);

/// <summary>
/// KPI 4: Mức đầy đủ dữ liệu cho thuật toán gợi ý (Q2: GPA, Tiếng Anh, SAT/ACT, Ngân sách, Ngành học).
/// </summary>
public record DataCompletenessKpiDto(
    int FilledCount,
    int TotalCount,
    IReadOnlyList<DataCompletenessItemDto> Items,
    IReadOnlyList<string> MissingLabels,
    string Explanation
);

/// <summary>
/// Tổng hợp KPI màn hình Dashboard của Học sinh.
/// </summary>
public record StudentDashboardDto(
    RecommendationKpiDto Recommendation,
    ProfileProgressKpiDto ProfileProgress,
    UpcomingDeadlinesKpiDto UpcomingDeadlines,
    DataCompletenessKpiDto DataCompleteness
);

// ============================================================================
// 2. CHỈ SỐ PHỤ HUYNH
// ============================================================================

/// <summary>
/// Chi phí và khoản còn thiếu của một trường mục tiêu.
/// </summary>
public record SchoolShortfallItemDto(
    Guid UniversityId,
    string UniversityCode,
    string UniversityName,
    decimal? TotalCost1Year,
    decimal? TotalCost4Years,
    decimal? Budget4Years,
    decimal? Shortfall4Years,
    bool HasCostData
);

/// <summary>
/// Thống kê chỉ số tài chính cho phụ huynh theo danh sách trường mục tiêu của con.
/// </summary>
public record ParentFinancialKpiDto(
    decimal? BudgetPerYear,
    string BudgetSource, // "parent_preference" | "student_profile_tier" | "none"
    string BudgetSourceLabel,
    int SchoolsWithinBudgetCount,
    int SchoolsTotalWithCostCount,
    int SchoolsMissingCostCount,
    decimal TotalCost4YearsAllSchools,
    IReadOnlyList<SchoolShortfallItemDto> ShortfallList,
    SchoolShortfallItemDto? MostShortfallSchool,
    SchoolShortfallItemDto? LeastShortfallSchool,
    string WithinBudgetExplanation,
    string TotalCost4YearsExplanation,
    string ShortfallExplanation
);

/// <summary>
/// Tổng hợp Dashboard của Phụ huynh khi xem từng con (bao gồm KPI con và tài chính).
/// </summary>
public record ParentChildDashboardDto(
    Guid StudentId,
    string StudentName,
    bool IsManaged,
    StudentDashboardDto StudentKpis,
    ParentFinancialKpiDto FinancialKpis
);
