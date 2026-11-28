namespace StudyAbroad.Application.BirthDateChanges;

/// <summary>
/// [UC-107 / UC-150] Lưu tệp minh chứng ngày sinh (giấy khai sinh, căn cước...). Tệp được mã hóa trước khi rời api,
/// không có link công khai. Chỉ Admin tải qua API; tệp bị xóa ngay khi yêu cầu được duyệt, bị từ chối hoặc bị hủy.
/// </summary>
public interface IBirthDateEvidenceStorage
{
    /// <summary>Lưu tệp, trả về khóa lưu trữ để ghi vào yêu cầu.</summary>
    Task<string> SaveAsync(Guid requestId, byte[] content, string extension, CancellationToken ct = default);

    /// <summary>Đọc tệp đã giải mã; null khi tệp không còn hoặc bản mã không hợp lệ.</summary>
    Task<Stream?> OpenAsync(string path, CancellationToken ct = default);

    /// <summary>Xóa tệp; không ném lỗi khi tệp đã mất hoặc nơi lưu tạm lỗi.</summary>
    Task DeleteAsync(string? path, CancellationToken ct = default);
}
