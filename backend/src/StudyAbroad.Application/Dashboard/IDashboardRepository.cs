using StudyAbroad.Application.Costs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Dashboard;

/// <summary>
/// Dòng chi phí của một trường đại học theo năm học (USD).
/// TotalCostUsd: chi phí một năm theo AnnualCost (học phí + ăn ở + phí + chi khác + bảo hiểm y tế).
/// </summary>
public record UniversityCostRow(
    Guid UniversityId,
    decimal? TuitionUsd,
    decimal? LivingUsd,
    decimal? FeesUsd,
    decimal? OtherExpensesUsd,
    decimal? HealthInsuranceUsd = null
)
{
    public decimal? TotalCostUsd =>
        AnnualCost.Of(TuitionUsd, LivingUsd, FeesUsd, OtherExpensesUsd, HealthInsuranceUsd);
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
