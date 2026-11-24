using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

public interface IParentLinkService
{
    /// <summary>[UC-115] Trang phụ huynh mở từ link mời: link còn chờ, đã đồng ý hay đã từ chối. Link sai → NotFound, quá 7 ngày → Expired.</summary>
    Task<ParentLinkResult<ParentInviteInfo>> GetInviteAsync(string token, CancellationToken ct = default);

    /// <summary>[UC-115] Phụ huynh đồng ý qua link mời, không cần tài khoản. Bắt buộc tick là cha, mẹ hoặc người giám hộ hợp pháp.</summary>
    /// <param name="seenPolicyVersion">Phiên bản văn bản phụ huynh đã đọc; khác phiên bản đang hiệu lực thì không ghi đồng ý (Validation). Null thì bỏ qua kiểm tra.</param>
    Task<ParentLinkResult<ParentInviteInfo>> AcceptAsync(string token, bool isGuardian, string? ipAddress, CancellationToken ct = default, string? seenPolicyVersion = null);

    /// <summary>[UC-115] Phụ huynh từ chối qua link mời: tài khoản học sinh sang Từ chối, học sinh được báo.</summary>
    Task<ParentLinkResult<ParentInviteInfo>> RejectAsync(string token, string? ipAddress, CancellationToken ct = default);

    Task<ParentLinkResult<ParentLinkDto>> ResendAsync(Guid studentUserId, CancellationToken ct = default);
    Task<ParentLinkResult<ParentLinkDto>> ChangeParentEmailAsync(Guid studentUserId, string parentEmail, CancellationToken ct = default);
    Task<ParentLinkResult<ParentLinkDto>> RevokeAsync(Guid userId, Guid linkId, CancellationToken ct = default);
    Task<ParentLinkResult<List<ParentLinkDto>>> GetMineAsync(Guid userId, CancellationToken ct = default);

    /// <summary>[UC-117] Trang rút đồng ý đọc tên học sinh và ngày đồng ý từ token trong mail gửi phụ huynh.</summary>
    Task<ParentLinkResult<ParentRevokeInfo>> GetRevokeInfoAsync(string token, CancellationToken ct = default);

    /// <summary>[UC-117] Phụ huynh rút đồng ý bằng link trong mail, không cần đăng nhập. Token dùng một lần, chết khi học sinh đủ 16 tuổi.</summary>
    Task<ParentLinkResult<ParentRevokeInfo>> RevokeByTokenAsync(string token, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// UC-80: hủy mọi liên kết chưa bị hủy của tài khoản sắp xóa (cả phía con lẫn phía phụ huynh) và rút consent.
    /// Không chặn học sinh dưới 16 tuổi vì đặc tả cho phép tự xóa tài khoản.
    /// Không gửi email: trả danh sách thông báo để người gọi gửi sau khi transaction commit.
    /// </summary>
    Task<List<ParentLinkRevokeNotice>> RevokeAllForAccountDeletionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// [UC-75 / UC-115] Phụ huynh vừa xác thực email tài khoản mới: gắn tài khoản vào các liên kết đã đồng ý qua link mail
    /// cùng email, báo học sinh. Bản ghi đồng ý giữ nguyên (bằng chứng là email lúc đồng ý).
    /// </summary>
    Task LinkParentAccountAsync(User parent, CancellationToken ct = default);
}

/// <summary>UC-80: thư báo hủy liên kết gửi cho bên còn lại (gửi sau commit).</summary>
public record ParentLinkRevokeNotice(string ToEmail, string StudentName, string ParentName);

/// <summary>
/// [Q11] Luồng phụ huynh đồng ý cho hệ thống xử lý dữ liệu của học sinh (Luật BVDLCN 91/2025 Điều 24, NĐ 356/2025 Điều 6):
/// mời qua email → phụ huynh bấm link, đồng ý hoặc từ chối, không cần tài khoản (UC-115; lưu consent: email, lúc nào,
/// phiên bản nội dung, IP) → có thể rút lại (UC-117). Lịch sử link và consent không bao giờ bị xóa.
/// </summary>
public class ParentLinkService(
    IParentLinkRepository links,
    IUserRepository users,
    IEmailSender emailSender,
    IAuditLogRepository auditLogs,
    // Chuyển hồ sơ phụ huynh đã nhập sang con khi tài khoản phụ huynh được gắn vào liên kết; null trong test cũ
    IParentProfileShareService? profileShare = null,
    StudyAbroad.Application.Notifications.INotificationService? notifications = null,
    // Phiên bản "Đồng ý của phụ huynh" đang hiệu lực (UC-103); null trong test cũ thì dùng hằng số dự phòng
    StudyAbroad.Application.Admin.Policies.IPolicyVersionLookup? policyVersions = null,
    // [UC-111 bước 5] Thu hồi yêu cầu liên hệ còn mở khi học sinh dưới 16 không còn đồng ý; null trong test cũ
    StudyAbroad.Application.Leads.ILeadWithdrawalRepository? leadWithdrawals = null) : IParentLinkService
{
    /// <summary>[UC-117] Action ghi audit khi phụ huynh rút đồng ý qua link trong mail.</summary>
    public const string RevokeByEmailAuditAction = "parent_link.revoke_by_email";

    /// <summary>Tối đa số email phụ huynh mới học sinh được khai trong 24 giờ (chống dùng hệ thống gửi mail rác).</summary>
    public const int MaxNewLinksPerDay = 5;

    private const string InvalidInviteMessage = "Link không hợp lệ.";
    private const string ExpiredInviteMessage = "Link đã hết hạn. Vui lòng nhờ học sinh gửi lại lời mời từ trang Tài khoản.";
    private const string AlreadyAnsweredMessage = "Lời mời này đã được trả lời trước đó. Vui lòng tải lại trang để xem kết quả.";
    private const string GuardianRequiredMessage = "Vui lòng xác nhận bạn là cha, mẹ hoặc người giám hộ hợp pháp của học sinh.";
    private const string PolicyChangedMessage = "Nội dung đồng ý vừa được cập nhật. Vui lòng tải lại trang và đọc lại trước khi đồng ý.";
    private const string ConflictMessage = "Liên kết vừa được cập nhật bởi một thao tác khác. Vui lòng tải lại trang.";
    private const string NotFoundMessage = "Không tìm thấy liên kết.";
    private const string InvalidRevokeLinkMessage = "Liên kết rút đồng ý không hợp lệ hoặc đã được sử dụng.";
    private const string StudentAccountMessage = "Email này là tài khoản học sinh nên không thể làm phụ huynh. Bạn hãy bấm Từ chối để học sinh đổi sang email khác của phụ huynh.";
    private const string OtherAccountMessage = "Email này đã dùng cho tài khoản không phải phụ huynh nên không thể làm phụ huynh. Bạn hãy bấm Từ chối để học sinh đổi sang email khác của phụ huynh.";
    private const string RevokeLinkAgeMessage = "Học sinh đã đủ 16 tuổi nên link này không còn dùng được. Từ 16 tuổi, học sinh tự quyết định việc đồng ý xử lý dữ liệu của mình.";

    public async Task<ParentLinkResult<ParentInviteInfo>> GetInviteAsync(string token, CancellationToken ct = default)
    {
        var (link, error) = await FindInviteByTokenAsync(token, ct);
        if (link == null)
            return InviteFail(error);

        var student = await users.GetUserById(link.StudentUserId, ct);
        if (student == null)
            return InviteFail(ParentLinkError.NotFound);

        // Đã từ chối: thời điểm trả lời nằm ở consent (Granted = false) ghi lúc bấm
        DateTime? decidedAt = link.AcceptedAt;
        if (link.Status == ParentLinkStatuses.Rejected && link.ConsentId is { } consentId)
            decidedAt = (await links.GetConsentAsync(consentId, ct))?.CreatedAt;

        var info = ToInviteInfo(link, student, decidedAt, await CurrentConsentVersionAsync(ct));
        if (info.State == ParentInviteStates.Pending && await NonParentAccountMessageAsync(link.ParentEmail, ct) is { } blocked)
            info = info with { BlockedReason = blocked };
        return ParentLinkResult<ParentInviteInfo>.Ok(info);
    }

    public Task<ParentLinkResult<ParentInviteInfo>> AcceptAsync(
        string token, bool isGuardian, string? ipAddress, CancellationToken ct = default, string? seenPolicyVersion = null)
    {
        if (!isGuardian)
            return Task.FromResult(ParentLinkResult<ParentInviteInfo>.Fail(ParentLinkError.Validation, GuardianRequiredMessage));
        return DecideAsync(token, accept: true, ipAddress, ct, seenPolicyVersion);
    }

    public Task<ParentLinkResult<ParentInviteInfo>> RejectAsync(string token, string? ipAddress, CancellationToken ct = default) =>
        DecideAsync(token, accept: false, ipAddress, ct);

    /// <summary>
    /// [UC-115] Ghi quyết định của phụ huynh. Không kiểm tuổi hay quan hệ của người bấm (tài liệu: hệ thống không kiểm được);
    /// bằng chứng là consent: email nhận link, thời điểm, IP, phiên bản văn bản. Link chỉ trả lời được một lần;
    /// giữ TokenHash để mở lại link vẫn thấy kết quả đã ghi nhận.
    /// </summary>
    private async Task<ParentLinkResult<ParentInviteInfo>> DecideAsync(
        string token, bool accept, string? ipAddress, CancellationToken ct, string? seenPolicyVersion = null)
    {
        var (link, error) = await FindInviteByTokenAsync(token, ct);
        if (link == null)
            return InviteFail(error);
        if (link.Status != ParentLinkStatuses.Pending)
            return ParentLinkResult<ParentInviteInfo>.Fail(ParentLinkError.Conflict, AlreadyAnsweredMessage);

        var student = await users.GetUserById(link.StudentUserId, ct);
        if (student == null)
            return InviteFail(ParentLinkError.NotFound);

        // Email là tài khoản học sinh / trung tâm / quản trị thì không làm phụ huynh được; từ chối vẫn ghi nhận bình thường
        if (accept && await NonParentAccountMessageAsync(link.ParentEmail, ct) is { } blockedMessage)
            return ParentLinkResult<ParentInviteInfo>.Fail(ParentLinkError.Validation, blockedMessage);

        // UC-75: email nhận link đã là tài khoản Phụ huynh thì gắn luôn, phụ huynh thấy con trong trang Tài khoản
        var parentAccount = accept ? await FindParentAccountAsync(link.ParentEmail, ct) : null;

        // Ghi ĐÚNG phiên bản đang hiệu lực. Phụ huynh đã đọc bản khác (admin vừa công bố bản mới) thì không ghi đồng ý,
        // để bằng chứng luôn khớp với văn bản người đó thực sự đã đọc. Từ chối thì không cần kiểm tra.
        var policyVersion = await CurrentConsentVersionAsync(ct);
        if (accept && seenPolicyVersion is not null && !string.Equals(seenPolicyVersion, policyVersion, StringComparison.Ordinal))
            return ParentLinkResult<ParentInviteInfo>.Fail(ParentLinkError.Validation, PolicyChangedMessage);

        var now = DateTime.UtcNow;
        var consent = new Consent
        {
            UserId = student.Id,
            Purpose = ConsentPurposes.ChildDataProcessing,
            Granted = accept,
            PolicyVersion = policyVersion,
            GrantedByUserId = parentAccount?.Id,
            GrantedByEmail = link.ParentEmail,
            GrantedIp = Truncate(ipAddress, 45),
            CreatedAt = now,
        };
        link.ConsentId = consent.Id;
        link.TokenExpiresAt = null;

        string? revokeToken = null;
        if (accept)
        {
            link.Status = ParentLinkStatuses.Active;
            link.ParentUserId = parentAccount?.Id;
            link.AcceptedAt = now;
            // [UC-117] Link rút đồng ý gửi phụ huynh: DB chỉ giữ hash
            revokeToken = ParentInvite.GenerateToken();
            link.RevokeTokenHash = ParentInvite.HashToken(revokeToken);
        }
        else
        {
            link.Status = ParentLinkStatuses.Rejected;
        }

        try
        {
            await links.SaveAcceptAsync(link, consent, ct);
        }
        catch (ConcurrencyConflictException)
        {
            return ParentLinkResult<ParentInviteInfo>.Fail(ParentLinkError.Conflict, AlreadyAnsweredMessage);
        }

        var parentLabel = parentAccount?.FullName ?? link.ParentEmail;
        if (accept)
        {
            await emailSender.SendParentLinkChangedEmailAsync(student.Email, student.FullName, parentLabel, linked: true, ct);
            // [UC-110 / UC-75] Báo học sinh phụ huynh đã xác nhận liên kết
            await NotifyAsync(student.Id, StudyAbroad.Application.Notifications.NotificationTypes.ParentLinkLinked, "Phụ huynh đã xác nhận liên kết",
                $"{parentLabel} đã liên kết với tài khoản của bạn.", ct);
            await emailSender.SendParentConsentConfirmedEmailAsync(link.ParentEmail, student.FullName, revokeToken!, ct);
            if (parentAccount != null && profileShare != null)
                await profileShare.PrepareMatchAfterLinkAsync(parentAccount.Id, [link], ct);
        }
        else
        {
            await emailSender.SendParentConsentRejectedEmailAsync(student.Email, student.FullName, parentLabel, ct);
        }

        return ParentLinkResult<ParentInviteInfo>.Ok(
            ToInviteInfo(link, student, now, policyVersion),
            accept ? "Đã ghi nhận bạn đồng ý." : "Đã ghi nhận bạn từ chối.");
    }

    /// <summary>Tài khoản vai trò Phụ huynh, chưa xóa, đúng email nhận link mời; không có thì null.</summary>
    private async Task<User?> FindParentAccountAsync(string parentEmail, CancellationToken ct)
    {
        var user = await users.GetUserByEmail(parentEmail, ct);
        return user is { Role: UserRoles.Parent, DeletedAt: null } ? user : null;
    }

    /// <summary>Email đã thuộc một tài khoản còn hoạt động nhưng không phải vai trò Phụ huynh: trả lời báo; không thì null.</summary>
    private async Task<string?> NonParentAccountMessageAsync(string parentEmail, CancellationToken ct)
    {
        var user = await users.GetUserByEmail(parentEmail, ct);
        if (user is not { DeletedAt: null } || user.Role == UserRoles.Parent)
            return null;
        return user.Role == UserRoles.Student ? StudentAccountMessage : OtherAccountMessage;
    }

    public async Task<ParentLinkResult<ParentLinkDto>> ResendAsync(Guid studentUserId, CancellationToken ct = default)
    {
        var student = await users.GetUserById(studentUserId, ct);
        if (student == null || student.Role != UserRoles.Student)
            return Fail(ParentLinkError.Forbidden, "Chỉ học sinh mới gửi được lời mời phụ huynh.");

        // Gửi lại được cả lời mời đã hết hạn hoặc bị từ chối: về lại Chờ phụ huynh xác nhận
        var link = (await links.GetByStudentAsync(studentUserId, ct)).FirstOrDefault(IsReinvitable);
        if (link == null)
            return Fail(ParentLinkError.NotFound, "Không có lời mời nào để gửi lại.");

        var now = DateTime.UtcNow;
        if (link.LastInviteSentAt is { } last)
        {
            if (now - last < ParentInvite.ResendCooldown)
                return Fail(ParentLinkError.TooManyRequests, $"Vui lòng đợi {(int)ParentInvite.ResendCooldown.TotalSeconds} giây trước khi gửi lại lời mời.");
            if (now - last < TimeSpan.FromDays(1) && link.InviteSentCount >= ParentInvite.MaxSendsPerDay)
                return Fail(ParentLinkError.TooManyRequests, $"Đã gửi lời mời {ParentInvite.MaxSendsPerDay} lần trong 24 giờ. Vui lòng thử lại sau.");
        }

        var token = ParentInvite.Issue(link, now);
        // Consent từ chối lần trước vẫn giữ trong bảng consent làm bằng chứng, chỉ bỏ khỏi link
        link.Status = ParentLinkStatuses.Pending;
        link.ConsentId = null;
        try
        {
            await links.UpdateAsync(link, ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Fail(ParentLinkError.Conflict, ConflictMessage);
        }

        await emailSender.SendParentInviteEmailAsync(link.ParentEmail, student.FullName, token, ParentInvite.ExpiryDays, ct);
        return ParentLinkResult<ParentLinkDto>.Ok(ToDto(link, null, student, canRevoke: true), "Đã gửi lại lời mời tới email phụ huynh.");
    }

    public async Task<ParentLinkResult<ParentLinkDto>> ChangeParentEmailAsync(Guid studentUserId, string parentEmail, CancellationToken ct = default)
    {
        var student = await users.GetUserById(studentUserId, ct);
        if (student == null || student.Role != UserRoles.Student)
            return Fail(ParentLinkError.Forbidden, "Chỉ học sinh mới khai được email phụ huynh.");

        if (AgeOf(student) is { } age && age >= AgeGroups.AdultAge)
            return Fail(ParentLinkError.Validation, $"Bạn đã đủ {AgeGroups.AdultAge} tuổi nên không cần phụ huynh đồng ý.");

        var email = AuthService.NormalizeEmail(parentEmail);
        if (!AuthService.IsValidEmail(email))
            return Fail(ParentLinkError.Validation, $"Email phụ huynh không hợp lệ hoặc vượt quá {AuthService.MaxEmailLength} ký tự.");
        if (email == AuthService.NormalizeEmail(student.Email))
            return Fail(ParentLinkError.Validation, "Email phụ huynh phải khác email của học sinh.");

        var existing = await links.GetByStudentAsync(studentUserId, ct);
        if (existing.Any(l => l.Status == ParentLinkStatuses.Active && l.ParentEmail == email))
            return Fail(ParentLinkError.Conflict, "Email này đã liên kết với tài khoản của bạn.");

        var now = DateTime.UtcNow;
        if (existing.Count(l => now - l.CreatedAt < TimeSpan.FromDays(1)) >= MaxNewLinksPerDay)
            return Fail(ParentLinkError.TooManyRequests, $"Bạn đã đổi email phụ huynh {MaxNewLinksPerDay} lần trong 24 giờ. Vui lòng thử lại sau.");

        // Mỗi lúc chỉ một lời mời chưa xong: hủy lời mời cũ đang chờ / hết hạn / bị từ chối (token cũ hết hiệu lực)
        foreach (var pending in existing.Where(IsReinvitable))
        {
            MarkRevoked(pending, studentUserId, now);
            try
            {
                await links.UpdateAsync(pending, ct);
            }
            catch (ConcurrencyConflictException)
            {
                return Fail(ParentLinkError.Conflict, ConflictMessage);
            }
        }

        var link = new ParentStudentLink { StudentUserId = studentUserId, ParentEmail = email };
        var token = ParentInvite.Issue(link, now);
        await links.AddAsync(link, ct);

        await emailSender.SendParentInviteEmailAsync(email, student.FullName, token, ParentInvite.ExpiryDays, ct);
        return ParentLinkResult<ParentLinkDto>.Ok(ToDto(link, null, student, canRevoke: true), "Đã gửi lời mời tới email phụ huynh mới.");
    }

    public async Task<ParentLinkResult<ParentLinkDto>> RevokeAsync(Guid userId, Guid linkId, CancellationToken ct = default)
    {
        var link = await links.GetByIdAsync(linkId, ct);
        var user = await users.GetUserById(userId, ct);
        if (link == null || user == null || !IsOpen(link))
            return Fail(ParentLinkError.NotFound, NotFoundMessage);

        var isParentSide = link.ParentUserId == userId && link.Status == ParentLinkStatuses.Active;
        var isStudentSide = link.StudentUserId == userId;
        if (!isParentSide && !isStudentSide)
            return Fail(ParentLinkError.NotFound, NotFoundMessage);

        // Điều 24: với trẻ dưới 16, quyền rút đồng ý thuộc người đại diện (phụ huynh)
        if (isStudentSide && !CanStudentRevoke(link, user))
            return Fail(ParentLinkError.Forbidden, $"Học sinh dưới {AgeGroups.ChildMaxAgeExclusive} tuổi không thể tự rút sự đồng ý của phụ huynh. Phụ huynh có thể rút trong trang Tài khoản của mình.");

        var wasActive = link.Status == ParentLinkStatuses.Active;
        if (!await RevokeAndSaveAsync(link, userId, ct))
            return Fail(ParentLinkError.Conflict, ConflictMessage);

        var student = isStudentSide ? user : await users.GetUserById(link.StudentUserId, ct);
        var parent = isParentSide ? user : link.ParentUserId is { } pid ? await users.GetUserById(pid, ct) : null;

        // Báo bên còn lại khi một liên kết đang hiệu lực bị hủy (hủy lời mời chưa thành thì không cần).
        // Học sinh hủy liên kết của phụ huynh chưa có tài khoản (UC-115) → báo qua email nhận link mời.
        if (wasActive && student != null)
        {
            var parentLabel = parent?.FullName ?? link.ParentEmail;
            var toEmail = isParentSide ? student.Email : parent?.Email ?? link.ParentEmail;
            await emailSender.SendParentLinkChangedEmailAsync(toEmail, student.FullName, parentLabel, linked: false, ct);
            // [UC-110 / UC-75] Báo bên còn lại (nếu có tài khoản) liên kết đã bị hủy
            var otherId = isParentSide ? student.Id : parent?.Id;
            if (otherId is { } oid)
                await NotifyAsync(oid, StudyAbroad.Application.Notifications.NotificationTypes.ParentLinkRevoked, "Liên kết phụ huynh đã bị hủy",
                    $"Liên kết giữa {parentLabel} và {student.FullName} đã bị hủy.", ct);
            // [UC-111 1a] Phụ huynh rút đồng ý ở mục Con của tôi: xử lý như rút qua email
            await WithdrawLeadsIfConsentGoneAsync(student, ct);
        }

        return ParentLinkResult<ParentLinkDto>.Ok(
            ToDto(link, parent, student, canRevoke: false),
            wasActive ? "Đã hủy liên kết và rút lại sự đồng ý." : "Đã hủy lời mời.");
    }

    public async Task LinkParentAccountAsync(User parent, CancellationToken ct = default)
    {
        if (parent.Role != UserRoles.Parent)
            return;

        var email = AuthService.NormalizeEmail(parent.Email);
        var linked = new List<ParentStudentLink>();
        foreach (var link in await links.GetActiveWithoutParentAccountAsync(email, ct))
        {
            link.ParentUserId = parent.Id;
            try
            {
                await links.UpdateAsync(link, ct);
            }
            catch (ConcurrencyConflictException)
            {
                // Liên kết vừa bị rút / hủy cùng lúc: bỏ qua, không gắn
                continue;
            }
            linked.Add(link);

            var student = await users.GetUserById(link.StudentUserId, ct);
            if (student != null)
                await emailSender.SendParentAccountLinkedEmailAsync(student.Email, student.FullName, parent.FullName, ct);
        }

        if (linked.Count > 0 && profileShare != null)
            await profileShare.PrepareMatchAfterLinkAsync(parent.Id, linked, ct);
    }

    public async Task<List<ParentLinkRevokeNotice>> RevokeAllForAccountDeletionAsync(Guid userId, CancellationToken ct = default)
    {
        var notices = new List<ParentLinkRevokeNotice>();
        var user = await users.GetUserById(userId, ct);
        if (user == null)
            return notices;

        var asStudent = await links.GetByStudentAsync(userId, ct);
        var asParent = await links.GetActiveByParentAsync(userId, ct);
        // Cả lời mời hết hạn / bị từ chối: hủy để link mời cũ không còn tra được
        var open = asStudent.Concat(asParent).Where(IsOpen).ToList();

        var now = DateTime.UtcNow;
        foreach (var link in open)
        {
            var wasActive = link.Status == ParentLinkStatuses.Active;
            Consent? consent = null;
            if (wasActive && link.ConsentId is { } consentId)
            {
                consent = await links.GetConsentAsync(consentId, ct);
                if (consent != null)
                    consent.Granted = false;
            }
            MarkRevoked(link, userId, now);

            try
            {
                await links.SaveRevokeAsync(link, consent, ct);
            }
            catch (ConcurrencyConflictException)
            {
                // Liên kết vừa bị thao tác khác đổi (thường là bên kia vừa hủy): bỏ qua, không báo
                continue;
            }

            if (!wasActive)
                continue;

            var isStudentSide = link.StudentUserId == userId;
            if (isStudentSide)
            {
                // Báo phụ huynh: tài khoản phụ huynh nếu có, không thì email nhận link mời (UC-115)
                var parent = link.ParentUserId is { } pid ? await users.GetUserById(pid, ct) : null;
                notices.Add(new ParentLinkRevokeNotice(parent?.Email ?? link.ParentEmail, user.FullName, parent?.FullName ?? link.ParentEmail));
            }
            else
            {
                var student = await users.GetUserById(link.StudentUserId, ct);
                if (student != null)
                    notices.Add(new ParentLinkRevokeNotice(student.Email, student.FullName, user.FullName));
            }
        }

        return notices;
    }

    public async Task<ParentLinkResult<ParentRevokeInfo>> GetRevokeInfoAsync(string token, CancellationToken ct = default)
    {
        var (link, student, error) = await FindRevocableByTokenAsync(token, ct);
        if (link == null || student == null)
            return RevokeFail(error);

        return ParentLinkResult<ParentRevokeInfo>.Ok(new ParentRevokeInfo(student.FullName, link.AcceptedAt));
    }

    public async Task<ParentLinkResult<ParentRevokeInfo>> RevokeByTokenAsync(string token, string? ipAddress, CancellationToken ct = default)
    {
        var (link, student, error) = await FindRevocableByTokenAsync(token, ct);
        if (link == null || student == null)
            return RevokeFail(error);

        // Người rút là phụ huynh của liên kết; null khi phụ huynh đồng ý qua email mà chưa có tài khoản (UC-115)
        var parentId = link.ParentUserId;
        if (!await RevokeAndSaveAsync(link, parentId, ct))
            return ParentLinkResult<ParentRevokeInfo>.Fail(ParentLinkError.Conflict, ConflictMessage);

        // Chỉ ghi id liên kết và IP, không ghi email
        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = parentId,
            Action = RevokeByEmailAuditAction,
            EntityType = "parent_student_link",
            EntityId = link.Id.ToString(),
            IpAddress = Truncate(ipAddress, 45),
        }, ct);

        var parent = parentId is { } pid ? await users.GetUserById(pid, ct) : null;
        await emailSender.SendParentLinkChangedEmailAsync(student.Email, student.FullName, parent?.FullName ?? link.ParentEmail, linked: false, ct);
        // [UC-110 / UC-75] Phụ huynh rút đồng ý qua email: báo học sinh
        await NotifyAsync(student.Id, StudyAbroad.Application.Notifications.NotificationTypes.ParentLinkRevoked, "Phụ huynh đã rút lại sự đồng ý",
            $"{parent?.FullName ?? link.ParentEmail} đã hủy liên kết với tài khoản của bạn.", ct);
        await WithdrawLeadsIfConsentGoneAsync(student, ct);

        return ParentLinkResult<ParentRevokeInfo>.Ok(new ParentRevokeInfo(student.FullName, link.AcceptedAt), "Đã rút lại sự đồng ý.");
    }

    /// <summary>
    /// [UC-117] Token sai / đã dùng / liên kết không còn hiệu lực → NotFound (một câu chung, không lộ thông tin học sinh);
    /// học sinh đã đủ 16 tuổi → Forbidden.
    /// </summary>
    private async Task<(ParentStudentLink? Link, User? Student, ParentLinkError Error)> FindRevocableByTokenAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
            return (null, null, ParentLinkError.NotFound);

        var link = await links.GetByRevokeTokenHashAsync(ParentInvite.HashToken(token), ct);
        if (link == null || link.Status != ParentLinkStatuses.Active)
            return (null, null, ParentLinkError.NotFound);

        var student = await users.GetUserById(link.StudentUserId, ct);
        if (student == null)
            return (null, null, ParentLinkError.NotFound);

        if (AgeOf(student) is { } age && age >= AgeGroups.ChildMaxAgeExclusive)
            return (null, null, ParentLinkError.Forbidden);

        return (link, student, ParentLinkError.None);
    }

    /// <summary>
    /// [UC-111 bước 5] Học sinh dưới 16 không còn đồng ý nào của phụ huynh: yêu cầu liên hệ chưa kết thúc chuyển Đã thu hồi
    /// (trung tâm chỉ còn thấy mã, trạng thái, ngày; nhắn tin bị khóa), yêu cầu còn chờ phụ huynh bị hủy, trung tâm nhận thông báo.
    /// Còn phụ huynh khác đang đồng ý thì giữ nguyên.
    /// </summary>
    private async Task WithdrawLeadsIfConsentGoneAsync(User student, CancellationToken ct)
    {
        if (leadWithdrawals is null || AgeOf(student) is not { } age || age >= AgeGroups.ChildMaxAgeExclusive)
            return;
        if (await links.HasGrantedConsentAsync(student.Id, ct))
            return;

        var centerOwners = await leadWithdrawals.ListCenterOwnersOfOpenLeadsAsync(student.Id, ct);
        await leadWithdrawals.WithdrawOpenLeadsAsync(student.Id, DateTime.UtcNow, ct);
        if (notifications is null)
            return;
        foreach (var ownerId in centerOwners)
            await notifications.NotifyAsync(ownerId, StudyAbroad.Application.Notifications.NotificationTypes.LeadStatusChanged,
                "Yêu cầu liên hệ đã bị thu hồi",
                "Người đại diện của học sinh đã rút đồng ý nên yêu cầu liên hệ đã bị thu hồi. Dữ liệu của học sinh không còn hiển thị.",
                "/center/leads", ct);
    }

    private static ParentLinkResult<ParentRevokeInfo> RevokeFail(ParentLinkError error) =>
        ParentLinkResult<ParentRevokeInfo>.Fail(error, error == ParentLinkError.Forbidden ? RevokeLinkAgeMessage : InvalidRevokeLinkMessage);

    /// <summary>
    /// Đánh dấu liên kết đã hủy rồi lưu; liên kết đang hiệu lực thì rút consent (Granted = false).
    /// Consent từ chối (đã Granted = false) giữ nguyên. Trả false khi bị request khác ghi trước.
    /// </summary>
    private async Task<bool> RevokeAndSaveAsync(ParentStudentLink link, Guid? byUserId, CancellationToken ct)
    {
        Consent? consent = null;
        if (link.Status == ParentLinkStatuses.Active && link.ConsentId is { } consentId)
        {
            consent = await links.GetConsentAsync(consentId, ct);
            if (consent != null)
                consent.Granted = false;
        }
        MarkRevoked(link, byUserId, DateTime.UtcNow);

        try
        {
            await links.SaveRevokeAsync(link, consent, ct);
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            return false;
        }
    }

    public async Task<ParentLinkResult<List<ParentLinkDto>>> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.GetUserById(userId, ct);
        if (user == null)
            return ParentLinkResult<List<ParentLinkDto>>.Fail(ParentLinkError.NotFound, NotFoundMessage);

        var result = new List<ParentLinkDto>();
        if (user.Role == UserRoles.Student)
        {
            foreach (var link in (await links.GetByStudentAsync(userId, ct)).Where(IsOpen))
            {
                var parent = link.ParentUserId is { } pid ? await users.GetUserById(pid, ct) : null;
                result.Add(ToDto(link, parent, user, CanStudentRevoke(link, user)));
            }
        }
        else if (user.Role == UserRoles.Parent)
        {
            foreach (var link in await links.GetActiveByParentAsync(userId, ct))
            {
                var student = await users.GetUserById(link.StudentUserId, ct);
                result.Add(ToDto(link, user, student, canRevoke: true));
            }
        }

        return ParentLinkResult<List<ParentLinkDto>>.Ok(result);
    }

    /// <summary>
    /// [UC-115] Tra link mời: token sai / link đã bị hủy → NotFound (một câu chung, không lộ thông tin);
    /// quá 7 ngày → Expired (lần đầu thấy quá hạn thì đặt status expired luôn, không cần job).
    /// Link đã đồng ý / đã từ chối vẫn trả về để trang hiện kết quả đã ghi nhận.
    /// </summary>
    private async Task<(ParentStudentLink? Link, ParentLinkError Error)> FindInviteByTokenAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
            return (null, ParentLinkError.NotFound);

        var link = await links.GetByTokenHashAsync(ParentInvite.HashToken(token), ct);
        if (link == null || !IsOpen(link))
            return (null, ParentLinkError.NotFound);

        if (link.Status == ParentLinkStatuses.Expired)
            return (null, ParentLinkError.Expired);

        if (link.Status == ParentLinkStatuses.Pending && IsPastExpiry(link, DateTime.UtcNow))
        {
            link.Status = ParentLinkStatuses.Expired;
            try
            {
                await links.UpdateAsync(link, ct);
            }
            catch (ConcurrencyConflictException)
            {
                // Request khác vừa ghi (thường là học sinh vừa gửi lại): lần tra sau sẽ thấy trạng thái mới
            }
            return (null, ParentLinkError.Expired);
        }

        return (link, ParentLinkError.None);
    }

    private static ParentLinkResult<ParentInviteInfo> InviteFail(ParentLinkError error) =>
        ParentLinkResult<ParentInviteInfo>.Fail(error, error == ParentLinkError.Expired ? ExpiredInviteMessage : InvalidInviteMessage);

    // Phiên bản "Đồng ý của phụ huynh" đang hiệu lực do admin công bố (UC-103); chưa tra được thì dùng hằng số của bản đầu tiên
    private async Task<string> CurrentConsentVersionAsync(CancellationToken ct) =>
        (policyVersions is null ? null : await policyVersions.GetPublishedVersionAsync(PolicyKinds.ChildDataConsent, ct))
        ?? ConsentPurposes.ChildDataPolicyVersion;

    private static ParentInviteInfo ToInviteInfo(ParentStudentLink link, User student, DateTime? decidedAt, string policyVersion)
    {
        var state = link.Status switch
        {
            ParentLinkStatuses.Active => ParentInviteStates.Accepted,
            ParentLinkStatuses.Rejected => ParentInviteStates.Rejected,
            _ => ParentInviteStates.Pending,
        };
        var pending = state == ParentInviteStates.Pending;
        return new ParentInviteInfo(state, student.FullName, AgeOf(student), link.ParentEmail,
            pending ? link.TokenExpiresAt : null, pending ? null : decidedAt, policyVersion,
            HasParentAccount: link.ParentUserId is not null);
    }

    /// <summary>Liên kết chưa bị hủy: đang chờ, đã đồng ý, hết hạn hoặc bị từ chối.</summary>
    private static bool IsOpen(ParentStudentLink link) =>
        link.Status is ParentLinkStatuses.Pending or ParentLinkStatuses.Active or ParentLinkStatuses.Expired or ParentLinkStatuses.Rejected;

    /// <summary>Lời mời chưa thành: học sinh gửi lại / đổi email được.</summary>
    private static bool IsReinvitable(ParentStudentLink link) =>
        link.Status is ParentLinkStatuses.Pending or ParentLinkStatuses.Expired or ParentLinkStatuses.Rejected;

    private static bool IsPastExpiry(ParentStudentLink link, DateTime now) =>
        link.TokenExpiresAt is { } expiresAt && expiresAt <= now;

    /// <summary>Trạng thái trả cho client: lời mời chờ đã quá 7 ngày hiện là hết hạn dù chưa ai mở link.</summary>
    private static string EffectiveStatus(ParentStudentLink link) =>
        link.Status == ParentLinkStatuses.Pending && IsPastExpiry(link, DateTime.UtcNow) ? ParentLinkStatuses.Expired : link.Status;

    /// <summary>Học sinh tự hủy lời mời chưa thành được; hủy liên kết đã đồng ý chỉ khi từ 16 tuổi (hoặc tài khoản cũ chưa có ngày sinh).</summary>
    private static bool CanStudentRevoke(ParentStudentLink link, User student) =>
        link.Status != ParentLinkStatuses.Active ||
        AgeOf(student) is not { } age || age >= AgeGroups.ChildMaxAgeExclusive;

    private static void MarkRevoked(ParentStudentLink link, Guid? byUserId, DateTime now)
    {
        link.Status = ParentLinkStatuses.Revoked;
        link.RevokedAt = now;
        link.RevokedByUserId = byUserId;
        link.TokenHash = null;
        link.TokenExpiresAt = null;
        link.RevokeTokenHash = null;
    }

    private static int? AgeOf(User user) =>
        user.DateOfBirth is { } dob ? AgePolicy.GetAge(dob, AgePolicy.TodayInVietnam(DateTime.UtcNow)) : null;

    private static string? Truncate(string? value, int max) =>
        value == null || value.Length <= max ? value : value[..max];

    private static ParentLinkDto ToDto(ParentStudentLink link, User? parent, User? student, bool canRevoke) =>
        new(link.Id, EffectiveStatus(link), link.ParentEmail, parent?.FullName, student?.FullName ?? string.Empty,
            student == null ? null : AgeOf(student), link.StudentUserId, link.AcceptedAt, link.LastInviteSentAt, link.TokenExpiresAt, canRevoke);

    private static ParentLinkResult<ParentLinkDto> Fail(ParentLinkError error, string message) =>
        ParentLinkResult<ParentLinkDto>.Fail(error, message);

    private Task NotifyAsync(Guid userId, string type, string title, string body, CancellationToken ct) =>
        notifications is null
            ? Task.CompletedTask
            : notifications.NotifyAsync(userId, type, title, body, "/account#parent-links", ct);
}
