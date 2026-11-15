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
        Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
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
        /// <summary>[UC-80] Xóa tài khoản và ẩn danh hóa dữ liệu cá nhân theo luật PDPA 2025.</summary>
        Task<AuthResult> AnonymizeUserAccountAsync(Guid userId, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);
        /// <summary>[UC-64] Trung tâm tự đăng ký tài khoản (User role center status unverified + StudyCenter status unverified).</summary>
        Task<AuthResult> RegisterCenterAsync(RegisterCenterRequest request, CancellationToken ct = default);
        /// <summary>[UC-91] Xác thực email trung tâm qua OTP 6 số.</summary>
        Task<AuthResult> VerifyCenterEmailAsync(VerifyCenterEmailRequest request, CancellationToken ct = default);
        /// <summary>Đổi số điện thoại liên lạc của trung tâm (không còn xác thực SMS).</summary>
        Task<AuthResult> ChangeCenterPhoneAsync(Guid userId, string newPhone, CancellationToken ct = default);
        /// <summary>Gửi OTP tới email MỚI để trung tâm đổi email liên lạc.</summary>
        Task<AuthResult> RequestCenterEmailChangeAsync(Guid userId, RequestContactChangeDto request, CancellationToken ct = default);
        /// <summary>Xác nhận OTP và đổi email liên lạc của trung tâm (email đăng nhập giữ nguyên).</summary>
        Task<AuthResult> ConfirmCenterEmailChangeAsync(Guid userId, ConfirmContactChangeDto request, CancellationToken ct = default);
    }

    public class AuthService(
        IUserRepository users,
        IPasswordHasher hasher,
        ITokenService tokens,
        IEmailSender emailSender,
        IParentLinkRepository parentLinks,
        IAuditLogRepository auditLogs,
        IStudyCenterRepository? studyCenters = null) : IAuthService
    {
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

        // Hash giả để so mật khẩu khi email không tồn tại: thời gian phản hồi như khi email có thật
        private static string? _dummyPasswordHash;

        public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
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

            // Luôn hash mật khẩu trước khi kiểm trùng: thời gian xử lý như nhau dù email đã có tài khoản hay chưa
            var passwordHash = hasher.Hash(request.Password);
            var successMessage = $"Đăng ký thành công. Mã xác thực OTP đã được gửi đến email {email} (hiệu lực {OtpExpiryMinutes} phút).";

            // [Bảo mật UC-01] Trùng email/SĐT: trả cùng thông báo như đăng ký mới (chống dò tài khoản),
            // chỉ chủ hộp thư mới biết qua mail
            var existing = await users.GetUserByEmail(email, ct);
            if (existing != null)
            {
                await emailSender.SendAccountAlreadyExistsEmailAsync(existing.Email, existing.FullName, ct);
                return AuthResult.Ok(successMessage);
            }

            if (!string.IsNullOrEmpty(normalizedPhone) && await users.PhoneExistAsync(normalizedPhone, ct))
            {
                await emailSender.SendPhoneAlreadyInUseEmailAsync(email, ct);
                return AuthResult.Ok(successMessage);
            }

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
            await users.AddAsync(user, ct);

            // Ghi lại email phụ huynh; mail mời chỉ gửi sau khi học sinh xác thực email (tránh tài khoản rác gửi mail cho người lạ)
            if (parentEmail != null)
                await parentLinks.AddAsync(new ParentStudentLink { StudentUserId = user.Id, ParentEmail = parentEmail }, ct);

            // Sinh mã OTP 6 số và gửi qua Email thật (hiệu lực 5 phút)
            var otpCode = GenerateOtpCode();
            var otp = new OtpToken
            {
                UserId = user.Id,
                Purpose = OtpPurposes.VerifyEmail,
                CodeHash = HashOtp(otpCode),
                ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
                Attempts = 0
            };
            await users.SaveOtpAsync(otp, ct);

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

            // Tự động đăng nhập
            var (token, expiresAt) = tokens.GenerateToken(user);
            var tokenHash = tokens.HashToken(token);
            await users.CreateSessionAsync(new UserSession
            {
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
                IsRevoked = false
            }, ct);

            return AuthResult.Ok(new AuthResponse(token, expiresAt, await ToDtoAsync(user, ct)));
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
            if (user == null || user.Status == UserStatuses.Locked)
                return AuthResult.Ok(genericMessage);

            if (request.Purpose == OtpPurposes.VerifyEmail && user.Status != UserStatuses.Unverified)
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
            if (user == null)
            {
                // Vẫn so mật khẩu với một hash giả để thời gian phản hồi như khi email tồn tại
                _dummyPasswordHash ??= hasher.Hash(Guid.NewGuid().ToString());
                hasher.VerifyPassword(_dummyPasswordHash, request.Password);
                return AuthResult.Fail(AuthError.InvalidCredentials, InvalidCredentialsMessage);
            }

            // 1. Tạm khóa do sai 5 lần: kiểm TRƯỚC mật khẩu để tài khoản đang khóa không thành nơi dò mật khẩu.
            //    Yêu cầu Quản trị viên mở khóa (không tự động mở khóa).
            if (user.Status == UserStatuses.TempLocked)
            {
                return AuthResult.Fail(AuthError.TempLocked, $"Tài khoản đã bị khóa do nhập sai mật khẩu {MaxFailedLoginAttempts} lần liên tiếp. Vui lòng liên hệ Quản trị viên để được mở khóa tài khoản.");
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

            // 3. [Bảo mật] Ghi nhận lần thử TRƯỚC khi so mật khẩu. Nhiều request song song cùng đọc một giá trị bộ đếm
            //    thì chỉ một request ghi được (Version/xmin), các request còn lại bị từ chối mà không được so mật khẩu.
            user.FailedLoginAttempts++;
            if (!await TryUpdateUserAsync(user, ct))
                return AuthResult.Fail(AuthError.InvalidCredentials, InvalidCredentialsMessage);

            if (!hasher.VerifyPassword(user.PasswordHash, request.Password))
            {
                if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
                {
                    // Đạt 5 lần sai -> Khóa tài khoản và gửi email thông báo (cần Admin mở khóa)
                    user.Status = UserStatuses.TempLocked;
                    user.LockoutEnd = null;
                    if (await TryUpdateUserAsync(user, ct))
                        await emailSender.SendAccountTempLockedNotificationAsync(user.Email, user.FullName, 0, ct);

                    return AuthResult.Fail(AuthError.TempLocked, $"Tài khoản đã bị khóa do nhập sai mật khẩu {MaxFailedLoginAttempts} lần liên tiếp. Vui lòng liên hệ Quản trị viên để được mở khóa tài khoản.");
                }

                return AuthResult.Fail(AuthError.InvalidCredentials, InvalidCredentialsMessage);
            }

            // Đăng nhập thành công -> Reset bộ đếm số lần sai và thời gian khóa
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            if (!await TryUpdateUserAsync(user, ct))
                return AuthResult.Fail(AuthError.InvalidCredentials, InvalidCredentialsMessage);

            // Sinh JWT Token và lưu phiên
            var (token, expiresAt) = tokens.GenerateToken(user);
            var tokenHash = tokens.HashToken(token);

            var session = new UserSession
            {
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
                IsRevoked = false,
                IpAddress = ipAddress,
                UserAgent = userAgent
            };
            await users.CreateSessionAsync(session, ct);

            return AuthResult.Ok(new AuthResponse(token, expiresAt, await ToDtoAsync(user, ct)));
        }

        public async Task<AuthResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);
            var user = await users.GetUserByEmail(email, ct);

            // Anti-enumeration: Luôn trả về thông báo thành công dù email có tồn tại hay không.
            // Mail đi qua hàng đợi nền nên thời gian phản hồi cũng như nhau.
            if (user != null && user.Status != UserStatuses.Locked && user.Status != UserStatuses.TempLocked &&
                await CanSendOtpAsync(user.Id, OtpPurposes.ResetPassword, ct))
            {
                var otpCode = await IssueOtpAsync(user.Id, OtpPurposes.ResetPassword, ct);
                await emailSender.SendPasswordResetEmailAsync(user.Email, user.FullName, otpCode, OtpExpiryMinutes, ct);
            }

            return AuthResult.Ok($"Nếu email tồn tại trong hệ thống, mã xác thực đặt lại mật khẩu đã được gửi đến hộp thư (hiệu lực {OtpExpiryMinutes} phút).");
        }

        public async Task<AuthResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
        {
            var passwordError = ValidatePassword(request.NewPassword, "Mật khẩu mới");
            if (passwordError != null)
                return AuthResult.Fail(AuthError.Validation, passwordError);

            var email = NormalizeEmail(request.Email);
            var user = await users.GetUserByEmail(email, ct);
            if (user == null || user.Status == UserStatuses.Locked || user.Status == UserStatuses.TempLocked)
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
            if (user == null || user.Status == UserStatuses.Locked || user.Status == UserStatuses.TempLocked)
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
                    user.LockoutEnd = null;
                    if (await TryUpdateUserAsync(user, ct))
                    {
                        await users.RevokeAllSessionsAsync(user.Id, ct);
                        await emailSender.SendAccountTempLockedNotificationAsync(user.Email, user.FullName, 0, ct);
                    }

                    return AuthResult.Fail(AuthError.TempLocked, $"Tài khoản đã bị khóa do nhập sai mật khẩu hiện tại {MaxFailedLoginAttempts} lần liên tiếp. Vui lòng liên hệ Quản trị viên để được mở khóa tài khoản.");
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
                await emailSender.SendParentInviteEmailAsync(link.ParentEmail, student.FullName, token, ParentInvite.ExpiryDays, ct);
            }
        }

        /// <summary>Kiểm tra chính sách mật khẩu (8–100 ký tự), dùng chung cho đăng ký, đặt lại và đổi mật khẩu.</summary>
        public static string? ValidatePassword(string? password, string label)
        {
            if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
                return $"{label} phải có tối thiểu {MinPasswordLength} ký tự.";

            if (password.Length > MaxPasswordLength)
                return $"{label} không được vượt quá {MaxPasswordLength} ký tự.";

            return null;
        }

        /// <summary>
        /// Đối chiếu mã OTP mới nhất của user. Tăng bộ đếm lần thử và lưu TRƯỚC khi so mã: nhiều request song song
        /// thì chỉ một request ghi được (Version/xmin), các request còn lại thất bại mà không được so mã.
        /// Đúng mã thì đánh dấu đã dùng (dùng 1 lần); sai đủ 5 lần thì vô hiệu hóa mã.
        /// </summary>
        private async Task<bool> TryConsumeOtpAsync(Guid userId, string purpose, string? code, CancellationToken ct)
        {
            var otp = await users.GetLatestOtpAsync(userId, purpose, ct);
            if (otp == null || otp.UsedAt != null || DateTime.UtcNow > otp.ExpiresAt || otp.Attempts >= MaxOtpAttempts)
                return false;

            otp.Attempts++;
            if (!await TryUpdateOtpAsync(otp, ct))
                return false;

            if (string.IsNullOrWhiteSpace(code) || otp.CodeHash != HashOtp(code))
            {
                if (otp.Attempts >= MaxOtpAttempts)
                {
                    otp.UsedAt = DateTime.UtcNow; // Vô hiệu hóa mã sau 5 lần sai
                    await TryUpdateOtpAsync(otp, ct);
                }
                return false;
            }

            // Đúng mã -> đánh dấu đã dùng; request song song khác dùng cùng mã sẽ bị từ chối
            otp.UsedAt = DateTime.UtcNow;
            return await TryUpdateOtpAsync(otp, ct);
        }

        /// <summary>Kiểm tra cooldown 60 giây giữa 2 lần gửi và giới hạn 5 lần/giờ.</summary>
        private async Task<bool> CanSendOtpAsync(Guid userId, string purpose, CancellationToken ct)
        {
            var latest = await users.GetLatestOtpAsync(userId, purpose, ct);
            if (latest != null && (DateTime.UtcNow - latest.CreatedAt).TotalSeconds < MinSecondsBetweenOtpRequests)
                return false;

            return await users.CountOtpRequestsInLastHourAsync(userId, purpose, ct) < MaxOtpRequestsPerHour;
        }

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

        private async Task<bool> TryUpdateOtpAsync(OtpToken otp, CancellationToken ct)
        {
            try
            {
                await users.UpdateOtpAsync(otp, ct);
                return true;
            }
            catch (ConcurrencyConflictException)
            {
                return false;
            }
        }

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
            if (user is null)
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
            if (user is null)
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
        /// [UC-80] Xóa tài khoản và ẩn danh hóa dữ liệu (Right to be Forgotten - PDPA 2025).
        /// Áp dụng cơ chế Anonymization thay vì Hard Delete để bảo toàn tính toàn vẹn CSDL và khóa ngoại.
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
                return AuthResult.Fail(AuthError.Validation, "Tài khoản đã được xóa trước đó.");

            var originalEmail = user.Email;
            var shortId = user.Id.ToString("N")[..8];

            // 1. Ẩn danh hóa thông tin định danh cá nhân (PII)
            user.Email = $"deleted_{shortId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}@anonymized.studyabroad";
            user.FullName = "Người dùng đã xóa";
            user.Phone = null;
            user.PasswordHash = string.Empty;
            user.Status = UserStatuses.Deleted;
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;

            await users.UpdateUserAsync(user, ct);

            // 2. Thu hồi toàn bộ phiên đăng nhập đang hoạt động
            await users.RevokeAllSessionsAsync(user.Id, ct);

            // 3. Vô hiệu hóa toàn bộ mã OTP liên quan
            await users.InvalidateOtpsAsync(user.Id, "verify_email", ct);
            await users.InvalidateOtpsAsync(user.Id, "reset_password", ct);

            // 4. Ghi vết kiểm toán (Audit Trail) bất biến
            var detailJson = JsonSerializer.Serialize(new
            {
                action = "anonymize_account",
                userId = user.Id,
                anonymizedAt = DateTime.UtcNow,
                previousEmailDomain = originalEmail.Contains('@') ? originalEmail.Split('@')[1] : null
            });

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = user.Id,
                Action = "user.delete_account",
                EntityType = "user",
                EntityId = user.Id.ToString(),
                DetailJson = detailJson,
                IpAddress = ipAddress,
                UserAgent = userAgent
            }, ct);

            return AuthResult.Ok("Tài khoản của bạn đã được xóa và ẩn danh hóa thành công theo quy định bảo vệ dữ liệu.");
        }

        public async Task<AuthResult> RegisterCenterAsync(RegisterCenterRequest request, CancellationToken ct = default)
        {
            if (studyCenters is null)
                return AuthResult.Fail(AuthError.Validation, "Dịch vụ đăng ký trung tâm tạm thời chưa sẵn sàng.");

            if (!request.AgreeTerms || !request.AgreeTruthfulnessCommitment)
                return AuthResult.Fail(AuthError.Validation, "Bạn cần đồng ý với Điều khoản sử dụng và cam kết tính trung thực của thông tin.");

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password != request.ConfirmPassword)
                return AuthResult.Fail(AuthError.Validation, "Mật khẩu xác nhận không khớp.");

            var email = NormalizeEmail(request.Email);
            if (string.IsNullOrEmpty(email) || email.Length > MaxEmailLength || !IsValidEmail(email))
                return AuthResult.Fail(AuthError.Validation, $"Email không hợp lệ hoặc vượt quá {MaxEmailLength} ký tự.");

            var passwordError = ValidatePassword(request.Password, "Mật khẩu");
            if (passwordError != null)
                return AuthResult.Fail(AuthError.Validation, passwordError);

            var centerName = request.CenterName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(centerName) || centerName.Length > 200)
                return AuthResult.Fail(AuthError.Validation, "Tên trung tâm không được để trống và tối đa 200 ký tự.");

            var registrantName = request.RegistrantName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(registrantName) || registrantName.Length > MaxFullNameLength)
                return AuthResult.Fail(AuthError.Validation, $"Họ tên người đăng ký không được để trống và tối đa {MaxFullNameLength} ký tự.");

            var rawPhone = request.Phone?.Trim();
            if (string.IsNullOrEmpty(rawPhone))
                return AuthResult.Fail(AuthError.Validation, "Số điện thoại không được để trống.");

            if (!TryValidateAndNormalizeVietnamesePhone(rawPhone, out var normalizedPhone))
                return AuthResult.Fail(AuthError.Validation, "Số điện thoại không đúng định dạng Việt Nam (ví dụ: 0912345678 hoặc +84912345678).");

            var existing = await users.GetUserByEmail(email, ct);
            if (existing != null)
            {
                if (existing.Status == UserStatuses.Unverified)
                    return AuthResult.Fail(AuthError.Validation, "Email này đã được đăng ký nhưng chưa xác thực. Vui lòng kiểm tra hộp thư hoặc yêu cầu gửi lại mã OTP.");
                return AuthResult.Fail(AuthError.Validation, "Email này đã được sử dụng.");
            }

            if (await users.PhoneExistAsync(normalizedPhone, ct))
                return AuthResult.Fail(AuthError.Validation, "Số điện thoại này đã được sử dụng.");

            string code;
            do
            {
                code = $"CTR_{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            } while (await studyCenters.ExistsByCodeAsync(code, ct));

            var user = new User
            {
                Email = email,
                PasswordHash = hasher.Hash(request.Password),
                FullName = registrantName,
                Role = UserRoles.Center,
                Status = UserStatuses.Unverified,
                Phone = normalizedPhone,
                FailedLoginAttempts = 0,
                LockoutEnd = null
            };
            await users.AddAsync(user, ct);

            var center = new StudyCenter
            {
                Code = code,
                Name = centerName,
                Email = email,
                Phone = normalizedPhone,
                OwnerUserId = user.Id,
                VerificationStatus = CenterVerificationStatuses.Unverified,
                TermsAgreedAt = DateTime.UtcNow,
                CommitmentAgreedAt = DateTime.UtcNow,
                RegistrantName = registrantName,
                RegistrantPosition = string.IsNullOrWhiteSpace(request.RegistrantPosition) ? null : request.RegistrantPosition.Trim(),
                IsActive = true
            };
            await studyCenters.AddAsync(center, ct);

            var otpCode = GenerateOtpCode();
            var otp = new OtpToken
            {
                UserId = user.Id,
                Purpose = OtpPurposes.VerifyEmail,
                CodeHash = HashOtp(otpCode),
                ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
                Attempts = 0
            };
            await users.SaveOtpAsync(otp, ct);

            await emailSender.SendVerificationEmailAsync(user.Email, user.FullName, otpCode, OtpExpiryMinutes, ct);

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = user.Id,
                Action = "center.register",
                EntityType = "StudyCenter",
                EntityId = center.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new { email, centerName, registrantName, code })
            }, ct);

            return AuthResult.Ok($"Đăng ký thành công. Mã xác thực OTP đã được gửi đến email {email} (hiệu lực {OtpExpiryMinutes} phút).");
        }

        public async Task<AuthResult> VerifyCenterEmailAsync(VerifyCenterEmailRequest request, CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);
            var user = await users.GetUserByEmail(email, ct);
            if (user == null || user.Status != UserStatuses.Unverified)
                return AuthResult.Fail(AuthError.OtpInvalid, OtpFailedMessage);

            if (!await TryConsumeOtpAsync(user.Id, OtpPurposes.VerifyEmail, request.Code, ct))
                return AuthResult.Fail(AuthError.OtpInvalid, OtpFailedMessage);

            user.Status = UserStatuses.Active;
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            await users.UpdateUserAsync(user, ct);

            if (studyCenters != null)
            {
                var center = await studyCenters.GetByOwnerOrEmailAsync(user.Id, user.Email, ct);
                if (center != null)
                {
                    center.EmailVerifiedAt = DateTime.UtcNow;
                    await studyCenters.UpdateAsync(center, ct);
                }
            }

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = user.Id,
                Action = "center.email_verified",
                EntityType = "User",
                EntityId = user.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new { email = user.Email })
            }, ct);

            var (token, expiresAt) = tokens.GenerateToken(user);
            var tokenHash = tokens.HashToken(token);
            await users.CreateSessionAsync(new UserSession
            {
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
                IsRevoked = false
            }, ct);

            return AuthResult.Ok(new AuthResponse(token, expiresAt, await ToDtoAsync(user, ct)));
        }

        // ----- Đổi số điện thoại / email liên lạc của trung tâm (xác thực OTP tại giá trị mới) -----

        /// <summary>Gắn mã OTP với giá trị mới: mã gửi tới số A không dùng được để xác nhận số B.</summary>
        private static string BindOtp(string code, string target) => $"{code.Trim()}|{target}";

        public async Task<AuthResult> ChangeCenterPhoneAsync(Guid userId, string newPhone, CancellationToken ct = default)
        {
            if (studyCenters is null)
                return AuthResult.Fail(AuthError.Validation, "Dịch vụ trung tâm tạm thời chưa sẵn sàng.");

            if (!TryValidateAndNormalizeVietnamesePhone(newPhone, out var normalized))
                return AuthResult.Fail(AuthError.Validation, "Số điện thoại không đúng định dạng Việt Nam (ví dụ: 0912345678 hoặc +84912345678).");

            var center = await studyCenters.GetByOwnerOrEmailAsync(userId, null, ct);
            var user = await users.GetUserById(userId, ct);
            if (center is null || user is null)
                return AuthResult.Fail(AuthError.Validation, "Không tìm thấy thông tin trung tâm cho tài khoản này.");

            if (normalized == center.Phone)
                return AuthResult.Fail(AuthError.Validation, "Đây là số điện thoại hiện tại của trung tâm.");

            if (await users.PhoneExistAsync(normalized, ct))
                return AuthResult.Fail(AuthError.Validation, "Số điện thoại này đã được sử dụng.");

            var oldPhone = center.Phone;
            center.Phone = normalized;
            user.Phone = normalized;
            await studyCenters.UpdateAsync(center, ct);
            await users.UpdateUserAsync(user, ct);

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = userId,
                Action = "center.phone_changed",
                EntityType = "StudyCenter",
                EntityId = center.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new { oldPhone, newPhone = normalized })
            }, ct);

            return AuthResult.Ok("Đã cập nhật số điện thoại liên lạc.");
        }

        public async Task<AuthResult> RequestCenterEmailChangeAsync(Guid userId, RequestContactChangeDto request, CancellationToken ct = default)
        {
            if (studyCenters is null)
                return AuthResult.Fail(AuthError.Validation, "Dịch vụ trung tâm tạm thời chưa sẵn sàng.");

            var newEmail = NormalizeEmail(request.NewValue);
            if (string.IsNullOrWhiteSpace(newEmail) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(newEmail))
                return AuthResult.Fail(AuthError.Validation, "Email không hợp lệ.");

            var center = await studyCenters.GetByOwnerOrEmailAsync(userId, null, ct);
            var user = await users.GetUserById(userId, ct);
            if (center is null || user is null)
                return AuthResult.Fail(AuthError.Validation, "Không tìm thấy thông tin trung tâm cho tài khoản này.");

            if (string.Equals(newEmail, center.Email, StringComparison.OrdinalIgnoreCase))
                return AuthResult.Fail(AuthError.Validation, "Đây là email liên lạc hiện tại của trung tâm.");

            var other = await users.GetUserByEmail(newEmail, ct);
            if (other is not null && other.Id != userId)
                return AuthResult.Fail(AuthError.Validation, "Email này đã được sử dụng bởi tài khoản khác.");

            if (!await CanSendOtpAsync(userId, OtpPurposes.ChangeEmail, ct))
                return AuthResult.Fail(AuthError.Validation, "Bạn yêu cầu mã quá nhanh hoặc quá nhiều lần. Vui lòng thử lại sau.");

            await users.InvalidateOtpsAsync(userId, OtpPurposes.ChangeEmail, ct);
            var otpCode = GenerateOtpCode();
            await users.SaveOtpAsync(new OtpToken
            {
                UserId = userId,
                Purpose = OtpPurposes.ChangeEmail,
                CodeHash = HashOtp(BindOtp(otpCode, newEmail)),
                ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
                Attempts = 0
            }, ct);

            await emailSender.SendVerificationEmailAsync(newEmail, user.FullName, otpCode, OtpExpiryMinutes, ct);

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = userId,
                Action = "center.email_change_requested",
                EntityType = "StudyCenter",
                EntityId = center.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new { newEmail })
            }, ct);

            return AuthResult.Ok($"Mã OTP đã được gửi đến {newEmail} (hiệu lực {OtpExpiryMinutes} phút).");
        }

        public async Task<AuthResult> ConfirmCenterEmailChangeAsync(Guid userId, ConfirmContactChangeDto request, CancellationToken ct = default)
        {
            if (studyCenters is null)
                return AuthResult.Fail(AuthError.Validation, "Dịch vụ trung tâm tạm thời chưa sẵn sàng.");

            var newEmail = NormalizeEmail(request.NewValue);
            if (string.IsNullOrWhiteSpace(newEmail))
                return AuthResult.Fail(AuthError.Validation, "Email không hợp lệ.");

            if (string.IsNullOrWhiteSpace(request.Code))
                return AuthResult.Fail(AuthError.Validation, "Vui lòng nhập mã OTP.");

            if (!await TryConsumeOtpAsync(userId, OtpPurposes.ChangeEmail, BindOtp(request.Code, newEmail), ct))
                return AuthResult.Fail(AuthError.OtpInvalid, "Mã OTP không đúng hoặc đã hết hạn.");

            var center = await studyCenters.GetByOwnerOrEmailAsync(userId, null, ct);
            if (center is null)
                return AuthResult.Fail(AuthError.Validation, "Không tìm thấy thông tin trung tâm cho tài khoản này.");

            var oldEmail = center.Email;
            center.Email = newEmail;
            center.EmailVerifiedAt = DateTime.UtcNow;
            await studyCenters.UpdateAsync(center, ct);

            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = userId,
                Action = "center.email_changed",
                EntityType = "StudyCenter",
                EntityId = center.Id.ToString(),
                DetailJson = JsonSerializer.Serialize(new { oldEmail, newEmail })
            }, ct);

            return AuthResult.Ok("Đã đổi và xác thực email liên lạc mới. Email đăng nhập của bạn không thay đổi.");
        }
    }
}
