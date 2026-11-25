using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// [UC-75, UC-124] Bản chụp tối thiểu thông tin người mua của tài khoản đã xóa hẳn, giữ 10 năm kể từ khi kết thúc năm tài chính có giao dịch.
/// Tách khỏi tài khoản bằng mã người mua ngẫu nhiên (<see cref="BuyerCode"/>); chỉ Administrator xem được và mỗi lần xem được ghi nhật ký truy cập (UC-91).
/// </summary>
public class DeletedBuyerRetention : BaseEntity
{
    /// <summary>Mã người mua ngẫu nhiên hiển thị thay họ tên trên danh sách đơn của tài khoản đã xóa (vd. DEL-A1B2C3D4).</summary>
    public string BuyerCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>Mốc hết hạn lưu trữ (UTC): 10 năm sau khi kết thúc năm tài chính (theo giờ Việt Nam) của giao dịch mới nhất.</summary>
    public DateTime RetainUntilUtc { get; set; }
}
