using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Luật ngày tháng và phần trăm của gói (kế hoạch billing v2: "Pools and tiers", "Percent-only usage").
public class BillingCycleRulesTests
{
    private static DateTime Utc(int y, int m, int d, int h = 0, int min = 0, int s = 0) => new(y, m, d, h, min, s, DateTimeKind.Utc);

    private static UserSubscription Active(DateTime startsAt, int days = 30, long quota = 500_000) => new()
    {
        Kind = PlanKinds.Personal, Status = CycleStatuses.Active, StartsAt = startsAt, EndsAt = startsAt.AddDays(days),
        DurationDays = days, TokenQuota = quota, PlanName = "Cá nhân 1 tháng",
    };

    private static UserSubscription Queued(int days = 30, long quota = 500_000) => new()
    {
        Kind = PlanKinds.Personal, Status = CycleStatuses.Queued, DurationDays = days, TokenQuota = quota, PlanName = "Cá nhân 3 tháng",
    };

    private static void ApplyAll(IReadOnlyList<CycleChange> changes)
    {
        foreach (var c in changes)
            c.Apply();
    }

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

    // ---- chuyển kỳ

    [Fact]
    public void NothingDue_NoChanges()
    {
        var active = Active(Utc(2026, 11, 1));

        Assert.Empty(CycleRules.Advance(active, Queued(), 100, Utc(2026, 11, 20)));
    }

    [Fact]
    public void R2_QueuedStartsExactlyAtTheOldEndDate()
    {
        var active = Active(Utc(2026, 11, 1));
        var queued = Queued(days: 90);
        var oldEnd = active.EndsAt!.Value;

        var changes = CycleRules.Advance(active, queued, 100, oldEnd.AddHours(5));   // job trễ 5 giờ
        ApplyAll(changes);

        Assert.Equal(2, changes.Count);
        Assert.Same(active, changes[0].Cycle);   // kết thúc kỳ cũ trước, kích hoạt kỳ mới sau (index một active mỗi làn)
        Assert.Equal((CycleStatuses.Ended, CycleEndReasons.Expired, oldEnd), (active.Status, active.EndedReason, active.EndedAt));
        Assert.Equal((CycleStatuses.Active, oldEnd, oldEnd.AddDays(90)), (queued.Status, queued.StartsAt, queued.EndsAt));
    }

    [Fact]
    public void R3_ExhaustedWithQueued_OldEndsNowAndQueuedStartsNowForItsFullDuration()
    {
        var active = Active(Utc(2026, 11, 1));
        var queued = Queued(days: 30);
        var now = Utc(2026, 11, 10, 8);

        ApplyAll(CycleRules.Advance(active, queued, 500_000, now));

        Assert.Equal((CycleStatuses.Ended, CycleEndReasons.ExhaustedRollover, now), (active.Status, active.EndedReason, active.EndedAt));
        Assert.Equal(Utc(2026, 12, 1), active.EndsAt);   // ngày còn lại bị bỏ (D10), không sửa end date cũ
        Assert.Equal((CycleStatuses.Active, now, now.AddDays(30)), (queued.Status, queued.StartsAt, queued.EndsAt));
    }

    [Fact]
    public void ExhaustedWithoutQueued_KeepsRunningUntilItsEndDate()
    {
        var active = Active(Utc(2026, 11, 1));

        Assert.Empty(CycleRules.Advance(active, null, 900_000, Utc(2026, 11, 10)));
        Assert.Equal(CycleStatuses.Active, active.Status);
    }

    [Fact]
    public void R7_ExpiresWithNoRenewal()
    {
        var active = Active(Utc(2026, 10, 1));

        var changes = CycleRules.Advance(active, null, 0, Utc(2026, 11, 20));
        ApplyAll(changes);

        Assert.Single(changes);
        Assert.Equal((CycleStatuses.Ended, CycleEndReasons.Expired, Utc(2026, 10, 31)), (active.Status, active.EndedReason, active.EndedAt));
    }

    [Fact]
    public void R9_JobStoppedLongerThanTheRenewal_BothCyclesEndInOrder()
    {
        var active = Active(Utc(2026, 9, 1));        // hết 1/10
        var queued = Queued(days: 30);                // chạy 1/10 → 31/10

        var changes = CycleRules.Advance(active, queued, 0, Utc(2026, 11, 20));
        ApplyAll(changes);

        Assert.Equal(3, changes.Count);
        Assert.Equal((CycleStatuses.Ended, Utc(2026, 10, 1), Utc(2026, 10, 31)), (queued.Status, queued.StartsAt, queued.EndedAt));
        Assert.Equal(CycleEndReasons.Expired, queued.EndedReason);
    }

    [Fact]
    public void T10_QuotaZeroWithQueued_RollsOverOnceAndStops()
    {
        var active = Active(Utc(2026, 11, 1), quota: 0);
        var queued = Queued(quota: 0);
        var now = Utc(2026, 11, 2);

        var changes = CycleRules.Advance(active, queued, 0, now);
        ApplyAll(changes);

        Assert.Equal(2, changes.Count);
        Assert.Equal(CycleStatuses.Active, queued.Status);   // kỳ mới vẫn active (Premium theo ngày), chỉ không còn token
    }
}
