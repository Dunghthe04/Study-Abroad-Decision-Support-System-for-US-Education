using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Profile.Financial;

/// <summary>
/// [USAS-364] Interface Repository quản lý lưu trữ Hồ sơ tài chính và Hoạt động ngoại khóa.
/// Sử dụng trực tiếp thực thể StudentProfile và ProfileActivity theo kiến trúc chuẩn của nhóm.
/// </summary>
public interface IFinancialProfileRepository
{
    Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default);
    Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default);

    Task<IReadOnlyList<ProfileActivity>> GetActivitiesByProfileIdAsync(Guid studentProfileId, string? kind = null, CancellationToken ct = default);
    Task<ProfileActivity?> GetActivityByIdAsync(Guid activityId, CancellationToken ct = default);
    Task AddActivityAsync(ProfileActivity activity, CancellationToken ct = default);
    Task UpdateActivityAsync(ProfileActivity activity, CancellationToken ct = default);
    Task DeleteActivityAsync(ProfileActivity activity, CancellationToken ct = default);
}
