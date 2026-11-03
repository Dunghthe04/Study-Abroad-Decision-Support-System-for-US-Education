using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using StudyAbroad.Infrastructure.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>Tỷ giá USD→VNĐ: lấy mới, nhớ lại, lỗi mạng thì dùng tỷ giá cũ hoặc 25.000</summary>
public class ExchangeRateProviderTests
{
    // Trả lần lượt các câu trả lời đã chuẩn bị; null = lỗi mạng
    private sealed class FakeHandler(params string?[] bodies) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = bodies[Math.Min(Calls++, bodies.Length - 1)];
            if (body is null)
                throw new HttpRequestException("mất mạng");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static ExchangeRateProvider Provider(FakeHandler handler, IMemoryCache? cache = null) =>
        new(new HttpClient(handler), cache ?? new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ExchangeRateProvider>.Instance);

    [Fact]
    public async Task ReadsVndRate_AndRemembersIt()
    {
        var handler = new FakeHandler("""{"rates":{"VND":25805.8}}""");
        var provider = Provider(handler);

        Assert.Equal(25805.8m, await provider.GetVndPerUsdAsync());
        Assert.Equal(25805.8m, await provider.GetVndPerUsdAsync());
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task NetworkError_UsesFallback()
    {
        var provider = Provider(new FakeHandler((string?)null));

        Assert.Equal(ExchangeRateProvider.FallbackVndPerUsd, await provider.GetVndPerUsdAsync());
    }

    [Theory]
    [InlineData("""{"rates":{"VND":5}}""")]
    [InlineData("""{"rates":{"USD":1}}""")]
    [InlineData("không phải json")]
    public async Task StrangeData_UsesFallback(string body)
    {
        var provider = Provider(new FakeHandler(body));

        Assert.Equal(ExchangeRateProvider.FallbackVndPerUsd, await provider.GetVndPerUsdAsync());
    }

    [Fact]
    public async Task ErrorAfterGoodRate_KeepsLastGoodRate()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new FakeHandler("""{"rates":{"VND":26000}}""", null);
        var provider = Provider(handler, cache);
        await provider.GetVndPerUsdAsync();
        cache.Remove("exchange_rate_vnd_per_usd"); // giả như đã qua 1 ngày

        Assert.Equal(26000m, await provider.GetVndPerUsdAsync());
        Assert.Equal(2, handler.Calls);
    }
}
