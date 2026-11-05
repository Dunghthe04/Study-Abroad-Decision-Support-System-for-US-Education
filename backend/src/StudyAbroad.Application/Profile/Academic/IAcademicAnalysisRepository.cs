using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Profile.Academic;

/// <summary>
/// [USAS-365] Interface Repository truy xuất dữ liệu bảng điểm, cấu hình thang điểm và kết quả phân tích học thuật.
/// </summary>
public interface IAcademicAnalysisRepository
{
    /// <summary>
    /// [USAS-365] Lấy hồ sơ học sinh theo UserId (chống truy cập trái quyền IDOR).
    /// </summary>
    Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// [USAS-365] Đảm bảo học sinh đã có bản ghi student_profiles trước khi nhập điểm.
    /// </summary>
    Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// [USAS-365] Lấy toàn bộ bảng điểm chi tiết theo hồ sơ học sinh.
    /// </summary>
    Task<IReadOnlyList<TranscriptScore>> GetScoresByProfileIdAsync(Guid profileId, CancellationToken ct = default);

    /// <summary>
    /// [USAS-365] Lưu hoặc cập nhật hàng loạt các đầu điểm trong bảng điểm.
    /// </summary>
    Task<IReadOnlyList<TranscriptScore>> SaveScoresAsync(Guid profileId, IEnumerable<TranscriptScore> scores, CancellationToken ct = default);

    /// <summary>
    /// [USAS-365] Xóa một hoặc nhiều đầu điểm theo danh sách Id thuộc quyền sở hữu của hồ sơ.
    /// </summary>
    Task DeleteScoresAsync(Guid profileId, IEnumerable<Guid> scoreIds, CancellationToken ct = default);

    /// <summary>
    /// [USAS-365] Đọc cấu hình thang quy đổi điểm từ bảng app_settings (key: grade_scale.10).
    /// </summary>
    Task<AppSetting?> GetAppSettingByKeyAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// [USAS-365] Lưu kết quả phân tích vào analysis_results (kind = 'gpa') và đồng bộ cột overall_gpa trong student_profiles.
    /// </summary>
    Task<AnalysisResult> SaveAnalysisResultAsync(AnalysisResult result, decimal overallGpa, CancellationToken ct = default);

    /// <summary>
    /// [USAS-365] Lấy bản ghi phân tích học thuật gần nhất (kind = 'gpa') của học sinh.
    /// </summary>
    Task<AnalysisResult?> GetLatestAnalysisAsync(Guid profileId, CancellationToken ct = default);
}
