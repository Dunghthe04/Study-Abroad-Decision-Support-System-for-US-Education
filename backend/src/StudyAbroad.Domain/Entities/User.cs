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
        public bool ParentAcknowledged { get; set; }

        /// <summary>[USAS-362] Số điện thoại liên hệ cá nhân (chỉ dùng lưu hồ sơ/tư vấn liên lạc, không dùng đăng nhập hay OTP để tránh tốn phí SMS).</summary>
        public string? Phone { get; set; }

        /// <summary>Số lần đăng nhập sai mật khẩu liên tiếp. Đạt 5 lần sẽ tự động chuyển sang trạng thái temp_locked.</summary>
        public int FailedLoginAttempts { get; set; } = 0;

        /// <summary>Thời điểm hết hạn khóa tạm thời (15 phút sau khi gõ sai 5 lần). Sau thời điểm này tài khoản tự động được mở khóa.</summary>
        public DateTime? LockoutEnd { get; set; }
    }
}
