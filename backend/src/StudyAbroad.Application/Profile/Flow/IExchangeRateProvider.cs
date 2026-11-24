using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>[USAS-2029] Tỷ giá kèm ngày áp dụng và nguồn. IsFallback: không lấy được tỷ giá thật nên dùng số tạm.</summary>
public record ExchangeRateQuote(decimal VndPerUsd, DateTimeOffset? AsOf, string Source, bool IsFallback);

/// <summary>Tỷ giá 1 USD = ? VNĐ để form đổi ngân sách VNĐ sang USD (tương đối, nhớ 1 ngày)</summary>
public interface IExchangeRateProvider
{
    Task<decimal> GetVndPerUsdAsync(CancellationToken ct = default);

    /// <summary>Mặc định chỉ có số tiền, chưa biết ngày; bản thật ghi đè để trả ngày và nguồn.</summary>
    async Task<ExchangeRateQuote> GetQuoteAsync(CancellationToken ct = default) =>
        new(await GetVndPerUsdAsync(ct), null, "unknown", false);
}
