using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Billing;
using System;
using System.Globalization;
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

        public async Task SendAccountTempLockedNotificationAsync(string toEmail, string fullName, int lockoutMinutes = 0, CancellationToken ct = default)
        {
            var subject = "[USAS] Cảnh báo bảo mật: Tài khoản đã bị khóa do nhập sai mật khẩu";
            // [UC-03 E2] Khóa tạm có thời hạn thì tự mở; Quản trị viên vẫn mở sớm được
            var unlockNote = lockoutMinutes > 0
                ? $"Tài khoản sẽ <strong>tự động mở khóa sau {lockoutMinutes} phút</strong>. Nếu cần mở sớm, vui lòng liên hệ <strong>Quản trị viên hệ thống</strong>."
                : "Vui lòng liên hệ <strong>Quản trị viên hệ thống</strong> để được hỗ trợ xác minh danh tính và mở khóa tài khoản.";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #fecaca; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #b91c1c; margin-bottom: 16px;">⚠️ Cảnh báo: Tài khoản đã bị khóa</h2>
                    <p style="font-size: 15px; color: #334155;">Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Hệ thống phát hiện tài khoản của bạn đã bị <strong>nhập sai mật khẩu liên tiếp 5 lần</strong>. Để bảo vệ dữ liệu và an toàn tài khoản, hệ thống đã chuyển tài khoản sang trạng thái khóa.</p>
                    <div style="background-color: #fff7ed; border-left: 4px solid #f97316; padding: 14px; margin: 20px 0; border-radius: 4px;">
                        <p style="margin: 0; font-size: 14px; color: #9a3412; font-weight: 600;">Thời gian khóa:</p>
                        <p style="margin: 6px 0 0; font-size: 13px; color: #7c2d12;">{unlockNote}</p>
                    </div>
                    <p style="font-size: 13px; color: #64748b; line-height: 1.5;">Nếu đây không phải thao tác của bạn, có thể tài khoản của bạn đang có nguy cơ bị xâm nhập. Vui lòng thông báo ngay cho ban quản trị khi yêu cầu mở khóa.</p>
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

        // [UC-115] Phụ huynh không cần tài khoản: link một lần là xác thực. Hai nút cùng dẫn tới trang xác nhận
        // (không đổi trạng thái chỉ bằng mở link, tránh máy quét link trong hộp thư tự bấm).
        public async Task SendParentInviteEmailAsync(string toEmail, string studentName, string inviteToken, int expiryDays, bool isConsentRequest, CancellationToken ct = default)
        {
            var link = $"{_options.AppBaseUrl.TrimEnd('/')}/parent-consent?token={Uri.EscapeDataString(inviteToken)}";
            var rejectLink = link + "&choice=reject";
            var student = WebUtility.HtmlEncode(studentName);
            if (!isConsentRequest)
            {
                await SendParentLinkInviteEmailAsync(toEmail, student, link, rejectLink, expiryDays, ct);
                return;
            }
            var subject = "[USAS] Phụ huynh xác nhận đồng ý cho con dùng USAS";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">Xác nhận đồng ý của phụ huynh</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Học sinh <strong>{student}</strong> vừa tạo tài khoản trên Hệ thống Hỗ trợ Ra quyết định Du học Mỹ (USAS) và khai địa chỉ email này là email của phụ huynh.</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Theo Luật Bảo vệ dữ liệu cá nhân, hệ thống cần cha mẹ hoặc người giám hộ đồng ý trước khi lưu và phân tích hồ sơ học tập của học sinh dưới 16 tuổi. Bạn không cần tạo tài khoản để trả lời.</p>
                    <p style="font-size: 14px; color: #0f172a; font-weight: 600; margin-bottom: 4px;">Dữ liệu được xử lý</p>
                    <ul style="font-size: 14px; color: #475569; line-height: 1.6; margin-top: 0; padding-left: 20px;">
                        <li>Họ tên, ngày sinh, email, số điện thoại của con.</li>
                        <li>Hồ sơ học thuật: trường, lớp, bảng điểm, chứng chỉ, hoạt động ngoại khóa.</li>
                        <li>Thông tin tài chính du học do gia đình tự khai (nếu nhập).</li>
                        <li>Nội dung con trao đổi với trợ lý tư vấn AI.</li>
                    </ul>
                    <p style="font-size: 14px; color: #0f172a; font-weight: 600; margin-bottom: 4px;">Mục đích</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6; margin-top: 0;">Lưu hồ sơ, phân tích năng lực và gợi ý trường phù hợp cho việc du học Mỹ. Không bán hay chia sẻ dữ liệu cho bên thứ ba; hồ sơ chỉ gửi cho trung tâm tư vấn khi bạn xác nhận riêng từng lần.</p>
                    <div style="text-align: center; margin: 24px 0;">
                        <a href="{WebUtility.HtmlEncode(link)}" style="display: inline-block; background-color: #2563eb; color: #ffffff; text-decoration: none; font-weight: 600; padding: 12px 24px; border-radius: 8px; margin: 4px;">Đồng ý</a>
                        <a href="{WebUtility.HtmlEncode(rejectLink)}" style="display: inline-block; background-color: #ffffff; color: #b91c1c; text-decoration: none; font-weight: 600; padding: 11px 23px; border-radius: 8px; border: 1px solid #fca5a5; margin: 4px;">Từ chối</a>
                        <p style="margin: 8px 0 0; font-size: 13px; color: #64748b;">Hai nút mở trang xác nhận để bạn đọc lại và bấm chọn. Link có hiệu lực trong <strong>{expiryDays} ngày</strong> và chỉ trả lời được một lần.</p>
                    </div>
                    <p style="font-size: 13px; color: #475569; line-height: 1.5;">Sau khi đồng ý, bạn nhận thêm một email xác nhận có link <strong>Rút đồng ý</strong>: bấm link đó bất cứ lúc nào để hệ thống ngừng xử lý dữ liệu của con, không cần đăng nhập.</p>
                    <p style="font-size: 13px; color: #94a3b8; line-height: 1.5;">Nếu bạn không phải phụ huynh của em {student}, hãy bỏ qua email này; hệ thống không xử lý dữ liệu của em khi chưa có phụ huynh đồng ý.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        // [UC-109] Học sinh 16–17 tự đồng ý được và đã dùng hệ thống: thư chỉ mời phụ huynh liên kết để theo dõi,
        // không có câu luật, không có văn bản đồng ý xử lý dữ liệu, không có link rút đồng ý
        private async Task SendParentLinkInviteEmailAsync(string toEmail, string student, string link, string rejectLink, int expiryDays, CancellationToken ct)
        {
            var subject = "[USAS] Lời mời liên kết tài khoản với con";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">Lời mời liên kết tài khoản với con</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Học sinh <strong>{student}</strong> đang dùng Hệ thống Hỗ trợ Ra quyết định Du học Mỹ (USAS) và khai địa chỉ email này là email của phụ huynh.</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Em đã đủ 16 tuổi nên tự đồng ý và đã dùng được hệ thống. Khi chấp nhận liên kết, bạn theo dõi được hồ sơ du học và các gợi ý trường của con. Bạn không cần tạo tài khoản để trả lời.</p>
                    <div style="text-align: center; margin: 24px 0;">
                        <a href="{WebUtility.HtmlEncode(link)}" style="display: inline-block; background-color: #2563eb; color: #ffffff; text-decoration: none; font-weight: 600; padding: 12px 24px; border-radius: 8px; margin: 4px;">Chấp nhận liên kết</a>
                        <a href="{WebUtility.HtmlEncode(rejectLink)}" style="display: inline-block; background-color: #ffffff; color: #b91c1c; text-decoration: none; font-weight: 600; padding: 11px 23px; border-radius: 8px; border: 1px solid #fca5a5; margin: 4px;">Từ chối</a>
                        <p style="margin: 8px 0 0; font-size: 13px; color: #64748b;">Hai nút mở trang xác nhận để bạn đọc lại và bấm chọn. Link có hiệu lực trong <strong>{expiryDays} ngày</strong> và chỉ trả lời được một lần.</p>
                    </div>
                    <p style="font-size: 13px; color: #94a3b8; line-height: 1.5;">Nếu bạn không phải phụ huynh của em {student}, hãy bỏ qua email này.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendCenterVerificationOutcomeEmailAsync(string toEmail, string centerName, string outcome, string? reason, IReadOnlyList<string>? unmetConditions, CancellationToken ct = default)
        {
            var center = WebUtility.HtmlEncode(centerName);
            var (subject, title, content) = outcome switch
            {
                "verified" => ("[USAS] Trung tâm đã được xác minh", "Trung tâm đã được xác minh",
                    $"Hồ sơ xác minh của <strong>{center}</strong> đã được duyệt. Trung tâm nay có dấu xác minh, xuất hiện ở đầu danh bạ và nhận được yêu cầu liên hệ từ học sinh, phụ huynh."),
                "submitted" => ("[USAS] Đã nhận hồ sơ xác minh", "Đã nhận hồ sơ xác minh",
                    $"USAS đã nhận hồ sơ xác minh của <strong>{center}</strong>. Kiểm duyệt viên sẽ xem xét và báo kết quả qua email này. Trong lúc chờ duyệt, bạn xem được hồ sơ nhưng chưa sửa được tên pháp lý và mã số thuế."),
                "changes_requested" => ("[USAS] Hồ sơ xác minh cần bổ sung", "Hồ sơ xác minh cần bổ sung",
                    $"Hồ sơ xác minh của <strong>{center}</strong> chưa đủ để duyệt. Vui lòng bổ sung theo góp ý bên dưới rồi nộp lại."),
                "revoked" => ("[USAS] Dấu xác minh của trung tâm đã bị thu hồi", "Dấu xác minh đã bị thu hồi",
                    $"Dấu xác minh của <strong>{center}</strong> đã bị thu hồi; trung tâm tạm ngừng nhận yêu cầu liên hệ mới."),
                _ => ("[USAS] Hồ sơ xác minh chưa được duyệt", "Hồ sơ xác minh chưa được duyệt",
                    $"Hồ sơ xác minh của <strong>{center}</strong> chưa được duyệt. Bạn có thể sửa hồ sơ và nộp lại."),
            };

            var detail = string.Empty;
            if (unmetConditions is { Count: > 0 })
                detail += "<p style=\"font-size: 14px; color: #475569; margin-bottom: 4px;\"><strong>Điều kiện chưa đạt:</strong></p><ul style=\"font-size: 14px; color: #475569; line-height: 1.6;\">"
                          + string.Concat(unmetConditions.Select(c => $"<li>{WebUtility.HtmlEncode(c)}</li>")) + "</ul>";
            if (!string.IsNullOrWhiteSpace(reason))
                detail += $"<p style=\"font-size: 14px; color: #475569; line-height: 1.6;\"><strong>Lý do:</strong> {WebUtility.HtmlEncode(reason)}</p>";

            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">{title}</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">{content}</p>
                    {detail}
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendCenterDeletionOutcomeEmailAsync(string toEmail, string centerName, string outcome, string? note, DateTime? purgeAt, CancellationToken ct = default)
        {
            var center = WebUtility.HtmlEncode(centerName);
            // Ngày hiển thị theo giờ Việt Nam
            var purgeDate = purgeAt?.AddHours(7).ToString("dd/MM/yyyy");
            var (subject, title, content) = outcome switch
            {
                "received" => ("[USAS] Đã nhận yêu cầu xóa tài khoản trung tâm", "Đã nhận yêu cầu xóa",
                    $"USAS đã nhận yêu cầu xóa tài khoản và dữ liệu của <strong>{center}</strong>. Yêu cầu sẽ được xử lý chậm nhất vào ngày <strong>{purgeDate}</strong> (20 ngày kể từ khi nhận). Chúng tôi sẽ gửi email khi có kết quả."),
                "extended" => ("[USAS] Gia hạn thời gian xử lý yêu cầu xóa tài khoản trung tâm", "Đã gia hạn thời gian xử lý",
                    $"Thời gian xử lý yêu cầu xóa tài khoản và dữ liệu của <strong>{center}</strong> được gia hạn một lần đến ngày <strong>{purgeDate}</strong>."),
                "held" => ("[USAS] Yêu cầu xóa tài khoản trung tâm chưa thể thực hiện", "Yêu cầu xóa chưa thể thực hiện",
                    $"Yêu cầu xóa tài khoản và dữ liệu của <strong>{center}</strong> hiện chưa thể hoàn tất do USAS cần hoàn thành một số thủ tục xử lý nội bộ theo quy định. Trong thời gian này, tài khoản và dữ liệu được giữ nguyên. Nếu cần bổ sung thông tin, chúng tôi sẽ liên hệ với bạn."),
                "resumed" => ("[USAS] Yêu cầu xóa tài khoản trung tâm đang được xử lý tiếp", "Yêu cầu xóa đang được xử lý tiếp",
                    $"Yêu cầu xóa tài khoản và dữ liệu của <strong>{center}</strong> đang được xử lý tiếp. Chúng tôi sẽ gửi email khi có kết quả."),
                "approved" => ("[USAS] Yêu cầu xóa tài khoản trung tâm đã được duyệt", "Yêu cầu xóa đã được duyệt",
                    $"Yêu cầu xóa tài khoản và dữ liệu của <strong>{center}</strong> đã được duyệt. Tài khoản đã bị khóa, trung tâm không còn hiển thị và các yêu cầu liên hệ đang mở đã được đóng. Dữ liệu sẽ được xóa hẳn vào ngày <strong>{purgeDate}</strong>."),
                "completed_partial" => ("[USAS] Đã xóa tài khoản trung tâm, còn một phần dữ liệu chưa xóa được", "Đã xóa phần lớn dữ liệu trung tâm",
                    $"Tài khoản và dữ liệu của <strong>{center}</strong> đã được xóa khỏi USAS theo yêu cầu của bạn, trừ một số nội dung trao đổi hiện chưa thể xóa vì USAS cần hoàn thành một số thủ tục xử lý nội bộ theo quy định. Phần này đã được tách khỏi thông tin nhận diện của bạn và sẽ được xóa khi thủ tục hoàn tất."),
                "completed" => ("[USAS] Đã xóa tài khoản và dữ liệu trung tâm", "Đã xóa dữ liệu trung tâm",
                    $"Tài khoản và dữ liệu của <strong>{center}</strong> đã được xóa khỏi USAS theo yêu cầu của bạn."),
                _ => ("[USAS] Yêu cầu xóa tài khoản trung tâm chưa được chấp nhận", "Yêu cầu xóa chưa được chấp nhận",
                    $"Yêu cầu xóa tài khoản và dữ liệu của <strong>{center}</strong> chưa được chấp nhận. Tài khoản vẫn hoạt động bình thường."),
            };
            var label = outcome switch { "rejected" => "Lý do", "extended" => "Lý do gia hạn", _ => "Ghi chú" };
            var detail = string.IsNullOrWhiteSpace(note) || outcome == "approved"
                ? string.Empty
                : $"<p style=\"font-size: 14px; color: #475569; line-height: 1.6;\"><strong>{label}:</strong> {WebUtility.HtmlEncode(note)}</p>";

            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">{title}</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">{content}</p>
                    {detail}
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

        public async Task SendParentConsentConfirmedEmailAsync(string toEmail, string studentName, string revokeToken, CancellationToken ct = default)
        {
            var baseUrl = _options.AppBaseUrl.TrimEnd('/');
            var link = $"{baseUrl}/parent-consent/revoke?token={Uri.EscapeDataString(revokeToken)}";
            var student = WebUtility.HtmlEncode(studentName);
            var subject = "[USAS] Xác nhận bạn đã đồng ý cho học sinh sử dụng hệ thống";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">Bạn đã đồng ý cho học sinh sử dụng hệ thống</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Bạn đã đồng ý cho Hệ thống Hỗ trợ Ra quyết định Du học Mỹ (USAS) lưu và phân tích hồ sơ học tập của học sinh <strong>{student}</strong>. Hãy giữ lại email này.</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Nếu muốn rút lại sự đồng ý, bấm nút bên dưới, không cần đăng nhập. Sau khi rút, học sinh sẽ tạm không dùng được các chức năng cần hồ sơ cho đến khi có phụ huynh đồng ý lại.</p>
                    <div style="text-align: center; margin: 24px 0;">
                        <a href="{WebUtility.HtmlEncode(link)}" style="display: inline-block; background-color: #dc2626; color: #ffffff; text-decoration: none; font-weight: 600; padding: 12px 24px; border-radius: 8px;">Rút đồng ý</a>
                        <p style="margin: 8px 0 0; font-size: 13px; color: #64748b;">Link dùng được một lần và hết tác dụng khi học sinh đủ 16 tuổi.</p>
                    </div>
                    <p style="font-size: 13px; color: #94a3b8; line-height: 1.5;">Bạn cũng có thể rút đồng ý bất cứ lúc nào bằng cách đăng nhập vào <a href="{WebUtility.HtmlEncode(baseUrl)}" style="color: #2563eb;">USAS</a> và mở trang Tài khoản.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendParentConsentRejectedEmailAsync(string toEmail, string studentName, string parentLabel, CancellationToken ct = default)
        {
            var student = WebUtility.HtmlEncode(studentName);
            var parent = WebUtility.HtmlEncode(parentLabel);
            var subject = "[USAS] Phụ huynh đã từ chối lời mời";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">Phụ huynh đã từ chối lời mời</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Chào <strong>{student}</strong>, phụ huynh <strong>{parent}</strong> đã từ chối cho hệ thống xử lý dữ liệu học tập của bạn. Bạn tạm chưa dùng được các chức năng cần hồ sơ.</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Nếu phụ huynh bấm nhầm, bạn có thể gửi lại lời mời hoặc khai email phụ huynh khác trong trang Tài khoản.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendParentAccountLinkedEmailAsync(string toEmail, string studentName, string parentName, CancellationToken ct = default)
        {
            var student = WebUtility.HtmlEncode(studentName);
            var parent = WebUtility.HtmlEncode(parentName);
            var subject = "[USAS] Phụ huynh đã liên kết tài khoản";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">Phụ huynh đã liên kết tài khoản</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Chào <strong>{student}</strong>, phụ huynh <strong>{parent}</strong> (người đã đồng ý qua email) vừa tạo tài khoản Phụ huynh trên USAS. Hệ thống đã liên kết tài khoản này với bạn.</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Sự đồng ý xử lý dữ liệu giữ nguyên. Phụ huynh có thể xem liên kết và rút đồng ý trong trang Tài khoản.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        // [UC-75 1a / UC-111 3a] Ngày xóa tính theo giờ Việt Nam
        public async Task SendChildAccountDeletedByParentEmailAsync(string toEmail, string studentName, DateTime deletionDateUtc, CancellationToken ct = default)
        {
            var student = WebUtility.HtmlEncode(studentName);
            var date = deletionDateUtc.AddHours(7).ToString("dd/MM/yyyy");
            var subject = "[USAS] Người đại diện đã yêu cầu xóa tài khoản của bạn";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #dc2626; margin-bottom: 16px;">Tài khoản của bạn đang chờ xóa</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Chào <strong>{student}</strong>, người đại diện của bạn (cha, mẹ hoặc người giám hộ) đã rút lại sự đồng ý và yêu cầu xóa tài khoản USAS của bạn. Bạn đã bị đăng xuất khỏi mọi thiết bị.</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Tài khoản và dữ liệu sẽ bị xóa vĩnh viễn vào ngày <strong>{date}</strong>. Vì bạn chưa đủ 16 tuổi nên không tự khôi phục được; nếu muốn giữ tài khoản, hãy nhờ người đại diện bấm nút khôi phục trong email hệ thống đã gửi cho họ.</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Nếu bạn đủ 16 tuổi trước ngày xóa, bạn có thể đăng nhập để tự khôi phục.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        // [UC-75 4b / UC-109 3b] Hai nút dùng một lần, hiệu lực đến ngày xóa vĩnh viễn
        public async Task SendParentChildDeletionConfirmedEmailAsync(string toEmail, string studentName, DateTime deletionDateUtc, string reconsentToken, string restoreOnlyToken, CancellationToken ct = default)
        {
            var baseUrl = _options.AppBaseUrl.TrimEnd('/');
            var reconsentLink = $"{baseUrl}/parent-consent/restore?token={Uri.EscapeDataString(reconsentToken)}";
            var restoreOnlyLink = $"{baseUrl}/parent-consent/restore?token={Uri.EscapeDataString(restoreOnlyToken)}";
            var student = WebUtility.HtmlEncode(studentName);
            var date = deletionDateUtc.AddHours(7).ToString("dd/MM/yyyy");
            var subject = "[USAS] Xác nhận bạn đã rút đồng ý và yêu cầu xóa tài khoản của con";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">Đã rút đồng ý và yêu cầu xóa tài khoản</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Bạn đã rút lại sự đồng ý và yêu cầu xóa tài khoản của học sinh <strong>{student}</strong>. Tài khoản đã bị khóa đăng nhập và sẽ bị xóa vĩnh viễn vào ngày <strong>{date}</strong>. Hãy giữ lại email này.</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Nếu đổi ý trước ngày đó, bạn có thể hủy yêu cầu xóa bằng một trong hai nút bên dưới, không cần đăng nhập:</p>
                    <div style="text-align: center; margin: 24px 0;">
                        <a href="{WebUtility.HtmlEncode(reconsentLink)}" style="display: inline-block; background-color: #2563eb; color: #ffffff; text-decoration: none; font-weight: 600; padding: 12px 24px; border-radius: 8px; margin: 4px;">Hủy xóa và đồng ý lại cho con</a>
                        <a href="{WebUtility.HtmlEncode(restoreOnlyLink)}" style="display: inline-block; background-color: #ffffff; color: #2563eb; border: 1px solid #2563eb; text-decoration: none; font-weight: 600; padding: 12px 24px; border-radius: 8px; margin: 4px;">Chỉ hủy xóa</a>
                    </div>
                    <p style="font-size: 13px; color: #64748b; line-height: 1.5;">"Hủy xóa và đồng ý lại cho con": khôi phục tài khoản và ghi lại sự đồng ý của bạn, con dùng lại được các chức năng cần hồ sơ. "Chỉ hủy xóa": khôi phục tài khoản nhưng không đồng ý lại, con tạm chưa dùng được các chức năng cần hồ sơ. Mỗi nút dùng được một lần.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendChildAccountRestoredByParentEmailAsync(string toEmail, string studentName, bool reconsented, CancellationToken ct = default)
        {
            var student = WebUtility.HtmlEncode(studentName);
            var subject = "[USAS] Tài khoản của bạn đã được khôi phục";
            var detail = reconsented
                ? "Người đại diện cũng đã đồng ý lại, bạn dùng lại được đầy đủ các chức năng."
                : "Người đại diện chưa đồng ý lại nên bạn tạm chưa dùng được các chức năng cần hồ sơ. Bạn có thể gửi lại lời mời trong trang Tài khoản.";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">Tài khoản đã được khôi phục</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Chào <strong>{student}</strong>, người đại diện của bạn đã hủy yêu cầu xóa tài khoản. Bạn có thể đăng nhập lại như bình thường.</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">{detail}</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        // [UC-116] Giống thư mời UC-115: hai nút mở trang xác nhận, mở link không đổi trạng thái
        public async Task SendLeadParentConfirmEmailAsync(string toEmail, string studentName, string centerName, IReadOnlyList<StudyAbroad.Application.Leads.LeadSharedPreviewGroup> preview, string confirmToken, int expiryDays, CancellationToken ct = default)
        {
            var link = $"{_options.AppBaseUrl.TrimEnd('/')}/parent-consent/lead?token={Uri.EscapeDataString(confirmToken)}";
            var rejectLink = link + "&choice=reject";
            var student = WebUtility.HtmlEncode(studentName);
            var center = WebUtility.HtmlEncode(centerName);
            // [UC-110 bước 1] Tóm tắt hồ sơ: mỗi nhóm con đã chọn kèm giá trị thật, đúng như trung tâm sẽ thấy
            var fields = string.Concat(preview.Select(g =>
                $"""<p style="font-size: 14px; color: #0f172a; font-weight: 600; margin: 12px 0 4px;">{WebUtility.HtmlEncode(g.Title)}</p>"""
                + """<table style="font-size: 14px; color: #475569; line-height: 1.6; border-collapse: collapse; width: 100%;">"""
                + string.Concat(g.Items.Select(i =>
                    $"""<tr><td style="padding: 2px 12px 2px 0; width: 40%; vertical-align: top;">{WebUtility.HtmlEncode(i.Label)}</td><td style="padding: 2px 0; color: #0f172a; white-space: pre-line;">{WebUtility.HtmlEncode(i.Value)}</td></tr>"""))
                + "</table>"));
            var subject = "[USAS] Con muốn gửi yêu cầu liên hệ tới trung tâm tư vấn";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">Phụ huynh xác nhận gửi yêu cầu liên hệ</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Học sinh <strong>{student}</strong> muốn gửi yêu cầu liên hệ tới trung tâm tư vấn du học <strong>{center}</strong> trên USAS. Vì con dưới 16 tuổi, trung tâm chỉ nhận yêu cầu khi phụ huynh đồng ý.</p>
                    <div style="background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 12px 16px; margin: 16px 0;">
                        <p style="font-size: 15px; color: #0f172a; font-weight: 700; margin: 0;">Tóm tắt hồ sơ sẽ chia sẻ cho trung tâm</p>
                        {fields}
                    </div>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;"><strong>Mục đích chia sẻ:</strong> trung tâm {center} dùng các thông tin trên để liên hệ và tư vấn du học Mỹ cho con. Trung tâm chỉ thấy đúng những mục trong bản tóm tắt này.</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Bạn không cần tài khoản để trả lời.</p>
                    <div style="text-align: center; margin: 24px 0;">
                        <a href="{WebUtility.HtmlEncode(link)}" style="display: inline-block; background-color: #2563eb; color: #ffffff; text-decoration: none; font-weight: 600; padding: 12px 24px; border-radius: 8px; margin: 4px;">Đồng ý</a>
                        <a href="{WebUtility.HtmlEncode(rejectLink)}" style="display: inline-block; background-color: #ffffff; color: #b91c1c; text-decoration: none; font-weight: 600; padding: 11px 23px; border-radius: 8px; border: 1px solid #fca5a5; margin: 4px;">Từ chối</a>
                        <p style="margin: 8px 0 0; font-size: 13px; color: #64748b;">Hai nút mở trang xác nhận để bạn xem lại và bấm chọn. Link có hiệu lực trong <strong>{expiryDays} ngày</strong>. Nếu con có nhiều phụ huynh nhận mail này, người trả lời trước sẽ quyết định.</p>
                    </div>
                    <p style="font-size: 13px; color: #94a3b8; line-height: 1.5;">Nếu bạn không phải phụ huynh của em {student}, hãy bỏ qua email này; yêu cầu sẽ tự hết hạn và trung tâm không nhận được gì.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendLeadParentDecisionEmailAsync(string toEmail, string studentName, string centerName, string outcome, CancellationToken ct = default)
        {
            var student = WebUtility.HtmlEncode(studentName);
            var center = WebUtility.HtmlEncode(centerName);
            var (title, text) = outcome switch
            {
                "new" => ("Phụ huynh đã đồng ý", $"Phụ huynh đã đồng ý. Yêu cầu liên hệ của bạn đã được gửi tới <strong>{center}</strong>; trung tâm sẽ liên hệ lại với bạn."),
                "cancelled" => ("Phụ huynh đã từ chối", $"Phụ huynh đã từ chối gửi yêu cầu liên hệ tới <strong>{center}</strong>. Trung tâm không nhận được thông tin của bạn. Bạn có thể trao đổi lại với phụ huynh rồi gửi yêu cầu mới."),
                // [UC-110] Yêu cầu đang giữ sau khi ngày sinh được sửa về dưới 16, không có phụ huynh xác nhận
                "withdrawn" => ("Yêu cầu liên hệ đã bị thu hồi", $"Ngày sinh của bạn đã được cập nhật nên yêu cầu liên hệ tới <strong>{center}</strong> cần phụ huynh xác nhận. Yêu cầu chưa được phụ huynh xác nhận nên đã bị thu hồi; dữ liệu của bạn không còn hiển thị với trung tâm."),
                _ => ("Yêu cầu đã hết hạn", $"Phụ huynh chưa trả lời trong 7 ngày nên yêu cầu liên hệ tới <strong>{center}</strong> đã hết hạn. Bạn có thể gửi lại yêu cầu mới."),
            };
            var subject = $"[USAS] {title}";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">{title}</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Chào <strong>{student}</strong>,</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">{text}</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Xem chi tiết trong mục Yêu cầu liên hệ đã gửi trên USAS.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendAccountLockedEmailAsync(string toEmail, string fullName, string reason, CancellationToken ct = default)
        {
            var subject = "[USAS] Thông báo: Tài khoản của bạn đã bị khóa";
            var encodedName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "Người dùng USAS" : fullName);
            var encodedReason = WebUtility.HtmlEncode(reason);
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #fecaca; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #dc2626; margin-bottom: 16px;">⚠️ Thông báo: Tài khoản của bạn đã bị khóa</h2>
                    <p style="font-size: 15px; color: #334155;">Xin chào <strong>{encodedName}</strong>,</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">
                        Hệ thống Quản trị USAS thông báo: Tài khoản của bạn ({WebUtility.HtmlEncode(toEmail)}) đã bị khóa quyền truy cập theo quyết định của Quản trị viên.
                    </p>
                    <div style="background-color: #fef2f2; border-left: 4px solid #ef4444; border-radius: 6px; padding: 16px; margin: 20px 0;">
                        <p style="margin: 0 0 6px 0; font-size: 13px; font-weight: bold; color: #991b1b; text-transform: uppercase;">Lý do khóa tài khoản:</p>
                        <p style="margin: 0; font-size: 14px; color: #7f1d1d; line-height: 1.5; font-style: italic;">"{encodedReason}"</p>
                    </div>
                    <p style="font-size: 13px; color: #64748b; line-height: 1.6;">
                        Trong thời gian bị khóa, bạn sẽ không thể đăng nhập hoặc thực hiện các thao tác trên hệ thống. Nếu bạn cho rằng đây là sự nhầm lẫn hoặc cần giải trình, vui lòng liên hệ Ban Quản trị qua email hỗ trợ của USAS.
                    </p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendAccountUnlockedEmailAsync(string toEmail, string fullName, string reason, CancellationToken ct = default)
        {
            var subject = "[USAS] Thông báo: Tài khoản của bạn đã được mở khóa";
            var encodedName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "Người dùng USAS" : fullName);
            var encodedReason = WebUtility.HtmlEncode(reason);
            var loginUrl = $"{_options.AppBaseUrl.TrimEnd('/')}/login";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #bbf7d0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #16a34a; margin-bottom: 16px;">✅ Thông báo: Tài khoản của bạn đã được mở khóa</h2>
                    <p style="font-size: 15px; color: #334155;">Xin chào <strong>{encodedName}</strong>,</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">
                        Hệ thống Quản trị USAS thông báo: Tài khoản của bạn ({WebUtility.HtmlEncode(toEmail)}) đã được <strong>mở khóa thành công</strong> bởi Quản trị viên.
                    </p>
                    <div style="background-color: #f0fdf4; border-left: 4px solid #22c55e; border-radius: 6px; padding: 16px; margin: 20px 0;">
                        <p style="margin: 0 0 6px 0; font-size: 13px; font-weight: bold; color: #166534; text-transform: uppercase;">Ghi chú từ Quản trị viên:</p>
                        <p style="margin: 0; font-size: 14px; color: #14532d; line-height: 1.5; font-style: italic;">"{encodedReason}"</p>
                    </div>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">
                        Hiện tại bạn đã có thể truy cập lại bình thường vào hệ thống.
                    </p>
                    <div style="text-align: center; margin: 24px 0;">
                        <a href="{WebUtility.HtmlEncode(loginUrl)}" style="display: inline-block; background-color: #2563eb; color: #ffffff; text-decoration: none; font-weight: 600; padding: 12px 28px; border-radius: 8px;">Đăng nhập ngay</a>
                    </div>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        // Biên nhận chỉ có mã đơn, tên gói và số tiền: không lưu, không in thông tin thẻ hay tài khoản ngân hàng.
        public async Task SendPaymentReceiptEmailAsync(string toEmail, string fullName, long orderCode, string planName, decimal amountVnd, DateTime paidAtUtc, DateTime? startsAtUtc, DateTime? endsAtUtc, CancellationToken ct = default)
        {
            var subject = $"Biên nhận thanh toán gói {planName}";
            var encodedName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "Người dùng USAS" : fullName);
            var validity = startsAtUtc is null || endsAtUtc is null
                ? "Gói của bạn đã được kích hoạt."
                : $"Hiệu lực từ <strong>{VietnamTime(startsAtUtc.Value)}</strong> đến <strong>{VietnamTime(endsAtUtc.Value)}</strong>.";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">Biên nhận thanh toán</h2>
                    <p style="font-size: 15px; color: #334155;">Xin chào <strong>{encodedName}</strong>,</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Cảm ơn bạn đã thanh toán. Dưới đây là thông tin đơn hàng:</p>
                    <div style="background-color: #eff6ff; border-radius: 8px; padding: 16px; margin: 20px 0; font-size: 14px; color: #1e293b; line-height: 1.8;">
                        <div>Mã đơn: <strong>{orderCode}</strong></div>
                        <div>Gói: <strong>{WebUtility.HtmlEncode(planName)}</strong></div>
                        <div>Số tiền: <strong>{Vnd(amountVnd)}</strong></div>
                        <div>Thời điểm thanh toán: <strong>{VietnamTime(paidAtUtc)}</strong> (giờ Việt Nam)</div>
                    </div>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">{validity}</p>
                    <p style="font-size: 13px; color: #64748b; line-height: 1.6;">Hệ thống không lưu thông tin thẻ hay tài khoản ngân hàng.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        public async Task SendSubscriptionExpiringEmailAsync(string toEmail, string fullName, string planName, DateTime endsAtUtc, CancellationToken ct = default)
        {
            var subject = $"Gói {planName} sắp hết hạn";
            var encodedName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "Người dùng USAS" : fullName);
            var plansUrl = $"{_options.AppBaseUrl.TrimEnd('/')}/plans";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">Gói của bạn sắp hết hạn</h2>
                    <p style="font-size: 15px; color: #334155;">Xin chào <strong>{encodedName}</strong>,</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Gói <strong>{WebUtility.HtmlEncode(planName)}</strong> của bạn sẽ hết hạn vào <strong>{VietnamTime(endsAtUtc)}</strong> (giờ Việt Nam).</p>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;">Việc gia hạn là thủ công: hệ thống không tự động trừ tiền của bạn. Nếu muốn tiếp tục dùng, bạn có thể chọn gói mới bất cứ lúc nào.</p>
                    <div style="text-align: center; margin: 24px 0;">
                        <a href="{WebUtility.HtmlEncode(plansUrl)}" style="display: inline-block; background-color: #2563eb; color: #ffffff; text-decoration: none; font-weight: 600; padding: 12px 28px; border-radius: 8px;">Xem các gói</a>
                    </div>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        // Token là chuỗi dùng một lần nên mới đưa vào link; mở link chỉ dẫn tới trang xác nhận, chưa đổi trạng thái gì.
        public async Task SendFamilyInviteEmailAsync(string toEmail, string ownerName, string seatRole, string inviteToken, int expiryDays, CancellationToken ct = default)
        {
            var subject = $"{ownerName} mời bạn tham gia gói Gia đình";
            var owner = WebUtility.HtmlEncode(ownerName);
            var role = seatRole switch { "student" => "Học sinh", "parent" => "Phụ huynh", _ => WebUtility.HtmlEncode(seatRole) };
            var link = $"{_options.AppBaseUrl.TrimEnd('/')}/family/invite?token={Uri.EscapeDataString(inviteToken)}";
            var body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <h2 style="color: #2563eb; margin-bottom: 16px;">Lời mời tham gia gói Gia đình</h2>
                    <p style="font-size: 14px; color: #475569; line-height: 1.6;"><strong>{owner}</strong> mời bạn tham gia gói Gia đình trên Hệ thống Hỗ trợ Ra quyết định Du học Mỹ (USAS) với vai trò <strong>{role}</strong>.</p>
                    <div style="text-align: center; margin: 24px 0;">
                        <a href="{WebUtility.HtmlEncode(link)}" style="display: inline-block; background-color: #2563eb; color: #ffffff; text-decoration: none; font-weight: 600; padding: 12px 28px; border-radius: 8px;">Xem lời mời</a>
                        <p style="margin: 8px 0 0; font-size: 13px; color: #64748b;">Lời mời hết hạn sau <strong>{expiryDays} ngày</strong>.</p>
                    </div>
                    <p style="font-size: 13px; color: #94a3b8; line-height: 1.5;">Nếu bạn không quen người mời, hãy bỏ qua email này.</p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center;">USAS - Hệ thống hỗ trợ ra quyết định du học Mỹ</p>
                </div>
                """;

            await SendEmailAsync(toEmail, subject, body, ct);
        }

        /// <summary>Giờ Việt Nam dạng dd/MM/yyyy HH:mm (chuỗi chỉ gồm số và dấu / :, an toàn để chèn thẳng vào HTML).</summary>
        public static string VietnamTime(DateTime utc) =>
            CycleRules.ToVietnamTime(utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        /// <summary>"79.000 đ": dấu chấm ngăn nghìn như vi-VN, không phần lẻ. Tự ngăn nghìn để không phụ thuộc ICU của máy chủ.</summary>
        public static string Vnd(decimal amount) =>
            amount.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.') + " đ";

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
