using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Auth
{
    public interface IEmailSender
    {
        /// <summary>Gửi mã OTP 6 số để kích hoạt email khi đăng ký mới (hiệu lực 5 phút).</summary>
        Task SendVerificationEmailAsync(string toEmail, string fullName, string otpCode, int expiryMinutes = 5, CancellationToken ct = default);

        /// <summary>Gửi mã OTP 6 số để đặt lại mật khẩu khi người dùng quên mật khẩu (hiệu lực 5 phút).</summary>
        Task SendPasswordResetEmailAsync(string toEmail, string fullName, string otpCode, int expiryMinutes = 5, CancellationToken ct = default);

        /// <summary>Gửi email thông báo tài khoản tạm thời bị khóa 15 phút do nhập sai mật khẩu liên tiếp 5 lần (tự động mở sau 15 phút).</summary>
        Task SendAccountTempLockedNotificationAsync(string toEmail, string fullName, int lockoutMinutes = 15, CancellationToken ct = default);
    }
}
