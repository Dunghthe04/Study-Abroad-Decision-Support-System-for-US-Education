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
        var activeEndsAt = active?.EndsAt;
        var activeQuota = active?.TokenQuota ?? 0;
        while (true)
        {
            if (active is null)
            {
                if (queued is null)
                    break;
                // Không có kỳ active mà còn kỳ queued (lần lưu trước bị dở): bắt đầu ngay
                (active, activeEndsAt, activeQuota, activeUsed) = Start(changes, queued, nowUtc);
                queued = null;
                continue;
            }
            if (activeEndsAt is not { } end || nowUtc >= end)
            {
                var endedAt = activeEndsAt ?? nowUtc;
                changes.Add(new CycleChange(active, CycleStatuses.Ended, active.StartsAt, activeEndsAt, CycleEndReasons.Expired, endedAt));
                active = null;
                if (queued is null)
                    break;
                (active, activeEndsAt, activeQuota, activeUsed) = Start(changes, queued, endedAt);
                queued = null;
                continue;
            }
            if (queued is not null && IsExhausted(activeUsed, activeQuota))
            {
                changes.Add(new CycleChange(active, CycleStatuses.Ended, active.StartsAt, activeEndsAt, CycleEndReasons.ExhaustedRollover, nowUtc));
                (active, activeEndsAt, activeQuota, activeUsed) = Start(changes, queued, nowUtc);
                queued = null;
                continue;
            }
            break;
        }
        return changes;
    }

    private static (UserSubscription, DateTime?, long, long) Start(List<CycleChange> changes, UserSubscription cycle, DateTime startsAt)
    {
        var endsAt = startsAt.AddDays(cycle.DurationDays);
        changes.Add(new CycleChange(cycle, CycleStatuses.Active, startsAt, endsAt, null, null));
        return (cycle, endsAt, cycle.TokenQuota, 0);   // kỳ vừa bắt đầu chưa dùng token nào
    }
}
