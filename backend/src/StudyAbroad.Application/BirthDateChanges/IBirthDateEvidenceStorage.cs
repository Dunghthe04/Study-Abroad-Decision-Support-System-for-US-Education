namespace StudyAbroad.Application.BirthDateChanges;

/// <summary>
/// [UC-107 / UC-150] Lưu tệp minh chứng ngày sinh trên máy chủ (không phục vụ qua static files).
/// Chỉ Admin tải qua API; tệp bị xóa ngay khi yêu cầu được duyệt, bị từ chối hoặc bị hủy.
/// </summary>
public interface IBirthDateEvidenceStorage
{
    /// <summary>Lưu tệp, trả về đường dẫn tương đối để ghi vào yêu cầu.</summary>
    Task<string> SaveAsync(Guid requestId, byte[] content, string extension, CancellationToken ct = default);

    /// <summary>Mở tệp để đọc; null khi tệp không còn.</summary>
    Stream? Open(string path);

    /// <summary>Xóa tệp và thư mục của yêu cầu; không ném lỗi khi tệp đã mất.</summary>
    void Delete(string? path);
}
