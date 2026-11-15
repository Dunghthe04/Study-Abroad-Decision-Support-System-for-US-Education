using StudyAbroad.Application.Profile.Academic;

namespace StudyAbroad.Application.Admin.Settings;

/// <summary>
/// Bảng quy đổi đang dùng. Version là mốc sửa gần nhất (UpdatedAt, chưa sửa lần nào thì CreatedAt);
/// gửi lại khi lưu để biết có ai sửa trước không. IsDefault: DB chưa có bảng hoặc bảng sai luật, đang dùng bảng mặc định.
/// DefaultConfig: bảng mặc định (mốc THPT EducationUSA) cho nút "Dùng bảng mặc định" bên trang quản trị, để FE không phải chép mốc.
/// </summary>
public record GradeScaleSettingDto(GradeScaleConfigDto Config, DateTime? Version, bool IsDefault, GradeScaleConfigDto DefaultConfig);

/// <summary>Một khoảng do moderator nhập. MaxScore server tự tính từ mốc kế tiếp nên không cần gửi.</summary>
public record GradeScaleRuleInput(decimal MinScore, decimal Gpa4, string? LetterGrade);

/// <summary>Version lấy từ lần GET; null chỉ hợp lệ khi DB chưa có bảng.</summary>
public record UpdateGradeScaleRequest(List<GradeScaleRuleInput>? Rules, string? SourceName, DateTime? Version);

/// <summary>Một lần sửa bảng, đọc từ audit_logs (action grade_scale.update).</summary>
public record GradeScaleHistoryItemDto(
    DateTime ChangedAt, Guid? ActorUserId, string? ActorName, GradeScaleConfigDto? Old, GradeScaleConfigDto? New);

/// <summary>Kết quả lưu: bảng mới và số hồ sơ đã tính lại GPA (chỉ hồ sơ có GPA lấy từ học bạ).</summary>
public record GradeScaleSavedDto(GradeScaleSettingDto Setting, int RecomputedProfiles);

public enum GradeScaleUpdateStatus { Ok, Invalid, Conflict }

public record GradeScaleUpdateResult(
    GradeScaleUpdateStatus Status, GradeScaleSettingDto? Value, IReadOnlyList<string> Errors, int RecomputedProfiles = 0)
{
    public static GradeScaleUpdateResult Ok(GradeScaleSettingDto value, int recomputedProfiles) =>
        new(GradeScaleUpdateStatus.Ok, value, [], recomputedProfiles);
    public static GradeScaleUpdateResult Invalid(IReadOnlyList<string> errors) => new(GradeScaleUpdateStatus.Invalid, null, errors);
    public static GradeScaleUpdateResult Conflict(GradeScaleSettingDto current) =>
        new(GradeScaleUpdateStatus.Conflict, current, ["Bảng đã được người khác sửa, hãy tải lại rồi sửa tiếp."]);
}
