using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Profile.Flow;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Services;

/// <summary>
/// Lấy tỷ giá USD→VNĐ từ open.er-api.com (miễn phí, không cần khóa), nhớ 1 ngày rồi mới lấy lại.
/// Lấy lỗi thì giữ tỷ giá cũ (chưa có thì 25.000), 1 giờ sau thử lại. Giống exchange_rate.py bên advisor
/// </summary>
public class ExchangeRateProvider(HttpClient http, IMemoryCache cache, ILogger<ExchangeRateProvider> logger)
    : IExchangeRateProvider
{
    public const string RateUrl = "https://open.er-api.com/v6/latest/USD";
    public const decimal FallbackVndPerUsd = 25_000m;
    private const string CacheKey = "exchange_rate_vnd_per_usd";
    private const string LastGoodKey = "exchange_rate_vnd_per_usd_last_good";
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(1);
    private static readonly TimeSpan RetryAfter = TimeSpan.FromHours(1);
    // Tỷ giá ngoài khoảng này coi là dữ liệu hỏng, không dùng
    private const decimal MinSane = 10_000m, MaxSane = 100_000m;

    public async Task<decimal> GetVndPerUsdAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out decimal rate))
            return rate;
        try
        {
            using var doc = await http.GetFromJsonAsync<JsonDocument>(RateUrl, ct)
                ?? throw new JsonException("không có dữ liệu");
            rate = doc.RootElement.GetProperty("rates").GetProperty("VND").GetDecimal();
            if (rate is <= MinSane or >= MaxSane)
                throw new JsonException($"tỷ giá lạ: {rate}");
        }
        // Hết giờ chờ (TaskCanceled) cũng coi là lỗi mạng, trừ khi chính request bị hủy
        catch (Exception ex) when (!ct.IsCancellationRequested
                                   && ex is HttpRequestException or JsonException or KeyNotFoundException
                                       or InvalidOperationException or FormatException or TaskCanceledException)
        {
            var old = cache.TryGetValue(LastGoodKey, out decimal lastGood) ? lastGood : FallbackVndPerUsd;
            logger.LogWarning("exchange_rate: không lấy được tỷ giá ({Error}), dùng {Rate}", ex.Message, old);
            cache.Set(CacheKey, old, RetryAfter);
            return old;
        }
        cache.Set(CacheKey, rate, MaxAge);
        cache.Set(LastGoodKey, rate);
        return rate;
    }
}
