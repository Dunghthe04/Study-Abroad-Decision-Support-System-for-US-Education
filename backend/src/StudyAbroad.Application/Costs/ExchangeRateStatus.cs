using StudyAbroad.Application.Profile.Flow;

namespace StudyAbroad.Application.Costs;

/// <summary>
/// [UC-65] Trạng thái tỉ giá USD sang VNĐ cho Moderator theo dõi. MidVndPerUsd là tỉ giá trung bình thị trường đang lưu; SellVndPerUsd là tỉ giá đã cộng
/// biên độ giá bán ra của ngân hàng (BankSellRateSpread), dùng quy đổi chi phí (UC-26). AsOf null khi chưa từng lấy được (đang dùng tỉ giá dự phòng).
/// DaysSinceUpdate: số ngày từ lần cập nhật gần nhất (null khi chưa có). IsStale: quá 2 ngày chưa cập nhật được. IsFallback: đang dùng tỉ giá dự phòng.
/// </summary>
public record ExchangeRateStatusDto(
    decimal MidVndPerUsd,
    decimal BankSellRateSpread,
    decimal SellVndPerUsd,
    DateTimeOffset? AsOf,
    string Source,
    int? DaysSinceUpdate,
    bool IsStale,
    bool IsFallback,
    decimal FallbackVndPerUsd);

public static class ExchangeRateStatus
{
    /// <summary>Quá chừng này ngày chưa cập nhật được thì cảnh báo dữ liệu cũ (UC-65 E1).</summary>
    public const int StaleAfterDays = 2;

    public static ExchangeRateStatusDto From(ExchangeRateQuote quote, decimal bankSellRateSpread, DateTimeOffset now)
    {
        int? days = quote.AsOf is { } asOf ? Math.Max(0, (int)Math.Floor((now - asOf).TotalDays)) : null;
        var sell = Math.Round(quote.VndPerUsd * (1m + bankSellRateSpread), 2, MidpointRounding.AwayFromZero);
        return new ExchangeRateStatusDto(
            quote.VndPerUsd, bankSellRateSpread, sell, quote.AsOf, quote.Source, days,
            // Tỉ giá dự phòng không có ngày, nên không coi là "cũ" mà báo riêng bằng IsFallback; có ngày thì cũ khi quá StaleAfterDays
            IsStale: days is { } d && d > StaleAfterDays,
            IsFallback: quote.IsFallback,
            FallbackVndPerUsd: StudyAbroad.Domain.Constants.CostConstants.FallbackVndPerUsd);
    }
}
