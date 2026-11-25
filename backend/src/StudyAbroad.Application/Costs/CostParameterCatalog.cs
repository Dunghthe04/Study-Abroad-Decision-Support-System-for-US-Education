using System.Globalization;
using System.Text.Json;
using StudyAbroad.Application.Admin.Settings;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Costs;

/// <summary>
/// [UC-83] Danh mục tham số của ước tính chi phí để trang "Căn cứ tham số" hiển thị cạnh tham số thuật toán gợi ý.
/// Chỉ xem. Khác AlgorithmParameters (hằng số trong code), phần lớn giá trị ở đây là DỮ LIỆU đang chạy: phí visa và SEVIS nằm trong
/// app_settings, mức ngân sách trong metadata hồ sơ, tỉ giá lấy mỗi ngày, chỉ số giá theo bang trong bảng riêng. Nên danh mục đọc lúc gọi.
/// Mỗi nguồn đọc lỗi thì tham số tương ứng ghi "Chưa có số liệu", không làm hỏng cả trang.
/// Phí Visa Integrity Fee, tỷ lệ tăng chi phí hằng năm, biên độ giá bán ra và số năm học đại học đọc từ app_settings (cost.parameters,
/// Moderator sửa), giống giá trị ước tính chi phí đang dùng.
/// </summary>
public interface ICostParameterCatalog
{
    Task<IReadOnlyList<AlgorithmParameter>> ListAsync(CancellationToken ct = default);
}

public class CostParameterCatalog(ICostRepository costs, IExchangeRateProvider rates, ICostParametersSettingService settings) : ICostParameterCatalog
{
    /// <summary>Mã bước của các tham số chi phí (giao diện gom thành phần "Ước tính chi phí").</summary>
    public const string Step = "Chi phí";

    public const string NoData = "Chưa có số liệu";

    public async Task<IReadOnlyList<AlgorithmParameter>> ListAsync(CancellationToken ct = default)
    {
        var feesJson = await Attempt(() => costs.GetSettingJsonAsync(CostConstants.ReferenceFeesAppSettingKey, ct));
        var metadataJson = await Attempt(() => costs.GetSettingJsonAsync(ProfileConstants.ProfileMetadataAppSettingKey, ct));
        var quote = await Attempt(() => rates.GetQuoteAsync(ct));
        var states = await Attempt(() => costs.GetStateLivingCostsAsync(ct));
        var p = await Attempt(() => settings.GetCurrentParametersAsync(ct)) ?? CostParameters.Default;

        var fees = CostService.ParseFees(feesJson);
        var withIndex = states?.Where(s => s.Index is not null).ToList() ?? [];

        return
        [
            Parameter("budget_tiers", "Cận trên các mức ngân sách", BudgetTiers(metadataJson),
                "Mức ngân sách mỗi năm học sinh chọn khi nhập hồ sơ. Số tiền cận trên của mức được dùng làm ngân sách khi lọc trường và đối chiếu chi phí; mức trên cùng không có trần."),
            Parameter("visa_fee", "Phí xin visa (MRV)", Money(fees.VisaFeeUsd, "USD"),
                "Khoản thu một lần, cộng vào chi phí năm đầu. Số tiền lấy từ cài đặt hệ thống" + Source(feesJson, "visaSourceUrl")),
            Parameter("sevis_fee", "Phí SEVIS I-901", Money(fees.SevisFeeUsd, "USD"),
                "Khoản thu một lần, cộng vào chi phí năm đầu. Số tiền lấy từ cài đặt hệ thống" + Source(feesJson, "sevisSourceUrl")),
            Parameter("visa_integrity_fee", "Phí Visa Integrity Fee", Money(p.VisaIntegrityFeeUsd, "USD"),
                "Khoản thu một lần của visa Mỹ, cộng vào chi phí năm đầu cùng phí MRV và SEVIS. Moderator sửa trên màn quản lý tham số."),
            Parameter("cost_growth", "Tỷ lệ tăng chi phí hằng năm", Pct(p.AnnualEscalationRate),
                "Chi phí năm thứ k bằng chi phí năm đầu nhân (1 + tỷ lệ)^(k − 1) khi tính tổng cả khóa."),
            Parameter("study_years", "Số năm học",
                $"Đại học {p.UndergraduateYears} năm · Cao đẳng cộng đồng {Years(StudyLevels.CommunityCollege)} năm · Thạc sĩ {Years(StudyLevels.Master)} năm",
                "Dùng để tính tổng chi phí cả khóa."),
            Parameter("exchange_rate", "Tỉ giá USD sang VNĐ",
                quote is null ? NoData : $"{Number(quote.VndPerUsd)} VNĐ/USD",
                RateMeaning(quote)),
            Parameter("exchange_margin", "Biên độ giá bán ra của ngân hàng", Pct(p.BankSellRateSpread),
                "Cộng lên tỉ giá trung bình thị trường khi quy đổi USD sang VNĐ; số VNĐ chỉ để tham khảo."),
            Parameter("exchange_fallback", "Tỉ giá dự phòng", $"{Number(CostConstants.FallbackVndPerUsd)} VNĐ/USD",
                "Dùng khi chưa từng lấy được tỉ giá từ nguồn công khai."),
            Parameter("state_cost_index", "Chỉ số giá sinh hoạt theo bang",
                withIndex.Count == 0 ? NoData : $"{withIndex.Count} bang · năm {withIndex.Max(s => s.Year)}",
                "Chỉ số tham khảo theo từng bang (năm mới nhất có số liệu). Không cộng vào tổng chi phí vì không phải số tiền USD."),
        ];
    }

    private static AlgorithmParameter Parameter(string key, string name, string value, string meaning) =>
        new($"cost_{key}", Step, name, value, meaning);

    private static string RateMeaning(ExchangeRateQuote? quote)
    {
        if (quote is null) return "Tỉ giá không lấy được lúc này.";
        if (quote.IsFallback) return "Đang dùng tỉ giá dự phòng vì chưa lấy được tỉ giá mới từ nguồn công khai.";
        var date = quote.AsOf is { } asOf ? $", lần cập nhật gần nhất {asOf.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}" : "";
        return $"Tỉ giá trung bình thị trường lấy từ {quote.Source}, cập nhật mỗi ngày{date}. Số VNĐ chỉ để tham khảo.";
    }

    // " (nguồn: https://...)." nếu cài đặt có link nguồn, không thì chỉ dấu chấm
    private static string Source(string? json, string key) =>
        ReadText(json, key) is { } url ? $" (nguồn: {url})." : ".";

    private static string? ReadText(string? json, string key)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                   && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // "20 000 USD · 35 000 USD · 50 000 USD · 70 000 USD · Trên 70 000 USD: không có trần", đọc từ metadata hồ sơ theo thứ tự mức.
    // Dựng từ số, không dùng nhãn của mức: nhãn có dấu chấm hàng nghìn ("20.000") mà trang sẽ đổi nhầm thành dấu phẩy thập phân
    private static string BudgetTiers(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return NoData;
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (!doc.RootElement.TryGetProperty("budgetTiers", out var tiers) || tiers.ValueKind != JsonValueKind.Array)
                return NoData;

            var parts = new List<string>();
            foreach (var tier in tiers.EnumerateArray())
            {
                var max = ReadNumber(tier, "maxUsd");
                if (max is { } cap)
                    parts.Add($"{Number(cap)} USD");
                else if (ReadNumber(tier, "minUsd") is { } floor)
                    parts.Add($"Trên {Number(floor)} USD: không có trần");   // mức trên cùng: maxUsd null
            }
            return parts.Count == 0 ? NoData : string.Join(" · ", parts);
        }
        catch (JsonException)
        {
            return NoData;
        }
    }

    private static decimal? ReadNumber(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)
            ? number
            : null;

    private static int Years(string level) => CostCalculator.YearsFor(level) ?? 0;

    private static string Pct(decimal rate) => $"{(rate * 100m).ToString("0.##", CultureInfo.InvariantCulture)}%";

    private static string Money(decimal? amount, string unit) => amount is { } value ? $"{Number(value)} {unit}" : NoData;

    // Dấu cách không ngắt làm ngăn cách hàng nghìn: dấu chấm sẽ bị trang đổi nhầm thành dấu phẩy thập phân
    private static string Number(decimal value) =>
        decimal.Round(value, 0, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', ' ');

    // Đọc nguồn dữ liệu lỗi (DB, mạng) thì trả null để tham số tương ứng ghi "Chưa có số liệu"
    private static async Task<T?> Attempt<T>(Func<Task<T>> read)
    {
        try
        {
            return await read();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return default;
        }
    }
}
