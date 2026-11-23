using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// [USAS-640] Kiểm tra tệp bảng điểm tải lên (không lưu tệp xuống ổ đĩa) và dọn tệp cũ của bản trước.
/// </summary>
public interface IProfileFileStorageService
{
    /// <summary>
    /// Kiểm tra tệp (dung lượng, đuôi, chữ ký đầu tệp) và trả về thông tin để ghi vào nháp, không ghi tệp ra đâu cả.
    /// Tệp sai định dạng (đuôi hoặc nội dung) hay quá dung lượng thì ném ArgumentException.
    /// </summary>
    DraftTranscriptInfoDto ValidateTranscriptFile(byte[] fileBytes, string fileName);
    /// <summary>Xoá 1 tệp nháp cũ của user (tệp còn lại từ bản trước khi bỏ lưu tệp).</summary>
    Task DeleteDraftFileAsync(Guid userId, string? draftRelativePath, CancellationToken ct = default);
    /// <summary>Xoá 1 tệp bảng điểm chính thức cũ của user (còn lại từ bản trước khi bỏ lưu tệp).</summary>
    Task DeleteTranscriptFileAsync(Guid userId, string? transcriptRelativePath, CancellationToken ct = default);
    Task DeleteDraftFilesAsync(Guid userId, CancellationToken ct = default);
    /// <summary>[UC-80] Xoá toàn bộ tệp của user (nháp và bảng điểm chính thức) khi xóa hẳn tài khoản. Lỗi thì ném ra để nơi gọi ghi log.</summary>
    Task DeleteAllUserFilesAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Giới hạn tệp bảng điểm dùng chung cho controller (chặn sớm) và storage (chặn lần cuối).</summary>
public static class ProfileFileStorageLimits
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
}
