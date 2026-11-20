using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Dashboard;

/// <summary>
/// Dòng chi phí của một trường đại học theo năm học (USD).
/// TotalCostUsd: tính theo công thức TotalCost chuẩn (Học phí + Ăn ở + Phí + Chi phí khác).
/// </summary>
public record UniversityCostRow(
    Guid UniversityId,
    decimal? TuitionUsd,
    decimal? LivingUsd,
    decimal? FeesUsd,
    decimal? OtherExpensesUsd
)
{
    public decimal? TotalCostUsd =>
        TuitionUsd.HasValue
            ? TuitionUsd.Value + (LivingUsd ?? 0) + (FeesUsd ?? 0) + (OtherExpensesUsd ?? 0)
            : null;
}

/// <summary>
/// Repository truy vấn chi phí của các trường mục tiêu phục vụ tính toán Dashboard KPI (UC-94).
/// </summary>
public interface IDashboardRepository
{
    Task<IReadOnlyDictionary<Guid, UniversityCostRow>> GetUniversityCostsAsync(
        IEnumerable<Guid> universityIds,
        string level = "undergraduate",
        CancellationToken ct = default);
}
