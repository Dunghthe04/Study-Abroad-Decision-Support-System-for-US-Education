using StudyAbroad.Application.Costs;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

// UC-65: Moderator theo dõi tỉ giá đang dùng, thời điểm cập nhật, nguồn, số ngày; cảnh báo quá 2 ngày hoặc đang dùng tỉ giá dự phòng
public class ExchangeRateStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private static ExchangeRateQuote Fresh(DateTimeOffset asOf, bool fallback = false) =>
        new(26_000m, asOf, "open.er-api.com", fallback);

    [Fact]
    public void ShowsTheStoredRate_TheSource_TheDate_AndTheDaysSinceTheUpdate()
    {
        var status = ExchangeRateStatus.From(Fresh(Now.AddDays(-1).AddHours(-3)), 0.015m, Now);

        Assert.Equal(26_000m, status.MidVndPerUsd);
        Assert.Equal("open.er-api.com", status.Source);
        Assert.Equal(Now.AddDays(-1).AddHours(-3), status.AsOf);
        Assert.Equal(1, status.DaysSinceUpdate);   // 1 ngày 3 giờ: tính tròn xuống
        Assert.False(status.IsStale);
        Assert.False(status.IsFallback);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]    // đúng 2 ngày chưa cảnh báo
    [InlineData(3, true)]     // quá 2 ngày thì cảnh báo dữ liệu cũ
    [InlineData(10, true)]
    public void TheDataIsStaleOnlyAfterMoreThanTwoDays(int days, bool stale)
    {
        var status = ExchangeRateStatus.From(Fresh(Now.AddDays(-days)), 0.015m, Now);

        Assert.Equal(days, status.DaysSinceUpdate);
        Assert.Equal(stale, status.IsStale);
    }

    [Fact]
    public void WhenNoRateWasEverFetched_TheFallbackIsReported_NotMarkedStale()
    {
        var quote = new ExchangeRateQuote(CostConstants.FallbackVndPerUsd, null, "open.er-api.com", true);

        var status = ExchangeRateStatus.From(quote, 0.015m, Now);

        Assert.True(status.IsFallback);
        Assert.False(status.IsStale);
        Assert.Null(status.AsOf);
        Assert.Null(status.DaysSinceUpdate);
        Assert.Equal(CostConstants.FallbackVndPerUsd, status.FallbackVndPerUsd);
    }

    [Fact]
    public void TheCostRateIsTheMidRatePlusTheBankSellSpread()
    {
        var status = ExchangeRateStatus.From(Fresh(Now), 0.015m, Now);

        Assert.Equal(0.015m, status.BankSellRateSpread);
        Assert.Equal(26_390m, status.SellVndPerUsd);   // 26.000 x 1,015
    }

    [Fact]
    public void AClockBehindTheUpdateTime_NeverGivesNegativeDays()
    {
        var status = ExchangeRateStatus.From(Fresh(Now.AddHours(1)), 0.015m, Now);

        Assert.Equal(0, status.DaysSinceUpdate);
    }
}
