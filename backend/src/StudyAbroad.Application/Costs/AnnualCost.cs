using System.Linq.Expressions;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Costs;

/// <summary>
/// Chi phí một năm của một trường theo UC-26: học phí + ăn ở + phí + chi khác + bảo hiểm y tế.
/// Không có học phí thì không có tổng (USAS-2066); các khoản khác trống thì cộng 0.
/// Mọi nơi hiện, lọc, xếp hoặc so ngân sách theo tổng chi phí năm đều dùng định nghĩa này để ra cùng một con số.
/// </summary>
public static class AnnualCost
{
    public static decimal? Of(decimal? tuition, decimal? living, decimal? fees, decimal? other, decimal? insurance) =>
        tuition is decimal t ? t + (living ?? 0m) + (fees ?? 0m) + (other ?? 0m) + (insurance ?? 0m) : null;

    public static decimal? Of(UniversityOffering o) =>
        Of(o.TuitionUsd, o.LivingUsd, o.FeesUsd, o.OtherExpensesUsd, o.HealthInsuranceUsd);

    /// <summary>Cùng công thức cho truy vấn EF (dịch ra SQL), dùng trong Select / Min / Max của câu truy vấn.</summary>
    public static readonly Expression<Func<UniversityOffering, decimal?>> Sql = o =>
        o.TuitionUsd == null
            ? null
            : o.TuitionUsd + (o.LivingUsd ?? 0m) + (o.FeesUsd ?? 0m) + (o.OtherExpensesUsd ?? 0m) + (o.HealthInsuranceUsd ?? 0m);
}
