using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Luật ngày tháng và phần trăm của gói (kế hoạch billing v2: "Pools and tiers", "Percent-only usage").
public class BillingCycleRulesTests
{
    private static DateTime Utc(int y, int m, int d, int h = 0, int min = 0, int s = 0) => new(y, m, d, h, min, s, DateTimeKind.Utc);

    // ---- phần trăm

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(79, 100, 79)]
    [InlineData(80, 100, 80)]
    [InlineData(999, 1000, 99)]        // làm tròn xuống, chưa hết thì không hiện 100
    [InlineData(1000, 1000, 100)]
    [InlineData(1500, 1000, 100)]      // T11: vượt hạn mức do lượt đang chạy, hiện 100
    [InlineData(5, 0, 100)]            // T10: hạn mức 0 luôn hiện 100
    [InlineData(0, 0, 100)]
    [InlineData(-5, 100, 0)]
    public void UsedPercent_IsFloorClampedAndQuotaZeroIsFull(long used, long quota, int expected)
    {
        Assert.Equal(expected, CycleRules.UsedPercent(used, quota));
    }

    [Fact]
    public void Exhausted_WhenUsedReachesQuota_QuotaZeroAlwaysExhausted()
    {
        Assert.False(CycleRules.IsExhausted(499_999, 500_000));
        Assert.True(CycleRules.IsExhausted(500_000, 500_000));
        Assert.True(CycleRules.IsExhausted(0, 0));   // T10
    }

    [Fact]
    public void F32_MemberSharesAddUpToThePoolPercentWithinRounding()
    {
        const long quota = 500_000;
        long[] members = [123_457, 200_001, 50_000, 3];
        var pool = CycleRules.UsedPercent(members.Sum(), quota);
        var shares = members.Sum(m => CycleRules.UsedPercent(m, quota));

        Assert.InRange(pool - shares, 0, members.Length - 1);
    }

    [Fact]
    public void LowQuotaAt80Percent_ExpiringSoonWithin7Days()
    {
        Assert.False(CycleRules.IsLowQuota(79));
        Assert.True(CycleRules.IsLowQuota(80));
        var now = Utc(2026, 11, 20);
        Assert.True(CycleRules.IsExpiringSoon(now.AddDays(7), now));
        Assert.False(CycleRules.IsExpiringSoon(now.AddDays(7).AddSeconds(1), now));
        Assert.False(CycleRules.IsExpiringSoon(null, now));
    }

    // ---- T2: hạn mức Free làm mới 00:00 ngày 1 giờ Việt Nam (17:00 UTC ngày cuối tháng trước)

    [Fact]
    public void T2_FreeMonthTurnsAt1700UtcOnTheLastDay()
    {
        Assert.Equal(Utc(2026, 10, 31, 17), CycleRules.FreeMonthStartUtc(Utc(2026, 11, 30, 16, 59, 59)));
        Assert.Equal(Utc(2026, 11, 30, 17), CycleRules.FreeMonthStartUtc(Utc(2026, 11, 30, 17)));
        Assert.Equal(Utc(2026, 11, 30, 17), CycleRules.FreeResetsAtUtc(Utc(2026, 11, 30, 16, 59, 59)));
        Assert.Equal(Utc(2026, 12, 31, 17), CycleRules.FreeResetsAtUtc(Utc(2026, 11, 30, 17)));
    }

    [Fact]
    public void T2_ResetsAtIsShownInVietnamTime()
    {
        var resetsAt = CycleRules.ToVietnamTime(CycleRules.FreeResetsAtUtc(Utc(2026, 11, 20, 3)));

        Assert.Equal("2026-12-01T00:00:00+07:00", resetsAt.ToString("yyyy-MM-ddTHH:mm:sszzz"));
    }

    [Fact]
    public void T2_ShortMonthsDoNotDrift()
    {
        // Ngày 1/3 giờ Việt Nam (28/2 17:00 UTC): làm mới tiếp theo là 1/4, không phải 29/3
        Assert.Equal(Utc(2027, 3, 31, 17), CycleRules.FreeResetsAtUtc(Utc(2027, 2, 28, 17)));
        Assert.Equal(Utc(2027, 2, 28, 17), CycleRules.FreeResetsAtUtc(Utc(2027, 1, 31, 17)));
    }

    // ---- chu kỳ cấp token động (FreeWindowUtc, CycleWindowUtc, QuotaMultiplier)

    [Fact]
    public void FreeWindowUtc_NullUsesCalendarMonth_PositiveUsesAnchorCadence()
    {
        var now = Utc(2026, 1, 10, 3);   // 2026-01-10 10:00 +07:00 (9 ngày sau 2026-01-01 00:00 +07:00)
        var (mStart, mEnd) = CycleRules.FreeWindowUtc(now, null);
        Assert.Equal((Utc(2025, 12, 31, 17), Utc(2026, 1, 31, 17)), (mStart, mEnd));

        var (wStart, wEnd) = CycleRules.FreeWindowUtc(now, 7);
        // 9 / 7 = idx 1 -> start = anchor + 7d = 2026-01-07 17:00 UTC, end = 2026-01-14 17:00 UTC
        Assert.Equal((Utc(2026, 1, 7, 17), Utc(2026, 1, 14, 17)), (wStart, wEnd));
    }

    [Fact]
    public void CycleWindowUtc_SplitsRecurringCycleAndClampsFinalWindow()
    {
        var start = Utc(2026, 11, 1);
        var cycle = new UserSubscription
        {
            Kind = PlanKinds.Personal, Status = CycleStatuses.Active, StartsAt = start, EndsAt = start.AddDays(35),
            DurationDays = 35, QuotaPeriodDays = 14, TokenQuota = 100_000, PlanName = "35d/14d",
        };

        var w0 = CycleRules.CycleWindowUtc(cycle, start.AddDays(5));
        var w1 = CycleRules.CycleWindowUtc(cycle, start.AddDays(14));
        var w2 = CycleRules.CycleWindowUtc(cycle, start.AddDays(30));

        Assert.Equal((start, start.AddDays(14), false), w0);
        Assert.Equal((start.AddDays(14), start.AddDays(28), false), w1);
        Assert.Equal((start.AddDays(28), start.AddDays(35), true), w2);
    }

    [Fact]
    public void QuotaMultiplier_ComputesDailyRatioAgainstFreePlan()
    {
        var free = new SubscriptionPlan { Kind = PlanKinds.Free, TokenQuota = 120_000, QuotaPeriodDays = null };
        var p1m = new SubscriptionPlan { Kind = PlanKinds.Personal, DurationDays = 30, QuotaPeriodDays = 30, TokenQuota = 500_000 };
        var p3m = new SubscriptionPlan { Kind = PlanKinds.Personal, DurationDays = 90, QuotaPeriodDays = 30, TokenQuota = 600_000 };
        var customFree = new SubscriptionPlan { Kind = PlanKinds.Free, TokenQuota = 70_000, QuotaPeriodDays = 7 };   // 10k/ngày
        var p14d = new SubscriptionPlan { Kind = PlanKinds.Personal, DurationDays = 14, QuotaPeriodDays = 7, TokenQuota = 350_000 };   // 50k/ngày

        Assert.Null(CycleRules.QuotaMultiplier(free, free));
        Assert.Equal(4.2m, CycleRules.QuotaMultiplier(p1m, free));
        Assert.Equal(5.0m, CycleRules.QuotaMultiplier(p3m, free));
        Assert.Equal(5.0m, CycleRules.QuotaMultiplier(p14d, customFree));
        Assert.Null(CycleRules.QuotaMultiplier(p1m, new SubscriptionPlan { Kind = PlanKinds.Free, TokenQuota = 0 }));
    }

    [Fact]
    public void IsRecurring_OnlyWhenTheQuotaPeriodIsShorterThanTheCycle()
    {
        Assert.True(CycleRules.IsRecurring(new UserSubscription { DurationDays = 90, QuotaPeriodDays = 30 }));
        Assert.False(CycleRules.IsRecurring(new UserSubscription { DurationDays = 30, QuotaPeriodDays = 30 }));
        Assert.False(CycleRules.IsRecurring(new UserSubscription { DurationDays = 30, QuotaPeriodDays = null }));
    }
}
