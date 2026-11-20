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

        /// <summary>Gửi email thông báo tài khoản bị khóa do nhập sai mật khẩu liên tiếp 5 lần (yêu cầu Admin mở khóa).</summary>
        Task SendAccountTempLockedNotificationAsync(string toEmail, string fullName, int lockoutMinutes = 0, CancellationToken ct = default);

        /// <summary>Có người đăng ký lại bằng email đã có tài khoản: báo chủ email thay vì báo lỗi trên màn hình (chống dò tài khoản).</summary>
        Task SendAccountAlreadyExistsEmailAsync(string toEmail, string fullName, CancellationToken ct = default);

        /// <summary>Đăng ký bằng số điện thoại đã gắn với tài khoản khác: báo cho email vừa đăng ký, không tạo tài khoản.</summary>
        Task SendPhoneAlreadyInUseEmailAsync(string toEmail, CancellationToken ct = default);

        /// <summary>Mật khẩu vừa được đổi (quên mật khẩu hoặc đổi khi đã đăng nhập): báo để chủ tài khoản phát hiện nếu không phải mình làm.</summary>
        Task SendPasswordChangedNotificationAsync(string toEmail, string fullName, CancellationToken ct = default);

        /// <summary>
        /// [UC-59] Báo kết quả xét duyệt hồ sơ xác minh cho trung tâm. <paramref name="outcome"/>: submitted (đã nhận hồ sơ) | verified | rejected | changes_requested | revoked.
        /// <paramref name="reason"/>: lý do Moderator nhập (từ chối, bổ sung, thu hồi); <paramref name="unmetConditions"/>: điều kiện nào chưa đạt.
        /// </summary>
        Task SendCenterVerificationOutcomeEmailAsync(string toEmail, string centerName, string outcome, string? reason, IReadOnlyList<string>? unmetConditions, CancellationToken ct = default);

        /// <summary>Mời phụ huynh liên kết và đồng ý cho hệ thống xử lý dữ liệu của học sinh; link chứa token dùng một lần.</summary>
        Task SendParentInviteEmailAsync(string toEmail, string studentName, string inviteToken, int expiryDays, CancellationToken ct = default);

        /// <summary>Báo cho một bên khi liên kết phụ huynh–học sinh được xác nhận (<paramref name="linked"/> = true) hoặc bị hủy / rút đồng ý.</summary>
        Task SendParentLinkChangedEmailAsync(string toEmail, string studentName, string parentLabel, bool linked, CancellationToken ct = default);

        /// <summary>[UC-117] Gửi phụ huynh ngay sau khi đồng ý: xác nhận đã đồng ý, kèm link rút đồng ý không cần đăng nhập.</summary>
        Task SendParentConsentConfirmedEmailAsync(string toEmail, string studentName, string revokeToken, CancellationToken ct = default);

        /// <summary>[UC-115] Báo học sinh: phụ huynh bấm Từ chối trong link mời. Học sinh gửi lại hoặc đổi email phụ huynh trong trang Tài khoản.</summary>
        Task SendParentConsentRejectedEmailAsync(string toEmail, string studentName, string parentLabel, CancellationToken ct = default);

        /// <summary>[UC-115] Báo học sinh: phụ huynh đã đồng ý qua mail nay đăng ký tài khoản Phụ huynh, hệ thống tự liên kết.</summary>
        Task SendParentAccountLinkedEmailAsync(string toEmail, string studentName, string parentName, CancellationToken ct = default);

        /// <summary>[UC-116] Học sinh dưới 16 gửi yêu cầu liên hệ: hỏi phụ huynh đồng ý chia sẻ cho trung tâm. Mỗi phụ huynh một link.</summary>
        Task SendLeadParentConfirmEmailAsync(string toEmail, string studentName, string centerName, IReadOnlyList<string> sharedFields, string confirmToken, int expiryDays, CancellationToken ct = default);

        /// <summary>[UC-116] Báo học sinh kết quả. outcome: "new" phụ huynh đồng ý (đã gửi trung tâm), "cancelled" từ chối, "expired" hết hạn.</summary>
        Task SendLeadParentDecisionEmailAsync(string toEmail, string studentName, string centerName, string outcome, CancellationToken ct = default);

        /// <summary>Gửi email thông báo tài khoản bị khóa bởi quản trị viên kèm lý do khóa.</summary>
        Task SendAccountLockedEmailAsync(string toEmail, string fullName, string reason, CancellationToken ct = default);

        /// <summary>Gửi email thông báo tài khoản đã được mở khóa bởi quản trị viên kèm lý do / ghi chú.</summary>
        Task SendAccountUnlockedEmailAsync(string toEmail, string fullName, string reason, CancellationToken ct = default);

        /// <summary>[UC-93] Kết quả xử lý yêu cầu xóa trung tâm. outcome: received (đã nhận, kèm hạn xử lý) | extended (gia hạn, kèm hạn mới và lý do) | held (chưa thể xóa vì giữ pháp lý, Luật 91/2025 Điều 14 khoản 5) | approved (kèm ngày xóa hẳn) | rejected (kèm lý do) | completed. Tham số purgeAt mang ngày tương ứng với từng outcome.</summary>
        Task SendCenterDeletionOutcomeEmailAsync(string toEmail, string centerName, string outcome, string? note, DateTime? purgeAt, CancellationToken ct = default);
    }
}
