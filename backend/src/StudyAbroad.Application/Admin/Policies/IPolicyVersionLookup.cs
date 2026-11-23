namespace StudyAbroad.Application.Admin.Policies;

/// <summary>
/// [UC-103] Tra cứu số phiên bản đang hiệu lực của một loại tài liệu, để các chức năng khác (đồng ý của phụ huynh, UC-104)
/// ghi ĐÚNG phiên bản người dùng đã được đọc, thay cho hằng số viết cứng trong mã.
/// </summary>
public interface IPolicyVersionLookup
{
    /// <summary>Phiên bản đang hiệu lực của loại này (ví dụ "2026-11"); null nếu chưa có bản nào được công bố.</summary>
    Task<string?> GetPublishedVersionAsync(string kind, CancellationToken ct = default);
}
