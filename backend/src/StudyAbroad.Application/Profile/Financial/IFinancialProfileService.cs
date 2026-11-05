namespace StudyAbroad.Application.Profile.Financial;

/// <summary>
/// [USAS-364] Service nghiệp vụ quản lý Hồ sơ tài chính và Hoạt động ngoại khóa.
/// </summary>
public interface IFinancialProfileService
{
    // Tài chính
    Task<FinancialProfileDto?> GetFinancialProfileAsync(Guid userId, CancellationToken ct = default);
    Task<FinancialProfileDto> SaveFinancialProfileAsync(Guid userId, SaveFinancialProfileRequest request, CancellationToken ct = default);

    // Hoạt động ngoại khóa
    Task<IReadOnlyList<ExtracurricularActivityDto>> GetActivitiesAsync(Guid userId, CancellationToken ct = default);
    Task<ExtracurricularActivityDto> AddActivityAsync(Guid userId, CreateExtracurricularRequest request, CancellationToken ct = default);
    Task<ExtracurricularActivityDto?> UpdateActivityAsync(Guid userId, Guid activityId, UpdateExtracurricularRequest request, CancellationToken ct = default);
    Task<bool> DeleteActivityAsync(Guid userId, Guid activityId, CancellationToken ct = default);

    // Thành tích / Giải thưởng
    Task<IReadOnlyList<StudentAchievementDto>> GetAchievementsAsync(Guid userId, CancellationToken ct = default);
    Task<StudentAchievementDto> AddAchievementAsync(Guid userId, CreateAchievementRequest request, CancellationToken ct = default);
    Task<bool> DeleteAchievementAsync(Guid userId, Guid achievementId, CancellationToken ct = default);

    // Tổng quan hồ sơ cho AI và Dashboard
    Task<StudentProfileSummaryDto> GetFullProfileSummaryAsync(Guid userId, CancellationToken ct = default);
}
