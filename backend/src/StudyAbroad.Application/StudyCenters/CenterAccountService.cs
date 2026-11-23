using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Policies;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using static StudyAbroad.Application.Auth.AuthService;

namespace StudyAbroad.Application.StudyCenters;

/// <summary>
/// Tài khoản của trung tâm: [UC-64] tự đăng ký, [UC-91] xác thực email bằng OTP, đổi email liên lạc (OTP gửi tới email mới)
/// và đổi số điện thoại liên lạc (không xác thực SMS OTP). Tách khỏi AuthService vì đây là nghiệp vụ trung tâm;
/// dùng chung cơ chế OTP (<see cref="OtpRules"/>) và tạo phiên đăng nhập qua <see cref="IAuthService.StartSessionAsync"/>.
/// </summary>
public interface ICenterAccountService
{
    /// <summary>[UC-64] Trung tâm tự đăng ký: tạo tài khoản và trung tâm ở trạng thái chưa xác minh, gửi OTP tới email.</summary>
    /// <param name="ipAddress">IP người đăng ký, ghi kèm bằng chứng đồng ý điều khoản (UC-104).</param>
    Task<AuthResult> RegisterAsync(RegisterCenterRequest request, CancellationToken ct = default, string? ipAddress = null);
    /// <summary>[UC-91] Xác thực email đăng ký bằng OTP 6 số; đúng thì kích hoạt tài khoản và đăng nhập luôn.</summary>
    Task<AuthResult> VerifyEmailAsync(VerifyCenterEmailRequest request, CancellationToken ct = default);
    /// <summary>[UC-91] Gửi OTP tới email MỚI để đổi email liên lạc.</summary>
    Task<AuthResult> RequestEmailChangeAsync(Guid userId, RequestContactChangeDto request, CancellationToken ct = default);
    /// <summary>[UC-91] Xác nhận OTP và đổi email liên lạc (email đăng nhập giữ nguyên).</summary>
    Task<AuthResult> ConfirmEmailChangeAsync(Guid userId, ConfirmContactChangeDto request, CancellationToken ct = default);
    /// <summary>[UC-91] Đổi số điện thoại liên lạc: không xác thực SMS OTP, chỉ kiểm tra định dạng và trùng số.</summary>
    Task<AuthResult> UpdatePhoneAsync(Guid userId, RequestContactChangeDto request, CancellationToken ct = default);
}

public class CenterAccountService(
    IUserRepository users,
    IPasswordHasher hasher,
    IEmailSender emailSender,
    IAuditLogRepository auditLogs,
    IStudyCenterRepository studyCenters,
    IAuthService auth,
    // [UC-104] Đồng ý Điều khoản và Chính sách bảo mật lúc đăng ký, cùng giao dịch với việc tạo tài khoản; null trong test cũ thì bỏ qua
    IRegistrationConsent? registrationConsent = null,
    IUnitOfWork? unitOfWork = null) : ICenterAccountService
{
        public async Task<AuthResult> RegisterAsync(RegisterCenterRequest request, CancellationToken ct = default, string? ipAddress = null)
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

            // [UC-104] Phải đồng ý đúng bản Điều khoản và Chính sách bảo mật đang hiệu lực (kèm phiên bản đã đọc)
            if (registrationConsent != null && await registrationConsent.ValidateAsync(request.AcceptedPolicies, ct) is { } consentError)
                return AuthResult.Fail(AuthError.Validation, consentError);

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

            var otpCode = GenerateOtpCode();
            var otp = new OtpToken
            {
                UserId = user.Id,
                Purpose = OtpPurposes.VerifyEmail,
                CodeHash = HashOtp(otpCode),
                ExpiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes),
                Attempts = 0
            };

            // Tài khoản, trung tâm, bằng chứng đồng ý điều khoản và OTP cùng một giao dịch: lỗi giữa chừng thì không còn tài khoản
            // thiếu bằng chứng. Không có kho giao dịch (test cũ) thì chạy tuần tự như trước.
            async Task SaveAllAsync(CancellationToken token)
            {
                await users.AddAsync(user, token);
                await studyCenters.AddAsync(center, token);
                if (registrationConsent != null)
                    await registrationConsent.RecordAsync(user.Id, request.AcceptedPolicies, ipAddress, token);
                await users.SaveOtpAsync(otp, token);
            }
            if (unitOfWork != null) await unitOfWork.ExecuteInTransactionAsync(SaveAllAsync, ct);
            else await SaveAllAsync(ct);

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

        public async Task<AuthResult> VerifyEmailAsync(VerifyCenterEmailRequest request, CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);
            var user = await users.GetUserByEmail(email, ct);
            if (user == null || user.Status != UserStatuses.Unverified)
                return AuthResult.Fail(AuthError.OtpInvalid, OtpFailedMessage);

            if (!await OtpRules.TryConsumeAsync(users, user.Id, OtpPurposes.VerifyEmail, request.Code, ct))
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

            return await auth.StartSessionAsync(user, ct);
        }

        // ----- Đổi số điện thoại / email liên lạc của trung tâm (UC-91; email xác thực OTP tại giá trị mới) -----

        /// <summary>Gắn mã OTP với giá trị mới: mã gửi tới số A không dùng được để xác nhận số B.</summary>
        private static string BindOtp(string code, string target) => $"{code.Trim()}|{target}";

        private const string PhoneFormatMessage = "Số điện thoại không đúng định dạng Việt Nam (ví dụ: 0912345678 hoặc +84912345678).";

        private static string MaskPhone(string phone) =>
            phone.Length <= 4 ? phone : new string('*', phone.Length - 4) + phone[^4..];

        /// <summary>
        /// Trung tâm đổi số điện thoại liên lạc. Số điện thoại chỉ là thông tin liên hệ: không xác thực bằng SMS OTP,
        /// chỉ kiểm tra định dạng Việt Nam và không trùng tài khoản khác. Ghi nhật ký số cũ và số mới (đã che).
        /// </summary>
        public async Task<AuthResult> UpdatePhoneAsync(Guid userId, RequestContactChangeDto request, CancellationToken ct = default)
        {
            if (studyCenters is null)
                return AuthResult.Fail(AuthError.Validation, "Dịch vụ trung tâm tạm thời chưa sẵn sàng.");

            if (!TryValidateAndNormalizeVietnamesePhone(request.NewValue, out var normalized))
                return AuthResult.Fail(AuthError.Validation, PhoneFormatMessage);

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
                DetailJson = JsonSerializer.Serialize(new { oldPhone = oldPhone is null ? null : MaskPhone(oldPhone), newPhone = MaskPhone(normalized) })
            }, ct);

            return AuthResult.Ok("Đã đổi số điện thoại liên lạc.");
        }

        public async Task<AuthResult> RequestEmailChangeAsync(Guid userId, RequestContactChangeDto request, CancellationToken ct = default)
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

            if (!await OtpRules.CanSendAsync(users, userId, OtpPurposes.ChangeEmail, ct))
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

        public async Task<AuthResult> ConfirmEmailChangeAsync(Guid userId, ConfirmContactChangeDto request, CancellationToken ct = default)
        {
            if (studyCenters is null)
                return AuthResult.Fail(AuthError.Validation, "Dịch vụ trung tâm tạm thời chưa sẵn sàng.");

            var newEmail = NormalizeEmail(request.NewValue);
            if (string.IsNullOrWhiteSpace(newEmail))
                return AuthResult.Fail(AuthError.Validation, "Email không hợp lệ.");

            if (string.IsNullOrWhiteSpace(request.Code))
                return AuthResult.Fail(AuthError.Validation, "Vui lòng nhập mã OTP.");

            if (!await OtpRules.TryConsumeAsync(users, userId, OtpPurposes.ChangeEmail, BindOtp(request.Code, newEmail), ct))
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
