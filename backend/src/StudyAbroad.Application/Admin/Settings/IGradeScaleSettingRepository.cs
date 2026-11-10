using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.Settings;

/// <summary>Đọc/ghi dòng grade_scale.10 trong app_settings và lịch sử sửa trong audit_logs.</summary>
public interface IGradeScaleSettingRepository
{
    /// <summary>Dòng app_settings grade_scale.10 (được theo dõi để sửa), null nếu chưa có.</summary>
    Task<AppSetting?> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Hồ sơ đã có GPA, bảng điểm thang 10, kèm điểm từng môn có học kỳ (transcript_scores), để tính lại GPA khi đổi bảng.
    /// Hồ sơ được theo dõi: sửa OverallGpa rồi SaveAsync sẽ lưu luôn.
    /// </summary>
    Task<IReadOnlyList<(StudentProfile Profile, IReadOnlyList<TranscriptScore> Scores)>> GetTranscriptGpaProfilesAsync(CancellationToken ct = default);

    /// <summary>Lưu bảng (thêm nếu mới), GPA hồ sơ đã sửa và audit trong cùng một lần SaveChanges.</summary>
    Task SaveAsync(AppSetting setting, bool isNew, AuditLog audit, CancellationToken ct = default);

    /// <summary>Các lần sửa gần nhất, mới trước, kèm tên người sửa.</summary>
    Task<IReadOnlyList<(AuditLog Log, string? ActorName)>> GetHistoryAsync(int limit, CancellationToken ct = default);
}
