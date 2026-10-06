using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyAbroad.Application.Auth;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Auth
{
    public class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
    {
        private readonly SmtpOptions _options = options.Value;

        public async Task SendVerificationEmailAsync(string toEmail, string fullName, string otpCode, int expiryMinutes = 10, CancellationToken ct = default)
        {
            var subject = "[USAS] Xác thực địa chỉ email đăng ký tài khoản";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">Chào mừng bạn đến với USAS!</h2>
                    <p style="font-size: 15px; color: #334155;">Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Cảm ơn bạn đã đăng ký tài khoản trên Hệ thống Hỗ trợ Ra quyết định Du học Mỹ (USAS). Để kích hoạt tài khoản, vui lòng sử dụng mã xác thực (OTP) bên dưới:</p>
                    <div style="background-color: #eff6ff; border: 2px dashed #3b82f6; border-radius: 8px; text-align: center; padding: 18px; margin: 24px 0;">
                        <span style="font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #1d4ed8;">{otpCode}</span>
                        <p style="margin: 8px 0 0; font-size: 13px; color: #64748b;">Mã có hiệu lực trong vòng <strong>{expiryMinutes} phút</strong>.</p>
                    </div>
                    <p style="font-size: 13px; color: #94a3b8; line-height: 1.5;">Nếu bạn không thực hiện đăng ký tài khoản này, vui lòng bỏ qua email này.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string fullName, string otpCode, int expiryMinutes = 10, CancellationToken ct = default)
        {
            var subject = "[USAS] Yêu cầu đặt lại mật khẩu";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #dc2626; margin-bottom: 16px;">Yêu cầu đặt lại mật khẩu</h2>
                    <p style="font-size: 15px; color: #334155;">Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản USAS của bạn. Vui lòng sử dụng mã xác thực bên dưới để thiết lập mật khẩu mới:</p>
                    <div style="background-color: #fef2f2; border: 2px dashed #ef4444; border-radius: 8px; text-align: center; padding: 18px; margin: 24px 0;">
                        <span style="font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #b91c1c;">{otpCode}</span>
                        <p style="margin: 8px 0 0; font-size: 13px; color: #64748b;">Mã có hiệu lực trong vòng <strong>{expiryMinutes} phút</strong>.</p>
                    </div>
                    <p style="font-size: 13px; color: #dc2626; line-height: 1.5;">⚠️ Nếu bạn không yêu cầu đặt lại mật khẩu, ai đó có thể đang cố truy cập tài khoản của bạn. Vui lòng bỏ qua email này hoặc liên hệ hỗ trợ.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendAccountTempLockedNotificationAsync(string toEmail, string fullName, int lockoutMinutes = 15, CancellationToken ct = default)
        {
            var subject = "[USAS] Cảnh báo bảo mật: Tài khoản tạm thời bị khóa 15 phút";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #fecaca; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #b91c1c; margin-bottom: 16px;">⚠️ Cảnh báo: Tài khoản tạm thời bị khóa</h2>
                    <p style="font-size: 15px; color: #334155;">Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Hệ thống phát hiện tài khoản của bạn đã bị <strong>nhập sai mật khẩu liên tiếp 5 lần</strong>. Để bảo vệ dữ liệu, tài khoản đã được tạm thời khóa trong <strong>{lockoutMinutes} phút</strong>.</p>
                    <div style="background-color: #fff7ed; border-left: 4px solid #f97316; padding: 14px; margin: 20px 0; border-radius: 4px;">
                        <p style="margin: 0; font-size: 14px; color: #9a3412; font-weight: 600;">Hệ thống sẽ TỰ ĐỘNG MỞ KHÓA tài khoản sau {lockoutMinutes} phút.</p>
                        <p style="margin: 6px 0 0; font-size: 13px; color: #7c2d12;">Bạn không cần thực hiện thêm thao tác mở khóa nào. Sau khi hết thời gian trên, bạn có thể đăng nhập lại bình thường.</p>
                    </div>
                    <p style="font-size: 13px; color: #64748b; line-height: 1.5;">Nếu đây không phải thao tác của bạn, có thể ai đó đang cố dò mật khẩu tài khoản của bạn. Vui lòng sử dụng tính năng <em>"Quên mật khẩu"</em> để đổi mật khẩu ngay sau khi tài khoản được tự động mở khóa.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_options.User) || string.IsNullOrWhiteSpace(_options.Password))
            {
                logger.LogWarning("[SMTP Warning] SMTP chưa được cấu hình thông tin đăng nhập trong .env (SMTP_USER/SMTP_PASSWORD). Email gửi tới {Email} với tiêu đề '{Subject}' đã được ghi nhận trong log hệ thống.", toEmail, subject);
                return;
            }

            try
            {
                using var client = new SmtpClient(_options.Host, _options.Port)
                {
                    EnableSsl = _options.EnableSsl,
                    Credentials = new NetworkCredential(_options.User.Trim(), _options.Password.Replace(" ", "").Trim()),
                    Timeout = 10000 // 10 giây
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(_options.FromEmail, _options.FromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(toEmail);

                await client.SendMailAsync(message, ct);
                logger.LogInformation("Đã gửi email thật thành công tới {Email} qua SMTP {Host}:{Port}", toEmail, _options.Host, _options.Port);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi gửi email SMTP tới {Email}: {Message}", toEmail, ex.Message);
            }
        }
    }
}
