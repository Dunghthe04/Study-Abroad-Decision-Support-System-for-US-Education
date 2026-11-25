using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

public interface ILeadParentHoldService
{
    /// <summary>
    /// [UC-150 bước 4] Học sinh vừa về dưới 16: giữ các yêu cầu liên hệ chưa kết thúc (Mới gửi, Trung tâm đã chấp nhận, Đang trao đổi)
    /// thành chờ phụ huynh, hạn 7 ngày tính từ lúc giữ. Chạy trong transaction của Admin duyệt. Trả số yêu cầu đã giữ.
    /// </summary>
    Task<int> HoldOpenLeadsAsync(Guid studentId, DateTime now, CancellationToken ct = default);

    /// <summary>
    /// [UC-110] Gửi mail xác nhận từng yêu cầu đang giữ tới mọi phụ huynh đang liên kết. Chỉ gọi khi học sinh đã có đồng ý của phụ huynh
    /// (phụ huynh vừa đồng ý tài khoản, hoặc đã đồng ý từ trước). Yêu cầu đã gửi mail rồi hoặc đã quá hạn thì bỏ qua.
    /// </summary>
    Task SendHeldLeadConfirmationsAsync(User student, CancellationToken ct = default);

    /// <summary>
    /// [UC-110] Phụ huynh từ chối tài khoản: yêu cầu đang giữ chuyển Đã thu hồi ngay, báo học sinh và chủ trung tâm.
    /// </summary>
    Task WithdrawHeldLeadsAsync(User student, CancellationToken ct = default);

    /// <summary>
    /// [UC-110 / UC-150] Học sinh đã đủ 16: yêu cầu còn chờ phụ huynh không cần phụ huynh nữa. Yêu cầu gửi lúc dưới 16 chuyển Mới gửi,
    /// yêu cầu đang giữ trở lại trạng thái cũ; hạn 7 ngày và mốc 90 ngày tính từ lúc chuyển. Yêu cầu đã quá hạn thì để nguyên
    /// (yêu cầu thường hiện Hết hạn, yêu cầu đang giữ do job thu hồi). Báo chủ trung tâm và học sinh. Trả số yêu cầu đã chuyển.
    /// </summary>
    Task<int> ReleaseParentHoldAsync(User student, DateTime now, CancellationToken ct = default);

    /// <summary>
    /// [UC-110] Job hằng ngày: trả yêu cầu chờ phụ huynh của học sinh vừa đủ 16, thu hồi yêu cầu đang giữ quá hạn (báo học sinh, trung tâm).
    /// </summary>
    Task<LeadHoldJobSummary> RunDailyAsync(DateTime now, CancellationToken ct = default);
}

/// <summary>Kết quả một lượt job: số yêu cầu đã trả cho trung tâm, số yêu cầu đang giữ đã thu hồi.</summary>
public record LeadHoldJobSummary(int Released, int Withdrawn);

/// <summary>
/// [UC-110 / UC-150] Yêu cầu liên hệ của học sinh được Admin sửa ngày sinh về dưới 16. Trung tâm không thấy yêu cầu đang giữ
/// (pending_parent đã ẩn). Phụ huynh đồng ý qua mail thì yêu cầu trở lại đúng trạng thái cũ (<see cref="LeadParentConfirmService"/>).
/// </summary>
public class LeadParentHoldService(
    ILeadHoldRepository holds,
    ILeadRepository leads,
    IParentLinkRepository parentLinks,
    IStudyCenterRepository studyCenters,
    IEmailSender emailSender,
    INotificationService? notifications = null,
    // Job hằng ngày đọc học sinh theo Id; null trong test chỉ gọi các hàm theo học sinh
    IUserRepository? users = null) : ILeadParentHoldService
{
    public Task<int> HoldOpenLeadsAsync(Guid studentId, DateTime now, CancellationToken ct = default) =>
        holds.HoldOpenLeadsAsync(studentId, now, now.AddDays(LeadParentConfirm.ExpiryDays), ct);

    public async Task SendHeldLeadConfirmationsAsync(User student, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // Đã gửi mail (có hash) thì không sinh token mới, để link phụ huynh đang giữ trong mail vẫn dùng được
        var held = (await holds.ListHeldAsync(student.Id, ct))
            .Where(l => l.ParentTokenHash is null && l.ParentTokenExpiresAt > now)
            .ToList();
        if (held.Count == 0)
            return;

        var activeLinks = (await parentLinks.GetByStudentAsync(student.Id, ct))
            .Where(l => l.Status == ParentLinkStatuses.Active)
            .ToList();
        if (activeLinks.Count == 0)
            return;

        foreach (var lead in held)
        {
            var secret = ParentInvite.GenerateToken();
            if (!await holds.SetParentTokenHashAsync(lead.Id, ParentInvite.HashToken(secret), now, ct))
                continue;

            var center = await studyCenters.GetByIdAsync(lead.StudyCenterId, ct);
            var view = await leads.GetSharedPreviewAsync(lead.Id, ct);
            var preview = view is null
                ? lead.SharedFields.Select(f => new LeadSharedPreviewGroup(LeadParentConfirm.FieldLabel(f), [])).ToList()
                : LeadParentConfirm.BuildPreview(view, lead.SharedFields);
            // Hạn tính từ lúc giữ (HeldAt), mail báo số ngày còn lại, tối thiểu 1
            var daysLeft = Math.Max(1, (int)Math.Ceiling((lead.ParentTokenExpiresAt!.Value - now).TotalDays));
            foreach (var link in activeLinks)
            {
                await emailSender.SendLeadParentConfirmEmailAsync(link.ParentEmail, student.FullName, center?.Name ?? "Trung tâm tư vấn",
                    preview, LeadParentConfirm.ComposeToken(secret, link.Id), daysLeft, ct);
            }
        }
    }

    public async Task WithdrawHeldLeadsAsync(User student, CancellationToken ct = default)
    {
        var held = await holds.ListHeldAsync(student.Id, ct);
        if (held.Count == 0)
            return;

        await holds.WithdrawHeldAsync(student.Id, DateTime.UtcNow, ct);
        foreach (var lead in held)
            await NotifyWithdrawnAsync(student, lead, "Người đại diện đã từ chối đồng ý cho tài khoản", ct);
    }

    public async Task<int> ReleaseParentHoldAsync(User student, DateTime now, CancellationToken ct = default)
    {
        var released = 0;
        foreach (var lead in await holds.ListPendingParentAsync(student.Id, ct))
        {
            if (LeadParentConfirm.IsPastDeadline(lead, now))
                continue;

            var held = lead.HeldFromStatus is not null;
            var status = lead.HeldFromStatus ?? LeadStatuses.New;
            var paused = held
                ? lead.PausedSeconds + SecondsBetween(lead.HeldAt ?? now, now)
                : SecondsBetween(lead.CreatedAt, now);
            if (!await holds.ReleaseAsync(lead.Id, status, paused, now, ct))
                continue;

            released++;
            await NotifyReleasedAsync(student, lead, held, ct);
        }
        return released;
    }

    public async Task<LeadHoldJobSummary> RunDailyAsync(DateTime now, CancellationToken ct = default)
    {
        if (users is null)
            return new LeadHoldJobSummary(0, 0);

        // Đủ 16 theo ngày Việt Nam: sinh từ ngày này 16 năm trước trở về trước
        var cutoff = AgePolicy.TodayInVietnam(now).AddYears(-AgeGroups.ChildMaxAgeExclusive);
        var released = 0;
        foreach (var studentId in await holds.ListStudentsToReleaseAsync(cutoff, ct))
        {
            if (await users.GetUserById(studentId, ct) is { } student)
                released += await ReleaseParentHoldAsync(student, now, ct);
        }

        var withdrawn = 0;
        foreach (var lead in await holds.ListHeldPastDeadlineAsync(now, ct))
        {
            if (!await holds.WithdrawHeldLeadAsync(lead.Id, now, ct))
                continue;
            withdrawn++;
            if (lead.UserId is { } id && await users.GetUserById(id, ct) is { } student)
                await NotifyWithdrawnAsync(student, lead, $"Phụ huynh chưa xác nhận trong {LeadParentConfirm.ExpiryDays} ngày", ct);
        }

        return new LeadHoldJobSummary(released, withdrawn);
    }

    private async Task NotifyReleasedAsync(User student, Lead lead, bool held, CancellationToken ct)
    {
        if (notifications is null)
            return;

        var center = await studyCenters.GetByIdAsync(lead.StudyCenterId, ct);
        var centerName = center?.Name ?? "Trung tâm tư vấn";
        await notifications.NotifyAsync(student.Id, NotificationTypes.LeadStatusChanged, "Yêu cầu liên hệ đã tới trung tâm",
            $"Bạn đã đủ {AgeGroups.ChildMaxAgeExclusive} tuổi nên yêu cầu liên hệ tới {centerName} không cần phụ huynh xác nhận nữa.", "/account", ct);
        if (center?.OwnerUserId is not { } ownerId)
            return;

        if (held)
            await notifications.NotifyAsync(ownerId, NotificationTypes.LeadStatusChanged, "Yêu cầu liên hệ đã hiện lại",
                "Yêu cầu liên hệ của một học sinh không còn cần phụ huynh xác nhận. Yêu cầu hiện lại trong danh sách với trạng thái như trước.",
                "/center/leads", ct);
        else
            await notifications.NotifyAsync(ownerId, NotificationTypes.LeadNew, "Có yêu cầu liên hệ mới",
                "Một học sinh / phụ huynh vừa gửi yêu cầu liên hệ tới trung tâm.", "/center/leads", ct);
    }

    private static long SecondsBetween(DateTime from, DateTime to) => Math.Max(0L, (long)(to - from).TotalSeconds);

    /// <summary>Báo học sinh (mail và thông báo) và chủ trung tâm khi yêu cầu đang giữ bị thu hồi.</summary>
    internal static async Task NotifyHeldWithdrawnAsync(
        User student, Lead lead, string reason, IStudyCenterRepository studyCenters, IEmailSender emailSender,
        INotificationService? notifications, CancellationToken ct)
    {
        var center = await studyCenters.GetByIdAsync(lead.StudyCenterId, ct);
        var centerName = center?.Name ?? "Trung tâm tư vấn";
        await emailSender.SendLeadParentDecisionEmailAsync(student.Email, student.FullName, centerName, LeadStatuses.Withdrawn, ct);
        if (notifications is null)
            return;

        await notifications.NotifyAsync(student.Id, NotificationTypes.LeadStatusChanged, "Yêu cầu liên hệ đã bị thu hồi",
            $"{reason} nên yêu cầu liên hệ tới {centerName} đã bị thu hồi.", "/account", ct);
        if (center?.OwnerUserId is { } ownerId)
            await notifications.NotifyAsync(ownerId, NotificationTypes.LeadStatusChanged, "Yêu cầu liên hệ đã bị thu hồi",
                "Yêu cầu liên hệ của một học sinh đã bị thu hồi vì chưa có xác nhận của phụ huynh. Dữ liệu của học sinh không còn hiển thị.",
                "/center/leads", ct);
    }

    private Task NotifyWithdrawnAsync(User student, Lead lead, string reason, CancellationToken ct) =>
        NotifyHeldWithdrawnAsync(student, lead, reason, studyCenters, emailSender, notifications, ct);
}
