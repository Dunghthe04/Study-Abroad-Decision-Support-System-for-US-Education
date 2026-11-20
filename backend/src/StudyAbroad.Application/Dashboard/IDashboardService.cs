using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Dashboard;

/// <summary>
/// Service cung cấp các chỉ số KPI thống kê màn hình Dashboard (UC-94).
/// Quy tắc: Chỉ đọc, chỉ gom các số liệu sẵn có, không tính điểm mới, không tạo bảng/cột mới.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Lấy Dashboard KPI của học sinh (hoặc phụ huynh đang acts-as con managed).
    /// </summary>
    Task<StudentDashboardDto> GetStudentDashboardAsync(Guid studentId, CancellationToken ct = default);

    /// <summary>
    /// Lấy Dashboard KPI dành cho phụ huynh xem từng con (bao gồm KPI học tập và KPI tài chính).
    /// Trả về null nếu phụ huynh không có liên kết hiệu lực với con (controller trả 404).
    /// </summary>
    Task<ParentChildDashboardDto?> GetParentChildDashboardAsync(Guid parentId, Guid studentId, CancellationToken ct = default);
}
