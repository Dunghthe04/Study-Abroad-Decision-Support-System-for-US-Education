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
        Task<AuthResult> UnlockAccountAsync(UnlockAccountRequest request, CancellationToken ct = default);
        Task<bool> LogoutAsync(string rawToken, CancellationToken ct = default);
        Task<UserDto?> GetMeAsync(Guid userId, CancellationToken ct = default);
        Task<List<UserDto>> GetAllUsersAsync(CancellationToken ct = default);
    }

    public class AuthService(IUserRepository users, IPasswordHasher hasher, ITokenService tokens, IEmailSender emailSender) : IAuthService
    {
        public const int MinPasswordLength = 8;
        public const int MaxPasswordLength = 100;
        public const int MaxFullNameLength = 100;
        public const int MaxEmailLength = 100;
        public const int MaxRawPhoneLength = 20;
        public const int OtpExpiryMinutes = 10;
        public const int MaxOtpAttempts = 5;
        public const int MaxFailedLoginAttempts = 5;

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

        public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);
            var fullName = request.FullName?.Trim() ?? string.Empty;
            var rawPhone = request.Phone?.Trim();
            string? normalizedPhone = null;

            if (string.IsNullOrEmpty(email) || email.Length > MaxEmailLength || !IsValidEmail(email))
                return AuthResult.Fail(AuthError.Validation, $"Email không hợp lệ hoặc vượt quá {MaxEmailLength} ký tự.");

            if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
                return AuthResult.Fail(AuthError.Validation, $"Mật khẩu tối thiểu {MinPasswordLength} ký tự.");

            if (request.Password.Length > MaxPasswordLength)
                return AuthResult.Fail(AuthError.Validation, $"Mật khẩu không được vượt quá {MaxPasswordLength} ký tự.");

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

            if (request.Role == UserRoles.Student && request.ParentAcknowledged != true)
                return AuthResult.Fail(AuthError.Validation, "Học sinh cần xác nhận phụ huynh đã biết.");

            if (await users.EmailExistAsync(email, ct))
                return AuthResult.Fail(AuthError.EmailTaken, "Email đã được sử dụng.");

            if (!string.IsNullOrEmpty(normalizedPhone) && await users.PhoneExistAsync(normalizedPhone, ct))
                return AuthResult.Fail(AuthError.PhoneTaken, "Số điện thoại đã được sử dụng.");

            // [USAS-12] Khởi tạo tài khoản với trạng thái unverified (chờ kích hoạt email qua OTP)
            var user = new User
            {
                Email = email,
                PasswordHash = hasher.Hash(request.Password),
                FullName = fullName,
                Role = request.Role,
                Status = UserStatuses.Unverified,
                ParentAcknowledged = request.ParentAcknowledged == true,
                Phone = normalizedPhone,
                FailedLoginAttempts = 0
            };
            await users.AddAsync(user, ct);

            // Sinh mã OTP 6 số và gửi qua Email thật (hiệu lực 10 phút)
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

            return AuthResult.Ok(ToDto(user), $"Đăng ký thành công. Mã xác thực OTP đã được gửi đến email {user.Email} (hiệu lực {OtpExpiryMinutes} phút).");
        }

        public async Task<AuthResult> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);
            var user = await users.GetUserByEmail(email, ct);
            if (user == null)
                return AuthResult.Fail(AuthError.Validation, "Không tìm thấy thông tin tài khoản.");

            if (user.Status != UserStatuses.Unverified)
                return AuthResult.Fail(AuthError.Validation, "Tài khoản này đã được kích hoạt trước đó.");

            var otp = await users.GetLatestOtpAsync(user.Id, OtpPurposes.VerifyEmail, ct);
            if (otp == null)
                return AuthResult.Fail(AuthError.OtpInvalid, "Không tìm thấy mã OTP hợp lệ. Vui lòng yêu cầu gửi lại mã mới.");

            if (DateTime.UtcNow > otp.ExpiresAt)
                return AuthResult.Fail(AuthError.OtpExpired, $"Mã OTP đã hết hạn (chỉ có hiệu lực trong {OtpExpiryMinutes} phút). Vui lòng nhấn gửi lại mã.");

            if (otp.Attempts >= MaxOtpAttempts)
                return AuthResult.Fail(AuthError.OtpInvalid, $"Bạn đã nhập sai mã OTP quá {MaxOtpAttempts} lần. Vui lòng yêu cầu gửi lại mã mới.");

            if (otp.CodeHash != HashOtp(request.OtpCode))
            {
                otp.Attempts++;
                await users.SaveOtpAsync(otp, ct);
                var remaining = MaxOtpAttempts - otp.Attempts;
                return AuthResult.Fail(AuthError.OtpInvalid, $"Mã OTP không chính xác. Bạn còn {remaining} lần thử.");
            }

            // OTP đúng -> đánh dấu đã dùng và kích hoạt tài khoản
            otp.UsedAt = DateTime.UtcNow;
            user.Status = user.Role == UserRoles.Center ? UserStatuses.Pending : UserStatuses.Active;
            await users.UpdateUserAsync(user, ct);

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

            return AuthResult.Ok(new AuthResponse(token, expiresAt, ToDto(user)));
        }

        public async Task<AuthResult> ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);
            var user = await users.GetUserByEmail(email, ct);
            if (user == null)
            {
                // Anti-enumeration: luôn trả về thông báo đã gửi
                return AuthResult.Ok("Nếu email tồn tại, mã xác thực mới đã được gửi.");
            }

            if (request.Purpose != OtpPurposes.VerifyEmail &&
                request.Purpose != OtpPurposes.ResetPassword &&
                request.Purpose != OtpPurposes.UnlockAccount)
            {
                return AuthResult.Fail(AuthError.Validation, "Mục đích gửi OTP không hợp lệ.");
            }

            // Nếu user bị khóa bởi Admin thì không cho gửi OTP mở khóa/đổi mật khẩu
            if (user.Status == UserStatuses.Locked && request.Purpose != OtpPurposes.VerifyEmail)
            {
                return AuthResult.Fail(AuthError.Locked, "Tài khoản của bạn đã bị khóa bởi Quản trị viên và không thể tự thao tác.");
            }

            var latest = await users.GetLatestOtpAsync(user.Id, request.Purpose, ct);
            if (latest != null && (DateTime.UtcNow - latest.CreatedAt).TotalSeconds < 60)
            {
                var waitSeconds = 60 - (int)(DateTime.UtcNow - latest.CreatedAt).TotalSeconds;
                return AuthResult.Fail(AuthError.Validation, $"Vui lòng đợi thêm {waitSeconds} giây trước khi yêu cầu gửi lại mã mới.");
            }

            await users.InvalidateOtpsAsync(user.Id, request.Purpose, ct);

            var otpCode = GenerateOtpCode();
            var newOtp = new OtpToken
            {
                UserId = user.Id,
                Purpose = request.Purpose,
                CodeHash = HashOtp(otpCode),
                ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
                Attempts = 0
            };
            await users.SaveOtpAsync(newOtp, ct);

            if (request.Purpose == OtpPurposes.VerifyEmail)
                await emailSender.SendVerificationEmailAsync(user.Email, user.FullName, otpCode, OtpExpiryMinutes, ct);
            else if (request.Purpose == OtpPurposes.ResetPassword)
                await emailSender.SendPasswordResetEmailAsync(user.Email, user.FullName, otpCode, OtpExpiryMinutes, ct);
            else if (request.Purpose == OtpPurposes.UnlockAccount)
                await emailSender.SendAccountLockedEmailAsync(user.Email, user.FullName, otpCode, OtpExpiryMinutes, ct);

            return AuthResult.Ok($"Đã gửi mã xác thực mới tới email của bạn (hiệu lực {OtpExpiryMinutes} phút).");
        }

        public async Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(request.Email) || request.Email.Length > MaxEmailLength ||
                string.IsNullOrEmpty(request.Password) || request.Password.Length > MaxPasswordLength)
            {
                return AuthResult.Fail(AuthError.InvalidCredentials, "Sai tài khoản hoặc mật khẩu.");
            }

            var user = await users.GetUserByEmail(NormalizeEmail(request.Email), ct);
            if (user == null)
            {
                return AuthResult.Fail(AuthError.InvalidCredentials, "Sai tài khoản hoặc mật khẩu.");
            }

            // 1. Kiểm tra khóa bởi Admin (vĩnh viễn) -> Chặn tuyệt đối, không thể tự mở bằng OTP
            if (user.Status == UserStatuses.Locked)
            {
                return AuthResult.Fail(AuthError.Locked, "Tài khoản của bạn đã bị khóa bởi Quản trị viên. Vui lòng liên hệ ban quản trị để được hỗ trợ.");
            }

            // 2. Kiểm tra tạm khóa do sai 5 lần -> Kể cả gõ đúng mật khẩu vẫn bị chặn
            if (user.Status == UserStatuses.TempLocked)
            {
                return AuthResult.Fail(AuthError.TempLocked, "Tài khoản của bạn tạm thời bị khóa do nhập sai mật khẩu 5 lần liên tiếp. Vui lòng kiểm tra email để lấy mã OTP mở khóa.");
            }

            // 3. Kiểm tra chưa xác minh email
            if (user.Status == UserStatuses.Unverified)
            {
                return AuthResult.Fail(AuthError.Unverified, "Tài khoản chưa được kích hoạt email. Vui lòng kiểm tra hộp thư để nhập mã OTP xác thực.");
            }

            // 4. Kiểm tra mật khẩu
            if (!hasher.VerifyPassword(user.PasswordHash, request.Password ?? string.Empty))
            {
                user.FailedLoginAttempts++;

                if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
                {
                    // Đạt 5 lần sai -> Tạm khóa tài khoản và bắn email OTP mở khóa
                    user.Status = UserStatuses.TempLocked;

                    var otpCode = GenerateOtpCode();
                    await users.InvalidateOtpsAsync(user.Id, OtpPurposes.UnlockAccount, ct);
                    await users.SaveOtpAsync(new OtpToken
                    {
                        UserId = user.Id,
                        Purpose = OtpPurposes.UnlockAccount,
                        CodeHash = HashOtp(otpCode),
                        ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
                        Attempts = 0
                    }, ct);

                    await users.UpdateUserAsync(user, ct);
                    await emailSender.SendAccountLockedEmailAsync(user.Email, user.FullName, otpCode, OtpExpiryMinutes, ct);

                    return AuthResult.Fail(AuthError.TempLocked, $"Tài khoản đã bị tạm khóa do nhập sai mật khẩu {MaxFailedLoginAttempts} lần liên tiếp. Mã OTP mở khóa đã được gửi tới email của bạn (hiệu lực {OtpExpiryMinutes} phút).");
                }

                await users.UpdateUserAsync(user, ct);
                var remaining = MaxFailedLoginAttempts - user.FailedLoginAttempts;
                return AuthResult.Fail(AuthError.InvalidCredentials, $"Sai tài khoản hoặc mật khẩu. Bạn còn {remaining} lần thử trước khi tài khoản bị tạm khóa.");
            }

            // Đăng nhập thành công -> Reset bộ đếm số lần sai
            if (user.FailedLoginAttempts > 0)
            {
                user.FailedLoginAttempts = 0;
                await users.UpdateUserAsync(user, ct);
            }

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

            return AuthResult.Ok(new AuthResponse(token, expiresAt, ToDto(user)));
        }

        public async Task<AuthResult> UnlockAccountAsync(UnlockAccountRequest request, CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);
            var user = await users.GetUserByEmail(email, ct);
            if (user == null)
                return AuthResult.Fail(AuthError.Validation, "Không tìm thấy thông tin tài khoản.");

            // Bị Admin khóa -> Tuyệt đối không cho phép tự mở khóa bằng OTP!
            if (user.Status == UserStatuses.Locked)
            {
                return AuthResult.Fail(AuthError.Locked, "Tài khoản này đã bị khóa bởi Quản trị viên và không thể tự mở khóa. Vui lòng liên hệ ban quản trị để được hỗ trợ.");
            }

            if (user.Status != UserStatuses.TempLocked)
            {
                return AuthResult.Ok("Tài khoản đang hoạt động bình thường, không bị tạm khóa.");
            }

            var otp = await users.GetLatestOtpAsync(user.Id, OtpPurposes.UnlockAccount, ct);
            if (otp == null)
                return AuthResult.Fail(AuthError.OtpInvalid, "Không tìm thấy mã OTP mở khóa hợp lệ. Vui lòng yêu cầu gửi lại mã mới.");

            if (DateTime.UtcNow > otp.ExpiresAt)
                return AuthResult.Fail(AuthError.OtpExpired, $"Mã OTP mở khóa đã hết hạn (chỉ có hiệu lực trong {OtpExpiryMinutes} phút). Vui lòng yêu cầu gửi lại mã mới.");

            if (otp.Attempts >= MaxOtpAttempts)
                return AuthResult.Fail(AuthError.OtpInvalid, $"Bạn đã nhập sai mã OTP quá {MaxOtpAttempts} lần. Vui lòng yêu cầu gửi lại mã mới.");

            if (otp.CodeHash != HashOtp(request.OtpCode))
            {
                otp.Attempts++;
                await users.SaveOtpAsync(otp, ct);
                var remaining = MaxOtpAttempts - otp.Attempts;
                return AuthResult.Fail(AuthError.OtpInvalid, $"Mã OTP không chính xác. Bạn còn {remaining} lần thử.");
            }

            // OTP đúng -> Mở khóa thành công
            otp.UsedAt = DateTime.UtcNow;
            user.FailedLoginAttempts = 0;
            user.Status = UserStatuses.Active;
            await users.UpdateUserAsync(user, ct);

            return AuthResult.Ok("Mở khóa tài khoản thành công! Bạn có thể đăng nhập lại ngay bây giờ.");
        }

        public async Task<AuthResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);
            var user = await users.GetUserByEmail(email, ct);

            // Anti-enumeration: Luôn trả về thông báo thành công dù email có tồn tại hay không
            if (user != null && user.Status != UserStatuses.Locked)
            {
                var otpCode = GenerateOtpCode();
                await users.InvalidateOtpsAsync(user.Id, OtpPurposes.ResetPassword, ct);
                await users.SaveOtpAsync(new OtpToken
                {
                    UserId = user.Id,
                    Purpose = OtpPurposes.ResetPassword,
                    CodeHash = HashOtp(otpCode),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
                    Attempts = 0
                }, ct);

                await emailSender.SendPasswordResetEmailAsync(user.Email, user.FullName, otpCode, OtpExpiryMinutes, ct);
            }

            return AuthResult.Ok($"Nếu email tồn tại trong hệ thống, mã xác thực đặt lại mật khẩu đã được gửi đến hộp thư (hiệu lực {OtpExpiryMinutes} phút).");
        }

        public async Task<AuthResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < MinPasswordLength)
                return AuthResult.Fail(AuthError.Validation, $"Mật khẩu mới phải có tối thiểu {MinPasswordLength} ký tự.");

            if (request.NewPassword.Length > MaxPasswordLength)
                return AuthResult.Fail(AuthError.Validation, $"Mật khẩu mới không được vượt quá {MaxPasswordLength} ký tự.");

            var email = NormalizeEmail(request.Email);
            var user = await users.GetUserByEmail(email, ct);
            if (user == null)
                return AuthResult.Fail(AuthError.Validation, "Không tìm thấy thông tin tài khoản.");

            if (user.Status == UserStatuses.Locked)
                return AuthResult.Fail(AuthError.Locked, "Tài khoản của bạn đã bị khóa bởi Quản trị viên.");

            var otp = await users.GetLatestOtpAsync(user.Id, OtpPurposes.ResetPassword, ct);
            if (otp == null)
                return AuthResult.Fail(AuthError.OtpInvalid, "Không tìm thấy mã OTP đặt lại mật khẩu hợp lệ. Vui lòng yêu cầu gửi lại mã mới.");

            if (DateTime.UtcNow > otp.ExpiresAt)
                return AuthResult.Fail(AuthError.OtpExpired, $"Mã OTP đã hết hạn (chỉ có hiệu lực trong {OtpExpiryMinutes} phút). Vui lòng yêu cầu gửi lại mã mới.");

            if (otp.Attempts >= MaxOtpAttempts)
                return AuthResult.Fail(AuthError.OtpInvalid, $"Bạn đã nhập sai mã OTP quá {MaxOtpAttempts} lần. Vui lòng yêu cầu gửi lại mã mới.");

            if (otp.CodeHash != HashOtp(request.OtpCode))
            {
                otp.Attempts++;
                await users.SaveOtpAsync(otp, ct);
                var remaining = MaxOtpAttempts - otp.Attempts;
                return AuthResult.Fail(AuthError.OtpInvalid, $"Mã OTP không chính xác. Bạn còn {remaining} lần thử.");
            }

            // OTP đúng -> Cập nhật mật khẩu mới
            otp.UsedAt = DateTime.UtcNow;
            user.PasswordHash = hasher.Hash(request.NewPassword);
            user.FailedLoginAttempts = 0;
            if (user.Status == UserStatuses.TempLocked)
            {
                user.Status = UserStatuses.Active;
            }

            await users.UpdateUserAsync(user, ct);

            // [Bảo mật]: Thu hồi toàn bộ phiên đăng nhập cũ trên mọi thiết bị
            await users.RevokeAllSessionsAsync(user.Id, ct);

            return AuthResult.Ok("Đặt lại mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.");
        }

        internal static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        private static bool IsValidEmail(string email) =>
            email.Length <= MaxEmailLength && MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;

        internal static UserDto ToDto(User user) =>
            new(user.Id, user.Email, user.FullName, user.Role, user.Status, user.Phone);

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
            return user != null ? ToDto(user) : null;
        }

        public async Task<List<UserDto>> GetAllUsersAsync(CancellationToken ct = default)
        {
            var list = await users.GetAllUsersAsync(ct);
            return list.Select(ToDto).ToList();
        }
    }
}
