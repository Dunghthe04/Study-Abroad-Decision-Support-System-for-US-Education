using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.TargetSchools;

/// <summary>
/// Repository quản lý dữ liệu trường mục tiêu của người dùng (bảng target_schools).
/// </summary>
public interface ITargetSchoolRepository
{
    Task<TargetSchool?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TargetSchoolDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default);
    Task<TargetSchool?> GetByUserAndUniversityAsync(Guid userId, Guid universityId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid userId, Guid universityId, CancellationToken ct = default);
    Task<TargetSchool> AddAsync(TargetSchool targetSchool, CancellationToken ct = default);
    Task<University?> GetUniversityAsync(Guid universityId, CancellationToken ct = default);
    Task<University?> GetUniversityByCodeAsync(string code, CancellationToken ct = default);
    Task<string?> GetUniversityDeadlinesJsonAsync(Guid universityId, CancellationToken ct = default);
}
