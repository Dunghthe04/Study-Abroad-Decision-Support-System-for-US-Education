using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Auth
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
        Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);
        Task<bool> LogoutAsync(string rawToken, CancellationToken ct = default);
        Task<UserDto?> GetMeAsync(Guid userId, CancellationToken ct = default);
        Task<List<UserDto>> GetAllUsersAsync(CancellationToken ct = default);
    }

    public class AuthService(IUserRepository users, IPasswordHasher hasher, ITokenService tokens) : IAuthService
    {
        public const int MinPasswordLength = 8;
        public const int MaxPasswordLength = 100;
        public const int MaxFullNameLength = 100;
        public const int MaxEmailLength = 100;
        public const int MaxRawPhoneLength = 20;

        public static bool TryValidateAndNormalizeVietnamesePhone(string? rawPhone, out string normalizedPhone)
        {
            normalizedPhone = string.Empty;
            if (string.IsNullOrWhiteSpace(rawPhone))
                return false;

            // Loại bỏ khoảng trắng, dấu gạch ngang, chấm, ngoặc
            var cleaned = Regex.Replace(rawPhone.Trim(), @"[\s\.\-\(\)]", "");

            // Chuẩn hóa +84 hoặc 84 về đầu 0
            if (cleaned.StartsWith("+84"))
                cleaned = "0" + cleaned[3..];
            else if (cleaned.StartsWith("84") && cleaned.Length == 11)
                cleaned = "0" + cleaned[2..];

            // Định dạng số điện thoại di động Việt Nam: 10 chữ số, đầu số 03, 05, 07, 08, 09
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

            // 1. Validate: chống tràn độ dài (DoS/Buffer overflow) và kiểm tra định dạng
            if (string.IsNullOrEmpty(email) || email.Length > MaxEmailLength || !IsValidEmail(email))
                return AuthResult.Fail(AuthError.Validation, $"Email không hợp lệ hoặc vượt quá {MaxEmailLength} ký tự.");

            if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
                return AuthResult.Fail(AuthError.Validation, $"Mật khẩu tối thiểu {MinPasswordLength} ký tự.");

            if (request.Password.Length > MaxPasswordLength)
                return AuthResult.Fail(AuthError.Validation, $"Mật khẩu không được vượt quá {MaxPasswordLength} ký tự.");

            if (fullName.Length == 0 || fullName.Length > MaxFullNameLength)
                return AuthResult.Fail(AuthError.Validation, $"Họ tên không được để trống và tối đa {MaxFullNameLength} ký tự.");

            // Kiểm tra số điện thoại (chỉ chấp nhận số điện thoại di động Việt Nam)
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

            // 2. Kiểm tra trùng Email hoặc SĐT (Tiêu chí 1: trùng thì báo lỗi)
            if (await users.EmailExistAsync(email, ct))
                return AuthResult.Fail(AuthError.EmailTaken, "Email đã được sử dụng.");

            if (!string.IsNullOrEmpty(normalizedPhone) && await users.PhoneExistAsync(normalizedPhone, ct))
                return AuthResult.Fail(AuthError.PhoneTaken, "Số điện thoại đã được sử dụng.");

            // 3. Tạo tài khoản mới: Trung tâm (center) cần chờ duyệt (Pending), học sinh/phụ huynh thì Active
            var user = new User
            {
                Email = email,
                PasswordHash = hasher.Hash(request.Password),
                FullName = fullName,
                Role = request.Role,
                Status = request.Role == UserRoles.Center ? UserStatuses.Pending : UserStatuses.Active,
                ParentAcknowledged = request.ParentAcknowledged == true,
                Phone = normalizedPhone
            };
            await users.AddAsync(user, ct);
            return AuthResult.Ok(ToDto(user));
        }

        internal static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        private static bool IsValidEmail(string email) =>
            email.Length <= MaxEmailLength && MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;

        internal static UserDto ToDto(User user) =>
            new(user.Id, user.Email, user.FullName, user.Role, user.Status, user.Phone);

        public async Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(request.Email) || request.Email.Length > MaxEmailLength ||
                string.IsNullOrEmpty(request.Password) || request.Password.Length > MaxPasswordLength)
            {
                return AuthResult.Fail(AuthError.InvalidCredentials, "Sai tài khoản hoặc mật khẩu.");
            }

            var user = await users.GetUserByEmail(NormalizeEmail(request.Email), ct);

            // [AC-2] Nếu không có user hoặc sai mật khẩu: chỉ báo lỗi chung (anti-user enumeration)
            if (user == null || !hasher.VerifyPassword(user.PasswordHash, request.Password ?? string.Empty))
            {
                return AuthResult.Fail(AuthError.InvalidCredentials, "Sai tài khoản hoặc mật khẩu.");
            }

            if (user.Status == UserStatuses.Locked)
            {
                return AuthResult.Fail(AuthError.Locked, "Tài khoản đã bị khóa.");
            }

            // [AC-3] Sinh JWT Token và lưu phiên vào DB để quản lý thu hồi
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

        // [AC-3] Đăng xuất: thu hồi phiên trên server (đánh dấu IsRevoked = true)
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
