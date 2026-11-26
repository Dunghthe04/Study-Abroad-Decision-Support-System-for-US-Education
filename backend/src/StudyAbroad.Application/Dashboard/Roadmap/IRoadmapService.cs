namespace StudyAbroad.Application.Dashboard.Roadmap;

/// <summary>
/// Lộ trình theo trường mục tiêu (docs/KE_HOACH_LO_TRINH_THEO_TRUONG_MUC_TIEU.md): Tổng quan vẽ, chat nhận bản rút gọn.
/// </summary>
public interface IRoadmapService
{
    /// <summary>lưu tỉnh học sinh đang sống; null = xóa. Trả kết quả để controller chọn mã lỗi</summary>
    Task<SetProvinceResult> SetProvinceAsync(Guid studentId, string? province, CancellationToken ct = default);
}

public enum SetProvinceResult
{
    Saved,
    InvalidProvince,
    NoProfile,
}
