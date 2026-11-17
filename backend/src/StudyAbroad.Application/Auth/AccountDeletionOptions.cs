using System;

namespace StudyAbroad.Application.Auth
{
    /// <summary>
    /// [UC-80] Cấu hình xóa tài khoản (appsettings "AccountDeletion").
    /// RetentionPeriod: thời gian giữ tài khoản ở trạng thái chờ xóa, trong đó người dùng còn khôi phục được.
    /// CheckInterval: chu kỳ job xóa hẳn tài khoản quá hạn chạy kiểm tra.
    /// </summary>
    public class AccountDeletionOptions
    {
        public const string SectionName = "AccountDeletion";

        public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);

        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(1);
    }
}
