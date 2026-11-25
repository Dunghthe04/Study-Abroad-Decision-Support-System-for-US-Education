using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.StudyCenters;
using System.Text.Json;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Auth
{
    public interface IAuthService
    {
        /// <param name="ipAddress">IP người đăng ký, ghi kèm bằng chứng đồng ý điều khoản (UC-104).</param>
        Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default, string? ipAddress = null);
        /// <summary>Mở phiên đăng nhập cho tài khoản đã được xác thực ở luồng khác (dùng cho trung tâm sau khi xác thực email).</summary>
        Task<AuthResult> StartSessionAsync(User user, CancellationToken ct = default);
        Task<AuthResult> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default);
        Task<AuthResult> ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default);
        Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);
        Task<AuthResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
        Task<AuthResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
        Task<AuthResult> ChangePasswordAsync(Guid userId, string currentRawToken, ChangePasswordRequest request, CancellationToken ct = default);
        Task<AuthResult> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);
        Task<bool> LogoutAsync(string rawToken, CancellationToken ct = default);
        Task<UserDto?> GetMeAsync(Guid userId, CancellationToken ct = default);
        Task<List<UserDto>> GetAllUsersAsync(CancellationToken ct = default);
        Task<AuthResult> CreateModeratorAccountAsync(CreateModeratorAccountRequest request, CancellationToken ct = default);
        /// <summary>[Q11 / UC-71 Phụ trợ] Tạo tài khoản người dùng cho Trung tâm tư vấn (role center) — active ngay.</summary>
        Task<AuthResult> CreateCenterAccountAsync(CreateCenterAccountRequest request, CancellationToken ct = default);
        /// <summary>[UC-60] Xem danh sách tài khoản — tìm, lọc, phân trang.</summary>
        Task<PagedResult<UserDto>> GetAdminUserAccountListAsync(AdminUserAccountListQuery query, CancellationToken ct = default);
        /// <summary>[UC-61] Admin khóa tài khoản (student/parent/moderator/center) kèm lý do và audit.</summary>
        Task<AuthResult> LockUserAccountAsync(Guid targetUserId, LockUserAccountRequest request, Guid actorAdminId, CancellationToken ct = default);
        /// <summary>[UC-62] Admin mở khóa tài khoản (status locked hoặc temp_locked) kèm lý do và audit.</summary>
        Task<AuthResult> UnlockUserAccountAsync(Guid targetUserId, UnlockUserAccountRequest request, Guid actorAdminId, CancellationToken ct = default);
        /// <summary>[UC-113] Admin sửa ngày sinh Học sinh / Phụ huynh theo yêu cầu hỗ trợ, bắt buộc lý do, ghi audit giá trị cũ và mới.</summary>
        Task<AuthResult> UpdateUserBirthDateAsync(Guid targetUserId, UpdateUserBirthDateRequest request, Guid actorAdminId, CancellationToken ct = default);
        /// <summary>[UC-80] Yêu cầu xóa tài khoản: xóa mềm, khôi phục được trong thời hạn, hết hạn thì job xóa hẳn.</summary>
        Task<AuthResult> AnonymizeUserAccountAsync(Guid userId, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);
        /// <summary>[UC-80] Khôi phục tài khoản đang chờ xóa bằng mã khôi phục, đăng nhập luôn khi thành công.</summary>
        Task<AuthResult> RestoreAccountAsync(RestoreAccountRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);
    }

    /// <summary>
    /// [UC-75 1a / UC-111 3a] Xóa tài khoản học sinh dưới 16 theo yêu cầu người đại diện. Tách khỏi IAuthService để
    /// dịch vụ liên kết phụ huynh gọi được mà không phụ thuộc vòng; AuthService cài đặt.
    /// </summary>
    public interface IParentRequestedDeletion
    {
        Task<AuthResult> DeleteChildAccountByParentAsync(Guid studentUserId, ParentDeletionRequester requester, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);
    }

    public class AuthService(
        IUserRepository users,
        IPasswordHasher hasher,
        ITokenService tokens,
        IEmailSender emailSender,
        IParentLinkRepository parentLinks,
        IAuditLogRepository auditLogs,
        IParentLinkService parentLinkService,
        IUnitOfWork unitOfWork,
        StudyAbroad.Application.Leads.ILeadWithdrawalRepository leadWithdrawals,
        IStudyCenterRepository? studyCenters = null,
        AccountDeletionOptions? accountDeletion = null,
        StudyAbroad.Application.Billing.IBillingAccountHooks? billingHooks = null,
        // [UC-104] Đồng ý Điều khoản và Chính sách bảo mật lúc đăng ký; null trong test cũ thì bỏ qua
        StudyAbroad.Application.Policies.IRegistrationConsent? registrationConsent = null,
        // [SC-10 / UC-113] Gửi thông báo khi Admin cập nhật ngày sinh
        StudyAbroad.Application.Notifications.INotificationService? notifications = null) : IAuthService, IParentRequestedDeletion
    {
        private readonly AccountDeletionOptions _accountDeletion = accountDeletion ?? new AccountDeletionOptions();

        /// <summary>[UC-80] Hiệu lực của mã khôi phục tài khoản cấp khi đăng nhập: 10 phút.</summary>
        public const int RestoreTokenExpiryMinutes = 10;

        public const string RestoreTokenInvalidMessage = "Phiên khôi phục không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại để khôi phục tài khoản.";

        public const int MinPasswordLength = 8;
        public const int MaxPasswordLength = 100;
        public const int MaxFullNameLength = 100;
        public const int MaxEmailLength = 100;
        public const int MaxRawPhoneLength = 20;
        public const int MaxLockReasonLength = 500;

        private static readonly HashSet<string> AdminLockableRoles = new(StringComparer.Ordinal)
        {
            UserRoles.Student,
            UserRoles.Parent,
            UserRoles.Moderator,
            UserRoles.Center,
        };
        public const int MaxAge = 100;

        /// <summary>Thời hạn hiệu lực của mã OTP: 5 phút.</summary>
        public const int OtpExpiryMinutes = 5;

        /// <summary>Số lần nhập sai OTP tối đa trước khi mã OTP bị vô hiệu hóa: 5 lần.</summary>
        public const int MaxOtpAttempts = 5;

        /// <summary>Số lần gõ sai mật khẩu tối đa trước khi tài khoản bị tạm khóa: 5 lần.</summary>
        public const int MaxFailedLoginAttempts = 5;

        /// <summary>Thời gian tạm khóa tài khoản sau 5 lần nhập sai: 15 phút (tự động mở khóa sau 15 phút).</summary>
        public const int TempLockoutMinutes = 15;

        /// <summary>Khoảng thời gian tối thiểu giữa 2 lần yêu cầu mã OTP liên tiếp: 60 giây.</summary>
        public const int MinSecondsBetweenOtpRequests = 60;

        /// <summary>Giới hạn số lần yêu cầu mã OTP tối đa trong 1 giờ: 5 lần.</summary>
        public const int MaxOtpRequestsPerHour = 5;

        public static string GenerateOtpCode() =>
            RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");

        public static string HashOtp(string code)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()));
            return Convert.ToHexString(bytes);
        }

        public static bool TryValidateAndNormalizeVietnamesePhone(string? rawPhone, out string normalizedPhone)
        {
            normalizedPhone = string.Empty;
            if (string.IsNullOrWhiteSpace(rawPhone))
                return false;

            var cleaned = Regex.Replace(rawPhone.Trim(), @"[\s\.\-\(\)]", "");

            if (cleaned.StartsWith("+84"))
                cleaned = "0" + cleaned[3..];
            else if (cleaned.StartsWith("84") && cleaned.Length == 11)
                cleaned = "0" + cleaned[2..];

            if (Regex.IsMatch(cleaned, @"^0(3|5|7|8|9)\d{8}$"))
            {
                normalizedPhone = cleaned;
                return true;
            }

            return false;
        }

        // [Bảo mật] Thông báo chung cho mọi lần nhập OTP thất bại (không tồn tại tài khoản, mã sai, hết hạn, đã dùng,
        // đã vô hiệu hóa): không để lộ email có tài khoản hay không, cũng không báo còn bao nhiêu lần thử.
        public const string OtpFailedMessage = "Mã OTP không đúng hoặc đã hết hạn. Vui lòng kiểm tra lại hoặc yêu cầu gửi mã mới.";

        public const string InvalidCredentialsMessage = "Sai tài khoản hoặc mật khẩu.";
        public const string PhoneInUseMessage = "Số điện thoại đã thuộc một tài khoản khác.";

        // Hash giả để so mật khẩu khi email không tồn tại: thời gian phản hồi như khi email có thật
        private static string? _dummyPasswordHash;

        public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default, string? ipAddress = null)
        {
            var email = NormalizeEmail(request.Email);
            var fullName = request.FullName?.Trim() ?? string.Empty;
            var rawPhone = request.Phone?.Trim();
            string? normalizedPhone = null;

            if (string.IsNullOrEmpty(email) || email.Length > MaxEmailLength || !IsValidEmail(email))
                return AuthResult.Fail(AuthError.Validation, $"Email không hợp lệ hoặc vượt quá {MaxEmailLength} ký tự.");

            var passwordError = ValidatePassword(request.Password, "Mật khẩu");
            if (passwordError != null)
                return AuthResult.Fail(AuthError.Validation, passwordError);

            if (fullName.Length == 0 || fullName.Length > MaxFullNameLength)
                return AuthResult.Fail(AuthError.Validation, $"Họ tên không được để trống và tối đa {MaxFullNameLength} ký tự.");

            if (!string.IsNullOrEmpty(rawPhone))
            {
                if (rawPhone.Length > MaxRawPhoneLength)
                    return AuthResult.Fail(AuthError.Validation, $"Số điện thoại không được vượt quá {MaxRawPhoneLength} ký tự.");

                if (!TryValidateAndNormalizeVietnamesePhone(rawPhone, out var cleanPhone))
                    return AuthResult.Fail(AuthError.Validation, "Số điện thoại không đúng định dạng Việt Nam (ví dụ: 0912345678 hoặc +84912345678).");

                normalizedPhone = cleanPhone;
            }

            if (!UserRoles.SelfRegister.Contains(request.Role))
                return AuthResult.Fail(AuthError.Validation, $"Vai trò '{request.Role}' không hợp lệ.");

            // [Q11] Nhóm tuổi theo luật VN: dưới 16 cần phụ huynh đồng ý trước khi xử lý dữ liệu (Luật BVDLCN Điều 24),
            // 16–17 tự dùng nhưng có thể mời phụ huynh liên kết, phụ huynh phải đủ 18 tuổi
            var (ageError, parentEmail) = ValidateAge(request, email);
            if (ageError != null)
                return AuthResult.Fail(AuthError.Validation, ageError);

            // [UC-104] Chưa đồng ý đúng bản Điều khoản và Chính sách bảo mật đang hiệu lực thì không tạo tài khoản.
            // Lỗi này không phụ thuộc email đã tồn tại hay chưa nên không làm lộ tài khoản.
            if (registrationConsent != null && await registrationConsent.ValidateAsync(request.AcceptedPolicies, ct) is { } consentError)
                return AuthResult.Fail(AuthError.Validation, consentError);

            // Luôn hash mật khẩu trước khi kiểm trùng: thời gian xử lý như nhau dù email đã có tài khoản hay chưa
            var passwordHash = hasher.Hash(request.Password);
            var successMessage = $"Đăng ký thành công. Mã xác thực OTP đã được gửi đến email {email} (hiệu lực {OtpExpiryMinutes} phút).";

            // [Bảo mật UC-01 E1] Trùng email: trả cùng thông báo như đăng ký mới (chống dò tài khoản),
            // chỉ chủ hộp thư mới biết qua mail
            var existing = await users.GetUserByEmail(email, ct);
            if (existing != null)
            {
                await emailSender.SendAccountAlreadyExistsEmailAsync(existing.Email, existing.FullName, ct);
                return AuthResult.Ok(successMessage);
            }

            // [UC-01 E6] Số điện thoại đã thuộc tài khoản khác: báo lỗi tại ô Số điện thoại (đặc tả chỉ giấu với trùng email, E1)
            if (!string.IsNullOrEmpty(normalizedPhone) && await users.PhoneExistAsync(normalizedPhone, ct))
                return AuthResult.Fail(AuthError.Validation, PhoneInUseMessage);

            // [USAS-12] Khởi tạo tài khoản với trạng thái unverified (chờ kích hoạt email qua OTP)
            var user = new User
            {
                Email = email,
                PasswordHash = passwordHash,
                FullName = fullName,
                Role = request.Role,
                Status = UserStatuses.Unverified,
                DateOfBirth = request.DateOfBirth,
                Phone = normalizedPhone,
                FailedLoginAttempts = 0,
                LockoutEnd = null
            };
            // Sinh mã OTP 6 số (hiệu lực 5 phút)
            var otpCode = GenerateOtpCode();
            var otp = new OtpToken
            {
                UserId = user.Id,
                Purpose = OtpPurposes.VerifyEmail,
                CodeHash = HashOtp(otpCode),
                ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
                Attempts = 0
            };

            // Tài khoản, email phụ huynh, bằng chứng đồng ý điều khoản và OTP cùng một giao dịch: lỗi giữa chừng thì không
            // còn tài khoản thiếu bằng chứng. Mail chỉ gửi sau khi lưu xong.
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await users.AddAsync(user, token);

                // Ghi lại email phụ huynh; mail mời chỉ gửi sau khi học sinh xác thực email (tránh tài khoản rác gửi mail cho người lạ)
                if (parentEmail != null)
                    await parentLinks.AddAsync(new ParentStudentLink { StudentUserId = user.Id, ParentEmail = parentEmail }, token);

                if (registrationConsent != null)
                    await registrationConsent.RecordAsync(user.Id, request.AcceptedPolicies, ipAddress, token);

                await users.SaveOtpAsync(otp, token);
            }, ct);

            await emailSender.SendVerificationEmailAsync(user.Email, user.FullName, otpCode, OtpExpiryMinutes, ct);

            return AuthResult.Ok(successMessage);
        }

        public async Task<AuthResult> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);
            var user = await users.GetUserByEmail(email, ct);
            if (user == null || user.Status != UserStatuses.Unverified)
                return AuthResult.Fail(AuthError.OtpInvalid, OtpFailedMessage);

            if (!await TryConsumeOtpAsync(user.Id, OtpPurposes.VerifyEmail, request.OtpCode, ct))
                return AuthResult.Fail(AuthError.OtpInvalid, OtpFailedMessage);

            user.Status = user.Role == UserRoles.Center ? UserStatuses.Pending : UserStatuses.Active;
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            await users.UpdateUserAsync(user, ct);

            if (user.Role == UserRoles.Student)
                await SendFirstParentInvitesAsync(user, ct);
            else if (user.Role == UserRoles.Parent)
                await parentLinkService.LinkParentAccountAsync(user, ct);

            // Tự động đăng nhập
            return AuthResult.Ok(await CreateSessionAsync(user, null, null, ct));
        }

        public async Task<AuthResult> ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default)
        {
            if (request.Purpose != OtpPurposes.VerifyEmail &&
                request.Purpose != OtpPurposes.ResetPassword)
            {
                return AuthResult.Fail(AuthError.Validation, "Mục đích gửi OTP không hợp lệ.");
            }

            // [Bảo mật] Anti-enumeration: luôn trả cùng một thông báo. Email không tồn tại, tài khoản bị Admin khóa,
            // đã kích hoạt, chưa hết 60 giây chờ hoặc đã gửi đủ 5 lần/giờ thì lặng lẽ không gửi.
            var genericMessage = $"Nếu email hợp lệ, mã xác thực mới đã được gửi (hiệu lực {OtpExpiryMinutes} phút). Mỗi lần gửi cách nhau ít nhất {MinSecondsBetweenOtpRequests} giây.";

            var user = await users.GetUserByEmail(NormalizeEmail(request.Email), ct);
            if (user == null || user.Status == UserStatuses.Locked || user.Status == UserStatuses.Managed)
                return AuthResult.Ok(genericMessage);

            if (request.Purpose == OtpPurposes.VerifyEmail && user.Status != UserStatuses.Unverified)
                return AuthResult.Ok(genericMessage);
            if (request.Purpose == OtpPurposes.ResetPassword && !CanResetPassword(user))
                return AuthResult.Ok(genericMessage);

            if (!await CanSendOtpAsync(user.Id, request.Purpose, ct))
                return AuthResult.Ok(genericMessage);

            var otpCode = await IssueOtpAsync(user.Id, request.Purpose, ct);

            if (request.Purpose == OtpPurposes.VerifyEmail)
                await emailSender.SendVerificationEmailAsync(user.Email, user.FullName, otpCode, OtpExpiryMinutes, ct);
            else
                await emailSender.SendPasswordResetEmailAsync(user.Email, user.FullName, otpCode, OtpExpiryMinutes, ct);

            return AuthResult.Ok(genericMessage);
        }

        public async Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(request.Email) || request.Email.Length > MaxEmailLength ||
                string.IsNullOrEmpty(request.Password) || request.Password.Length > MaxPasswordLength)
            {
                return AuthResult.Fail(AuthError.InvalidCredentials, InvalidCredentialsMessage);
            }

            var user = await users.GetUserByEmail(NormalizeEmail(request.Email), ct);
            // [1.5c] Con managed không có mật khẩu, không tự đăng nhập: coi như email không tồn tại
            // (hash rỗng làm BCrypt ném lỗi, và đếm sai 5 lần sẽ đổi trạng thái managed thành temp_locked).
            if (user == null || user.Status == UserStatuses.Managed)
            {
                // Vẫn so mật khẩu với một hash giả để thời gian phản hồi như khi email tồn tại
                _dummyPasswordHash ??= hasher.Hash(Guid.NewGuid().ToString());
                hasher.VerifyPassword(_dummyPasswordHash, request.Password);
                return AuthResult.Fail(AuthError.InvalidCredentials, InvalidCredentialsMessage);
            }

            // 1. [UC-03 E2] Tạm khóa do sai 5 lần: hết 15 phút thì tự mở (không cần chờ job nền); Quản trị viên vẫn mở sớm được.
            //    [UC-03 quy tắc] Đang khóa thì chỉ báo khóa khi mật khẩu đúng, mật khẩu sai nhận lỗi chung. Dù đúng mật khẩu
            //    vẫn không tạo phiên nên không dò được mật khẩu qua tài khoản đang khóa; lần thử lúc khóa không đếm thêm.
            if (user.Status == UserStatuses.TempLocked)
            {
                if (!await TryReleaseExpiredTempLockAsync(user, ct))
                {
                    return hasher.VerifyPassword(user.PasswordHash, request.Password)
                        ? AuthResult.Fail(AuthError.TempLocked, TempLockedMessage(MaxFailedLoginAttempts, "mật khẩu"))
                        : AuthResult.Fail(AuthError.InvalidCredentials, InvalidCredentialsMessage);
                }
            }

            // 2. Bị Admin khóa / chưa kích hoạt email: chỉ báo trạng thái khi mật khẩu đúng (chống dò tài khoản).
            //    Không đếm lần sai cho 2 trạng thái này vì tạm khóa rồi mở lại sẽ đưa tài khoản về active.
            if (user.Status == UserStatuses.Locked || user.Status == UserStatuses.Unverified)
            {
                if (!hasher.VerifyPassword(user.PasswordHash, request.Password))
                    return AuthResult.Fail(AuthError.InvalidCredentials, InvalidCredentialsMessage);

                return user.Status == UserStatuses.Locked
                    ? AuthResult.Fail(AuthError.Locked, "Tài khoản của bạn đã bị khóa bởi Quản trị viên. Vui lòng liên hệ ban quản trị để được hỗ trợ.")
                    : AuthResult.Fail(AuthError.Unverified, "Tài khoản chưa được kích hoạt email. Vui lòng kiểm tra hộp thư để nhập mã OTP xác thực.");
            }

            // 2b. [UC-80] Đang chờ xóa: chỉ báo khi mật khẩu đúng (chống dò tài khoản), không tạo phiên.
            //     Cấp mã khôi phục ngắn hạn để người dùng chọn khôi phục. Quá thời hạn thì coi như tài khoản không còn.
            if (user.Status == UserStatuses.Deleted)
            {
                if (!hasher.VerifyPassword(user.PasswordHash, request.Password) || !IsWithinRetention(user, DateTime.UtcNow))
                    return AuthResult.Fail(AuthError.InvalidCredentials, InvalidCredentialsMessage);

                var deletionDate = user.DeletedAt!.Value + _accountDeletion.RetentionPeriod;

                // [UC-75 4b] Người đại diện yêu cầu xóa và học sinh còn dưới 16: không cấp mã, chỉ người đại diện khôi phục được.
                //            Đủ 16 trong thời gian chờ thì tự khôi phục như bình thường (4c).
                if (IsParentRequestedChildDeletion(user))
                    return AuthResult.PendingDeletionFail(
                        $"Người đại diện của bạn đã yêu cầu xóa tài khoản này. Dữ liệu sẽ bị xóa vĩnh viễn vào ngày {deletionDate.AddHours(7):dd/MM/yyyy}. " +
                        "Vì bạn chưa đủ 16 tuổi nên không tự khôi phục được; hãy nhờ người đại diện bấm nút khôi phục trong email hệ thống đã gửi cho họ.",
                        new PendingDeletionInfo(deletionDate, null, RequestedByParent: true));

                var restoreToken = await IssueRestoreTokenAsync(user.Id, ct);
                return AuthResult.PendingDeletionFail(
                    "Tài khoản đang chờ xóa. Bạn có thể khôi phục tài khoản trước khi dữ liệu bị xóa vĩnh viễn.",
                    new PendingDeletionInfo(deletionDate, restoreToken));
            }

            // 3. [Bảo mật] Ghi nhận lần thử TRƯỚC khi so mật khẩu. Nhiều request song song cùng đọc một giá trị bộ đếm
            //    thì chỉ một request ghi được (Version/xmin), các request còn lại bị từ chối mà không được so mật khẩu.
            user.FailedLoginAttempts++;
            if (!await TryUpdateUserAsync(user, ct))
                return AuthResult.Fail(AuthError.InvalidCredentials, InvalidCredentialsMessage);

            if (!hasher.VerifyPassword(user.PasswordHash, request.Password))
            {
                if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
                {
                    // Đạt 5 lần sai -> Khóa tạm 15 phút và gửi email thông báo (hết giờ tự mở)
                    user.Status = UserStatuses.TempLocked;
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(TempLockoutMinutes);
                    if (await TryUpdateUserAsync(user, ct))
                        await emailSender.SendAccountTempLockedNotificationAsync(user.Email, user.FullName, TempLockoutMinutes, ct);
                }

                // [UC-03 quy tắc] Mật khẩu sai luôn nhận lỗi chung, kể cả lần sai thứ 5 vừa khóa tài khoản
                return AuthResult.Fail(AuthError.InvalidCredentials, InvalidCredentialsMessage);
            }

            // Đăng nhập thành công -> Reset bộ đếm số lần sai và thời gian khóa
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            if (!await TryUpdateUserAsync(user, ct))
                return AuthResult.Fail(AuthError.InvalidCredentials, InvalidCredentialsMessage);

            return AuthResult.Ok(await CreateSessionAsync(user, ipAddress, userAgent, ct));
        }

        /// <summary>Mở phiên đăng nhập cho một tài khoản đã được xác thực ở nơi khác (ví dụ trung tâm vừa xác thực email, UC-91).</summary>
        public async Task<AuthResult> StartSessionAsync(User user, CancellationToken ct = default) =>
            AuthResult.Ok(await CreateSessionAsync(user, null, null, ct));

        /// <summary>Sinh access token (JWT 15 phút) và refresh token, lưu phiên (7 ngày, Moderator/Admin 8 giờ) và trả về thông tin phiên.</summary>
        private async Task<AuthResponse> CreateSessionAsync(User user, string? ipAddress, string? userAgent, CancellationToken ct)
        {
            var (token, expiresAt) = tokens.GenerateToken(user);
            var refreshToken = RefreshTokens.Generate();

            var session = new UserSession
            {
                UserId = user.Id,
                TokenHash = tokens.HashToken(token),
                RefreshTokenHash = tokens.HashToken(refreshToken),
                ExpiresAt = DateTime.UtcNow.Add(RefreshTokens.LifetimeFor(user.Role)),
                IsRevoked = false,
                IpAddress = ipAddress,
                UserAgent = userAgent
            };
            await users.CreateSessionAsync(session, ct);

            return new AuthResponse(token, expiresAt, await ToDtoAsync(user, ct), refreshToken, session.ExpiresAt);
        }

        /// <summary>[UC-75 4b/4c] Tài khoản bị xóa theo yêu cầu người đại diện và học sinh vẫn dưới 16 (chưa biết ngày sinh coi như dưới 16).</summary>
        private static bool IsParentRequestedChildDeletion(User user) =>
            user.DeletionRequestedByEmail != null && !IsAtLeast16(user);

        private static bool IsAtLeast16(User user) =>
            user.DateOfBirth is { } dob &&
            AgePolicy.GetAge(dob, AgePolicy.TodayInVietnam(DateTime.UtcNow)) >= AgeGroups.ChildMaxAgeExclusive;

        /// <summary>[UC-80] Tài khoản đã xóa mềm còn trong thời hạn khôi phục.</summary>
        private bool IsWithinRetention(User user, DateTime now) =>
            user.DeletedAt is { } deletedAt && now < deletedAt + _accountDeletion.RetentionPeriod;

        /// <summary>
        /// [UC-80] Cấp mã khôi phục: chuỗi ngẫu nhiên dài (không phải 6 số), lưu dạng băm trong bảng OTP,
        /// dùng một lần, hết hạn sau RestoreTokenExpiryMinutes. Mã cũ cùng mục đích bị vô hiệu hóa.
        /// </summary>
        private async Task<string> IssueRestoreTokenAsync(Guid userId, CancellationToken ct)
        {
            await users.InvalidateOtpsAsync(userId, OtpPurposes.RestoreAccount, ct);

            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            await users.SaveOtpAsync(new OtpToken
            {
                UserId = userId,
                Purpose = OtpPurposes.RestoreAccount,
                CodeHash = HashOtp(token),
                ExpiresAt = DateTime.UtcNow.AddMinutes(RestoreTokenExpiryMinutes),
                Attempts = 0
            }, ct);
            return token;
        }

        /// <summary>
        /// [UC-80] Khôi phục tài khoản đang chờ xóa: kiểm mã khôi phục (một lần), đưa về active,
        /// xóa DeletedAt, ghi audit trong một transaction rồi tạo phiên đăng nhập mới.
        /// Liên kết phụ huynh đã hủy và yêu cầu liên hệ đã thu hồi KHÔNG được nối lại.
        /// </summary>
        public async Task<AuthResult> RestoreAccountAsync(
            RestoreAccountRequest request,
            string? ipAddress = null,
            string? userAgent = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(request.Email) || request.Email.Length > MaxEmailLength ||
                string.IsNullOrWhiteSpace(request.RestoreToken) || request.RestoreToken.Length > 100)
                return AuthResult.Fail(AuthError.OtpInvalid, RestoreTokenInvalidMessage);

            var user = await users.GetUserByEmail(NormalizeEmail(request.Email), ct);
            var now = DateTime.UtcNow;
            if (user == null || user.Status != UserStatuses.Deleted || !IsWithinRetention(user, now) ||
                IsParentRequestedChildDeletion(user))
                return AuthResult.Fail(AuthError.OtpInvalid, RestoreTokenInvalidMessage);

            if (!await TryConsumeOtpAsync(user.Id, OtpPurposes.RestoreAccount, request.RestoreToken, ct))
                return AuthResult.Fail(AuthError.OtpInvalid, RestoreTokenInvalidMessage);

            var deletedAt = user.DeletedAt;
            try
            {
                await unitOfWork.ExecuteInTransactionAsync(async txCt =>
                {
                    var byParent = user.DeletionRequestedByEmail != null;
                    user.Status = UserStatuses.Active;
                    user.DeletedAt = null;
                    user.DeletionRequestedByEmail = null;
                    user.FailedLoginAttempts = 0;
                    user.LockoutEnd = null;
                    await users.UpdateUserAsync(user, txCt);

                    // [UC-75 4c] Học sinh đủ 16 tự khôi phục: hai nút trong mail người đại diện hết tác dụng
                    if (byParent)
                    {
                        await users.InvalidateOtpsAsync(user.Id, OtpPurposes.ParentRestoreReconsent, txCt);
                        await users.InvalidateOtpsAsync(user.Id, OtpPurposes.ParentRestoreOnly, txCt);
                    }

                    await auditLogs.AddAsync(new AuditLog
                    {
                        ActorUserId = user.Id,
                        Action = "user.restore_account",
                        EntityType = "user",
                        EntityId = user.Id.ToString(),
                        DetailJson = JsonSerializer.Serialize(new { userId = user.Id, deletedAt, restoredAt = now }),
                        IpAddress = ipAddress,
                        UserAgent = userAgent
                    }, txCt);
                }, ct);
            }
            catch (ConcurrencyConflictException)
            {
                // Job xóa hẳn hoặc request khác vừa đổi tài khoản này
                return AuthResult.Fail(AuthError.OtpInvalid, RestoreTokenInvalidMessage);
            }

            return AuthResult.Ok(await CreateSessionAsync(user, ipAddress, userAgent, ct));
        }

        public async Task<AuthResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);
            var user = await users.GetUserByEmail(email, ct);

            // Anti-enumeration: Luôn trả về thông báo thành công dù email có tồn tại hay không.
            // Mail đi qua hàng đợi nền nên thời gian phản hồi cũng như nhau.
            if (user != null && CanResetPassword(user) &&
                await CanSendOtpAsync(user.Id, OtpPurposes.ResetPassword, ct))
            {
                var otpCode = await IssueOtpAsync(user.Id, OtpPurposes.ResetPassword, ct);
                await emailSender.SendPasswordResetEmailAsync(user.Email, user.FullName, otpCode, OtpExpiryMinutes, ct);
            }

            return AuthResult.Ok($"Nếu email tồn tại trong hệ thống, mã xác thực đặt lại mật khẩu đã được gửi đến hộp thư (hiệu lực {OtpExpiryMinutes} phút).");
        }

        /// <summary>
        /// [UC-05] Chỉ tài khoản đã kích hoạt, không bị khóa mới được đặt lại mật khẩu. Tài khoản bị khóa,
        /// chưa kích hoạt (AUTH-126), con do phụ huynh quản lý hay đã xóa hẳn thì không nhận mã, vẫn nhận thông báo chung.
        /// Tài khoản đang chờ xóa còn trong 30 ngày vẫn đặt lại được (AUTH-223), kể cả học sinh dưới 16 do người đại diện
        /// yêu cầu xóa; đặt lại không khôi phục tài khoản, lần đăng nhập sau đi qua luồng khôi phục (UC-03, 3c).
        /// </summary>
        private bool CanResetPassword(User user) =>
            user.Status == UserStatuses.Deleted
                ? IsWithinRetention(user, DateTime.UtcNow)
                : user.Status != UserStatuses.Locked && user.Status != UserStatuses.TempLocked &&
                  user.Status != UserStatuses.Managed && user.Status != UserStatuses.Unverified;

        public async Task<AuthResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
        {
            var passwordError = ValidatePassword(request.NewPassword, "Mật khẩu mới");
            if (passwordError != null)
                return AuthResult.Fail(AuthError.Validation, passwordError);

            var email = NormalizeEmail(request.Email);
            var user = await users.GetUserByEmail(email, ct);
            if (user == null || !CanResetPassword(user))
                return AuthResult.Fail(AuthError.OtpInvalid, OtpFailedMessage);

            if (!await TryConsumeOtpAsync(user.Id, OtpPurposes.ResetPassword, request.OtpCode, ct))
                return AuthResult.Fail(AuthError.OtpInvalid, OtpFailedMessage);

            user.PasswordHash = hasher.Hash(request.NewPassword);
            user.FailedLoginAttempts = 0;

            await users.UpdateUserAsync(user, ct);

            // [Bảo mật]: Thu hồi toàn bộ phiên đăng nhập cũ trên mọi thiết bị và báo cho chủ tài khoản
            await users.RevokeAllSessionsAsync(user.Id, ct);
            await emailSender.SendPasswordChangedNotificationAsync(user.Email, user.FullName, ct);

            return AuthResult.Ok("Đặt lại mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.");
        }

        public const string WrongCurrentPasswordMessage = "Mật khẩu hiện tại không đúng.";

        /// <summary>
        /// [UC-06] Đổi mật khẩu khi đã đăng nhập. Sai mật khẩu hiện tại dùng chung bộ đếm với đăng nhập
        /// (phiên bị đánh cắp không thành nơi dò mật khẩu): đủ 5 lần thì tạm khóa 15 phút và thu hồi mọi phiên.
        /// Thành công thì giữ phiên hiện tại, đăng xuất các thiết bị khác và gửi mail báo cho chủ tài khoản.
        /// </summary>
        public async Task<AuthResult> ChangePasswordAsync(Guid userId, string currentRawToken, ChangePasswordRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > MaxPasswordLength)
                return AuthResult.Fail(AuthError.InvalidCredentials, WrongCurrentPasswordMessage);

            var passwordError = ValidatePassword(request.NewPassword, "Mật khẩu mới");
            if (passwordError != null)
                return AuthResult.Fail(AuthError.Validation, passwordError);

            var user = await users.GetUserById(userId, ct);
            // [UC-06 E1] Khóa tạm đã hết 15 phút thì mở lại trước khi xét
            if (user?.Status == UserStatuses.TempLocked)
                await TryReleaseExpiredTempLockAsync(user, ct);
            if (user == null || user.Status == UserStatuses.Locked || user.Status == UserStatuses.TempLocked ||
                user.Status == UserStatuses.Managed)
                return AuthResult.Fail(AuthError.InvalidCredentials, WrongCurrentPasswordMessage);

            // Ghi nhận lần thử TRƯỚC khi so mật khẩu, như đăng nhập
            user.FailedLoginAttempts++;
            if (!await TryUpdateUserAsync(user, ct))
                return AuthResult.Fail(AuthError.InvalidCredentials, WrongCurrentPasswordMessage);

            if (!hasher.VerifyPassword(user.PasswordHash, request.CurrentPassword))
            {
                if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
                {
                    user.Status = UserStatuses.TempLocked;
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(TempLockoutMinutes);
                    if (await TryUpdateUserAsync(user, ct))
                    {
                        await users.RevokeAllSessionsAsync(user.Id, ct);
                        await emailSender.SendAccountTempLockedNotificationAsync(user.Email, user.FullName, TempLockoutMinutes, ct);
                    }

                    return AuthResult.Fail(AuthError.TempLocked, TempLockedMessage(MaxFailedLoginAttempts, "mật khẩu hiện tại"));
                }

                return AuthResult.Fail(AuthError.InvalidCredentials, WrongCurrentPasswordMessage);
            }

            user.FailedLoginAttempts = 0;
            if (request.NewPassword == request.CurrentPassword)
            {
                await TryUpdateUserAsync(user, ct);
                return AuthResult.Fail(AuthError.Validation, "Mật khẩu mới phải khác mật khẩu hiện tại.");
            }

            user.PasswordHash = hasher.Hash(request.NewPassword);
            if (!await TryUpdateUserAsync(user, ct))
                return AuthResult.Fail(AuthError.InvalidCredentials, WrongCurrentPasswordMessage);

            await users.RevokeOtherSessionsAsync(user.Id, tokens.HashToken(currentRawToken), ct);
            await emailSender.SendPasswordChangedNotificationAsync(user.Email, user.FullName, ct);

            return AuthResult.Ok("Đổi mật khẩu thành công. Các thiết bị khác đã được đăng xuất.");
        }

        /// <summary>
        /// Sửa họ tên và số điện thoại của chính mình. Email đăng nhập không nằm trong request nên không đổi được.
        /// Số điện thoại kiểm tra như lúc đăng ký và không được trùng tài khoản khác.
        /// </summary>
        public async Task<AuthResult> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
        {
            var fullName = request.FullName?.Trim() ?? string.Empty;
            if (fullName.Length == 0 || fullName.Length > MaxFullNameLength)
                return AuthResult.Fail(AuthError.Validation, $"Họ tên không được để trống và tối đa {MaxFullNameLength} ký tự.");

            var rawPhone = request.Phone?.Trim();
            string? normalizedPhone = null;
            if (!string.IsNullOrEmpty(rawPhone))
            {
                if (rawPhone.Length > MaxRawPhoneLength)
                    return AuthResult.Fail(AuthError.Validation, $"Số điện thoại không được vượt quá {MaxRawPhoneLength} ký tự.");

                if (!TryValidateAndNormalizeVietnamesePhone(rawPhone, out var cleanPhone))
                    return AuthResult.Fail(AuthError.Validation, "Số điện thoại không đúng định dạng Việt Nam (ví dụ: 0912345678 hoặc +84912345678).");

                normalizedPhone = cleanPhone;
            }

            var user = await users.GetUserById(userId, ct);
            if (user == null)
                return AuthResult.Fail(AuthError.Validation, "Không tìm thấy tài khoản.");

            // [UC-111] Trung tâm đổi số điện thoại ở hồ sơ trung tâm (UC-91, có xác thực), không đổi tại trang tài khoản
            if (user.Role == UserRoles.Center && normalizedPhone != user.Phone)
                return AuthResult.Fail(AuthError.Validation, "Trung tâm đổi số điện thoại ở mục Hồ sơ trung tâm (cần xác thực).");

            if (normalizedPhone != null && normalizedPhone != user.Phone && await users.PhoneExistAsync(normalizedPhone, ct))
                return AuthResult.Fail(AuthError.Validation, "Số điện thoại này đã được dùng cho tài khoản khác.");

            user.FullName = fullName;
            user.Phone = normalizedPhone;
            if (!await TryUpdateUserAsync(user, ct))
                return AuthResult.Fail(AuthError.Validation, "Thông tin vừa được thay đổi ở nơi khác. Vui lòng tải lại trang và thử lại.");

            return AuthResult.Ok(await ToDtoAsync(user, ct), "Đã lưu thông tin cá nhân.");
        }

        /// <summary>
        /// Ngày sinh bắt buộc, không ở tương lai, tối đa 100 tuổi. Phụ huynh phải từ 18 tuổi. Học sinh dưới 16 phải có email
        /// phụ huynh; 16–17 nhập thì mời liên kết; từ 18 bỏ qua. Trả email phụ huynh đã chuẩn hóa (null nếu không mời).
        /// </summary>
        private static (string? Error, string? ParentEmail) ValidateAge(RegisterRequest request, string studentEmail)
        {
            if (request.DateOfBirth is not { } dob)
                return ("Vui lòng nhập ngày sinh.", null);

            var today = AgePolicy.TodayInVietnam(DateTime.UtcNow);
            if (dob > today)
                return ("Ngày sinh không được ở tương lai.", null);

            var age = AgePolicy.GetAge(dob, today);
            if (age > MaxAge)
                return ("Ngày sinh không hợp lệ.", null);

            if (request.Role == UserRoles.Parent)
                return age < AgeGroups.AdultAge
                    ? ($"Tài khoản phụ huynh phải từ {AgeGroups.AdultAge} tuổi.", null)
                    : (null, null);

            if (request.Role != UserRoles.Student || age >= AgeGroups.AdultAge)
                return (null, null);

            if (age < AgeGroups.StudentMinAge)
                return ($"Học sinh phải từ đủ {AgeGroups.StudentMinAge} tuổi (chuẩn bị từ lớp 8) để đăng ký tài khoản. Với học sinh nhỏ hơn, vui lòng nhờ Phụ huynh đăng ký tài khoản để quản lý hồ sơ cho bạn.", null);

            var parentEmail = NormalizeEmail(request.ParentEmail ?? string.Empty);
            if (parentEmail.Length == 0)
                return age < AgeGroups.ChildMaxAgeExclusive
                    ? ($"Học sinh dưới {AgeGroups.ChildMaxAgeExclusive} tuổi cần nhập email phụ huynh để phụ huynh đồng ý.", null)
                    : (null, null);

            if (!IsValidEmail(parentEmail))
                return ($"Email phụ huynh không hợp lệ hoặc vượt quá {MaxEmailLength} ký tự.", null);

            if (parentEmail == studentEmail)
                return ("Email phụ huynh phải khác email của học sinh.", null);

            return (null, parentEmail);
        }

        /// <summary>Gửi mail mời cho các email phụ huynh khai lúc đăng ký (link chưa gửi lần nào).</summary>
        private async Task SendFirstParentInvitesAsync(User student, CancellationToken ct)
        {
            var links = await parentLinks.GetByStudentAsync(student.Id, ct);
            // [UC-109] Học sinh 16–17 khai email phụ huynh: thư mời liên kết, không phải thư xin đồng ý
            var isConsentRequest = student.DateOfBirth is not { } dob ||
                AgePolicy.GetAge(dob, AgePolicy.TodayInVietnam(DateTime.UtcNow)) < AgeGroups.ChildMaxAgeExclusive;
            foreach (var link in links.Where(l => l.Status == ParentLinkStatuses.Pending && l.InviteSentCount == 0))
            {
                var token = ParentInvite.Issue(link, DateTime.UtcNow);
                try
                {
                    await parentLinks.UpdateAsync(link, ct);
                }
                catch (ConcurrencyConflictException)
                {
                    continue;
                }
                await emailSender.SendParentInviteEmailAsync(link.ParentEmail, student.FullName, token, ParentInvite.ExpiryDays, isConsentRequest, ct);
            }
        }

        /// <summary>Kiểm tra chính sách mật khẩu (8–100 ký tự), dùng chung cho đăng ký, đặt lại và đổi mật khẩu.</summary>
        /// <summary>[UC-03 E2] Thông báo khóa tạm: nói rõ khóa 15 phút và tự mở, khác thông báo bị Quản trị viên khóa.</summary>
        private static string TempLockedMessage(int attempts, string what) =>
            $"Tài khoản tạm khóa {TempLockoutMinutes} phút do nhập sai {what} {attempts} lần liên tiếp. Vui lòng thử lại sau {TempLockoutMinutes} phút.";

        /// <summary>
        /// [UC-03 E2] Khóa tạm đã quá hạn thì mở lại ngay (trả true), không phải đợi job nền chạy mỗi phút.
        /// Tài khoản khóa từ trước khi có hạn khóa (LockoutEnd rỗng) vẫn chờ Quản trị viên mở.
        /// </summary>
        private async Task<bool> TryReleaseExpiredTempLockAsync(User user, CancellationToken ct)
        {
            if (user.LockoutEnd is not { } end || end > DateTime.UtcNow)
                return false;

            user.Status = user.Role == UserRoles.Center ? UserStatuses.Pending : UserStatuses.Active;
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            return await TryUpdateUserAsync(user, ct);
        }

        public static string? ValidatePassword(string? password, string label)
        {
            if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
                return $"{label} phải có tối thiểu {MinPasswordLength} ký tự.";

            if (password.Length > MaxPasswordLength)
                return $"{label} không được vượt quá {MaxPasswordLength} ký tự.";

            // [UC-01] Không được toàn khoảng trắng, không thuộc danh sách mật khẩu phổ biến
            if (string.IsNullOrWhiteSpace(password))
                return $"{label} không được chỉ gồm khoảng trắng.";

            if (CommonPasswords.Contains(password))
                return $"{label} quá phổ biến, dễ đoán. Vui lòng chọn mật khẩu khác.";

            return null;
        }

        /// <summary>
        /// Đối chiếu mã OTP mới nhất của user. Tăng bộ đếm lần thử và lưu TRƯỚC khi so mã: nhiều request song song
        /// thì chỉ một request ghi được (Version/xmin), các request còn lại thất bại mà không được so mã.
        /// Đúng mã thì đánh dấu đã dùng (dùng 1 lần); sai đủ 5 lần thì vô hiệu hóa mã.
        /// </summary>
        private Task<bool> TryConsumeOtpAsync(Guid userId, string purpose, string? code, CancellationToken ct) =>
            OtpRules.TryConsumeAsync(users, userId, purpose, code, ct);

        /// <summary>Kiểm tra cooldown 60 giây giữa 2 lần gửi và giới hạn 5 lần/giờ.</summary>
        private Task<bool> CanSendOtpAsync(Guid userId, string purpose, CancellationToken ct) =>
            OtpRules.CanSendAsync(users, userId, purpose, ct);

        /// <summary>Vô hiệu hóa các mã cũ cùng mục đích, tạo mã mới và trả về mã gốc để gửi mail.</summary>
        private async Task<string> IssueOtpAsync(Guid userId, string purpose, CancellationToken ct)
        {
            await users.InvalidateOtpsAsync(userId, purpose, ct);

            var otpCode = GenerateOtpCode();
            await users.SaveOtpAsync(new OtpToken
            {
                UserId = userId,
                Purpose = purpose,
                CodeHash = HashOtp(otpCode),
                ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
                Attempts = 0
            }, ct);
            return otpCode;
        }

        private async Task<bool> TryUpdateUserAsync(User user, CancellationToken ct)
        {
            try
            {
                await users.UpdateUserAsync(user, ct);
                return true;
            }
            catch (ConcurrencyConflictException)
            {
                return false;
            }
        }

        private Task<bool> TryUpdateOtpAsync(OtpToken otp, CancellationToken ct) =>
            OtpRules.TryUpdateAsync(users, otp, ct);

        internal static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        internal static bool IsValidEmail(string email) =>
            email.Length <= MaxEmailLength && MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;

        internal static UserDto ToDto(User user) =>
            new(user.Id, user.Email, user.FullName, user.Role, user.Status, user.Phone);

        // [Q11] Kèm thông tin chặn theo tuổi cho người dùng đang đăng nhập
        private async Task<UserDto> ToDtoAsync(User user, CancellationToken ct)
        {
            var access = await AgeAccess.EvaluateAsync(user, parentLinks, DateTime.UtcNow, ct);
            string? lockReason = null;
            if (user.Status == UserStatuses.Locked)
            {
                lockReason = await auditLogs.GetLatestLockReasonAsync(user.Id, ct) ?? "Tài khoản bị khóa bởi quản trị viên do vi phạm quy định sử dụng.";
            }
            else if (user.Status == UserStatuses.TempLocked)
            {
                lockReason = "Nhập sai mật khẩu 5 lần liên tiếp. Tài khoản bị tạm khóa bảo vệ trong 15 phút.";
            }

            return ToDto(user) with
            {
                AgeGroup = access.AgeGroup,
                ParentConsentStatus = access.ParentConsentStatus,
                BlockedFeatures = access.BlockedFeatures,
                LockReason = lockReason,
                LockoutEnd = user.LockoutEnd,
                DateOfBirth = user.DateOfBirth,
                // [UC-113] Tuổi tròn theo giờ VN, để Admin thấy tuổi mới ngay sau khi sửa ngày sinh
                Age = user.DateOfBirth is { } dob ? AgePolicy.GetAge(dob, AgePolicy.TodayInVietnam(DateTime.UtcNow)) : null,
            };
        }

        public async Task<bool> LogoutAsync(string rawToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawToken)) return false;
            var tokenHash = tokens.HashToken(rawToken);
            await users.RevokeSessionAsync(tokenHash, ct);
            return true;
        }

        public async Task<UserDto?> GetMeAsync(Guid userId, CancellationToken ct = default)
        {
            var user = await users.GetUserById(userId, ct);
            return user != null ? await ToDtoAsync(user, ct) : null;
        }

        public async Task<List<UserDto>> GetAllUsersAsync(CancellationToken ct = default)
        {
            var list = await users.GetAllUsersAsync(ct);
            return list.Select(ToDto).ToList();
        }

        private const int AdminUserListMaxPageSize = 100;

        /// <summary>[UC-60] Danh sách tài khoản cho admin (một trang, có tìm/lọc).</summary>
        public async Task<PagedResult<UserDto>> GetAdminUserAccountListAsync(AdminUserAccountListQuery query, CancellationToken ct = default)
        {
            var normalized = query with
            {
                Page = Math.Max(1, query.Page),
                PageSize = Math.Clamp(query.PageSize, 1, AdminUserListMaxPageSize),
                Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
                Role = string.IsNullOrWhiteSpace(query.Role) ? null : query.Role.Trim(),
                Status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim(),
            };

            var page = await users.GetAdminUserAccountListAsync(normalized, ct);
            var dtos = new List<UserDto>(page.Items.Count);
            foreach (var item in page.Items)
            {
                string? lockReason = null;
                if (item.Status == UserStatuses.Locked)
                {
                    lockReason = await auditLogs.GetLatestLockReasonAsync(item.Id, ct) ?? "Tài khoản bị khóa bởi quản trị viên do vi phạm quy định sử dụng.";
                }
                else if (item.Status == UserStatuses.TempLocked)
                {
                    lockReason = "Nhập sai mật khẩu 5 lần liên tiếp. Tài khoản bị tạm khóa bảo vệ trong 15 phút.";
                }
                dtos.Add(ToDto(item) with { LockReason = lockReason, LockoutEnd = item.LockoutEnd });
            }

            return new PagedResult<UserDto>(
                dtos,
                page.Page,
                page.PageSize,
                page.TotalCount);
        }

        /// <summary>[UC-63] Administrator tạo tài khoản moderator — trạng thái active, không qua OTP đăng ký công khai.</summary>
        public async Task<AuthResult> CreateModeratorAccountAsync(CreateModeratorAccountRequest request, CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);
            var fullName = request.FullName?.Trim() ?? string.Empty;
            var rawPhone = request.Phone?.Trim();
            string? normalizedPhone = null;

            if (string.IsNullOrEmpty(email) || email.Length > MaxEmailLength || !IsValidEmail(email))
                return AuthResult.Fail(AuthError.Validation, $"Email không hợp lệ hoặc vượt quá {MaxEmailLength} ký tự.");

            var passwordError = ValidatePassword(request.Password, "Mật khẩu");
            if (passwordError != null)
                return AuthResult.Fail(AuthError.Validation, passwordError);

            if (fullName.Length == 0 || fullName.Length > MaxFullNameLength)
                return AuthResult.Fail(AuthError.Validation, $"Họ tên không được để trống và tối đa {MaxFullNameLength} ký tự.");

            if (!string.IsNullOrEmpty(rawPhone))
            {
                if (rawPhone.Length > MaxRawPhoneLength)
                    return AuthResult.Fail(AuthError.Validation, $"Số điện thoại không được vượt quá {MaxRawPhoneLength} ký tự.");

                if (!TryValidateAndNormalizeVietnamesePhone(rawPhone, out var cleanPhone))
                    return AuthResult.Fail(AuthError.Validation, "Số điện thoại không đúng định dạng Việt Nam (ví dụ: 0912345678 hoặc +84912345678).");

                normalizedPhone = cleanPhone;
            }

            if (await users.EmailExistAsync(email, ct))
                return AuthResult.Fail(AuthError.Validation, "Email đã được sử dụng.");

            if (!string.IsNullOrEmpty(normalizedPhone) && await users.PhoneExistAsync(normalizedPhone, ct))
                return AuthResult.Fail(AuthError.Validation, "Số điện thoại đã được sử dụng.");

            var user = new User
            {
                Email = email,
                PasswordHash = hasher.Hash(request.Password),
                FullName = fullName,
                Role = UserRoles.Moderator,
                Status = UserStatuses.Active,
                Phone = normalizedPhone,
                FailedLoginAttempts = 0,
                LockoutEnd = null,
            };
            await users.AddAsync(user, ct);

            return AuthResult.Ok(ToDto(user), "Đã tạo tài khoản moderator.");
        }

        /// <summary>[Q11 / UC-71 Phụ trợ] Tạo tài khoản người dùng cho Trung tâm tư vấn (role center) — trạng thái active, không qua OTP đăng ký công khai.</summary>
        public async Task<AuthResult> CreateCenterAccountAsync(CreateCenterAccountRequest request, CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);
            var fullName = string.IsNullOrWhiteSpace(request.FullName)
                ? (email.Contains('@') ? email.Split('@')[0] : "Người dùng trung tâm")
                : request.FullName.Trim();
            var rawPhone = request.Phone?.Trim();
            string? normalizedPhone = null;

            if (string.IsNullOrEmpty(email) || email.Length > MaxEmailLength || !IsValidEmail(email))
                return AuthResult.Fail(AuthError.Validation, $"Email không hợp lệ hoặc vượt quá {MaxEmailLength} ký tự.");

            var passwordError = ValidatePassword(request.Password, "Mật khẩu");
            if (passwordError != null)
                return AuthResult.Fail(AuthError.Validation, passwordError);

            if (fullName.Length > MaxFullNameLength)
                return AuthResult.Fail(AuthError.Validation, $"Họ và tên không được vượt quá {MaxFullNameLength} ký tự.");

            if (!string.IsNullOrEmpty(rawPhone))
            {
                if (rawPhone.Length > MaxRawPhoneLength)
                    return AuthResult.Fail(AuthError.Validation, $"Số điện thoại không được vượt quá {MaxRawPhoneLength} ký tự.");

                if (!TryValidateAndNormalizeVietnamesePhone(rawPhone, out var cleanPhone))
                    return AuthResult.Fail(AuthError.Validation, "Số điện thoại không đúng định dạng Việt Nam (ví dụ: 0912345678 hoặc +84912345678).");

                normalizedPhone = cleanPhone;
            }

            if (await users.EmailExistAsync(email, ct))
                return AuthResult.Fail(AuthError.Validation, "Email đã được sử dụng.");

            if (!string.IsNullOrEmpty(normalizedPhone) && await users.PhoneExistAsync(normalizedPhone, ct))
                return AuthResult.Fail(AuthError.Validation, "Số điện thoại đã được sử dụng.");

            var user = new User
            {
                Email = email,
                PasswordHash = hasher.Hash(request.Password),
                FullName = fullName,
                Role = UserRoles.Center,
                Status = UserStatuses.Active,
                Phone = normalizedPhone,
                FailedLoginAttempts = 0,
                LockoutEnd = null,
            };
            await users.AddAsync(user, ct);

            return AuthResult.Ok(ToDto(user), "Đã tạo tài khoản trung tâm tư vấn.");
        }

        /// <summary>[UC-61] Khóa vĩnh viễn tài khoản (không áp dụng admin).</summary>
        public async Task<AuthResult> LockUserAccountAsync(
            Guid targetUserId,
            LockUserAccountRequest request,
            Guid actorAdminId,
            CancellationToken ct = default)
        {
            var reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length == 0)
                return AuthResult.Fail(AuthError.Validation, "Lý do khóa không được để trống.");
            if (reason.Length > MaxLockReasonLength)
                return AuthResult.Fail(AuthError.Validation, $"Lý do khóa tối đa {MaxLockReasonLength} ký tự.");

            var user = await users.GetUserById(targetUserId, ct);
            // [1.5c] Con managed không phải tài khoản thật: admin thao tác theo id cũng coi như không tồn tại
            if (user is null || user.Status == UserStatuses.Managed)
                return AuthResult.Fail(AuthError.Validation, "Không tìm thấy tài khoản.");

            if (user.Id == actorAdminId)
                return AuthResult.Fail(AuthError.Validation, "Không thể khóa chính tài khoản đang đăng nhập.");

            if (user.Role == UserRoles.Admin)
                return AuthResult.Fail(AuthError.Validation, "Không thể khóa tài khoản quản trị viên.");

            var actor = await users.GetUserById(actorAdminId, ct);
            if (actor is not null && actor.Role == UserRoles.Moderator)
            {
                if (user.Role == UserRoles.Moderator)
                    return AuthResult.Fail(AuthError.Validation, "Kiểm duyệt viên không có quyền khóa tài khoản Kiểm duyệt viên khác.");

                if (user.Role != UserRoles.Center && user.Role != UserRoles.Student && user.Role != UserRoles.Parent)
                    return AuthResult.Fail(AuthError.Validation, "Kiểm duyệt viên chỉ có quyền khóa tài khoản người dùng và trung tâm.");
            }

            if (!AdminLockableRoles.Contains(user.Role))
                return AuthResult.Fail(AuthError.Validation, "Vai trò này không được khóa qua chức năng này.");

            if (user.Status == UserStatuses.Locked)
                return AuthResult.Fail(AuthError.Validation, "Tài khoản đã ở trạng thái khóa.");

            var previousStatus = user.Status;
            user.Status = UserStatuses.Locked;
            user.LockoutEnd = null;
            user.FailedLoginAttempts = 0;
            await users.UpdateUserAsync(user, ct);
            await users.RevokeAllSessionsAsync(user.Id, ct);

            var detailJson = JsonSerializer.Serialize(new
            {
                reason,
                previousStatus,
                targetEmail = user.Email,
            });
            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = actorAdminId,
                Action = "user.lock",
                EntityType = "user",
                EntityId = user.Id.ToString(),
                DetailJson = detailJson,
            }, ct);

            // [USAS-362] Gửi email thông báo tài khoản bị khóa kèm lý do
            _ = emailSender.SendAccountLockedEmailAsync(user.Email, user.FullName, reason, ct);

            return AuthResult.Ok(ToDto(user) with { LockReason = reason }, "Đã khóa tài khoản.");
        }

        /// <summary>
        /// [UC-113] Admin sửa ngày sinh theo yêu cầu hỗ trợ (ngày sinh bị khóa sau đăng ký, UC-01). Chỉ Học sinh / Phụ huynh.
        /// Tuổi và các quy tắc mốc 16 / 18 (đồng ý của phụ huynh, chức năng bị chặn theo tuổi) được tính lại từ ngày sinh mới
        /// mỗi lần gọi (AgeAccess), không có dữ liệu suy ra nào lưu sẵn. Phụ huynh phải còn từ 18 tuổi.
        /// </summary>
        public async Task<AuthResult> UpdateUserBirthDateAsync(
            Guid targetUserId,
            UpdateUserBirthDateRequest request,
            Guid actorAdminId,
            CancellationToken ct = default)
        {
            var reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length == 0)
                return AuthResult.Fail(AuthError.Validation, "Lý do sửa ngày sinh không được để trống.");
            if (reason.Length > MaxLockReasonLength)
                return AuthResult.Fail(AuthError.Validation, $"Lý do sửa ngày sinh tối đa {MaxLockReasonLength} ký tự.");

            if (request.BirthDate is not { } newDob)
                return AuthResult.Fail(AuthError.Validation, "Vui lòng nhập ngày sinh mới.");

            var user = await users.GetUserById(targetUserId, ct);
            // [1.5c] Con managed không phải tài khoản thật: admin thao tác theo id cũng coi như không tồn tại
            if (user is null || user.Status == UserStatuses.Managed)
                return AuthResult.Fail(AuthError.Validation, "Không tìm thấy tài khoản.");

            if (user.Role != UserRoles.Student && user.Role != UserRoles.Parent)
                return AuthResult.Fail(AuthError.Validation, "Chỉ sửa được ngày sinh của tài khoản Học sinh hoặc Phụ huynh.");

            var today = AgePolicy.TodayInVietnam(DateTime.UtcNow);
            if (newDob > today)
                return AuthResult.Fail(AuthError.Validation, "Ngày sinh không được ở tương lai.");

            var newAge = AgePolicy.GetAge(newDob, today);
            if (newAge > MaxAge)
                return AuthResult.Fail(AuthError.Validation, "Ngày sinh không hợp lệ.");

            if (user.Role == UserRoles.Parent && newAge < AgeGroups.AdultAge)
                return AuthResult.Fail(AuthError.Validation, $"Tài khoản phụ huynh phải từ {AgeGroups.AdultAge} tuổi.");

            if (user.DateOfBirth == newDob)
                return AuthResult.Fail(AuthError.Validation, "Ngày sinh mới trùng với ngày sinh hiện tại.");

            var oldDob = user.DateOfBirth;
            int? oldAge = oldDob is { } o ? AgePolicy.GetAge(o, today) : null;
            user.DateOfBirth = newDob;
            await users.UpdateUserAsync(user, ct);

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = actorAdminId,
                Action = "user.birth_date_update",
                EntityType = "user",
                EntityId = user.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new
                {
                    reason,
                    targetEmail = user.Email,
                    oldBirthDate = oldDob?.ToString("yyyy-MM-dd"),
                    newBirthDate = newDob.ToString("yyyy-MM-dd"),
                    oldAge,
                    newAge,
                }),
            }, ct);

            // [SC-10 / UC-113] Gửi thông báo trong hệ thống cho người dùng khi Administrator cập nhật ngày sinh
            if (notifications is not null)
            {
                try
                {
                    await notifications.NotifyAsync(
                        user.Id,
                        "user_birth_date_updated",
                        "Ngày sinh của bạn đã được cập nhật",
                        $"Administrator đã cập nhật ngày sinh của bạn thành {newDob:dd/MM/yyyy}. Lý do: {reason}",
                        "/account",
                        ct);
                }
                catch
                {
                    // Nuốt lỗi thông báo để không chặn luồng chính
                }
            }

            return AuthResult.Ok(await ToDtoAsync(user, ct), $"Đã sửa ngày sinh. Tuổi hiện tại: {newAge}.");
        }

        /// <summary>[UC-62] Mở khóa tài khoản kèm lý do (chỉ tài khoản đang bị locked hoặc temp_locked).</summary>
        public async Task<AuthResult> UnlockUserAccountAsync(
            Guid targetUserId,
            UnlockUserAccountRequest request,
            Guid actorAdminId,
            CancellationToken ct = default)
        {
            var reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length == 0)
                return AuthResult.Fail(AuthError.Validation, "Lý do mở khóa không được để trống.");
            if (reason.Length > MaxLockReasonLength)
                return AuthResult.Fail(AuthError.Validation, $"Lý do mở khóa tối đa {MaxLockReasonLength} ký tự.");

            var user = await users.GetUserById(targetUserId, ct);
            // [1.5c] Con managed không phải tài khoản thật: admin thao tác theo id cũng coi như không tồn tại
            if (user is null || user.Status == UserStatuses.Managed)
                return AuthResult.Fail(AuthError.Validation, "Không tìm thấy tài khoản.");

            if (user.Role == UserRoles.Admin)
                return AuthResult.Fail(AuthError.Validation, "Không thể thao tác trên tài khoản quản trị viên.");

            var actor = await users.GetUserById(actorAdminId, ct);
            if (actor is not null && actor.Role == UserRoles.Moderator)
            {
                if (user.Role == UserRoles.Moderator)
                    return AuthResult.Fail(AuthError.Validation, "Kiểm duyệt viên không có quyền mở khóa tài khoản Kiểm duyệt viên khác.");

                if (user.Role != UserRoles.Center && user.Role != UserRoles.Student && user.Role != UserRoles.Parent)
                    return AuthResult.Fail(AuthError.Validation, "Kiểm duyệt viên chỉ có quyền mở khóa tài khoản người dùng và trung tâm.");
            }

            if (user.Status != UserStatuses.Locked && user.Status != UserStatuses.TempLocked)
                return AuthResult.Fail(AuthError.Validation, "Tài khoản không ở trạng thái bị khóa.");

            var previousStatus = user.Status;
            user.Status = UserStatuses.Active;
            user.LockoutEnd = null;
            user.FailedLoginAttempts = 0;
            await users.UpdateUserAsync(user, ct);

            var detailJson = JsonSerializer.Serialize(new
            {
                reason,
                previousStatus,
                targetEmail = user.Email,
            });
            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = actorAdminId,
                Action = "user.unlock",
                EntityType = "user",
                EntityId = user.Id.ToString(),
                DetailJson = detailJson,
            }, ct);

            // [USAS-362] Gửi email thông báo tài khoản đã được mở khóa kèm ghi chú
            _ = emailSender.SendAccountUnlockedEmailAsync(user.Email, user.FullName, reason, ct);

            return AuthResult.Ok(ToDto(user), "Đã mở khóa tài khoản.");
        }

        /// <summary>
        /// [UC-80] Bước 1 của xóa tài khoản: xóa mềm (status deleted + DeletedAt), thu hồi phiên và OTP,
        /// hủy liên kết phụ huynh và rút consent, ghi audit. Tất cả trong một transaction.
        /// Email, tên, mật khẩu giữ nguyên để người dùng khôi phục trong thời hạn xóa mềm;
        /// hết hạn thì job AccountPurgeBackgroundService xóa hẳn dòng user (cascade phần còn lại).
        /// Email báo bên phụ huynh/học sinh chỉ gửi sau khi commit.
        /// </summary>
        public async Task<AuthResult> AnonymizeUserAccountAsync(
            Guid userId,
            string? ipAddress = null,
            string? userAgent = null,
            CancellationToken ct = default)
        {
            var user = await users.GetUserById(userId, ct);
            if (user is null)
                return AuthResult.Fail(AuthError.Validation, "Không tìm thấy tài khoản người dùng.");

            if (user.Status == UserStatuses.Deleted)
                return AuthResult.Fail(AuthError.Validation, "Tài khoản đang chờ xóa.");

            // Đặc tả UC-80 chỉ cho học sinh và phụ huynh tự xóa. Trung tâm đổi người phụ trách bằng cách đổi email, số điện thoại;
            // ngừng hoạt động trung tâm và tài khoản quản trị do quản trị viên xử lý
            if (!UserRoles.SelfDelete.Contains(user.Role))
            {
                var message = user.Role == UserRoles.Center
                    ? "Tài khoản trung tâm không tự xóa được. Muốn đổi người phụ trách, hãy đổi email và số điện thoại trong trang trung tâm; muốn ngừng hoạt động, hãy liên hệ quản trị viên."
                    : "Tài khoản quản trị không tự xóa được. Hãy liên hệ quản trị viên.";
                return AuthResult.Fail(AuthError.RoleNotAllowed, message);
            }

            await SoftDeleteAsync(user, DateTime.UtcNow, user.Id, "user.delete_account", "request_account_deletion",
                requester: null, inTransaction: null, ipAddress, userAgent, ct);

            return AuthResult.Ok("Tài khoản đã chuyển sang chờ xóa. Trong 30 ngày bạn có thể đăng nhập để khôi phục.");
        }

        /// <summary>
        /// [UC-75 1a / UC-111 3a] Người đại diện rút đồng ý và yêu cầu xóa tài khoản học sinh dưới 16: xóa mềm như UC-80,
        /// ghi email người yêu cầu để học sinh không tự khôi phục được, cấp hai mã một lần cho hai nút trong mail
        /// ("Hủy xóa và đồng ý lại cho con", "Chỉ hủy xóa"), hết hạn đúng ngày xóa vĩnh viễn.
        /// Bên gọi (dịch vụ liên kết) đã kiểm người yêu cầu có quyền với học sinh này.
        /// </summary>
        public async Task<AuthResult> DeleteChildAccountByParentAsync(
            Guid studentUserId,
            ParentDeletionRequester requester,
            string? ipAddress = null,
            string? userAgent = null,
            CancellationToken ct = default)
        {
            var user = await users.GetUserById(studentUserId, ct);
            if (user is null || user.Role != UserRoles.Student)
                return AuthResult.Fail(AuthError.Validation, "Không tìm thấy tài khoản học sinh.");
            if (user.Status == UserStatuses.Deleted)
                return AuthResult.Fail(AuthError.Validation, "Tài khoản của con đang chờ xóa.");
            if (IsAtLeast16(user))
                return AuthResult.Fail(AuthError.Validation, "Con đã đủ 16 tuổi và tự quản lý tài khoản. Bạn chỉ rút được đồng ý, không xóa được tài khoản của con.");

            var now = DateTime.UtcNow;
            var deletionDate = now + _accountDeletion.RetentionPeriod;
            string reconsentToken = "", restoreOnlyToken = "";

            await SoftDeleteAsync(user, now, requester.ParentUserId, "user.delete_account_by_parent", "parent_requested_child_deletion",
                requester,
                async txCt =>
                {
                    reconsentToken = await IssueParentRestoreTokenAsync(user.Id, OtpPurposes.ParentRestoreReconsent, deletionDate, txCt);
                    restoreOnlyToken = await IssueParentRestoreTokenAsync(user.Id, OtpPurposes.ParentRestoreOnly, deletionDate, txCt);
                },
                ipAddress, userAgent, ct);

            await emailSender.SendChildAccountDeletedByParentEmailAsync(user.Email, user.FullName, deletionDate, ct);
            await emailSender.SendParentChildDeletionConfirmedEmailAsync(requester.ParentEmail, user.FullName, deletionDate, reconsentToken, restoreOnlyToken, ct);

            return AuthResult.Ok($"Đã rút đồng ý và yêu cầu xóa tài khoản của {user.FullName}. Tài khoản sẽ bị xóa vĩnh viễn vào ngày {deletionDate.AddHours(7):dd/MM/yyyy}.");
        }

        /// <summary>[UC-75 4b] Mã dài ngẫu nhiên cho nút trong mail người đại diện, lưu dạng băm, hết hạn lúc xóa hẳn.</summary>
        private async Task<string> IssueParentRestoreTokenAsync(Guid userId, string purpose, DateTime expiresAt, CancellationToken ct)
        {
            await users.InvalidateOtpsAsync(userId, purpose, ct);
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            await users.SaveOtpAsync(new OtpToken
            {
                UserId = userId,
                Purpose = purpose,
                CodeHash = HashOtp(token),
                ExpiresAt = expiresAt,
                Attempts = 0
            }, ct);
            return token;
        }

        /// <summary>
        /// [UC-80] Lõi xóa mềm dùng chung cho tự xóa và xóa theo yêu cầu người đại diện: tất cả trong một transaction,
        /// email báo bên còn lại và hook thanh toán chạy sau commit.
        /// </summary>
        private async Task SoftDeleteAsync(
            User user,
            DateTime now,
            Guid? actorUserId,
            string auditAction,
            string detailAction,
            ParentDeletionRequester? requester,
            Func<CancellationToken, Task>? inTransaction,
            string? ipAddress,
            string? userAgent,
            CancellationToken ct)
        {
            List<ParentLinkRevokeNotice> notices = [];
            Func<CancellationToken, Task>? billingAfterCommit = null;

            await unitOfWork.ExecuteInTransactionAsync(async txCt =>
            {
                // 1. Xóa mềm: chặn đăng nhập thường, giữ dữ liệu để khôi phục
                user.Status = UserStatuses.Deleted;
                user.DeletedAt = now;
                user.DeletionRequestedByEmail = requester?.ParentEmail;
                user.FailedLoginAttempts = 0;
                user.LockoutEnd = null;
                await users.UpdateUserAsync(user, txCt);

                // 2. Thu hồi toàn bộ phiên đăng nhập đang hoạt động
                await users.RevokeAllSessionsAsync(user.Id, txCt);

                // 3. Vô hiệu hóa toàn bộ mã OTP liên quan
                await users.InvalidateOtpsAsync(user.Id, "verify_email", txCt);
                await users.InvalidateOtpsAsync(user.Id, "reset_password", txCt);

                // 4. Hủy liên kết phụ huynh (cả 2 phía) và rút consent
                notices = await parentLinkService.RevokeAllForAccountDeletionAsync(user.Id, txCt, requester);

                // 5. Thu hồi yêu cầu liên hệ còn mở (new/contacted); lead đã hoàn tất giữ nguyên
                var withdrawnLeads = await leadWithdrawals.WithdrawOpenLeadsAsync(user.Id, now, txCt);

                // 5b. Gói dịch vụ: thành viên Gia đình rời nhóm, đơn chờ thanh toán bị hủy (nhóm và kỳ của chủ gói chạy tiếp, O1)
                if (billingHooks is not null)
                    billingAfterCommit = await billingHooks.OnAccountSoftDeletedAsync(user.Id, now, txCt);

                if (inTransaction is not null)
                    await inTransaction(txCt);

                // 6. Ghi vết kiểm toán, không lưu email hay tên
                var detailJson = JsonSerializer.Serialize(new
                {
                    action = detailAction,
                    userId = user.Id,
                    deletedAt = now,
                    revokedParentLinks = notices.Count,
                    withdrawnLeads
                });

                await auditLogs.AddAsync(new AuditLog
                {
                    ActorUserId = actorUserId,
                    Action = auditAction,
                    EntityType = "user",
                    EntityId = user.Id.ToString(),
                    DetailJson = detailJson,
                    IpAddress = ipAddress,
                    UserAgent = userAgent
                }, txCt);
            }, ct);

            // Sau commit mới báo bên còn lại, để lỗi giữa chừng không gửi mail sai
            foreach (var notice in notices)
                await emailSender.SendParentLinkChangedEmailAsync(notice.ToEmail, notice.StudentName, notice.ParentName, linked: false, ct);

            // Thông báo chủ nhóm và hủy link thanh toán ở cổng: cũng chỉ sau commit
            if (billingAfterCommit is not null)
                await billingAfterCommit(ct);
        }
    }
}
