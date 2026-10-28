using StudyAbroad.Application.Common;
using StudyAbroad.Application.ParentLinks;
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
    }

    public class AuthService(IUserRepository users, IPasswordHasher hasher, ITokenService tokens, IEmailSender emailSender, IParentLinkRepository parentLinks) : IAuthService
    {
        public const int MinPasswordLength = 8;
        public const int MaxPasswordLength = 100;
        public const int MaxFullNameLength = 100;
        public const int MaxEmailLength = 100;
        public const int MaxRawPhoneLength = 20;
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
            //    Tự động mở khóa sau 15 phút.
            if (user.Status == UserStatuses.TempLocked)
            {
                if (user.LockoutEnd.HasValue && user.LockoutEnd.Value <= DateTime.UtcNow)
                {
                    user.Status = user.Role == UserRoles.Center ? UserStatuses.Pending : UserStatuses.Active;
                    user.FailedLoginAttempts = 0;
                    user.LockoutEnd = null;
                    if (!await TryUpdateUserAsync(user, ct))
                        return AuthResult.Fail(AuthError.InvalidCredentials, InvalidCredentialsMessage);
                }
                else
                {
                    var remainingMinutes = user.LockoutEnd.HasValue
                        ? Math.Max(1, (int)Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes))
                        : TempLockoutMinutes;

                    return AuthResult.Fail(AuthError.TempLocked, $"Tài khoản tạm thời bị khóa do nhập sai mật khẩu {MaxFailedLoginAttempts} lần liên tiếp. Hệ thống sẽ tự động mở khóa sau {remainingMinutes} phút.");
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

            // 3. [Bảo mật] Ghi nhận lần thử TRƯỚC khi so mật khẩu. Nhiều request song song cùng đọc một giá trị bộ đếm
            //    thì chỉ một request ghi được (Version/xmin), các request còn lại bị từ chối mà không được so mật khẩu.
            user.FailedLoginAttempts++;
            if (!await TryUpdateUserAsync(user, ct))
                return AuthResult.Fail(AuthError.InvalidCredentials, InvalidCredentialsMessage);

            if (!hasher.VerifyPassword(user.PasswordHash, request.Password))
            {
                if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
                {
                    // Đạt 5 lần sai -> Tạm khóa 15 phút và gửi email thông báo (email chỉ để thông báo)
                    user.Status = UserStatuses.TempLocked;
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(TempLockoutMinutes);
                    if (await TryUpdateUserAsync(user, ct))
                        await emailSender.SendAccountTempLockedNotificationAsync(user.Email, user.FullName, TempLockoutMinutes, ct);

                    return AuthResult.Fail(AuthError.TempLocked, $"Tài khoản đã bị tạm khóa do nhập sai mật khẩu {MaxFailedLoginAttempts} lần liên tiếp. Hệ thống sẽ tự động mở khóa sau {TempLockoutMinutes} phút.");
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
            if (user != null && user.Status != UserStatuses.Locked &&
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
            if (user == null || user.Status == UserStatuses.Locked)
                return AuthResult.Fail(AuthError.OtpInvalid, OtpFailedMessage);

            if (!await TryConsumeOtpAsync(user.Id, OtpPurposes.ResetPassword, request.OtpCode, ct))
                return AuthResult.Fail(AuthError.OtpInvalid, OtpFailedMessage);

            user.PasswordHash = hasher.Hash(request.NewPassword);
            user.FailedLoginAttempts = 0;
            if (user.Status == UserStatuses.TempLocked)
            {
                user.Status = user.Role == UserRoles.Center ? UserStatuses.Pending : UserStatuses.Active;
                user.LockoutEnd = null;
            }

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
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(TempLockoutMinutes);
                    if (await TryUpdateUserAsync(user, ct))
                    {
                        await users.RevokeAllSessionsAsync(user.Id, ct);
                        await emailSender.SendAccountTempLockedNotificationAsync(user.Email, user.FullName, TempLockoutMinutes, ct);
                    }

                    return AuthResult.Fail(AuthError.TempLocked, $"Tài khoản đã bị tạm khóa do nhập sai mật khẩu {MaxFailedLoginAttempts} lần liên tiếp. Hệ thống sẽ tự động mở khóa sau {TempLockoutMinutes} phút.");
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
            return ToDto(user) with
            {
                AgeGroup = access.AgeGroup,
                ParentConsentStatus = access.ParentConsentStatus,
                BlockedFeatures = access.BlockedFeatures,
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
    }
}
