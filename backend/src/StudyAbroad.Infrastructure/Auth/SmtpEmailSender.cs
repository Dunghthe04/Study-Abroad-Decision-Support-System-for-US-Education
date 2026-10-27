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

        public async Task SendAccountAlreadyExistsEmailAsync(string toEmail, string fullName, CancellationToken ct = default)
        {
            var subject = "[USAS] Email của bạn đã có tài khoản";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">Bạn đã có tài khoản USAS</h2>
                    <p style="font-size: 15px; color: #334155;">Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Vừa có yêu cầu đăng ký tài khoản mới bằng địa chỉ email này, nhưng email đã được dùng cho một tài khoản USAS nên hệ thống không tạo thêm tài khoản.</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Bạn có thể đăng nhập bằng mật khẩu hiện tại, hoặc dùng chức năng <em>"Quên mật khẩu"</em> nếu không nhớ mật khẩu.</p>
                    <p style="font-size: 13px; color: #94a3b8; line-height: 1.5;">Nếu không phải bạn gửi yêu cầu này, vui lòng bỏ qua email.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendPhoneAlreadyInUseEmailAsync(string toEmail, CancellationToken ct = default)
        {
            var subject = "[USAS] Chưa thể tạo tài khoản";
            var body = """
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #b45309; margin-bottom: 16px;">Chưa thể tạo tài khoản</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Vừa có yêu cầu đăng ký tài khoản USAS bằng địa chỉ email này, nhưng số điện thoại đi kèm đã được dùng cho một tài khoản khác nên hệ thống không tạo tài khoản.</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Vui lòng đăng ký lại với số điện thoại khác hoặc để trống số điện thoại.</p>
                    <p style="font-size: 13px; color: #94a3b8; line-height: 1.5;">Nếu không phải bạn gửi yêu cầu này, vui lòng bỏ qua email.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendPasswordChangedNotificationAsync(string toEmail, string fullName, CancellationToken ct = default)
        {
            var subject = "[USAS] Mật khẩu tài khoản vừa được thay đổi";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #fecaca; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #b91c1c; margin-bottom: 16px;">Mật khẩu đã được thay đổi</h2>
                    <p style="font-size: 15px; color: #334155;">Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Mật khẩu tài khoản USAS của bạn vừa được thay đổi lúc <strong>{DateTime.UtcNow:HH:mm dd/MM/yyyy} (UTC)</strong>. Các phiên đăng nhập trên thiết bị khác đã bị đăng xuất.</p>
                    <p style="font-size: 13px; color: #dc2626; line-height: 1.5;">⚠️ Nếu không phải bạn thực hiện, hãy dùng chức năng <em>"Quên mật khẩu"</em> để đặt lại mật khẩu ngay và liên hệ ban quản trị.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendParentInviteEmailAsync(string toEmail, string studentName, string inviteToken, int expiryDays, CancellationToken ct = default)
        {
            var link = $"{_options.AppBaseUrl.TrimEnd('/')}/parent-consent?token={Uri.EscapeDataString(inviteToken)}";
            var subject = "[USAS] Mời phụ huynh liên kết tài khoản học sinh";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">Lời mời liên kết tài khoản học sinh</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Học sinh <strong>{WebUtility.HtmlEncode(studentName)}</strong> vừa tạo tài khoản trên Hệ thống Hỗ trợ Ra quyết định Du học Mỹ (USAS) và khai địa chỉ email này là email của phụ huynh.</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Theo Luật Bảo vệ dữ liệu cá nhân, hệ thống cần cha mẹ hoặc người giám hộ đồng ý trước khi lưu và phân tích hồ sơ học tập của học sinh dưới 16 tuổi. Phụ huynh có thể xem nội dung đồng ý, chấp nhận liên kết và rút lại sự đồng ý bất cứ lúc nào.</p>
                    <div style="text-align: center; margin: 24px 0;">
                        <a href="{WebUtility.HtmlEncode(link)}" style="display: inline-block; background-color: #2563eb; color: #ffffff; text-decoration: none; font-weight: 600; padding: 12px 24px; border-radius: 8px;">Xem và xác nhận</a>
                        <p style="margin: 8px 0 0; font-size: 13px; color: #64748b;">Link có hiệu lực trong <strong>{expiryDays} ngày</strong> và chỉ dùng được một lần.</p>
                    </div>
                    <p style="font-size: 13px; color: #94a3b8; line-height: 1.5;">Nếu bạn không phải phụ huynh của học sinh này, vui lòng bỏ qua email; hệ thống không liên kết khi bạn chưa xác nhận.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendParentLinkChangedEmailAsync(string toEmail, string studentName, string parentLabel, bool linked, CancellationToken ct = default)
        {
            var student = WebUtility.HtmlEncode(studentName);
            var parent = WebUtility.HtmlEncode(parentLabel);
            var subject = linked
                ? "[USAS] Phụ huynh đã xác nhận liên kết tài khoản"
                : "[USAS] Liên kết phụ huynh đã bị hủy";
            var title = linked ? "Liên kết phụ huynh đã được xác nhận" : "Liên kết phụ huynh đã bị hủy";
            var content = linked
                ? $"Phụ huynh <strong>{parent}</strong> đã đồng ý cho hệ thống xử lý dữ liệu và liên kết với tài khoản học sinh <strong>{student}</strong>."
                : $"Liên kết giữa phụ huynh <strong>{parent}</strong> và học sinh <strong>{student}</strong> đã bị hủy; sự đồng ý xử lý dữ liệu (nếu có) đã được rút lại.";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">{title}</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">{content}</p>
                    <p style="font-size: 13px; color: #94a3b8; line-height: 1.5;">Bạn có thể xem trạng thái liên kết trong trang Tài khoản. Nếu không phải bạn hoặc người thân thực hiện, vui lòng đăng nhập và kiểm tra lại.</p>
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
                    Credentials = new NetworkCredential(_options.User, _options.Password),
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
