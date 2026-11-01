using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

public interface IStudyCenterRepository
{
    Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default);
    Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default);
    Task AddAsync(StudyCenter center, CancellationToken ct = default);

}
