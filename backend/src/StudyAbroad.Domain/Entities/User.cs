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
    }
}
