using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Auth
{
    public interface IEmailSender
    {
        /// <summary>Gửi mã OTP 6 số để kích hoạt email khi đăng ký mới (hiệu lực 10 phút).</summary>
        Task SendVerificationEmailAsync(string toEmail, string fullName, string otpCode, int expiryMinutes = 10, CancellationToken ct = default);

        /// <summary>Gửi mã OTP 6 số để đặt lại mật khẩu khi người dùng quên mật khẩu (hiệu lực 10 phút).</summary>
        Task SendPasswordResetEmailAsync(string toEmail, string fullName, string otpCode, int expiryMinutes = 10, CancellationToken ct = default);

        /// <summary>Gửi cảnh báo bảo mật và mã OTP mở khóa khi tài khoản bị tạm khóa do nhập sai mật khẩu 5 lần (hiệu lực 10 phút).</summary>
        Task SendAccountLockedEmailAsync(string toEmail, string fullName, string otpCode, int expiryMinutes = 10, CancellationToken ct = default);
    }
}
