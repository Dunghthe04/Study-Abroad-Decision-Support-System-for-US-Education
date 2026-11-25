using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

public interface ILeadParentConfirmService
{
    Task<LeadParentConfirmInfo> LookupAsync(string? token, CancellationToken ct = default);
    Task<LeadParentConfirmInfo> AcceptAsync(string? token, string? ip, CancellationToken ct = default);
    Task<LeadParentConfirmInfo> RejectAsync(string? token, string? ip, CancellationToken ct = default);
}

/// <summary>
/// [UC-116] Phụ huynh mở link trong mail, không cần tài khoản, đồng ý hoặc từ chối gửi yêu cầu liên hệ của con dưới 16.
/// Đồng ý: lead thành "new" (trung tâm nhận) và lưu bản ghi đồng ý chia sẻ. Từ chối: "cancelled".
/// Nhiều phụ huynh cùng nhận mail: ai bấm trước quyết định, link của người còn lại chỉ hiện kết quả.
/// Lỗi: token sai 404, hết hạn 410, đã có người trả lời 409.
/// </summary>
public class LeadParentConfirmService(
    ILeadRepository leads,
    IParentLinkRepository parentLinks,
    IUserRepository users,
    IStudyCenterRepository studyCenters,
    IEmailSender emailSender,
    StudyAbroad.Application.Admin.Policies.IPolicyVersionLookup? policyVersions = null,
    INotificationService? notifications = null) : ILeadParentConfirmService
{
    public async Task<LeadParentConfirmInfo> LookupAsync(string? token, CancellationToken ct = default)
    {
        var (lead, link) = await FindAsync(token, ct);
        var now = DateTime.UtcNow;

        await EnsureNotExpiredAsync(lead, now, ct);
        if (lead.Status == LeadStatuses.PendingParent && link.Status != ParentLinkStatuses.Active)
            throw LinkInactive();

        return await ToInfoAsync(lead, link, now, ct);
    }

    public Task<LeadParentConfirmInfo> AcceptAsync(string? token, string? ip, CancellationToken ct = default) =>
        DecideAsync(token, accept: true, ip, ct);

    public Task<LeadParentConfirmInfo> RejectAsync(string? token, string? ip, CancellationToken ct = default) =>
        DecideAsync(token, accept: false, ip, ct);

    private async Task<LeadParentConfirmInfo> DecideAsync(string? token, bool accept, string? ip, CancellationToken ct)
    {
        var (lead, link) = await FindAsync(token, ct);
        var now = DateTime.UtcNow;

        await EnsureNotExpiredAsync(lead, now, ct);
        if (lead.Status != LeadStatuses.PendingParent)
            throw Answered();
        // Phụ huynh đã rút đồng ý / hủy liên kết sau khi nhận mail
        if (link.Status != ParentLinkStatuses.Active)
            throw LinkInactive();

        var safeIp = Truncate(ip, 45);
        var consent = accept
            ? new Consent
            {
                UserId = lead.UserId ?? throw LinkInactive(),
                Purpose = AgeFeatures.ShareWithCenter,
                Granted = true,
                PolicyVersion = await LeadParentConfirm.ConsentPolicyVersionAsync(policyVersions, byGuardian: true, ct),
                GrantedByUserId = link.ParentUserId,
                GrantedByEmail = link.ParentEmail,
                GrantedIp = safeIp,
            }
            : null;
        var status = accept ? LeadStatuses.New : LeadStatuses.Cancelled;

        // Ghi có điều kiện "còn chờ": phụ huynh khác bấm cùng lúc thì một người thua, báo 409
        if (!await leads.TryCompleteParentConfirmAsync(lead.Id, new LeadParentDecision(status, link.ParentEmail, now, safeIp, now), consent, ct))
            throw Answered();

        await NotifyStudentAsync(lead, status, ct);

        var updated = await leads.GetByParentTokenHashAsync(lead.ParentTokenHash!, ct) ?? lead;
        return await ToInfoAsync(updated, link, now, ct);
    }

    private async Task<(Lead Lead, ParentStudentLink Link)> FindAsync(string? token, CancellationToken ct)
    {
        if (!LeadParentConfirm.TryParseToken(token, out var hash, out var linkId))
            throw Invalid();

        var lead = await leads.GetByParentTokenHashAsync(hash, ct) ?? throw Invalid();
        var link = await parentLinks.GetByIdAsync(linkId, ct);
        if (link is null || link.StudentUserId != lead.UserId)
            throw Invalid();

        return (lead, link);
    }

    /// <summary>Hết hạn lười: lead quá 7 ngày còn chờ thì ghi "expired" và báo học sinh, rồi trả 410.</summary>
    private async Task EnsureNotExpiredAsync(Lead lead, DateTime now, CancellationToken ct)
    {
        if (lead.Status == LeadStatuses.Expired)
            throw Expired();
        if (!LeadParentConfirm.IsPastDeadline(lead, now))
            return;

        if (await leads.TryCompleteParentConfirmAsync(lead.Id, new LeadParentDecision(LeadStatuses.Expired, null, null, null, now), null, ct))
            await NotifyStudentAsync(lead, LeadStatuses.Expired, ct);
        throw Expired();
    }

    private async Task NotifyStudentAsync(Lead lead, string outcome, CancellationToken ct)
    {
        var student = lead.UserId is { } studentId ? await users.GetUserById(studentId, ct) : null;
        if (student is null)
            return;

        var center = await studyCenters.GetByIdAsync(lead.StudyCenterId, ct);
        var centerName = center?.Name ?? "Trung tâm tư vấn";
        await emailSender.SendLeadParentDecisionEmailAsync(student.Email, student.FullName, centerName, outcome, ct);
        if (notifications is null)
            return;

        // [UC-110 bước 4] Học sinh nhận cả thông báo trong hệ thống, cho cả ba kết quả
        var text = outcome switch
        {
            LeadStatuses.New => $"Phụ huynh đã đồng ý. Yêu cầu liên hệ của bạn đã được gửi tới {centerName}.",
            LeadStatuses.Cancelled => $"Phụ huynh đã từ chối gửi yêu cầu liên hệ tới {centerName}. Trung tâm không nhận được thông tin của bạn.",
            _ => $"Phụ huynh chưa trả lời trong {LeadParentConfirm.ExpiryDays} ngày nên yêu cầu liên hệ tới {centerName} đã hết hạn.",
        };
        await notifications.NotifyAsync(student.Id, NotificationTypes.LeadStatusChanged, "Kết quả phụ huynh xác nhận yêu cầu liên hệ",
            text, "/account", ct);

        // [UC-110 bước 3] Đồng ý: yêu cầu tới trung tâm như yêu cầu mới gửi (UC-42)
        if (outcome == LeadStatuses.New && center?.OwnerUserId is { } ownerId)
            await notifications.NotifyAsync(ownerId, NotificationTypes.LeadNew, "Có yêu cầu liên hệ mới",
                "Một học sinh / phụ huynh vừa gửi yêu cầu liên hệ tới trung tâm.", "/center/leads", ct);
    }

    private async Task<LeadParentConfirmInfo> ToInfoAsync(Lead lead, ParentStudentLink link, DateTime now, CancellationToken ct)
    {
        var student = lead.UserId is { } studentId ? await users.GetUserById(studentId, ct) : null;
        var center = await studyCenters.GetByIdAsync(lead.StudyCenterId, ct);
        int? age = student?.DateOfBirth is { } dob ? AgePolicy.GetAge(dob, AgePolicy.TodayInVietnam(now)) : null;

        var state = lead.Status switch
        {
            LeadStatuses.PendingParent => LeadParentConfirmStates.Pending,
            LeadStatuses.Cancelled => LeadParentConfirmStates.Rejected,
            _ => LeadParentConfirmStates.Accepted, // đã đồng ý, sau đó trung tâm xử lý tiếp (contacted / closed / withdrawn)
        };
        var isPending = state == LeadParentConfirmStates.Pending;

        return new LeadParentConfirmInfo(
            state,
            student?.FullName ?? "Học sinh",
            age,
            center?.Name ?? "Trung tâm tư vấn",
            lead.SharedFields,
            lead.ContactPhone,
            lead.Message,
            link.ParentEmail,
            isPending ? lead.ParentTokenExpiresAt : null,
            lead.ParentDecidedAt,
            !isPending && string.Equals(lead.ParentEmail, link.ParentEmail, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];

    private static LeadRuleException Invalid() =>
        new("parent_confirm_invalid", 404, "Link xác nhận không hợp lệ.");

    private static LeadRuleException LinkInactive() =>
        new("parent_confirm_invalid", 404, "Liên kết phụ huynh với học sinh không còn hiệu lực nên link này không dùng được nữa.");

    private static LeadRuleException Expired() =>
        new("parent_confirm_expired", 410, $"Link đã hết hạn (quá {LeadParentConfirm.ExpiryDays} ngày). Con có thể gửi lại yêu cầu liên hệ.");

    private static LeadRuleException Answered() =>
        new("parent_confirm_answered", 409, "Yêu cầu này đã được phụ huynh trả lời.");
}
