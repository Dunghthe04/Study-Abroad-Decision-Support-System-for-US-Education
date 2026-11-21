using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Billing.Family;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Jobs;

/// <summary>
/// Năm bước của job nền thanh toán. Mỗi mục (đơn, chủ kỳ, kỳ) chạy trong try/catch riêng; mục lỗi bị bỏ tracking để không kéo các mục sau hỏng theo
/// (DbContext dùng chung trong một scope giữ lại thay đổi dang dở).
/// </summary>
public sealed class BillingJobService(
    IBillingJobRepository jobs,
    IBillingRepository billing,
    IOrderRepository orders,
    OrderReconciler reconciler,
    IEntitlementService entitlements,
    IFamilyService family,
    IUserRepository users,
    IEmailSender email,
    INotificationService notifications,
    BillingOptions options,
    ILogger<BillingJobService>? logger = null) : IBillingJobService
{
    public async Task<int> ReconcilePendingOrdersAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var handled = 0;
        foreach (var order in await jobs.ListPendingOrdersAsync(ct))
        {
            if (await TryAsync($"đối soát đơn {order.OrderCode}", () => reconciler.ReconcileAsync(order.OrderCode, nowUtc, ct), ct))
                handled++;
        }

        // Đơn đã hết hạn mà ExpiresAt còn trong cửa sổ trả muộn: hỏi lại payOS, PAID thì kích hoạt (C6), còn lại giữ nguyên. Không tính vào số đơn đã xử lý.
        // ponytail: mỗi lượt một lần gọi payOS cho mỗi đơn mới hết hạn (tối đa 200); nếu payOS giới hạn tốc độ thì chỉ hỏi lại thưa hơn khi đơn đã hết hạn lâu
        foreach (var order in await jobs.ListRecentlyExpiredOrdersAsync(nowUtc.AddHours(-options.UnknownStatusExpiryHours), ct))
            await TryAsync($"đối soát lại đơn đã hết hạn {order.OrderCode}", () => reconciler.ReconcileAsync(order.OrderCode, nowUtc, ct), ct);
        return handled;
    }

    public async Task<int> EndExpiredCyclesAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var handled = 0;
        foreach (var due in await jobs.FindDueCycleOwnersAsync(nowUtc, ct))
        {
            if (await TryAsync($"kết thúc kỳ hết hạn loại {due.Kind} của {due.OwnerUserId}", () => entitlements.EndExpiredCyclesAsync(due.OwnerUserId, due.Kind, nowUtc, ct), ct))
                handled++;
        }
        return handled;
    }

    public Task<int> ExpireFamilyInvitesAsync(DateTime nowUtc, CancellationToken ct = default) => family.ExpireInvitesAsync(nowUtc, ct);

    public async Task<int> SendRenewalRemindersAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var handled = 0;
        foreach (var cycle in await jobs.FindCyclesDueForReminderAsync(nowUtc, nowUtc.AddDays(options.ReminderDays), ct))
        {
            var ok = await TryAsync($"nhắc gia hạn kỳ {cycle.Id}", async () =>
            {
                // Ghi dấu trước rồi mới gửi: lỗi gửi thì mất một lần nhắc, không bao giờ nhắc lặp mỗi lượt
                cycle.ReminderSentAt = nowUtc;
                await jobs.SaveAsync(ct);
                if (await users.GetUserById(cycle.UserId, ct) is not { DeletedAt: null } owner)
                    return;
                var endsAt = cycle.EndsAt!.Value;
                await email.SendSubscriptionExpiringEmailAsync(owner.Email, owner.FullName, cycle.PlanName, endsAt, ct);
                var days = Math.Max(1, (int)Math.Ceiling((endsAt - nowUtc).TotalDays));
                await notifications.NotifyAsync(owner.Id, NotificationTypes.BillingExpiring, $"Gói {cycle.PlanName} sắp hết hạn",
                    $"Gói {cycle.PlanName} còn khoảng {days} ngày. Gia hạn để không bị gián đoạn.", "/plans", ct);
            }, ct);
            // Lỗi thì dừng bước: TryAsync đã bỏ theo dõi mọi kỳ còn lại trong danh sách, nên gán dấu thời gian cho chúng sẽ không được lưu
            // mà thông báo vẫn gửi (lượt sau gửi lại). Lượt sau mở scope mới, nạp lại và làm tiếp phần còn lại.
            if (!ok)
                break;
            handled++;
        }
        return handled;
    }

    public async Task<int> SendUsageNotificationsAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var cycles = await jobs.FindActiveCyclesAwaitingUsageNoticeAsync(ct);
        if (cycles.Count == 0)
            return 0;
        var singleWindowIds = cycles
            .Where(c => !(c.QuotaPeriodDays is > 0 && c.QuotaPeriodDays.Value < c.DurationDays))
            .Select(c => c.Id)
            .ToList();
        var used = singleWindowIds.Count > 0
            ? await jobs.SumCycleTokensAsync(singleWindowIds, ct)
            : new Dictionary<Guid, long>();

        var handled = 0;
        foreach (var cycle in cycles)
        {
            var (windowStart, windowEnd, _) = CycleRules.CycleWindowUtc(cycle, nowUtc);
            var isRecurring = cycle.QuotaPeriodDays is > 0 && cycle.QuotaPeriodDays.Value < cycle.DurationDays;
            var tokens = isRecurring
                ? await billing.SumCycleTokensAsync(cycle.Id, windowStart, windowEnd, ct)
                : used.GetValueOrDefault(cycle.Id);
            var warnedInWindow = cycle.Warned80At is { } w && w >= windowStart;
            var exhaustedInWindow = cycle.ExhaustedNotifiedAt is { } e && e >= windowStart;
            var reached80 = !warnedInWindow && CycleRules.IsLowQuota(CycleRules.UsedPercent(tokens, cycle.TokenQuota));
            var reached100 = !exhaustedInWindow && CycleRules.IsExhausted(tokens, cycle.TokenQuota);
            if (!reached80 && !reached100)
                continue;
            var flagged = false;
            var ok = await TryAsync($"thông báo dùng hạn mức kỳ {cycle.Id}", async () =>
            {
                // Danh sách đọc không theo dõi: nạp riêng kỳ này (đang theo dõi) rồi mới ghi dấu; kỳ đã được báo trong cửa sổ này thì thôi
                if (await jobs.FindCycleAsync(cycle.Id, ct) is not { } tracked)
                    return;
                var send80 = reached80 && !(tracked.Warned80At is { } tw && tw >= windowStart);
                var send100 = reached100 && !(tracked.ExhaustedNotifiedAt is { } te && te >= windowStart);
                if (!send80 && !send100)
                    return;
                // Ghi dấu trước rồi mới gửi (một lần mỗi cửa sổ)
                if (send80)
                    tracked.Warned80At = nowUtc;
                if (send100)
                    tracked.ExhaustedNotifiedAt = nowUtc;
                await jobs.SaveAsync(ct);
                flagged = true;
                foreach (var userId in await RecipientsAsync(tracked, ct))
                {
                    if (send80)
                        await notifications.NotifyAsync(userId, NotificationTypes.BillingUsage80, "Bạn đã dùng 80% lượt của kỳ này",
                            $"Gói {tracked.PlanName}: bạn đã dùng 80% lượt hỏi của kỳ này.", "/plans", ct);
                    if (send100)
                        await notifications.NotifyAsync(userId, NotificationTypes.BillingUsage100, "Bạn đã dùng hết lượt của kỳ này",
                            $"Gói {tracked.PlanName}: bạn đã dùng hết lượt hỏi của kỳ này.", "/plans", ct);
                }
            }, ct);
            // Lỗi thì dừng bước: ghi dấu hay gửi lỗi thường là sự cố chung (DB, thông báo), làm tiếp chỉ đốt thêm một lần báo mỗi kỳ sau.
            // Dấu đã ghi trước khi gửi nên kỳ lỗi mất một lần báo, không bao giờ báo lặp; lượt sau mở scope mới, nạp lại và làm tiếp phần còn lại.
            if (!ok)
                break;
            if (flagged)
                handled++;
        }
        return handled;
    }

    /// <summary>Chủ kỳ, và với kỳ Gia đình thì thêm các thành viên đang active (không có lời mời chờ).</summary>
    private async Task<IReadOnlyList<Guid>> RecipientsAsync(UserSubscription cycle, CancellationToken ct)
    {
        var recipients = new List<Guid> { cycle.UserId };
        if (cycle.Kind == PlanKinds.Family && cycle.FamilyGroupId is { } groupId)
            recipients.AddRange((await billing.GetOpenMembersAsync(groupId, ct))
                .Where(m => m.Status == FamilyMemberStatuses.Active && m.UserId is not null).Select(m => m.UserId!.Value));
        return recipients.Distinct().ToList();
    }

    /// <summary>Chạy một mục; lỗi thường thì ghi log, bỏ theo dõi các thay đổi dang dở và báo false. Hủy thì ném tiếp.</summary>
    private async Task<bool> TryAsync(string what, Func<Task> work, CancellationToken ct)
    {
        try
        {
            await work();
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogError(ex, "[Billing job] Lỗi khi {What}", what);
            orders.ClearTracking();
            return false;
        }
    }
}
