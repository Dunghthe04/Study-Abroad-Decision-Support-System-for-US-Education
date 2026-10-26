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

        /// <summary>Có người đăng ký lại bằng email đã có tài khoản: báo chủ email thay vì báo lỗi trên màn hình (chống dò tài khoản).</summary>
        Task SendAccountAlreadyExistsEmailAsync(string toEmail, string fullName, CancellationToken ct = default);

        /// <summary>Đăng ký bằng số điện thoại đã gắn với tài khoản khác: báo cho email vừa đăng ký, không tạo tài khoản.</summary>
        Task SendPhoneAlreadyInUseEmailAsync(string toEmail, CancellationToken ct = default);

        /// <summary>Mật khẩu vừa được đổi (quên mật khẩu hoặc đổi khi đã đăng nhập): báo để chủ tài khoản phát hiện nếu không phải mình làm.</summary>
        Task SendPasswordChangedNotificationAsync(string toEmail, string fullName, CancellationToken ct = default);

        /// <summary>Mời phụ huynh liên kết và đồng ý cho hệ thống xử lý dữ liệu của học sinh; link chứa token dùng một lần.</summary>
        Task SendParentInviteEmailAsync(string toEmail, string studentName, string inviteToken, int expiryDays, CancellationToken ct = default);
    }
}
