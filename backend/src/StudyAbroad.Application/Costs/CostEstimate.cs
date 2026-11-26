using System.Text.Json;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Universities;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Costs;

// ---- [USAS-2029][USAS-2066] UC-26 ước tính và UC-27 so sánh chi phí trường dùng chung một bộ tính ----

public record CostLineDto(string Key, decimal? AmountUsd, bool IsOneTime);

/// <summary>Âm = còn dư, dương = còn thiếu. Unlimited: mức trên 70.000 USD (không có trần). WithinTolerance: vượt ngân sách nhưng ≤ 10%.</summary>
public record BudgetCompareDto(string Source, decimal? AnnualBudgetUsd, bool Unlimited, decimal? AnnualGapUsd, decimal? TotalGapUsd, bool WithinTolerance = false);

public record SchoolCostDto(
    string Code, string Name, string? State, string StudyLevel, string? AcademicYear, int? Years,
    IReadOnlyList<CostLineDto> Lines,
    decimal? AnnualUsd, decimal? FirstYearUsd, decimal? TotalUsd,
    decimal ScholarshipUsdPerYear, decimal? AnnualAfterScholarshipUsd, decimal? TotalAfterScholarshipUsd,
    bool IsComplete, IReadOnlyList<string> MissingKeys,
    string? SourceUrl, DateOnly? RetrievedAt, BudgetCompareDto? Budget);

public record CostEstimateResponse(
    ExchangeRateQuote Rate, IReadOnlyList<SchoolCostDto> Schools,
    IReadOnlyList<string> MissingCodes, bool AcademicYearsDiffer,
    decimal AnnualEscalationRate = CostConstants.AnnualCostEscalationRate,
    decimal BankSellRateSpread = CostConstants.BankSellRateSpread,
    int UndergraduateYears = CostConstants.UndergraduateYears);

/// <summary>4 tham số ước tính chi phí UC-26 lưu trong app_settings (cost.parameters), mặc định theo CostConstants khi chưa cấu hình.</summary>
public record CostParameters(
    decimal VisaIntegrityFeeUsd,
    decimal AnnualEscalationRate,
    decimal BankSellRateSpread,
    int UndergraduateYears)
{
    public static CostParameters Default { get; } = new(
        CostConstants.DefaultVisaIntegrityFeeUsd,
        CostConstants.AnnualCostEscalationRate,
        CostConstants.BankSellRateSpread,
        CostConstants.UndergraduateYears);

    public static CostParameters Parse(string? json, decimal? fallbackVisaIntegrityFeeUsd = null)
    {
        var defaultFee = fallbackVisaIntegrityFeeUsd ?? CostConstants.DefaultVisaIntegrityFeeUsd;
        if (string.IsNullOrWhiteSpace(json))
            return Default with { VisaIntegrityFeeUsd = defaultFee };
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new CostParameters(
                ReadDecimal(root, "visaIntegrityFeeUsd", 0m, 100_000m) ?? defaultFee,
                ReadDecimal(root, "annualEscalationRate", 0m, 1m) ?? CostConstants.AnnualCostEscalationRate,
                ReadDecimal(root, "bankSellRateSpread", 0m, 1m) ?? CostConstants.BankSellRateSpread,
                ReadInt(root, "undergraduateYears", 1, 10) ?? CostConstants.UndergraduateYears);
        }
        catch (JsonException)
        {
            return Default with { VisaIntegrityFeeUsd = defaultFee };
        }
    }

    public static IReadOnlyList<string> Validate(CostParameters p)
    {
        var errors = new List<string>();
        if (p.VisaIntegrityFeeUsd is < 0m or > 100_000m)
            errors.Add("Phí Visa Integrity Fee phải từ 0 đến 100.000 USD.");
        if (p.AnnualEscalationRate is < 0m or > 1m)
            errors.Add("Tỷ lệ tăng chi phí hằng năm phải từ 0% đến 100%.");
        if (p.BankSellRateSpread is < 0m or > 1m)
            errors.Add("Biên độ giá bán ra ngân hàng phải từ 0% đến 100%.");
        if (p.UndergraduateYears is < 1 or > 10)
            errors.Add("Số năm học bậc đại học phải là số nguyên từ 1 đến 10.");
        return errors;
    }

    private static decimal? ReadDecimal(JsonElement root, string name, decimal min, decimal max) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) && d >= min && d <= max ? d : null;

    private static int? ReadInt(JsonElement root, string name, int min, int max) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && n >= min && n <= max ? n : null;
}

/// <summary>Một dòng chi phí của trường cho một bậc học; mọi số null nghĩa là chưa có dữ liệu.</summary>
public record SchoolCostRow(
    string Code, string Name, string? State, string StudyLevel, string? AcademicYear,
    decimal? TuitionUsd, decimal? LivingUsd, decimal? FeesUsd, decimal? OtherUsd, decimal? HealthInsuranceUsd,
    string? SourceUrl, DateOnly? RetrievedAt);

public record ReferenceFees(decimal? VisaFeeUsd, decimal? SevisFeeUsd, decimal? VisaIntegrityFeeUsd = null);

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
    public static int? YearsFor(string studyLevel, int undergraduateYears = CostConstants.UndergraduateYears) => studyLevel switch
    {
        StudyLevels.Undergraduate => undergraduateYears,
        StudyLevels.CommunityCollege => 2,
        StudyLevels.Master => 2,
        _ => null, // ponytail: tiến sĩ và THPT chưa có số năm cố định, không hiện tổng nhiều năm
    };

    /// <summary>Tổng nhiều năm theo công thức UC-26: sum_{k=1..years} annual * (1 + r)^(k - 1).</summary>
    public static decimal EscalatedMultiYear(decimal annual, int years, decimal escalationRate)
    {
        decimal sum = 0m;
        decimal factor = 1m;
        for (var k = 0; k < years; k++)
        {
            sum += annual * factor;
            factor *= 1m + escalationRate;
        }
        return Math.Round(sum, 2, MidpointRounding.AwayFromZero);
    }

    public static SchoolCostDto Build(
        SchoolCostRow row, ReferenceFees fees, decimal scholarshipPerYear, BudgetInput? budget,
        decimal escalationRate = CostConstants.AnnualCostEscalationRate,
        int undergraduateYears = CostConstants.UndergraduateYears)
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
            new("visa_integrity", fees.VisaIntegrityFeeUsd, true),
        };
        // Khoản trống không bị tính thành 0: chỉ cộng khoản đã có và báo rõ khoản thiếu.
        // "fees" và "other" là khoản gộp, trống thường nghĩa là không có, nên không bắt buộc.
        var required = new[] { "tuition", "living", "insurance", "visa", "sevis", "visa_integrity" };
        var missing = lines.Where(l => l.AmountUsd is null && required.Contains(l.Key)).Select(l => l.Key).ToList();
        var isComplete = missing.Count == 0;

        // [USAS-2066] Thiếu học phí thì báo chưa đủ dữ liệu, không hiện tổng chi phí và không so với ngân sách.
        var years = YearsFor(row.StudyLevel, undergraduateYears);
        decimal? annual = null, firstYear = null, total = null, annualAfter = null, totalAfter = null;
        if (row.TuitionUsd is not null)
        {
            var annualSum = AnnualCost.Of(row.TuitionUsd, row.LivingUsd, row.FeesUsd, row.OtherUsd, row.HealthInsuranceUsd)!.Value;
            var oneTime = lines.Where(l => l.IsOneTime).Sum(l => l.AmountUsd ?? 0m);
            annual = annualSum;
            firstYear = annualSum + oneTime;
            total = years is { } y ? EscalatedMultiYear(annualSum, y, escalationRate) + oneTime : null;
            annualAfter = Math.Max(annualSum - scholarshipPerYear, 0m);
            totalAfter = years is { } y2 ? Math.Max(total!.Value - scholarshipPerYear * y2, 0m) : null;
        }

        // UC-26 E2 (quyết định chốt): thiếu bất kỳ khoản bắt buộc nào → ẩn kết luận trong/vượt ngân sách (gap = null)
        BudgetCompareDto? compare = null;
        if (budget is not null)
        {
            if (budget.Cap is { } cap)
            {
                decimal? annualGap = isComplete && annualAfter is { } aa ? aa - cap : null;
                decimal? totalGap = isComplete && totalAfter is { } t && years is { } y3 ? t - cap * y3 : null;
                var withinTolerance = annualGap is > 0m && cap > 0m && annualGap.Value <= cap * CostConstants.BudgetTolerance;
                compare = new BudgetCompareDto(budget.Source, cap, false, annualGap, totalGap, withinTolerance);
            }
            else
            {
                compare = new BudgetCompareDto(budget.Source, null, budget.Unlimited, null, null, false);
            }
        }

        return new SchoolCostDto(
            row.Code, row.Name, row.State, row.StudyLevel, row.AcademicYear, years, lines,
            annual, firstYear, total, scholarshipPerYear, annualAfter, totalAfter,
            isComplete, missing, UniversityDetailService.PublicUrl(row.SourceUrl), row.RetrievedAt, compare);
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
        var parsedFees = ParseFees(await repository.GetSettingJsonAsync(CostConstants.ReferenceFeesAppSettingKey, ct));
        var costParams = CostParameters.Parse(
            await repository.GetSettingJsonAsync(CostConstants.CostParametersAppSettingKey, ct),
            parsedFees.VisaIntegrityFeeUsd);
        var fees = parsedFees with { VisaIntegrityFeeUsd = costParams.VisaIntegrityFeeUsd };

        // Ngân sách nhập tay (phụ huynh thử số) được ưu tiên; không có thì lấy từ hồ sơ của người đang đăng nhập
        BudgetInput? budget = budgetUsd is > 0
            ? new BudgetInput("input", budgetUsd, false)
            : userId is { } id ? await repository.GetProfileBudgetAsync(id, ct) : null;

        var scholarship = Math.Max(scholarshipPerYear ?? 0m, 0m);
        var schools = rows.Select(r => CostCalculator.Build(r, fees, scholarship, budget, costParams.AnnualEscalationRate, costParams.UndergraduateYears)).ToList();
        var found = rows.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingCodes = wanted.Where(c => !found.Contains(c)).ToList();
        var differ = schools.Select(s => s.AcademicYear).Where(a => a is not null).Distinct().Count() > 1;

        var midQuote = await rates.GetQuoteAsync(ct);
        var sellRate = Math.Round(midQuote.VndPerUsd * (1m + costParams.BankSellRateSpread), 2, MidpointRounding.AwayFromZero);
        var effectiveQuote = midQuote with { VndPerUsd = sellRate };
        return new CostEstimateResponse(
            effectiveQuote, schools, missingCodes, differ,
            costParams.AnnualEscalationRate, costParams.BankSellRateSpread, costParams.UndergraduateYears);
    }

    public async Task<IReadOnlyList<StateLivingCostDto>> GetStateLivingCostsAsync(CancellationToken ct = default) =>
        (await repository.GetStateLivingCostsAsync(ct))
            .Select(s => s with { SourceUrl = UniversityDetailService.PublicUrl(s.SourceUrl) })
            .ToList();

    public static ReferenceFees ParseFees(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ReferenceFees(null, null, null);
        try
        {
            using var doc = JsonDocument.Parse(json);
            return new ReferenceFees(
                Amount(doc.RootElement, "visaFeeUsd"),
                Amount(doc.RootElement, "sevisFeeUsd"),
                Amount(doc.RootElement, "visaIntegrityFeeUsd"));
        }
        catch (JsonException)
        {
            return new ReferenceFees(null, null, null);
        }
    }

    private static decimal? Amount(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) && d >= 0 ? d : null;
}
