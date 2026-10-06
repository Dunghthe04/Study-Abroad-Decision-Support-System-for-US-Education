using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Auth
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
        Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);
        Task<bool> LogoutAsync(string rawToken, CancellationToken ct = default);
        Task<UserDto?> GetMeAsync(Guid userId, CancellationToken ct = default);
    }

    public class AuthService(IUserRepository users, IPasswordHasher hasher, ITokenService tokens) : IAuthService
    {
        private const int MinPasswordLength = 8;
        private const int MaxFullNameLength = 128;
        private const int MaxPhoneLength = 32;

        public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);
            var fullName = request.FullName?.Trim() ?? string.Empty;
            var phone = request.Phone?.Trim();

            // 1. Validate: gặp lỗi đầu tiên là trả về ngay
            if (!IsValidEmail(email))
                return AuthResult.Fail(AuthError.Validation, "Email không hợp lệ.");
            if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
                return AuthResult.Fail(AuthError.Validation, $"Mật khẩu tối thiểu {MinPasswordLength} ký tự.");
            if (fullName.Length == 0 || fullName.Length > MaxFullNameLength)
                return AuthResult.Fail(AuthError.Validation, $"Họ tên không được để trống và tối đa {MaxFullNameLength} ký tự.");
            if (!string.IsNullOrEmpty(phone) && phone.Length > MaxPhoneLength)
                return AuthResult.Fail(AuthError.Validation, $"Số điện thoại không được vượt quá {MaxPhoneLength} ký tự.");
            if (!UserRoles.SelfRegister.Contains(request.Role))
                return AuthResult.Fail(AuthError.Validation, $"Vai trò '{request.Role}' không hợp lệ.");
            if (request.Role == UserRoles.Student && request.ParentAcknowledged != true)
                return AuthResult.Fail(AuthError.Validation, "Học sinh cần xác nhận phụ huynh đã biết.");

            // 2. Email tồn tại
            if (await users.EmailExistAsync(email, ct))
                return AuthResult.Fail(AuthError.EmailTaken, "Email đã được sử dụng.");

            // 3. Tạo tài khoản mới
            var user = new User
            {
                Email = email,
                PasswordHash = hasher.Hash(request.Password),
                FullName = fullName,
                Role = request.Role,
                Status = request.Role == UserRoles.Center ? UserStatuses.Pending : UserStatuses.Active,
                ParentAcknowledged = request.ParentAcknowledged == true,
                Phone = string.IsNullOrEmpty(phone) ? null : phone
            };
            await users.AddAsync(user, ct);
            return AuthResult.Ok(ToDto(user));
        }

        internal static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        private static bool IsValidEmail(string email) =>
            email.Length <= 256 && MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;

        internal static UserDto ToDto(User user) =>
            new(user.Id, user.Email, user.FullName, user.Role, user.Status, user.Phone);

        public async Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
        {
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
    } 

}
