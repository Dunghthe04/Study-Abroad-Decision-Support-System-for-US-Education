using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Billing.Family;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Jobs;

/// <summary>
/// Năm bước của job nền thanh toán. Mỗi mục (đơn, làn, kỳ) chạy trong try/catch riêng; mục lỗi bị bỏ tracking để không kéo các mục sau hỏng theo
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
        return handled;
    }

    public async Task<int> AdvanceDueLanesAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var handled = 0;
        foreach (var lane in await jobs.FindDueLanesAsync(nowUtc, ct))
        {
            if (await TryAsync($"chuyển kỳ làn {lane.Kind} của {lane.OwnerUserId}", () => entitlements.AdvanceLaneAsync(lane.OwnerUserId, lane.Kind, nowUtc, ct), ct))
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
            if (ok)
                handled++;
        }
        return handled;
    }

    public async Task<int> SendUsageNotificationsAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var cycles = await jobs.FindActiveCyclesAwaitingUsageNoticeAsync(ct);
        if (cycles.Count == 0)
            return 0;
        var used = await jobs.SumCycleTokensAsync(cycles.Select(c => c.Id).ToList(), ct);

        var handled = 0;
        foreach (var cycle in cycles)
        {
            var tokens = used.GetValueOrDefault(cycle.Id);
            var reached80 = cycle.Warned80At is null && CycleRules.IsLowQuota(CycleRules.UsedPercent(tokens, cycle.TokenQuota));
            var reached100 = cycle.ExhaustedNotifiedAt is null && CycleRules.IsExhausted(tokens, cycle.TokenQuota);
            if (!reached80 && !reached100)
                continue;
            var ok = await TryAsync($"thông báo dùng hạn mức kỳ {cycle.Id}", async () =>
            {
                // Ghi dấu trước rồi mới gửi (một lần mỗi kỳ)
                if (reached80)
                    cycle.Warned80At = nowUtc;
                if (reached100)
                    cycle.ExhaustedNotifiedAt = nowUtc;
                await jobs.SaveAsync(ct);
                foreach (var userId in await RecipientsAsync(cycle, ct))
                {
                    if (reached80)
                        await notifications.NotifyAsync(userId, NotificationTypes.BillingUsage80, "Bạn đã dùng 80% lượt của kỳ này",
                            $"Gói {cycle.PlanName}: bạn đã dùng 80% lượt hỏi của kỳ này.", "/plans", ct);
                    if (reached100)
                        await notifications.NotifyAsync(userId, NotificationTypes.BillingUsage100, "Bạn đã dùng hết lượt của kỳ này",
                            $"Gói {cycle.PlanName}: bạn đã dùng hết lượt hỏi của kỳ này.", "/plans", ct);
                }
            }, ct);
            if (ok)
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
