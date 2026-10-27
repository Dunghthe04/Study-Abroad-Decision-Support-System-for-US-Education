using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

public interface IParentLinkService
{
    Task<ParentLinkResult<ParentInviteInfo>> GetInviteAsync(string token, CancellationToken ct = default);
    Task<ParentLinkResult<ParentLinkDto>> AcceptAsync(Guid parentUserId, string token, string? ipAddress, CancellationToken ct = default);
    Task<ParentLinkResult<ParentLinkDto>> ResendAsync(Guid studentUserId, CancellationToken ct = default);
    Task<ParentLinkResult<ParentLinkDto>> ChangeParentEmailAsync(Guid studentUserId, string parentEmail, CancellationToken ct = default);
    Task<ParentLinkResult<ParentLinkDto>> RevokeAsync(Guid userId, Guid linkId, CancellationToken ct = default);
    Task<ParentLinkResult<List<ParentLinkDto>>> GetMineAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// [Q11] Luồng phụ huynh đồng ý cho hệ thống xử lý dữ liệu của học sinh (Luật BVDLCN 91/2025 Điều 24, NĐ 356/2025 Điều 6):
/// mời qua email → phụ huynh đăng nhập và đồng ý (lưu consent: ai, lúc nào, phiên bản nội dung, IP) → có thể rút lại.
/// Lịch sử link và consent không bao giờ bị xóa.
/// </summary>
public class ParentLinkService(IParentLinkRepository links, IUserRepository users, IEmailSender emailSender) : IParentLinkService
{
    /// <summary>Tối đa số email phụ huynh mới học sinh được khai trong 24 giờ (chống dùng hệ thống gửi mail rác).</summary>
    public const int MaxNewLinksPerDay = 5;

    private const string InvalidInviteMessage = "Lời mời không hợp lệ hoặc đã được sử dụng.";
    private const string ExpiredInviteMessage = "Lời mời đã hết hạn. Vui lòng nhờ học sinh gửi lại lời mời từ trang Tài khoản.";
    private const string ConflictMessage = "Liên kết vừa được cập nhật bởi một thao tác khác. Vui lòng tải lại trang.";
    private const string NotFoundMessage = "Không tìm thấy liên kết.";

    public async Task<ParentLinkResult<ParentInviteInfo>> GetInviteAsync(string token, CancellationToken ct = default)
    {
        var (link, error) = await FindPendingByTokenAsync(token, ct);
        if (link == null)
            return ParentLinkResult<ParentInviteInfo>.Fail(error, error == ParentLinkError.Expired ? ExpiredInviteMessage : InvalidInviteMessage);

        var student = await users.GetUserById(link.StudentUserId, ct);
        if (student == null)
            return ParentLinkResult<ParentInviteInfo>.Fail(ParentLinkError.NotFound, InvalidInviteMessage);

        return ParentLinkResult<ParentInviteInfo>.Ok(new ParentInviteInfo(
            student.FullName, AgeOf(student), link.ParentEmail, link.TokenExpiresAt!.Value, ConsentPurposes.ChildDataPolicyVersion));
    }

    public async Task<ParentLinkResult<ParentLinkDto>> AcceptAsync(Guid parentUserId, string token, string? ipAddress, CancellationToken ct = default)
    {
        var (link, error) = await FindPendingByTokenAsync(token, ct);
        if (link == null)
            return Fail(error, error == ParentLinkError.Expired ? ExpiredInviteMessage : InvalidInviteMessage);

        var parent = await users.GetUserById(parentUserId, ct);
        if (parent == null || parent.Role != UserRoles.Parent)
            return Fail(ParentLinkError.Forbidden, "Chỉ tài khoản phụ huynh mới xác nhận được lời mời. Vui lòng đăng nhập hoặc đăng ký tài khoản vai trò Phụ huynh.");

        // Tránh người khác cầm được link: phải đăng nhập đúng email học sinh đã khai
        if (AuthService.NormalizeEmail(parent.Email) != link.ParentEmail)
            return Fail(ParentLinkError.Forbidden, "Tài khoản đang đăng nhập không khớp với email được mời. Vui lòng đăng nhập bằng đúng email nhận lời mời.");

        if (AgeOf(parent) is { } parentAge && parentAge < AgeGroups.AdultAge)
            return Fail(ParentLinkError.Forbidden, $"Người xác nhận phải từ {AgeGroups.AdultAge} tuổi.");

        var student = await users.GetUserById(link.StudentUserId, ct);
        if (student == null)
            return Fail(ParentLinkError.NotFound, InvalidInviteMessage);

        var now = DateTime.UtcNow;
        var consent = new Consent
        {
            UserId = student.Id,
            Purpose = ConsentPurposes.ChildDataProcessing,
            Granted = true,
            PolicyVersion = ConsentPurposes.ChildDataPolicyVersion,
            GrantedByUserId = parent.Id,
            GrantedIp = Truncate(ipAddress, 45),
        };
        link.Status = ParentLinkStatuses.Active;
        link.ParentUserId = parent.Id;
        link.AcceptedAt = now;
        link.ConsentId = consent.Id;
        link.TokenHash = null;
        link.TokenExpiresAt = null;

        try
        {
            await links.SaveAcceptAsync(link, consent, ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Fail(ParentLinkError.Conflict, ConflictMessage);
        }

        await emailSender.SendParentLinkChangedEmailAsync(student.Email, student.FullName, parent.FullName, linked: true, ct);

        return ParentLinkResult<ParentLinkDto>.Ok(ToDto(link, parent, student, canRevoke: true), "Đã xác nhận đồng ý và liên kết với tài khoản học sinh.");
    }

    public async Task<ParentLinkResult<ParentLinkDto>> ResendAsync(Guid studentUserId, CancellationToken ct = default)
    {
        var student = await users.GetUserById(studentUserId, ct);
        if (student == null || student.Role != UserRoles.Student)
            return Fail(ParentLinkError.Forbidden, "Chỉ học sinh mới gửi được lời mời phụ huynh.");

        var link = (await links.GetByStudentAsync(studentUserId, ct)).FirstOrDefault(l => l.Status == ParentLinkStatuses.Pending);
        if (link == null)
            return Fail(ParentLinkError.NotFound, "Không có lời mời nào đang chờ phụ huynh xác nhận.");

        var now = DateTime.UtcNow;
        if (link.LastInviteSentAt is { } last)
        {
            if (now - last < ParentInvite.ResendCooldown)
                return Fail(ParentLinkError.TooManyRequests, $"Vui lòng đợi {(int)ParentInvite.ResendCooldown.TotalSeconds} giây trước khi gửi lại lời mời.");
            if (now - last < TimeSpan.FromDays(1) && link.InviteSentCount >= ParentInvite.MaxSendsPerDay)
                return Fail(ParentLinkError.TooManyRequests, $"Đã gửi lời mời {ParentInvite.MaxSendsPerDay} lần trong 24 giờ. Vui lòng thử lại sau.");
        }

        var token = ParentInvite.Issue(link, now);
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

        // Mỗi lúc chỉ một lời mời đang chờ: hủy các lời mời cũ (token cũ hết hiệu lực)
        foreach (var pending in existing.Where(l => l.Status == ParentLinkStatuses.Pending))
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
        if (link == null || user == null ||
            (link.Status != ParentLinkStatuses.Pending && link.Status != ParentLinkStatuses.Active))
            return Fail(ParentLinkError.NotFound, NotFoundMessage);

        var isParentSide = link.ParentUserId == userId && link.Status == ParentLinkStatuses.Active;
        var isStudentSide = link.StudentUserId == userId;
        if (!isParentSide && !isStudentSide)
            return Fail(ParentLinkError.NotFound, NotFoundMessage);

        // Điều 24: với trẻ dưới 16, quyền rút đồng ý thuộc người đại diện (phụ huynh)
        if (isStudentSide && !CanStudentRevoke(link, user))
            return Fail(ParentLinkError.Forbidden, $"Học sinh dưới {AgeGroups.ChildMaxAgeExclusive} tuổi không thể tự rút sự đồng ý của phụ huynh. Phụ huynh có thể rút trong trang Tài khoản của mình.");

        var wasActive = link.Status == ParentLinkStatuses.Active;
        Consent? consent = null;
        if (link.ConsentId is { } consentId)
        {
            consent = await links.GetConsentAsync(consentId, ct);
            if (consent != null)
                consent.Granted = false;
        }
        MarkRevoked(link, userId, DateTime.UtcNow);

        try
        {
            await links.SaveRevokeAsync(link, consent, ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Fail(ParentLinkError.Conflict, ConflictMessage);
        }

        var student = isStudentSide ? user : await users.GetUserById(link.StudentUserId, ct);
        var parent = isParentSide ? user : link.ParentUserId is { } pid ? await users.GetUserById(pid, ct) : null;

        // Báo bên còn lại khi một liên kết đang hiệu lực bị hủy (hủy lời mời đang chờ thì không cần)
        if (wasActive && student != null && parent != null)
        {
            var other = isParentSide ? student : parent;
            await emailSender.SendParentLinkChangedEmailAsync(other.Email, student.FullName, parent.FullName, linked: false, ct);
        }

        return ParentLinkResult<ParentLinkDto>.Ok(
            ToDto(link, parent, student, canRevoke: false),
            wasActive ? "Đã hủy liên kết và rút lại sự đồng ý." : "Đã hủy lời mời.");
    }

    public async Task<ParentLinkResult<List<ParentLinkDto>>> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.GetUserById(userId, ct);
        if (user == null)
            return ParentLinkResult<List<ParentLinkDto>>.Fail(ParentLinkError.NotFound, NotFoundMessage);

        var result = new List<ParentLinkDto>();
        if (user.Role == UserRoles.Student)
        {
            var mine = (await links.GetByStudentAsync(userId, ct))
                .Where(l => l.Status == ParentLinkStatuses.Pending || l.Status == ParentLinkStatuses.Active);
            foreach (var link in mine)
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

    /// <summary>Token sai / đã dùng → NotFound; quá hạn → Expired. Lời mời chỉ hợp lệ khi link còn pending.</summary>
    private async Task<(ParentStudentLink? Link, ParentLinkError Error)> FindPendingByTokenAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
            return (null, ParentLinkError.NotFound);

        var link = await links.GetByTokenHashAsync(ParentInvite.HashToken(token), ct);
        if (link == null || link.Status != ParentLinkStatuses.Pending || link.TokenExpiresAt == null)
            return (null, ParentLinkError.NotFound);

        if (link.TokenExpiresAt <= DateTime.UtcNow)
            return (null, ParentLinkError.Expired);

        return (link, ParentLinkError.None);
    }

    /// <summary>Học sinh tự hủy lời mời đang chờ được; hủy liên kết đã đồng ý chỉ khi từ 16 tuổi (hoặc tài khoản cũ chưa có ngày sinh).</summary>
    private static bool CanStudentRevoke(ParentStudentLink link, User student) =>
        link.Status == ParentLinkStatuses.Pending ||
        AgeOf(student) is not { } age || age >= AgeGroups.ChildMaxAgeExclusive;

    private static void MarkRevoked(ParentStudentLink link, Guid byUserId, DateTime now)
    {
        link.Status = ParentLinkStatuses.Revoked;
        link.RevokedAt = now;
        link.RevokedByUserId = byUserId;
        link.TokenHash = null;
        link.TokenExpiresAt = null;
    }

    private static int? AgeOf(User user) =>
        user.DateOfBirth is { } dob ? AgePolicy.GetAge(dob, AgePolicy.TodayInVietnam(DateTime.UtcNow)) : null;

    private static string? Truncate(string? value, int max) =>
        value == null || value.Length <= max ? value : value[..max];

    private static ParentLinkDto ToDto(ParentStudentLink link, User? parent, User? student, bool canRevoke) =>
        new(link.Id, link.Status, link.ParentEmail, parent?.FullName, student?.FullName ?? string.Empty,
            student == null ? null : AgeOf(student), link.AcceptedAt, link.LastInviteSentAt, link.TokenExpiresAt, canRevoke);

    private static ParentLinkResult<ParentLinkDto> Fail(ParentLinkError error, string message) =>
        ParentLinkResult<ParentLinkDto>.Fail(error, message);
}
