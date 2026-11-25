using StudyAbroad.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Domain.Entities
{
    public class User : BaseEntity
    {
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        /// <summary>Cột cũ (ô tick "phụ huynh đã biết"), giữ vì còn dữ liệu, không dùng nữa: thay bằng ParentStudentLink.</summary>
        public bool ParentAcknowledged { get; set; }

        /// <summary>Ngày sinh khai khi đăng ký, dùng tính nhóm tuổi theo luật VN. Null = tài khoản cũ / center / admin, không bị giới hạn theo tuổi.</summary>
        public DateOnly? DateOfBirth { get; set; }

        /// <summary>[USAS-362] Số điện thoại liên hệ cá nhân (chỉ dùng lưu hồ sơ/tư vấn liên lạc, không dùng đăng nhập hay OTP để tránh tốn phí SMS).</summary>
        public string? Phone { get; set; }

        /// <summary>Số lần đăng nhập sai mật khẩu liên tiếp. Đạt 5 lần sẽ tự động chuyển sang trạng thái temp_locked.</summary>
        public int FailedLoginAttempts { get; set; } = 0;

        /// <summary>Thời điểm hết hạn khóa tạm thời (15 phút sau khi gõ sai 5 lần). Sau thời điểm này tài khoản tự động được mở khóa.</summary>
        public DateTime? LockoutEnd { get; set; }

        /// <summary>Concurrency token (cột hệ thống xmin của Postgres): chặn 2 request song song cùng ghi đè bộ đếm đăng nhập sai.</summary>
        public uint Version { get; set; }

        /// <summary>Khóa ngày sinh sau đăng ký, chỉ admin sửa (UC-01).</summary>
        public bool IsBirthdateLocked { get; set; } = true;

        /// <summary>Khóa quyền đăng bài diễn đàn (UC-56).</summary>
        public DateTime? ForumRestrictedUntil { get; set; }

        /// <summary>Lý do hạn chế quyền diễn đàn.</summary>
        public string? ForumRestrictionReason { get; set; }

        /// <summary>Người thực hiện hạn chế quyền diễn đàn gần nhất.</summary>
        public Guid? ForumRestrictedByUserId { get; set; }

        /// <summary>UC-80: thời điểm yêu cầu xóa tài khoản (UTC). Null = chưa yêu cầu. Hết thời hạn xóa mềm thì job xóa hẳn.</summary>
        public DateTime? DeletedAt { get; set; }

        /// <summary>
        /// [UC-75 1a / UC-111 3a] Email người đại diện đã "Rút đồng ý và xóa tài khoản của con" (học sinh dưới 16).
        /// Null = tự xóa (UC-80) hoặc không chờ xóa. Khác null thì học sinh dưới 16 không tự khôi phục được (4b),
        /// chỉ người đại diện khôi phục bằng nút trong mail; đủ 16 trong thời gian chờ thì tự khôi phục như 4a (4c).
        /// Cột riêng thay vì suy ra từ mã trong otp_tokens: mã bị dùng hoặc bị job dọn đi thì mất dấu ai đã yêu cầu xóa.
        /// Dòng user bị xóa hẳn khi hết thời hạn nên email này cũng mất theo.
        /// </summary>
        public string? DeletionRequestedByEmail { get; set; }

        /// <summary>Legal Hold: đóng băng tài khoản khi có khiếu nại, tố cáo lừa đảo hoặc cơ quan điều tra yêu cầu. Job dọn dẹp không xóa tài khoản này. Căn cứ: Luật 91/2025/QH15 Điều 14 khoản 2 (không thực hiện yêu cầu xóa trong các trường hợp Điều 19) và Điều 19 khoản 1 điểm a, b; bên kiểm soát có trách nhiệm chứng minh trường hợp này nên phải ghi LegalHoldReason. Không xóa được thì phải thông báo cho chủ thể dữ liệu (Điều 14 khoản 5). Nghị định 13/2023/NĐ-CP đã hết hiệu lực từ 01/01/2026 (Điều 42 Nghị định 356/2025/NĐ-CP).</summary>
        public bool IsLegalHold { get; set; } = false;

        /// <summary>Lý do áp dụng Legal Hold.</summary>
        public string? LegalHoldReason { get; set; }

        /// <summary>
        /// [UC-03 3d] Khóa bí mật TOTP của Moderator, Administrator, đã mã hóa AES-GCM (không lưu bản rõ).
        /// Cột trên users vì mỗi tài khoản chỉ có một khóa; mã khôi phục và phiếu bước 2 dùng lại bảng otp_tokens.
        /// Có giá trị nhưng TotpEnabledAt trống = đang thiết lập dở (đã hiện QR, chưa nhập mã đầu tiên).
        /// </summary>
        public string? TotpSecret { get; set; }

        /// <summary>[UC-03 3d] Thời điểm thiết lập TOTP xong (nhập đúng mã đầu tiên). Trống = chưa thiết lập.</summary>
        public DateTime? TotpEnabledAt { get; set; }
    }
}
