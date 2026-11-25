using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

/// <summary>
/// [UC-75 1a/4b, UC-111 3a, UC-109 3b] Người đại diện của học sinh dưới 16 rút đồng ý kèm xóa tài khoản của con,
/// và hủy yêu cầu xóa bằng hai nút trong mail xác nhận (không cần đăng nhập).
/// </summary>
public interface IChildAccountDeletionService
{
    /// <summary>[UC-75 4b] Trang khôi phục đọc tên học sinh, ngày xóa và loại nút từ mã trong mail.</summary>
    Task<ParentLinkResult<ParentRestoreInfo>> GetRestoreInfoAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// [UC-75 4b] Hủy yêu cầu xóa. Mã dùng một lần; dùng một nút thì nút còn lại cũng hết tác dụng.
    /// Nút "đồng ý lại" cần tick là cha, mẹ hoặc người giám hộ và đọc đúng phiên bản đang hiệu lực, như khi đồng ý qua link mời.
    /// </summary>
    Task<ParentLinkResult<ParentRestoreInfo>> RestoreAsync(
        string token, bool isGuardian, string? seenPolicyVersion, string? ipAddress, string? userAgent = null, CancellationToken ct = default);

    /// <summary>[UC-111 3a] Rút đồng ý kèm xóa tài khoản con dưới 16 bằng link rút đồng ý trong email (không cần đăng nhập).</summary>
    Task<ParentLinkResult<ParentRevokeInfo>> RevokeAndDeleteByTokenAsync(
        string revokeToken, string? ipAddress, string? userAgent = null, CancellationToken ct = default);

    /// <summary>[UC-75 1a] Phụ huynh đã đăng nhập rút đồng ý kèm xóa tài khoản con dưới 16 trong "Con của tôi".</summary>
    Task<ParentLinkResult<ParentRevokeInfo>> RevokeAndDeleteAsync(
        Guid parentUserId, Guid linkId, string? ipAddress, string? userAgent = null, CancellationToken ct = default);
}

public class ChildAccountDeletionService(
    IUserRepository users,
    IParentLinkService parentLinks,
    IUnitOfWork unitOfWork,
    IAuditLogRepository auditLogs,
    IEmailSender emailSender,
    IParentLinkRepository links,
    IParentRequestedDeletion deletion) : IChildAccountDeletionService
{
    /// <summary>Action ghi audit khi người đại diện hủy yêu cầu xóa tài khoản của con.</summary>
    public const string RestoreAuditAction = "user.restore_by_parent";

    private static readonly string[] RestorePurposes = [OtpPurposes.ParentRestoreReconsent, OtpPurposes.ParentRestoreOnly];

    private const string InvalidRestoreMessage = "Liên kết khôi phục không hợp lệ, đã được sử dụng hoặc đã hết hạn.";
    private const string GuardianRequiredMessage = "Vui lòng xác nhận bạn là cha, mẹ hoặc người giám hộ hợp pháp của học sinh.";
    private const string PolicyChangedMessage = "Nội dung đồng ý vừa được cập nhật. Vui lòng tải lại trang và đọc lại trước khi đồng ý.";
    private const string LinkNotFoundMessage = "Liên kết không hợp lệ, đã được sử dụng hoặc không còn hiệu lực.";
    private const string DeleteAgeMessage = "Con đã đủ 16 tuổi và tự quản lý tài khoản. Bạn chỉ rút được đồng ý, không xóa được tài khoản của con.";
    private const string DeleteConflictMessage = "Thao tác vừa bị thay đổi bởi một yêu cầu khác. Vui lòng tải lại trang và thử lại.";
    private const string ReconsentAgeMessage = "Học sinh đã đủ 16 tuổi và tự quyết định việc đồng ý xử lý dữ liệu của mình. Bạn chỉ hủy được yêu cầu xóa bằng nút \"Chỉ hủy xóa\" trong email.";

    public async Task<ParentLinkResult<ParentRestoreInfo>> GetRestoreInfoAsync(string token, CancellationToken ct = default)
    {
        var (otp, student) = await FindRestorableAsync(token, ct);
        if (otp == null || student == null)
            return Fail(ParentLinkError.NotFound, InvalidRestoreMessage);

        return ParentLinkResult<ParentRestoreInfo>.Ok(await ToInfoAsync(otp, student, ct));
    }

    public async Task<ParentLinkResult<ParentRestoreInfo>> RestoreAsync(
        string token, bool isGuardian, string? seenPolicyVersion, string? ipAddress, string? userAgent = null, CancellationToken ct = default)
    {
        var (otp, student) = await FindRestorableAsync(token, ct);
        if (otp == null || student == null)
            return Fail(ParentLinkError.NotFound, InvalidRestoreMessage);

        var info = await ToInfoAsync(otp, student, ct);
        var reconsent = info.Mode == ParentRestoreModes.Reconsent;
        if (reconsent)
        {
            if (!info.CanReconsent)
                return Fail(ParentLinkError.Forbidden, ReconsentAgeMessage);
            if (!isGuardian)
                return Fail(ParentLinkError.Validation, GuardianRequiredMessage);
            // Bằng chứng đồng ý phải khớp văn bản người đó thực sự đã đọc (UC-103)
            if (seenPolicyVersion is not null && !string.Equals(seenPolicyVersion, info.PolicyVersion, StringComparison.Ordinal))
                return Fail(ParentLinkError.Validation, PolicyChangedMessage);
        }

        // Chỉ tạo lại liên kết với đúng người đã yêu cầu xóa (email ghi lúc xóa)
        var parentEmail = student.DeletionRequestedByEmail!;
        var parent = await users.GetUserByEmail(AuthService.NormalizeEmail(parentEmail), ct);
        var actorId = parent is { Role: UserRoles.Parent } ? parent.Id : (Guid?)null;
        var deletedAt = student.DeletedAt;
        var now = DateTime.UtcNow;
        Func<CancellationToken, Task>? afterCommit = null;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async txCt =>
            {
                // Dùng mã trước tiên: hai lần bấm cùng lúc thì chỉ một lần ghi được (Version), lần kia rollback
                otp.UsedAt = now;
                await users.UpdateOtpAsync(otp, txCt);
                foreach (var purpose in RestorePurposes)
                    await users.InvalidateOtpsAsync(student.Id, purpose, txCt);

                student.Status = UserStatuses.Active;
                student.DeletedAt = null;
                student.DeletionRequestedByEmail = null;
                student.FailedLoginAttempts = 0;
                student.LockoutEnd = null;
                await users.UpdateUserAsync(student, txCt);

                if (reconsent)
                    afterCommit = await parentLinks.GrantConsentAfterRestoreAsync(student, parentEmail, ipAddress, txCt);

                // Không ghi email hay tên
                await auditLogs.AddAsync(new AuditLog
                {
                    ActorUserId = actorId,
                    Action = RestoreAuditAction,
                    EntityType = "user",
                    EntityId = student.Id.ToString(),
                    DetailJson = JsonSerializer.Serialize(new { userId = student.Id, mode = info.Mode, deletedAt, restoredAt = now }),
                    IpAddress = Truncate(ipAddress, 45),
                    UserAgent = Truncate(userAgent, 512),
                }, txCt);
            }, ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Nút còn lại vừa được bấm, học sinh đủ 16 vừa tự khôi phục, hoặc job xóa hẳn vừa chạy
            return Fail(ParentLinkError.NotFound, InvalidRestoreMessage);
        }

        if (afterCommit != null)
            await afterCommit(ct);
        else
            await emailSender.SendChildAccountRestoredByParentEmailAsync(student.Email, student.FullName, reconsented: false, ct);

        return ParentLinkResult<ParentRestoreInfo>.Ok(info, reconsent
            ? $"Đã hủy yêu cầu xóa và ghi nhận bạn đồng ý lại. Tài khoản của {student.FullName} đã được khôi phục."
            : $"Đã hủy yêu cầu xóa. Tài khoản của {student.FullName} đã được khôi phục; con tạm chưa dùng được các chức năng cần hồ sơ cho tới khi có người đại diện đồng ý.");
    }

    public async Task<ParentLinkResult<ParentRevokeInfo>> RevokeAndDeleteByTokenAsync(
        string revokeToken, string? ipAddress, string? userAgent = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(revokeToken) || revokeToken.Length > 128)
            return RevokeFail(ParentLinkError.NotFound, LinkNotFoundMessage);

        var link = await links.GetByRevokeTokenHashAsync(ParentInvite.HashToken(revokeToken), ct);
        return await DeleteForLinkAsync(link, ipAddress, userAgent, ct);
    }

    public async Task<ParentLinkResult<ParentRevokeInfo>> RevokeAndDeleteAsync(
        Guid parentUserId, Guid linkId, string? ipAddress, string? userAgent = null, CancellationToken ct = default)
    {
        var link = await links.GetByIdAsync(linkId, ct);
        // Chỉ phụ huynh của đúng liên kết đang hiệu lực; liên kết của người khác báo như không tồn tại
        if (link?.ParentUserId != parentUserId)
            return RevokeFail(ParentLinkError.NotFound, LinkNotFoundMessage);

        return await DeleteForLinkAsync(link, ipAddress, userAgent, ct);
    }

    /// <summary>
    /// Kiểm liên kết đang hiệu lực và học sinh dưới 16 (chưa biết ngày sinh coi như dưới 16), rồi xóa mềm theo yêu cầu người đại diện.
    /// Việc rút đồng ý của mọi liên kết, thu hồi yêu cầu liên hệ, ghi audit và gửi mail nằm trong lõi xóa (UC-80).
    /// </summary>
    private async Task<ParentLinkResult<ParentRevokeInfo>> DeleteForLinkAsync(
        ParentStudentLink? link, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        if (link is not { Status: ParentLinkStatuses.Active })
            return RevokeFail(ParentLinkError.NotFound, LinkNotFoundMessage);

        var student = await users.GetUserById(link.StudentUserId, ct);
        if (student is not { Role: UserRoles.Student } || student.Status == UserStatuses.Deleted)
            return RevokeFail(ParentLinkError.NotFound, LinkNotFoundMessage);
        if (student.DateOfBirth is { } dob &&
            AgePolicy.GetAge(dob, AgePolicy.TodayInVietnam(DateTime.UtcNow)) >= AgeGroups.ChildMaxAgeExclusive)
            return RevokeFail(ParentLinkError.Forbidden, DeleteAgeMessage);

        AuthResult result;
        try
        {
            result = await deletion.DeleteChildAccountByParentAsync(
                student.Id, new ParentDeletionRequester(link.ParentEmail, link.ParentUserId), ipAddress, userAgent, ct);
        }
        catch (ConcurrencyConflictException)
        {
            return RevokeFail(ParentLinkError.Conflict, DeleteConflictMessage);
        }

        if (result.Error != AuthError.None)
            return RevokeFail(ParentLinkError.Validation, result.Message ?? LinkNotFoundMessage);

        return ParentLinkResult<ParentRevokeInfo>.Ok(new ParentRevokeInfo(student.FullName, link.AcceptedAt), result.Message);
    }

    private static ParentLinkResult<ParentRevokeInfo> RevokeFail(ParentLinkError error, string message) =>
        ParentLinkResult<ParentRevokeInfo>.Fail(error, message);

    /// <summary>
    /// Mã sai / đã dùng / hết hạn (hết hạn đúng ngày xóa hẳn) hoặc tài khoản không còn chờ xóa theo yêu cầu người đại diện
    /// → không tìm thấy, một câu chung không lộ thông tin học sinh.
    /// </summary>
    private async Task<(OtpToken? Otp, User? Student)> FindRestorableAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
            return (null, null);

        var otp = await users.FindUnusedOtpByHashAsync(AuthService.HashOtp(token), RestorePurposes, ct);
        if (otp == null || otp.ExpiresAt <= DateTime.UtcNow)
            return (null, null);

        var student = await users.GetUserById(otp.UserId, ct);
        if (student is not { Status: UserStatuses.Deleted, DeletedAt: not null, DeletionRequestedByEmail: not null })
            return (null, null);

        return (otp, student);
    }

    private async Task<ParentRestoreInfo> ToInfoAsync(OtpToken otp, User student, CancellationToken ct)
    {
        var mode = otp.Purpose == OtpPurposes.ParentRestoreReconsent ? ParentRestoreModes.Reconsent : ParentRestoreModes.RestoreOnly;
        var canReconsent = student.DateOfBirth is not { } dob ||
            AgePolicy.GetAge(dob, AgePolicy.TodayInVietnam(DateTime.UtcNow)) < AgeGroups.ChildMaxAgeExclusive;
        // Mã hết hạn đúng ngày xóa vĩnh viễn
        return new ParentRestoreInfo(student.FullName, otp.ExpiresAt, mode, canReconsent, await parentLinks.GetConsentPolicyVersionAsync(ct));
    }

    private static ParentLinkResult<ParentRestoreInfo> Fail(ParentLinkError error, string message) =>
        ParentLinkResult<ParentRestoreInfo>.Fail(error, message);

    private static string? Truncate(string? value, int max) =>
        value == null || value.Length <= max ? value : value[..max];
}
