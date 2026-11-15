namespace StudyAbroad.Application.TargetSchools;

/// <summary>
/// Dịch vụ xử lý nghiệp vụ liên quan đến danh sách trường mục tiêu.
/// </summary>
public interface ITargetSchoolService
{
    /// <summary>
    /// [UC-30] Thêm một trường vào danh sách mục tiêu của học sinh.
    /// </summary>
    Task<AddTargetSchoolResult> AddTargetSchoolAsync(Guid userId, AddTargetSchoolRequest request, CancellationToken ct = default);

    /// <summary>
    /// Lấy chi tiết một trường mục tiêu theo ID (kiểm tra quyền sở hữu của user).
    /// </summary>
    Task<TargetSchoolDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// [UC-33] Lấy danh sách trường mục tiêu kèm tóm tắt số lượng theo phân nhóm và hạn nộp gần nhất.
    /// </summary>
    Task<TargetSchoolSummaryDto> GetTargetSchoolsAsync(Guid userId, string? category = null, string? status = null, CancellationToken ct = default);

    /// <summary>
    /// [UC-32] Chọn hoặc thay đổi vòng nộp hồ sơ (Early Action, Early Decision, Regular...) và hạn nộp.
    /// </summary>
    Task<UpdateApplicationRoundResult> UpdateApplicationRoundAsync(Guid userId, Guid targetSchoolId, UpdateApplicationRoundRequest request, CancellationToken ct = default);

    /// <summary>
    /// [UC-31] Xóa một trường khỏi danh sách mục tiêu theo TargetSchool ID.
    /// </summary>
    Task<bool> RemoveTargetSchoolAsync(Guid userId, Guid targetSchoolId, CancellationToken ct = default);

    /// <summary>
    /// [UC-31] Xóa trường khỏi danh sách mục tiêu theo University Code hoặc University ID.
    /// </summary>
    Task<bool> RemoveTargetSchoolByUniversityAsync(Guid userId, string universityIdentifier, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra trạng thái trường mục tiêu theo mã trường hoặc ID trường.
    /// </summary>
    Task<TargetSchoolDto?> CheckTargetSchoolAsync(Guid userId, string? universityCode, Guid? universityId, CancellationToken ct = default);
}
