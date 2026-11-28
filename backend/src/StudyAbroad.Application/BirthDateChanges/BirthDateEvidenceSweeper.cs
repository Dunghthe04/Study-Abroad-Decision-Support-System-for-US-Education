namespace StudyAbroad.Application.BirthDateChanges;

/// <summary>Kết quả một lượt dọn: số tệp đang có trên nơi lưu (đủ cũ), số tệp đã xóa vì không thuộc yêu cầu nào.</summary>
public record BirthDateEvidenceSweepResult(int Listed, int Deleted);

public interface IBirthDateEvidenceSweeper
{
    Task<BirthDateEvidenceSweepResult> SweepAsync(DateTime now, CancellationToken ct = default);
}

/// <summary>
/// [UC-107 / UC-150] Minh chứng ngày sinh không lưu lại sau khi yêu cầu đóng. Duyệt, từ chối, hủy, gửi trùng và xóa hẳn
/// tài khoản đều xóa tệp ngay; job này dọn phần sót: Cloudinary xóa lỗi (mạng) khi DB đã bỏ đường dẫn, hoặc api dừng
/// giữa lúc tải lên và lúc ghi yêu cầu. Tệp nào không còn đường dẫn trong DB là bị xóa.
/// Chỉ xét tệp tải lên quá <see cref="MinAge"/> để không xóa tệp của yêu cầu đang gửi dở.
/// </summary>
public sealed class BirthDateEvidenceSweeper(
    IBirthDateEvidenceStorage storage,
    IBirthDateChangeRepository repository) : IBirthDateEvidenceSweeper
{
    public static readonly TimeSpan MinAge = TimeSpan.FromHours(1);

    public async Task<BirthDateEvidenceSweepResult> SweepAsync(DateTime now, CancellationToken ct = default)
    {
        // Liệt kê trước rồi mới đọc DB: yêu cầu ghi xong trong lúc liệt kê vẫn có trong danh sách giữ lại
        var stored = await storage.ListAsync(now - MinAge, ct);
        if (stored.Count == 0)
            return new BirthDateEvidenceSweepResult(0, 0);

        var keep = (await repository.GetEvidencePathsAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var deleted = 0;
        foreach (var path in stored)
        {
            if (keep.Contains(path))
                continue;
            await storage.DeleteAsync(path, ct);
            deleted++;
        }
        return new BirthDateEvidenceSweepResult(stored.Count, deleted);
    }
}
