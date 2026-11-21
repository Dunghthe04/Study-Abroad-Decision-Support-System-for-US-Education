using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Family;

/// <summary>
/// Gói Gia đình (kế hoạch billing v2, mục "Family rules"). "Gia đình hiện tại" của một người: gọi
/// <see cref="IEntitlementService.GetSummaryAsync"/> trước (áp dụng chuyển kỳ và cho nhóm hết hiệu lực kết thúc), rồi tìm nhóm chủ / thành viên active.
/// Thao tác đổi chỗ ngồi chạy trong transaction, khóa dòng nhóm trước, đếm chỗ lại từ DB sau khóa; ghi nào cần giữ lại dù sau đó báo lỗi (hết hạn lời mời)
/// thì lưu ngoài transaction vì <see cref="IUnitOfWork"/> chỉ commit khi delegate trả về bình thường.
/// </summary>
public class FamilyService(
    IFamilyRepository family,
    IBillingRepository billing,
    IEntitlementService entitlements,
    IUserRepository users,
    INotificationService notifications,
    IEmailSender emailSender,
    IUnitOfWork unitOfWork,
    ILogger<FamilyService>? logger = null) : IFamilyService
{
    private const string AccountLink = "/account";

    // ---- đọc

    public async Task<FamilyDto> GetAsync(Guid userId, string role, DateTime nowUtc, CancellationToken ct = default)
    {
        var (group, isOwner) = await FindCurrentGroupAsync(userId, nowUtc, ct) ?? throw FamilyErrors.NotInFamily();

        // Nhóm dùng chung MỌI kỳ Gia đình đang chạy của chủ gói: phần trăm là tổng đã dùng trên tổng hạn mức của các cửa sổ hiện tại
        var cycles = (await billing.GetActiveCyclesAsync(group.OwnerUserId, PlanKinds.Family, ct))
            .OrderBy(c => c.EndsAt).ThenBy(c => c.StartsAt).ThenBy(c => c.Id).ToList();
        var cycle = cycles.FirstOrDefault();   // hết hạn sớm nhất: tên, hạn và ngày làm mới hiện cho kỳ này
        var totalQuota = cycles.Sum(c => c.TokenQuota);
        long totalUsed = 0;
        var tokensByUser = new Dictionary<Guid, long>();
        DateTimeOffset? quotaResetsAt = null;
        foreach (var c in cycles)
        {
            var (ws, we, isFinal) = CycleRules.CycleWindowUtc(c, nowUtc);
            var recurring = CycleRules.IsRecurring(c);
            DateTime? from = recurring ? ws : null, to = recurring ? we : null;
            totalUsed += await billing.SumCycleTokensAsync(c.Id, from, to, ct);
            foreach (var (uid, tokens) in await family.SumTokensByUserAsync(c.Id, from, to, ct))
                tokensByUser[uid] = tokensByUser.GetValueOrDefault(uid) + tokens;
            if (c == cycle && !isFinal)
                quotaResetsAt = CycleRules.ToVietnamTime(we);
        }
        int Share(Guid personId) =>
            tokensByUser.TryGetValue(personId, out var tokens) && tokens > 0 ? CycleRules.UsedPercent(tokens, totalQuota) : 0;

        var open = await billing.GetOpenMembersAsync(group.Id, ct);
        var active = open.Where(m => m.Status == FamilyMemberStatuses.Active).OrderBy(m => m.JoinedAt).ToList();
        var pending = open.Where(m => IsLivePending(m, nowUtc)).OrderBy(m => m.InvitedAt).ToList();

        var ownerName = await NameOfAsync(group.OwnerUserId, ct);
        var members = new List<FamilyMemberDto>
        {
            new(group.OwnerUserId, ownerName, FamilySeats.Owner, Share(group.OwnerUserId), group.CreatedAt, group.OwnerUserId == userId),
        };
        foreach (var m in active)
            members.Add(new FamilyMemberDto(
                m.Id, m.UserId is { } uid ? await NameOfAsync(uid, ct) : string.Empty, m.SeatRole,
                m.UserId is { } sid ? Share(sid) : 0, m.JoinedAt, m.UserId == userId));

        int Used(string seatRole) => active.Count(m => m.SeatRole == seatRole) + pending.Count(m => m.SeatRole == seatRole);
        var seats = new FamilySeatsDto(
            new FamilySeatCountDto(Used(FamilySeats.Student), FamilySeats.MaxStudents),
            new FamilySeatCountDto(Used(FamilySeats.Parent), FamilySeats.MaxParents));

        // Lời mời và email chỉ chủ gói thấy
        var invites = isOwner
            ? pending.Select(m => new FamilyInviteDto(m.Id, m.InvitedEmail, m.SeatRole, m.ExpiresAt!.Value, FamilyInviteToken.NextResendAt(m, nowUtc))).ToList()
            : [];

        return new FamilyDto(
            group.Id, isOwner, ownerName,
            cycle is null ? null : new FamilyCycleDto(cycle.PlanName, cycle.EndsAt, CycleRules.UsedPercent(totalUsed, totalQuota), cycle.QuotaPeriodDays, quotaResetsAt),
            seats, members, invites);
    }

    public async Task<FamilyInviteLookupDto> LookupAsync(string token, DateTime nowUtc, CancellationToken ct = default)
    {
        var member = await FindInviteAsync(token, ct);
        var group = await family.FindGroupAsync(member.FamilyGroupId, ct) ?? throw FamilyErrors.InviteInvalid();
        return new FamilyInviteLookupDto(
            await NameOfAsync(group.OwnerUserId, ct), member.SeatRole, member.ExpiresAt ?? member.InvitedAt.AddDays(BillingRules.InviteExpiryDays),
            LookupStatus(member, nowUtc));
    }

    // ---- lời mời (chủ gói)

    public async Task InviteAsync(Guid ownerId, string email, string seatRole, DateTime nowUtc, CancellationToken ct = default)
    {
        var group = await RequireOwnedGroupAsync(ownerId, nowUtc, ct);
        if (seatRole is not (FamilySeats.Student or FamilySeats.Parent))
            throw FamilyErrors.InvalidSeatRole();

        var owner = await users.GetUserById(ownerId, ct);
        var normalized = AuthService.NormalizeEmail(email);
        if (!AuthService.IsValidEmail(normalized) || normalized == AuthService.NormalizeEmail(owner?.Email ?? string.Empty))
            throw FamilyErrors.InvalidEmail();

        var member = new FamilyMember
        {
            FamilyGroupId = group.Id, InvitedEmail = normalized, SeatRole = seatRole, Status = FamilyMemberStatuses.Pending, InvitedAt = nowUtc,
        };
        var token = FamilyInviteToken.Issue(member, nowUtc);

        await RunAsync(async txCt =>
        {
            if (!await family.LockActiveGroupAsync(group.Id, txCt))
                throw FamilyErrors.NoFamily();
            // Lời mời quá hạn chưa được job dọn vẫn giữ index "một lời mời pending mỗi email": dọn ngay trong nhóm này
            await family.ExpireDueInvitesAsync(nowUtc, group.Id, txCt);
            if (await family.HasOpenInviteForEmailAsync(group.Id, normalized, txCt))
                throw FamilyErrors.InviteExists();
            if (await family.CountSeatsAsync(group.Id, seatRole, null, nowUtc, txCt) >= MaxSeats(seatRole))
                throw FamilyErrors.SeatFull();
            await family.AddMemberAsync(member, txCt);
            await family.SaveAsync(txCt);
        }, ct);

        var ownerName = owner?.FullName ?? string.Empty;
        await TrySendInviteEmailAsync(normalized, ownerName, seatRole, token, ct);
        // Có tài khoản với email này thì thêm thông báo trong app; phản hồi cho chủ gói vẫn như nhau dù có hay không
        if (await users.GetUserByEmail(normalized, ct) is { } invitee)
            await notifications.NotifyAsync(invitee.Id, NotificationTypes.FamilyInvite, "Lời mời tham gia gói Gia đình",
                $"{ownerName} mời bạn tham gia gói Gia đình.", $"/family/invite?token={token}", ct);
    }

    public async Task ResendInviteAsync(Guid ownerId, Guid inviteId, DateTime nowUtc, CancellationToken ct = default)
    {
        var group = await RequireOwnedGroupAsync(ownerId, nowUtc, ct);
        string email = string.Empty, seatRole = string.Empty, token = string.Empty;

        await RunAsync(async txCt =>
        {
            if (!await family.LockActiveGroupAsync(group.Id, txCt))
                throw FamilyErrors.NoFamily();
            var member = await family.FindMemberAsync(inviteId, txCt);
            if (member is null || member.FamilyGroupId != group.Id || !IsLivePending(member, nowUtc))
                throw FamilyErrors.NotPending();
            if (FamilyInviteToken.NextResendAt(member, nowUtc) is not null)
                throw FamilyErrors.ResendTooSoon();
            token = FamilyInviteToken.Issue(member, nowUtc);   // token mới: link cũ hết tác dụng
            (email, seatRole) = (member.InvitedEmail, member.SeatRole);
            await family.SaveAsync(txCt);
        }, ct);

        var owner = await users.GetUserById(ownerId, ct);
        await TrySendInviteEmailAsync(email, owner?.FullName ?? string.Empty, seatRole, token, ct);
    }

    // Lời mời đã commit trước khi gửi thư: thư lỗi (SMTP sập...) chỉ ghi cảnh báo, không biến lời mời đã lưu thành 500.
    // Chủ gói vẫn thấy lời mời trong danh sách và bấm gửi lại được; người được mời có tài khoản vẫn nhận thông báo trong app
    private async Task TrySendInviteEmailAsync(string email, string ownerName, string seatRole, string token, CancellationToken ct)
    {
        try
        {
            await emailSender.SendFamilyInviteEmailAsync(email, ownerName, seatRole, token, BillingRules.InviteExpiryDays, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger?.LogWarning(ex, "Lời mời gói Gia đình đã lưu nhưng gửi thư thất bại; chủ gói có thể bấm gửi lại");
        }
    }

    public async Task CancelInviteAsync(Guid ownerId, Guid inviteId, DateTime nowUtc, CancellationToken ct = default)
    {
        var group = await RequireOwnedGroupAsync(ownerId, nowUtc, ct);
        await RunAsync(async txCt =>
        {
            if (!await family.LockActiveGroupAsync(group.Id, txCt))
                throw FamilyErrors.NoFamily();
            var member = await family.FindMemberAsync(inviteId, txCt);
            if (member is null || member.FamilyGroupId != group.Id || member.Status != FamilyMemberStatuses.Pending)
                throw FamilyErrors.NotPending();
            member.Status = FamilyMemberStatuses.Cancelled;
            member.EndedAt = nowUtc;
            member.TokenHash = null;
            await family.SaveAsync(txCt);
        }, ct);
    }

    // ---- lời mời (người được mời)

    public async Task<FamilyDto> AcceptAsync(Guid userId, string role, string token, DateTime nowUtc, CancellationToken ct = default)
    {
        var member = await FindInviteAsync(token, ct);
        if (member.Status != FamilyMemberStatuses.Pending)
            throw FamilyErrors.NotPending();
        if (!IsLivePending(member, nowUtc))
        {
            // Phải lưu TRƯỚC khi ném: ném trong transaction thì mất ghi
            member.Status = FamilyMemberStatuses.Expired;
            member.TokenHash = null;
            member.EndedAt = nowUtc;
            await SaveOrNotPendingAsync(ct);
            throw FamilyErrors.InviteExpired();
        }

        // Nhóm hết hiệu lực khi đọc: áp dụng chuyển kỳ của chủ gói (nhóm kết thúc kéo theo lời mời kết thúc) rồi mới xét
        var group = await family.FindGroupAsync(member.FamilyGroupId, ct) ?? throw FamilyErrors.InviteInvalid();
        await entitlements.GetSummaryAsync(group.OwnerUserId, nowUtc, ct);
        if (await billing.FindActiveOwnedGroupAsync(group.OwnerUserId, ct) is not { } liveGroup || liveGroup.Id != group.Id)
            throw FamilyErrors.InviteExpired();

        var user = await users.GetUserById(userId, ct);
        // Tài khoản đã xóa mềm không nhận lời mời được nữa: tư cách active chen vào sau lúc xóa sẽ không ai dọn tới khi xóa hẳn
        if (user is null || user.DeletedAt is not null || !SameEmail(user.Email, member.InvitedEmail))
            throw FamilyErrors.WrongAccount();
        if (role != member.SeatRole)   // nhân viên và trung tâm không bao giờ khớp
            throw FamilyErrors.RoleMismatch();

        // Gia đình cũ của người này đã hết hạn thì không còn là thành viên (summary kết thúc nó trước): F12
        await entitlements.GetSummaryAsync(userId, nowUtc, ct);
        if (await billing.FindActiveOwnedGroupAsync(userId, ct) is not null || await billing.FindActiveMemberGroupAsync(userId, ct) is not null)
            throw FamilyErrors.AlreadyInFamily();
        if (await family.HasPendingFamilyOrderAsync(userId, ct))
            throw FamilyErrors.PendingFamilyOrder();

        await RunAsync(async txCt =>
        {
            if (!await family.LockActiveGroupAsync(group.Id, txCt))
                throw FamilyErrors.InviteExpired();
            // Đếm lại sau khóa, bỏ lời mời này ra
            if (await family.CountSeatsAsync(group.Id, member.SeatRole, member.Id, nowUtc, txCt) >= MaxSeats(member.SeatRole))
                throw FamilyErrors.SeatFull();
            member.Status = FamilyMemberStatuses.Active;
            member.UserId = userId;
            member.JoinedAt = nowUtc;
            await family.SaveAsync(txCt);   // index "một thành viên active mỗi người" chặn hai lần chấp nhận cùng lúc
        }, ct);

        await notifications.NotifyAsync(group.OwnerUserId, NotificationTypes.FamilyInviteAccepted, "Có thành viên mới trong gói Gia đình",
            $"{user.FullName} đã chấp nhận lời mời tham gia gói Gia đình của bạn.", AccountLink, ct);
        return await GetAsync(userId, role, nowUtc, ct);
    }

    public async Task DeclineAsync(Guid userId, string token, DateTime nowUtc, CancellationToken ct = default)
    {
        var member = await FindInviteAsync(token, ct);
        if (member.Status != FamilyMemberStatuses.Pending)
            throw FamilyErrors.NotPending();
        var user = await users.GetUserById(userId, ct);
        if (user is null || !SameEmail(user.Email, member.InvitedEmail))
            throw FamilyErrors.WrongAccount();
        var group = await family.FindGroupAsync(member.FamilyGroupId, ct) ?? throw FamilyErrors.InviteInvalid();

        await RunAsync(async txCt =>
        {
            if (!await family.LockActiveGroupAsync(group.Id, txCt))
                throw FamilyErrors.InviteExpired();
            member.Status = FamilyMemberStatuses.Declined;
            member.EndedAt = nowUtc;
            member.TokenHash = null;
            await family.SaveAsync(txCt);
        }, ct);

        await notifications.NotifyAsync(group.OwnerUserId, NotificationTypes.FamilyInviteDeclined, "Lời mời gói Gia đình bị từ chối",
            $"{member.InvitedEmail} đã từ chối lời mời tham gia gói Gia đình của bạn.", AccountLink, ct);
    }

    // ---- thành viên

    public async Task RemoveMemberAsync(Guid ownerId, Guid memberId, DateTime nowUtc, CancellationToken ct = default)
    {
        var group = await RequireOwnedGroupAsync(ownerId, nowUtc, ct);
        if (memberId == ownerId)
            throw FamilyErrors.CannotRemoveOwner();

        Guid? removedUserId = null;
        await RunAsync(async txCt =>
        {
            if (!await family.LockActiveGroupAsync(group.Id, txCt))
                throw FamilyErrors.NoFamily();
            var member = await family.FindMemberAsync(memberId, txCt);
            if (member is null || member.FamilyGroupId != group.Id || member.Status != FamilyMemberStatuses.Active)
                throw FamilyErrors.MemberNotFound();
            member.Status = FamilyMemberStatuses.Removed;
            member.EndedAt = nowUtc;
            member.TokenHash = null;
            removedUserId = member.UserId;
            await family.SaveAsync(txCt);
        }, ct);

        if (removedUserId is { } removed)
            await notifications.NotifyAsync(removed, NotificationTypes.FamilyMemberRemoved, "Bạn đã được gỡ khỏi gói Gia đình",
                $"Bạn đã được gỡ khỏi gói Gia đình của {await NameOfAsync(ownerId, ct)}.", AccountLink, ct);
    }

    public async Task LeaveAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
    {
        var (group, isOwner) = await FindCurrentGroupAsync(userId, nowUtc, ct) ?? throw FamilyErrors.NotInFamily();
        if (isOwner)
            throw FamilyErrors.OwnerCannotLeave();

        await RunAsync(async txCt =>
        {
            if (!await family.LockActiveGroupAsync(group.Id, txCt))
                throw FamilyErrors.NotInFamily();
            var member = await family.FindActiveMembershipAsync(group.Id, userId, txCt) ?? throw FamilyErrors.NotInFamily();
            member.Status = FamilyMemberStatuses.Left;
            member.EndedAt = nowUtc;
            await family.SaveAsync(txCt);   // giữ TokenHash để link cũ còn báo "đã chấp nhận"; dùng lại thì cần lời mời mới
        }, ct);

        await notifications.NotifyAsync(group.OwnerUserId, NotificationTypes.FamilyMemberLeft, "Có thành viên rời gói Gia đình",
            $"{await NameOfAsync(userId, ct)} đã rời gói Gia đình của bạn.", AccountLink, ct);
    }

    public Task<int> ExpireInvitesAsync(DateTime nowUtc, CancellationToken ct = default) =>
        family.ExpireDueInvitesAsync(nowUtc, null, ct);

    // ---- nội bộ

    /// <summary>Nhóm hiện tại của người này (chủ trước, rồi thành viên), sau khi áp dụng chuyển kỳ. Null = không ở trong nhóm nào.</summary>
    private async Task<(FamilyGroup Group, bool IsOwner)?> FindCurrentGroupAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        await entitlements.GetSummaryAsync(userId, nowUtc, ct);
        if (await billing.FindActiveOwnedGroupAsync(userId, ct) is { } owned)
            return (owned, true);
        if (await billing.FindActiveMemberGroupAsync(userId, ct) is { } joined)
            return (joined, false);
        return null;
    }

    /// <summary>Nhóm của chủ gói còn kỳ chạy. Chỉ là thành viên → 403 not_owner; không ở nhóm nào → 404 no_family.</summary>
    private async Task<FamilyGroup> RequireOwnedGroupAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        var current = await FindCurrentGroupAsync(userId, nowUtc, ct);
        if (current is null)
            throw FamilyErrors.NoFamily();
        return current.Value.IsOwner ? current.Value.Group : throw FamilyErrors.NotOwner();
    }

    /// <summary>Lời mời theo token; token trống hoặc không có → 404 invite_invalid (HashToken sẽ ném với null).</summary>
    private async Task<FamilyMember> FindInviteAsync(string? token, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(token) ? throw FamilyErrors.InviteInvalid()
            : await family.FindMemberByTokenHashAsync(FamilyInviteToken.Hash(token), ct) ?? throw FamilyErrors.InviteInvalid();

    /// <summary>Chạy trong transaction; dòng bị thao tác khác sửa trước (xmin) nghĩa là lời mời không còn ở trạng thái chờ.</summary>
    private async Task RunAsync(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(work, ct);
        }
        catch (ConcurrencyConflictException)
        {
            throw FamilyErrors.NotPending();
        }
    }

    private async Task SaveOrNotPendingAsync(CancellationToken ct)
    {
        try
        {
            await family.SaveAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            throw FamilyErrors.NotPending();
        }
    }

    private async Task<string> NameOfAsync(Guid userId, CancellationToken ct) =>
        (await users.GetUserById(userId, ct))?.FullName ?? string.Empty;

    private static bool SameEmail(string a, string b) => AuthService.NormalizeEmail(a) == AuthService.NormalizeEmail(b);

    private static int MaxSeats(string seatRole) => seatRole == FamilySeats.Student ? FamilySeats.MaxStudents : FamilySeats.MaxParents;

    /// <summary>Lời mời giữ chỗ khi còn pending và chưa quá hạn (job dọn chạy chậm vẫn đọc đúng).</summary>
    private static bool IsLivePending(FamilyMember m, DateTime nowUtc) =>
        m.Status == FamilyMemberStatuses.Pending && m.ExpiresAt > nowUtc;

    internal static string LookupStatus(FamilyMember m, DateTime nowUtc) => m.Status switch
    {
        FamilyMemberStatuses.Pending => m.ExpiresAt > nowUtc ? FamilyInviteStatuses.Pending : FamilyInviteStatuses.Expired,
        FamilyMemberStatuses.Active or FamilyMemberStatuses.Removed or FamilyMemberStatuses.Left or FamilyMemberStatuses.Ended => FamilyInviteStatuses.Accepted,
        FamilyMemberStatuses.Declined => FamilyInviteStatuses.Declined,
        FamilyMemberStatuses.Cancelled => FamilyInviteStatuses.Cancelled,
        _ => FamilyInviteStatuses.Expired,
    };
}
