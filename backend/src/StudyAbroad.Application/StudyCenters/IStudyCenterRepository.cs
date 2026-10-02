using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

public interface IStudyCenterRepository
{
    Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default);
    Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
