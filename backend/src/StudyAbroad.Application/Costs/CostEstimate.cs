using System.Text.Json;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Universities;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Costs;

// ---- [USAS-2029][USAS-2066] UC-26 ước tính và UC-27 so sánh chi phí trường dùng chung một bộ tính ----

public record CostLineDto(string Key, decimal? AmountUsd, bool IsOneTime);

/// <summary>Âm = còn dư, dương = còn thiếu. Unlimited: mức trên 70.000 USD (không có trần).</summary>
public record BudgetCompareDto(string Source, decimal? AnnualBudgetUsd, bool Unlimited, decimal? AnnualGapUsd, decimal? TotalGapUsd);

public record SchoolCostDto(
    string Code, string Name, string? State, string StudyLevel, string? AcademicYear, int? Years,
    IReadOnlyList<CostLineDto> Lines,
    decimal? AnnualUsd, decimal? FirstYearUsd, decimal? TotalUsd,
    decimal ScholarshipUsdPerYear, decimal? AnnualAfterScholarshipUsd, decimal? TotalAfterScholarshipUsd,
    bool IsComplete, IReadOnlyList<string> MissingKeys,
    string? SourceUrl, DateOnly? RetrievedAt, BudgetCompareDto? Budget);

public record CostEstimateResponse(
    ExchangeRateQuote Rate, IReadOnlyList<SchoolCostDto> Schools,
    IReadOnlyList<string> MissingCodes, bool AcademicYearsDiffer);

/// <summary>Một dòng chi phí của trường cho một bậc học; mọi số null nghĩa là chưa có dữ liệu.</summary>
public record SchoolCostRow(
    string Code, string Name, string? State, string StudyLevel, string? AcademicYear,
    decimal? TuitionUsd, decimal? LivingUsd, decimal? FeesUsd, decimal? OtherUsd, decimal? HealthInsuranceUsd,
    string? SourceUrl, DateOnly? RetrievedAt);

public record ReferenceFees(decimal? VisaFeeUsd, decimal? SevisFeeUsd);

/// <summary>Ngân sách một năm. Cap null: không có trần (tier_above_70k).</summary>
public record BudgetInput(string Source, decimal? Cap, bool Unlimited);

/// <summary>Số liệu năm mới nhất của bang; bang chưa có dòng nào thì mọi số là null.</summary>
public record StateLivingCostDto(
    string Code, string Name, string Region, decimal? Index, decimal? HousingRentsIndex,
    short? Year, string? SourceUrl, DateOnly? RetrievedAt);

public interface ICostRepository
{
    Task<IReadOnlyList<SchoolCostRow>> GetSchoolCostsAsync(IReadOnlyList<string> codes, string studyLevel, CancellationToken ct = default);
    Task<string?> GetSettingJsonAsync(string key, CancellationToken ct = default);
    Task<BudgetInput?> GetProfileBudgetAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<StateLivingCostDto>> GetStateLivingCostsAsync(CancellationToken ct = default);
}

public static class CostCalculator
{
    public static int? YearsFor(string studyLevel) => studyLevel switch
    {
        StudyLevels.Undergraduate => 4,
        StudyLevels.CommunityCollege => 2,
        StudyLevels.Master => 2,
        _ => null, // ponytail: tiến sĩ và THPT chưa có số năm cố định, không hiện tổng nhiều năm
    };

    public static SchoolCostDto Build(SchoolCostRow row, ReferenceFees fees, decimal scholarshipPerYear, BudgetInput? budget)
    {
        var lines = new List<CostLineDto>
        {
            new("tuition", row.TuitionUsd, false),
            new("living", row.LivingUsd, false),
            new("fees", row.FeesUsd, false),
            new("other", row.OtherUsd, false),
            new("insurance", row.HealthInsuranceUsd, false),
            new("visa", fees.VisaFeeUsd, true),
            new("sevis", fees.SevisFeeUsd, true),
        };
        // Khoản trống không bị tính thành 0: chỉ cộng khoản đã có và báo rõ khoản thiếu.
        // "fees" và "other" là khoản gộp, trống thường nghĩa là không có, nên không bắt buộc.
        var required = new[] { "tuition", "living", "insurance", "visa", "sevis" };
        var missing = lines.Where(l => l.AmountUsd is null && required.Contains(l.Key)).Select(l => l.Key).ToList();

        // [USAS-2066] Thiếu học phí thì báo chưa đủ dữ liệu, không hiện tổng chi phí và không so với ngân sách.
        var years = YearsFor(row.StudyLevel);
        decimal? annual = null, firstYear = null, total = null, annualAfter = null, totalAfter = null;
        if (row.TuitionUsd is not null)
        {
            var annualSum = lines.Where(l => !l.IsOneTime).Sum(l => l.AmountUsd ?? 0m);
            var oneTime = lines.Where(l => l.IsOneTime).Sum(l => l.AmountUsd ?? 0m);
            annual = annualSum;
            firstYear = annualSum + oneTime;
            total = years is { } y ? annualSum * y + oneTime : null;
            annualAfter = Math.Max(annualSum - scholarshipPerYear, 0m);
            totalAfter = years is { } y2 ? Math.Max(total!.Value - scholarshipPerYear * y2, 0m) : null;
        }

        BudgetCompareDto? compare = null;
        if (budget is not null)
        {
            compare = budget.Cap is { } cap
                ? new BudgetCompareDto(budget.Source, cap, false, annualAfter is { } aa ? aa - cap : null,
                    totalAfter is { } t && years is { } y3 ? t - cap * y3 : null)
                : new BudgetCompareDto(budget.Source, null, budget.Unlimited, null, null);
        }

        return new SchoolCostDto(
            row.Code, row.Name, row.State, row.StudyLevel, row.AcademicYear, years, lines,
            annual, firstYear, total, scholarshipPerYear, annualAfter, totalAfter,
            missing.Count == 0, missing, UniversityDetailService.PublicUrl(row.SourceUrl), row.RetrievedAt, compare);
    }
}

public interface ICostService
{
    Task<CostEstimateResponse> EstimateAsync(IReadOnlyList<string> codes, string? studyLevel, decimal? scholarshipPerYear,
        decimal? budgetUsd, Guid? userId, CancellationToken ct = default);
    Task<IReadOnlyList<StateLivingCostDto>> GetStateLivingCostsAsync(CancellationToken ct = default);
}

public class CostService(ICostRepository repository, IExchangeRateProvider rates) : ICostService
{
    public async Task<CostEstimateResponse> EstimateAsync(IReadOnlyList<string> codes, string? studyLevel,
        decimal? scholarshipPerYear, decimal? budgetUsd, Guid? userId, CancellationToken ct = default)
    {
        var level = StudyLevels.IsValid(studyLevel) ? studyLevel! : StudyLevels.Undergraduate;
        var wanted = codes.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var rows = await repository.GetSchoolCostsAsync(wanted, level, ct);
        var fees = ParseFees(await repository.GetSettingJsonAsync(CostConstants.ReferenceFeesAppSettingKey, ct));

        // Ngân sách nhập tay (phụ huynh thử số) được ưu tiên; không có thì lấy từ hồ sơ của người đang đăng nhập
        BudgetInput? budget = budgetUsd is > 0
            ? new BudgetInput("input", budgetUsd, false)
            : userId is { } id ? await repository.GetProfileBudgetAsync(id, ct) : null;

        var scholarship = Math.Max(scholarshipPerYear ?? 0m, 0m);
        var schools = rows.Select(r => CostCalculator.Build(r, fees, scholarship, budget)).ToList();
        var found = rows.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingCodes = wanted.Where(c => !found.Contains(c)).ToList();
        var differ = schools.Select(s => s.AcademicYear).Where(a => a is not null).Distinct().Count() > 1;

        return new CostEstimateResponse(await rates.GetQuoteAsync(ct), schools, missingCodes, differ);
    }

    public async Task<IReadOnlyList<StateLivingCostDto>> GetStateLivingCostsAsync(CancellationToken ct = default) =>
        (await repository.GetStateLivingCostsAsync(ct))
            .Select(s => s with { SourceUrl = UniversityDetailService.PublicUrl(s.SourceUrl) })
            .ToList();

    public static ReferenceFees ParseFees(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ReferenceFees(null, null);
        try
        {
            using var doc = JsonDocument.Parse(json);
            return new ReferenceFees(Amount(doc.RootElement, "visaFeeUsd"), Amount(doc.RootElement, "sevisFeeUsd"));
        }
        catch (JsonException)
        {
            return new ReferenceFees(null, null);
        }
    }

    private static decimal? Amount(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) && d >= 0 ? d : null;
}
