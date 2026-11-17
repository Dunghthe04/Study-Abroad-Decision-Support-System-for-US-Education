using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.Scholarships;

public interface IAdminScholarshipRepository
{
    Task<PagedResult<AdminScholarshipDto>> SearchAsync(AdminScholarshipQuery query, CancellationToken ct = default);

    /// <summary>Học bổng có tracking để sửa; null nếu không có.</summary>
    Task<Scholarship?> GetAsync(Guid scholarshipId, CancellationToken ct = default);

    /// <summary>Tên trường; null nếu không có id hoặc trường không tồn tại.</summary>
    Task<string?> GetUniversityNameAsync(Guid? universityId, CancellationToken ct = default);

    Task<string?> GetUserDisplayNameAsync(Guid? userId, CancellationToken ct = default);

    void Add(Scholarship scholarship);

    void AddAudit(AuditLog entry);

    Task SaveChangesAsync(CancellationToken ct = default);
}
