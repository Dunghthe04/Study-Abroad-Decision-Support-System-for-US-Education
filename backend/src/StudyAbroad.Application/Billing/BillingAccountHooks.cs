using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Application.Notifications;

namespace StudyAbroad.Application.Billing;

/// <summary>
/// [UC-80] Gói dịch vụ và gói Gia đình khi tài khoản bị xóa. Cả hai hàm chạy bên trong transaction của người gọi và chỉ ghi DB;
/// việc phải làm SAU khi commit (thông báo, hủy link ở cổng thanh toán) trả về dưới dạng một hàm không ném lỗi, người gọi chạy nó sau commit.
/// </summary>
public interface IBillingAccountHooks
{
    /// <summary>Xóa mềm (D1, D3): thành viên rời nhóm, đơn đang chờ bị hủy. Nhóm và kỳ của chủ gói chạy tiếp tới hết hạn (O1).</summary>
    Task<Func<CancellationToken, Task>> OnAccountSoftDeletedAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Ngay trước khi xóa hẳn (D4, D6): nhóm của chủ gói và các kỳ còn mở kết thúc, thành viên được báo; email còn sót trong lịch sử thành viên của chính người này bị xóa.</summary>
    Task<Func<CancellationToken, Task>> OnBeforePurgeAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default);
}

public sealed class BillingAccountHooks(
    IBillingAccountRepository repository,
    INotificationService notifications,
    IPaymentGateway gateway,
    ILogger<BillingAccountHooks>? logger = null) : IBillingAccountHooks
{
    private static readonly Func<CancellationToken, Task> Nothing = _ => Task.CompletedTask;

    public async Task<Func<CancellationToken, Task>> OnAccountSoftDeletedAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
    {
        // Đơn trước, thành viên sau: ApplyPaymentAsync khóa dòng đơn rồi mới đổi dòng thành viên (C15), cùng thứ tự thì hai bên không chờ vòng nhau
        var cancelled = await repository.CancelPendingOrdersAsync(userId, nowUtc, ct);
        var membership = await repository.EndActiveMembershipAsync(userId, nowUtc, ct);
        if (membership is null && cancelled.Count == 0)
            return Nothing;

        return async after =>
        {
            if (membership is not null)
                await notifications.NotifyAsync(membership.OwnerUserId, NotificationTypes.FamilyMemberLeft, "Có thành viên rời gói Gia đình",
                    $"{membership.MemberName} đã xóa tài khoản và rời gói Gia đình của bạn.", "/account", after);
            foreach (var code in cancelled)
            {
                try
                {
                    await gateway.CancelAsync(code, "Người mua đã xóa tài khoản", after);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger?.LogWarning(ex, "Không hủy được link thanh toán của đơn {OrderCode}", code);
                }
            }
        };
    }

    public async Task<Func<CancellationToken, Task>> OnBeforePurgeAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
    {
        // Nhóm và kỳ sẽ bị cascade xóa cùng dòng users; kết thúc trước để biết ai cần báo và để mọi FK không còn chặn
        var groups = await repository.EndOwnedGroupsAsync(userId, nowUtc, ct);
        await repository.EndOpenCyclesAsync(userId, nowUtc, ct);
        // Cuối cùng (sau khóa nhóm, cùng thứ tự với FamilyService): xóa email trong lịch sử thành viên của người sắp bị xóa
        await repository.BlankMembershipEmailsAsync(userId, nowUtc, ct);
        if (groups is null || groups.MemberUserIds.Count == 0)
            return Nothing;

        return async after =>
        {
            foreach (var memberId in groups.MemberUserIds)
                await notifications.NotifyAsync(memberId, NotificationTypes.FamilyEnded, "Gói Gia đình đã kết thúc",
                    $"Gói Gia đình của {groups.OwnerName} đã kết thúc.", "/plans", after);
        };
    }
}
