using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Auth
{
    public enum TwoFactorResetError { None, NotFound, Validation }

    public record TwoFactorResetRequest(string Reason);

    public record TwoFactorResetResult(TwoFactorResetError Error, string? Message = null);

    /// <summary>[UC-58 2a] Administrator đặt lại xác thực hai lớp cho Moderator mất cả điện thoại lẫn mã khôi phục.</summary>
    public interface ITwoFactorResetService
    {
        Task<TwoFactorResetResult> ResetModeratorAsync(Guid targetUserId, TwoFactorResetRequest request, Guid actorAdminId,
            string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);
    }

    /// <summary>
    /// [UC-58 2a, UC-03 3d] Xóa khóa TOTP, vô hiệu mã khôi phục và phiếu bước 2 còn dở, thu hồi mọi phiên, ghi nhật ký và báo email.
    /// Lần đăng nhập kế tiếp Moderator thiết lập lại từ đầu (quét QR mới, nhận 10 mã khôi phục mới).
    /// Administrator mất cả hai do nhóm phát triển đặt lại bằng scripts/reset-admin-2fa.ps1, không qua đây.
    /// </summary>
    public class TwoFactorResetService(
        IUserRepository users,
        IAuditLogRepository auditLogs,
        IEmailSender emailSender,
        IUnitOfWork unitOfWork) : ITwoFactorResetService
    {
        public const int MaxReasonLength = 500;

        public async Task<TwoFactorResetResult> ResetModeratorAsync(Guid targetUserId, TwoFactorResetRequest request, Guid actorAdminId,
            string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
        {
            var reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length == 0)
                return new(TwoFactorResetError.Validation, "Lý do đặt lại không được để trống.");
            if (reason.Length > MaxReasonLength)
                return new(TwoFactorResetError.Validation, $"Lý do đặt lại tối đa {MaxReasonLength} ký tự.");

            var user = await users.GetUserById(targetUserId, ct);
            if (user is null || user.Status == UserStatuses.Managed || user.Status == UserStatuses.Deleted)
                return new(TwoFactorResetError.NotFound, "Không tìm thấy tài khoản.");
            if (user.Role != UserRoles.Moderator)
                return new(TwoFactorResetError.Validation, "Chỉ đặt lại xác thực hai lớp cho tài khoản Kiểm duyệt viên.");

            var hadTwoFactor = user.TotpEnabledAt != null;
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                user.TotpSecret = null;
                user.TotpEnabledAt = null;
                await users.UpdateUserAsync(user, token);
                await users.InvalidateOtpsAsync(user.Id, OtpPurposes.RecoveryCode, token);
                await users.InvalidateOtpsAsync(user.Id, OtpPurposes.LoginTwoFactor, token);
                await users.RevokeAllSessionsAsync(user.Id, token);
                await auditLogs.AddAsync(new AuditLog
                {
                    ActorUserId = actorAdminId,
                    Action = "user.two_factor_reset",
                    EntityType = "user",
                    EntityId = user.Id.ToString(),
                    DetailJson = JsonSerializer.Serialize(new { userId = user.Id, reason, hadTwoFactor }),
                    IpAddress = ipAddress,
                    UserAgent = userAgent
                }, token);
            }, ct);

            await emailSender.SendTwoFactorResetEmailAsync(user.Email, user.FullName, reason, ct);
            return new(TwoFactorResetError.None);
        }
    }
}
