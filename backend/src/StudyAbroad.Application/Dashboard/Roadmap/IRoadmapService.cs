namespace StudyAbroad.Application.Dashboard.Roadmap;

/// <summary>
/// Lộ trình theo trường mục tiêu (docs/KE_HOACH_LO_TRINH_THEO_TRUONG_MUC_TIEU.md): Tổng quan vẽ, chat nhận bản rút gọn.
/// </summary>
public interface IRoadmapService
{
    /// <summary>lộ trình của học sinh; null khi chưa có hồ sơ</summary>
    Task<RoadmapDto?> GetRoadmapAsync(Guid studentId, CancellationToken ct = default);

    /// <summary>phụ huynh chỉ xem lộ trình của con; Linked = false khi không có liên kết đang hiệu lực</summary>
    Task<(bool Linked, RoadmapDto? Roadmap)> GetChildRoadmapAsync(Guid parentId, Guid studentId, CancellationToken ct = default);

    /// <summary>lưu tỉnh học sinh đang sống; null = xóa. Trả kết quả để controller chọn mã lỗi</summary>
    Task<SetProvinceResult> SetProvinceAsync(Guid studentId, string? province, CancellationToken ct = default);
}

public enum SetProvinceResult
{
    Saved,
    InvalidProvince,
    NoProfile,
}
