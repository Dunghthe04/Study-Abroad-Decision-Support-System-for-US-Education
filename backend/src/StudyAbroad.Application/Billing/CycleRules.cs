using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing;

/// <summary>Một thay đổi trên một kỳ; áp dụng theo đúng thứ tự (kết thúc kỳ cũ trước khi bắt đầu kỳ mới).</summary>
public sealed record CycleChange(UserSubscription Cycle, string Status, DateTime? StartsAt, DateTime? EndsAt, string? EndedReason, DateTime? EndedAt)
{
    public void Apply()
    {
        Cycle.Status = Status;
        Cycle.StartsAt = StartsAt;
        Cycle.EndsAt = EndsAt;
        Cycle.EndedReason = EndedReason;
        Cycle.EndedAt = EndedAt;
    }
}

/// <summary>Luật ngày tháng và phần trăm của gói, thuần (không DB) để kiểm bằng test. Mọi DateTime là UTC.</summary>
public static class CycleRules
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    /// <summary>00:00 ngày 1 tháng hiện tại giờ Việt Nam, đổi ra UTC (17:00 UTC ngày cuối tháng trước).</summary>
    public static DateTime FreeMonthStartUtc(DateTime nowUtc)
    {
        var vn = nowUtc + VietnamOffset;
        return new DateTime(vn.Year, vn.Month, 1, 0, 0, 0, DateTimeKind.Utc) - VietnamOffset;
    }

    /// <summary>Lúc hạn mức Free làm mới: 00:00 ngày 1 tháng sau giờ Việt Nam, đổi ra UTC.</summary>
    public static DateTime FreeResetsAtUtc(DateTime nowUtc)
    {
        var vn = nowUtc + VietnamOffset;
        return new DateTime(vn.Year, vn.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1) - VietnamOffset;
    }

    private static readonly DateTime FreeAnchorUtc = new(2025, 12, 31, 17, 0, 0, DateTimeKind.Utc);

    /// <summary>Cửa sổ hạn mức Free: null = theo tháng lịch (UTC+7); > 0 = mỗi N ngày tính từ 2026-01-01 00:00 +07:00.</summary>
    public static (DateTime StartUtc, DateTime ResetsAtUtc) FreeWindowUtc(DateTime nowUtc, int? quotaPeriodDays)
    {
        if (quotaPeriodDays is not > 0)
            return (FreeMonthStartUtc(nowUtc), FreeResetsAtUtc(nowUtc));
        var elapsedDays = Math.Max(0, (int)Math.Floor((nowUtc - FreeAnchorUtc).TotalDays));
        var idx = elapsedDays / quotaPeriodDays.Value;
        var start = FreeAnchorUtc.AddDays(idx * quotaPeriodDays.Value);
        return (start, start.AddDays(quotaPeriodDays.Value));
    }

    /// <summary>Cửa sổ cấp token hiện tại của một kỳ trả phí và cờ cho biết đây có phải cửa sổ cuối của kỳ không.</summary>
    public static (DateTime StartUtc, DateTime EndUtc, bool IsFinalWindow) CycleWindowUtc(UserSubscription cycle, DateTime nowUtc)
    {
        if (cycle.StartsAt is not { } startsAt || cycle.EndsAt is not { } endsAt)
            return (DateTime.MinValue, DateTime.MaxValue, true);
        var period = cycle.QuotaPeriodDays is > 0 ? Math.Min(cycle.QuotaPeriodDays.Value, cycle.DurationDays) : cycle.DurationDays;
        if (period >= cycle.DurationDays)
            return (startsAt, endsAt, true);
        var elapsedDays = Math.Max(0, (int)Math.Floor((nowUtc - startsAt).TotalDays));
        var idx = elapsedDays / period;
        var start = startsAt.AddDays(idx * period);
        var rawEnd = start.AddDays(period);
        var isFinal = rawEnd >= endsAt;
        return (start, isFinal ? endsAt : rawEnd, isFinal);
    }

    /// <summary>Hệ số hạn mức mỗi ngày so với gói Free (làm tròn 1 chữ số thập phân); null với gói Free hoặc khi thiếu dữ liệu.</summary>
    public static decimal? QuotaMultiplier(SubscriptionPlan plan, SubscriptionPlan? freePlan)
    {
        if (plan.Kind == PlanKinds.Free || freePlan is null || freePlan.TokenQuota <= 0 || plan.TokenQuota <= 0 || plan.DurationDays is not > 0)
            return null;
        var freePeriod = freePlan.QuotaPeriodDays is > 0 ? freePlan.QuotaPeriodDays.Value : 30;
        var planPeriod = plan.QuotaPeriodDays is > 0 ? Math.Min(plan.QuotaPeriodDays.Value, plan.DurationDays.Value) : plan.DurationDays.Value;
        var ratio = ((decimal)plan.TokenQuota / planPeriod) / ((decimal)freePlan.TokenQuota / freePeriod);
        return Math.Round(ratio, 1, MidpointRounding.AwayFromZero);
    }

    public static DateTimeOffset ToVietnamTime(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(VietnamOffset);

    /// <summary>floor(used × 100 / quota) trong 0..100; quota 0 hiện 100.</summary>
    public static int UsedPercent(long used, long quota) =>
        quota <= 0 ? 100 : (int)Math.Clamp(used * 100 / quota, 0, 100);

    /// <summary>Dùng hết = đã dùng ≥ hạn mức (hạn mức 0 luôn hết).</summary>
    public static bool IsExhausted(long used, long quota) => used >= quota;

    public static bool IsLowQuota(int usedPercent) => usedPercent >= BillingRules.LowQuotaUsedPercent;

    public static bool IsExpiringSoon(DateTime? endsAt, DateTime nowUtc) =>
        endsAt is { } end && end - nowUtc <= TimeSpan.FromDays(BillingRules.ExpiringSoonDays);

    /// <summary>
    /// Đưa một làn tới thời điểm nowUtc. Trả các thay đổi theo thứ tự:
    /// kỳ active quá hạn thì kết thúc (expired) và kỳ queued bắt đầu đúng lúc kỳ cũ hết hạn (không mất ngày đã trả);
    /// kỳ active dùng hết mà có kỳ queued thì kỳ cũ kết thúc ngay (exhausted_rollover) và kỳ queued bắt đầu ngay.
    /// Không có kỳ queued thì kỳ dùng hết vẫn chạy tới hạn (vẫn Premium theo ngày).
    /// </summary>
    public static IReadOnlyList<CycleChange> Advance(UserSubscription? active, UserSubscription? queued, long activeUsed, DateTime nowUtc)
    {
        var changes = new List<CycleChange>();
        var activeStartsAt = active?.StartsAt;   // theo dõi riêng: kỳ vừa bắt đầu trong vòng này chưa được ghi vào thực thể
        var activeEndsAt = active?.EndsAt;
        var activeQuota = active?.TokenQuota ?? 0;
        while (true)
        {
            if (active is null)
            {
                if (queued is null)
                    break;
                // Không có kỳ active mà còn kỳ queued (lần lưu trước bị dở): bắt đầu ngay
                (active, activeStartsAt, activeEndsAt, activeQuota, activeUsed) = Start(changes, queued, nowUtc);
                queued = null;
                continue;
            }
            if (activeEndsAt is not { } end || nowUtc >= end)
            {
                var endedAt = activeEndsAt ?? nowUtc;
                changes.Add(new CycleChange(active, CycleStatuses.Ended, activeStartsAt, activeEndsAt, CycleEndReasons.Expired, endedAt));
                active = null;
                if (queued is null)
                    break;
                (active, activeStartsAt, activeEndsAt, activeQuota, activeUsed) = Start(changes, queued, endedAt);
                queued = null;
                continue;
            }
            if (queued is not null && IsExhausted(activeUsed, activeQuota) && CycleWindowUtc(active, nowUtc).IsFinalWindow)
            {
                // Hết hạn thật của kỳ cũ là lúc đổi kỳ (nowUtc), giống đường thanh toán (OrderService.Payment); không giữ ngày hết hạn ban đầu
                changes.Add(new CycleChange(active, CycleStatuses.Ended, activeStartsAt, nowUtc, CycleEndReasons.ExhaustedRollover, nowUtc));
                (active, activeStartsAt, activeEndsAt, activeQuota, activeUsed) = Start(changes, queued, nowUtc);
                queued = null;
                continue;
            }
            break;
        }
        return changes;
    }

    private static (UserSubscription, DateTime?, DateTime?, long, long) Start(List<CycleChange> changes, UserSubscription cycle, DateTime startsAt)
    {
        var endsAt = startsAt.AddDays(cycle.DurationDays);
        changes.Add(new CycleChange(cycle, CycleStatuses.Active, startsAt, endsAt, null, null));
        return (cycle, startsAt, endsAt, cycle.TokenQuota, 0);   // kỳ vừa bắt đầu chưa dùng token nào
    }
}
