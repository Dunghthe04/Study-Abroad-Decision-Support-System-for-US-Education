namespace StudyAbroad.Application.Auth;

/// <summary>[UC-75] Thời hạn giữ dữ liệu của tài khoản đã xóa hẳn (đặc tả UC-75, quy tắc ba lớp).</summary>
public static class RetentionPeriods
{
    /// <summary>Lớp 1b: tin nhắn với trung tâm giữ tới hết 90 ngày kể từ ngày gửi yêu cầu.</summary>
    public static readonly TimeSpan LeadMessages = TimeSpan.FromDays(90);

    /// <summary>Lớp 2: đơn hàng giữ 10 năm kể từ khi kết thúc năm tài chính có giao dịch.</summary>
    public const int OrderYears = 10;

    // Năm tài chính trùng năm dương lịch (Luật Kế toán), tính theo giờ Việt Nam
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    /// <summary>Cùng mốc với <see cref="AccountPurgeService.ComputeRetentionExpiryUtc"/>. Đơn có thời điểm tạo trước mốc này (UTC) đã hết hạn giữ tại <paramref name="nowUtc"/>.</summary>
    public static DateTime OrderPurgeCutoff(DateTime nowUtc) =>
        StartOfVietnamYearUtc((nowUtc + VietnamOffset).Year - OrderYears);

    private static DateTime StartOfVietnamYearUtc(int year) =>
        DateTime.SpecifyKind(new DateTime(year, 1, 1) - VietnamOffset, DateTimeKind.Utc);
}
