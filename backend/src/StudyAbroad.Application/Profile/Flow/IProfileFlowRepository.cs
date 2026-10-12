using StudyAbroad.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// [USAS-640] Repository phục vụ luồng hồ sơ đa bước và metadata động.
/// </summary>
public interface IProfileFlowRepository
{
    Task<string?> GetAppSettingValueAsync(string key, CancellationToken ct = default);
    Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default);
    Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetOfferingMajorsAsync(CancellationToken ct = default);
}
