using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// [UC-103] Một phiên bản của một chính sách hoặc điều khoản (Điều khoản sử dụng, Chính sách bảo mật, ...).
/// Bản nháp sửa được; bản đã công bố KHÔNG BAO GIỜ sửa được nữa, vì người dùng đã đồng ý đúng văn bản đó (bằng chứng theo Luật BVDLCN).
/// Muốn đổi nội dung thì tạo bản mới (chép từ bản đang hiệu lực) rồi công bố; bản cũ chuyển thành hết hiệu lực nhưng vẫn giữ nguyên.
/// Mỗi phiên bản lưu TOÀN VĂN, không lưu phần chênh lệch.
/// </summary>
public class PolicyDocument : BaseEntity
{
    /// <summary>Loại tài liệu, một trong PolicyKinds.</summary>
    public string Kind { get; set; } = string.Empty;
    /// <summary>
    /// Số phiên bản dạng yyyy-MM (thêm -02, -03... nếu một tháng có nhiều bản), hệ thống tự đặt lúc công bố.
    /// Null khi còn là nháp. So sánh theo thứ tự chữ là đúng thứ tự thời gian.
    /// </summary>
    public string? Version { get; set; }
    /// <summary>Một trong PolicyStatuses.</summary>
    public string Status { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>Toàn văn, định dạng Markdown đơn giản (tiêu đề, đoạn, gạch đầu dòng).</summary>
    public string Content { get; set; } = string.Empty;
    /// <summary>Tóm tắt thay đổi so với bản trước, bắt buộc khi công bố; UC-104 hiện cho người dùng "bản mới có gì thay đổi".</summary>
    public string? ChangeSummary { get; set; }
    /// <summary>Bản này có bắt người dùng đồng ý lại không (sửa chính tả thì không, thêm điều khoản quan trọng thì có). UC-104 dựa vào cờ này.</summary>
    public bool RequiresReconsent { get; set; }
    /// <summary>Bản được chép ra để tạo nháp này (thường là bản đang hiệu lực lúc tạo); null nếu soạn từ đầu.</summary>
    public Guid? BasedOnId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedByUserId { get; set; }
    /// <summary>Lúc bản này hết hiệu lực (có bản mới công bố thay).</summary>
    public DateTime? RetiredAt { get; set; }
}
