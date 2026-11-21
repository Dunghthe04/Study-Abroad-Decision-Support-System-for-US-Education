using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Orders;

// Áp dụng thanh toán: đường tiền. Một transaction, khóa dòng đơn, các thay đổi làn / nhóm / đơn lưu theo từng bước
// (kết thúc kỳ cũ trước khi thêm kỳ mới để index một-active-mỗi-làn không vấp), thử lại một lần khi vướng index duy nhất.
public sealed partial class OrderService
{
    private const string LaneFullNote = "lane_full: đã có kỳ đang chạy và kỳ chờ";

    public async Task<bool> HandleWebhookAsync(string rawBody, DateTime nowUtc, CancellationToken ct = default)
    {
        var webhook = await gateway.VerifyWebhookAsync(rawBody, ct);
        if (webhook is null)
            return false;
        // Webhook báo thanh toán thất bại: không có gì để làm. Đơn không biết cũng trả 200 để payOS không gửi lại mãi (P18)
        if (webhook.Code != VerifiedWebhook.SuccessCode)
            return true;
        await ApplyPaymentAsync(webhook.OrderCode, webhook.Amount, webhook.SanitizedDataJson, nowUtc, ct);
        return true;
    }

    public async Task<PaymentApplyResult> ApplyPaymentAsync(
        long orderCode, long amountPaid, string? payloadJson, DateTime nowUtc, CancellationToken ct = default, PaymentApplyOptions? options = null)
    {
        options ??= new PaymentApplyOptions();
        for (var attempt = 1; ; attempt++)
        {
            // Việc sau commit gom theo từng lần thử: lần thử bị hủy không gửi biên nhận, lần thử thành công mới gửi
            var afterCommit = new List<Func<CancellationToken, Task>>();
            var result = new PaymentApplyResult(PaymentApplyStatus.UnknownOrder);
            // Xóa theo dõi để đọc dữ liệu mới (người gọi có thể đã nạp đơn hay kỳ này) và không ghi lại phần dang dở của lần thử trước
            orders.ClearTracking();
            try
            {
                await unitOfWork.ExecuteInTransactionAsync(
                    async txCt => result = await ApplyInTransactionAsync(orderCode, amountPaid, payloadJson, nowUtc, options, afterCommit, txCt), ct);
            }
            catch (Exception ex) when (attempt == 1 && orders.IsConflict(ex))
            {
                logger?.LogWarning(ex, "Xung đột khi áp dụng thanh toán cho đơn {OrderCode}, thử lại một lần", orderCode);
                continue;
            }

            // Tiền đã vào sổ: thông báo và biên nhận lỗi không được làm hỏng kết quả (và không chạy lại khi webhook gửi lại)
            foreach (var effect in afterCommit)
            {
                try
                {
                    await effect(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Việc sau thanh toán của đơn {OrderCode} lỗi", orderCode);
                }
            }
            return result;
        }
    }

    private async Task<PaymentApplyResult> ApplyInTransactionAsync(
        long orderCode, long amountPaid, string? payloadJson, DateTime now, PaymentApplyOptions options,
        List<Func<CancellationToken, Task>> afterCommit, CancellationToken ct)
    {
        // 1. Khóa dòng đơn rồi nạp bản mới nhất: webhook, job và admin cùng lúc chỉ một bên đi tiếp, bên kia thấy đơn đã trả
        var order = await orders.LockByCodeAsync(orderCode, ct);
        if (order is null)
            return new PaymentApplyResult(PaymentApplyStatus.UnknownOrder);
        if (order.Status == OrderStatuses.Paid)
        {
            // Job đối soát thắng cuộc đua nên đơn đã trả mà chưa có payload: webhook thật tới sau thì lưu payload đã lọc, không đổi gì khác (không thông báo, không kỳ mới).
            // Người mua đã bị xóa hẳn (UserId null) thì payload đã được xóa theo luật lưu trữ: không lưu lại
            if (order.PayloadJson is null && payloadJson is not null && order.UserId is not null)
            {
                order.PayloadJson = payloadJson;
                await orders.SaveAsync(ct);
            }
            return new PaymentApplyResult(PaymentApplyStatus.AlreadyPaid, order.UserSubscriptionId);
        }

        var alreadyMismatch = order.Status == OrderStatuses.Mismatch;
        // Tài khoản người mua đã bị xóa hẳn: có tiền nhưng không có ai để nhận gói, admin xử lý
        if (order.UserId is not { } buyerId)
        {
            await MarkMismatchAsync(order, payloadJson, "Tài khoản người mua đã bị xóa", ct);
            return new PaymentApplyResult(PaymentApplyStatus.Mismatch);
        }

        // 2. Số tiền phải khớp đơn (trừ khi admin kích hoạt tay)
        if (!options.SkipAmountCheck && (decimal)amountPaid != order.AmountVnd)
        {
            await MarkMismatchAsync(order, payloadJson, null, ct);
            if (!alreadyMismatch)
                afterCommit.Add(NotifyMismatch(buyerId, order.OrderCode, "Số tiền chuyển không khớp, quản trị viên sẽ xử lý."));
            return new PaymentApplyResult(PaymentApplyStatus.Mismatch);
        }

        // 3. Chỗ đặt kỳ mới được quyết định lúc thanh toán (C5)
        var lane = await entitlements.AdvanceLaneAsync(buyerId, order.PlanKind, now, ct, afterCommit);   // thông báo chuyển kỳ xếp hàng, gửi sau commit
        if (lane.Queued is not null)
        {
            // P22: đã nhận tiền nhưng làn đầy (đang chạy + đang chờ). Admin kích hoạt sau
            await MarkMismatchAsync(order, payloadJson, LaneFullNote, ct);
            if (!alreadyMismatch)
                afterCommit.Add(NotifyMismatch(buyerId, order.OrderCode, "Đã nhận tiền nhưng gói chưa kích hoạt được, quản trị viên sẽ xử lý."));
            return new PaymentApplyResult(PaymentApplyStatus.LaneFull);
        }
        var rollover = lane.Active is not null && lane.ActiveExhausted;   // R4: kỳ cũ đã dùng hết, kỳ mới thay ngay
        var queued = lane.Active is not null && !rollover;                // R1: kỳ cũ còn token, kỳ mới xếp hàng

        // 4. Gói Gia đình: nhóm của người mua (tạo mới nếu chưa có, cùng transaction với kỳ); C15: đang là thành viên nhóm khác thì rời nhóm đó
        FamilyGroup? group = null;
        var newGroup = false;
        FamilyMember? leaving = null;
        Guid? leftOwnerId = null;
        if (order.PlanKind == PlanKinds.Family)
        {
            group = await billing.FindActiveOwnedGroupAsync(buyerId, ct);
            if (group is null)
            {
                group = new FamilyGroup { OwnerUserId = buyerId, Status = FamilyGroupStatuses.Active, CreatedAt = now };
                newGroup = true;
            }
            if (await billing.FindActiveMemberGroupAsync(buyerId, ct) is { } memberOf)
            {
                await entitlements.AdvanceLaneAsync(memberOf.OwnerUserId, PlanKinds.Family, now, ct, afterCommit);   // nhóm đã hết kỳ thì kết thúc ở đây, không còn gì để rời
                leaving = (await billing.GetOpenMembersAsync(memberOf.Id, ct)).FirstOrDefault(m => m.UserId == buyerId && m.Status == FamilyMemberStatuses.Active);
                leftOwnerId = memberOf.OwnerUserId;
            }
        }

        // 5. Kỳ mới lấy gói, giá, hạn mức từ ĐƠN (bản chụp lúc đặt), không từ gói hiện tại
        var cycle = new UserSubscription
        {
            UserId = buyerId,
            SubscriptionPlanId = order.SubscriptionPlanId,
            Kind = order.PlanKind,
            FamilyGroupId = group?.Id,
            Status = queued ? CycleStatuses.Queued : CycleStatuses.Active,
            StartsAt = queued ? null : now,
            EndsAt = queued ? null : now.AddDays(order.PlanDurationDays),
            TokenQuota = order.PlanTokenQuota,
            PlanName = order.PlanName,
            PriceVnd = order.AmountVnd,
            DurationDays = order.PlanDurationDays,
            ActivatedFromOrderId = order.Id,
            CreatedAt = now,
        };

        // 6. Lưu theo thứ tự: kết thúc kỳ cũ → thêm nhóm và kỳ mới → đánh dấu đơn đã trả
        var steps = new List<Action>();
        if (rollover)
        {
            var old = lane.Active!;
            steps.Add(() =>
            {
                old.Status = CycleStatuses.Ended;
                old.EndedReason = CycleEndReasons.ExhaustedRollover;
                old.EndedAt = now;
                old.EndsAt = now;
            });
        }
        steps.Add(() =>
        {
            if (newGroup)
                orders.AddGroup(group!);
            orders.AddCycle(cycle);
        });
        steps.Add(() =>
        {
            order.Status = OrderStatuses.Paid;
            order.PaidAt = now;
            order.PayloadJson = payloadJson ?? order.PayloadJson;
            order.UserSubscriptionId = cycle.Id;
            order.AdminNote = options.AdminNote ?? order.AdminNote;
            order.HandledByUserId = options.AdminUserId ?? order.HandledByUserId;
            if (leaving is not null)
            {
                leaving.Status = FamilyMemberStatuses.Ended;
                leaving.EndedReason = FamilyMemberEndReasons.JoinedOwnFamily;
                leaving.EndedAt = now;
                leaving.TokenHash = null;
            }
        });
        await billing.SaveStepsAsync(steps, ct);

        // 7. Sau commit: biên nhận, thông báo, và (C15) báo hai bên khi người mua rời nhóm cũ
        var (code, planName, amount, startsAt, endsAt) = (order.OrderCode, order.PlanName, order.AmountVnd, cycle.StartsAt, cycle.EndsAt);
        afterCommit.Add(async c =>
        {
            if (await users.GetUserById(buyerId, c) is { } buyer)
                await email.SendPaymentReceiptEmailAsync(buyer.Email, buyer.FullName, code, planName, amount, now, startsAt, endsAt, queued, c);
        });
        afterCommit.Add(c => notifications.NotifyAsync(
            buyerId, NotificationTypes.BillingPaymentPaid, "Thanh toán thành công",
            queued ? $"Gói {planName} đã thanh toán, kỳ này sẽ bắt đầu khi kỳ hiện tại kết thúc." : $"Gói {planName} đã được kích hoạt.",
            $"/orders/{code}", c));
        if (leaving is not null && leftOwnerId is { } ownerId)
        {
            afterCommit.Add(async c =>
            {
                var buyerName = await billing.GetUserNameAsync(buyerId, c) ?? string.Empty;
                await notifications.NotifyAsync(ownerId, NotificationTypes.FamilyMemberLeft, "Thành viên đã rời gói Gia đình",
                    $"{buyerName} đã chuyển sang gói Gia đình của riêng mình.", "/account", c);
            });
            afterCommit.Add(async c =>
            {
                var ownerName = await billing.GetUserNameAsync(ownerId, c) ?? string.Empty;
                await notifications.NotifyAsync(buyerId, NotificationTypes.FamilyMemberLeft, "Bạn đã rời gói Gia đình",
                    $"Bạn đã mua gói Gia đình riêng nên được chuyển khỏi nhóm của {ownerName}.", "/account", c);
            });
        }

        return new PaymentApplyResult(queued ? PaymentApplyStatus.Queued : PaymentApplyStatus.Activated, cycle.Id);
    }

    private async Task MarkMismatchAsync(PaymentTransaction order, string? payloadJson, string? adminNote, CancellationToken ct)
    {
        order.Status = OrderStatuses.Mismatch;
        order.PayloadJson = payloadJson ?? order.PayloadJson;
        order.AdminNote = adminNote ?? order.AdminNote;
        await orders.SaveAsync(ct);
    }

    private Func<CancellationToken, Task> NotifyMismatch(Guid buyerId, long orderCode, string body) =>
        c => notifications.NotifyAsync(buyerId, NotificationTypes.BillingPaymentMismatch, "Thanh toán cần được kiểm tra", body, $"/orders/{orderCode}", c);
}
