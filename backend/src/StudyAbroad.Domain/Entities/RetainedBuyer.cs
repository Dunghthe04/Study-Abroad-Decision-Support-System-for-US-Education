using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// [UC-75 lớp 2] Họ tên và email người mua của tài khoản đã xóa hẳn, giữ để đối soát và hoàn tiền.
/// Bảng riêng vì đặc tả bắt buộc tách khỏi đơn hàng: đơn chỉ còn mã người mua ngẫu nhiên (Id),
/// chỉ Admin xem được họ tên/email và mỗi lần xem ghi nhật ký truy cập (UC-91, UC-124).
/// Không dùng lại bảng users: dòng user đã bị xóa hẳn cùng mọi dữ liệu khác.
/// </summary>
public class RetainedBuyer : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Hết 10 năm kể từ khi kết thúc năm tài chính có giao dịch mới nhất; quá mốc này tác vụ định kỳ xóa hẳn.</summary>
    public DateTime RetainUntil { get; set; }
}
