using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Universities;

public record UniversityDetailData(
    University University, string? StateName, IReadOnlyList<UniversityOffering> Offerings,
    IReadOnlyList<UniversityMajorRow> Majors, IReadOnlyList<Media> Media,
    IReadOnlyList<UniversityRankingDto> Rankings, IReadOnlyList<FieldEvidence> Evidence);

public record UniversityMajorRow(Guid OfferingId, string Name, string? CipCode, bool IsStem, bool IsVerified);

public interface IUniversityDetailRepository
{
    Task<UniversityDetailData?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<PagedResult<UniversityListItemDto>> SearchAsync(UniversitySearchQuery query, CancellationToken ct = default);
    Task<UniversityFilterOptionsDto> GetFilterOptionsAsync(CancellationToken ct = default);
}

public interface IUniversityDetailService
{
    Task<UniversityDetailDto?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<PagedResult<UniversityListItemDto>> SearchAsync(UniversitySearchQuery query, CancellationToken ct = default);
    Task<UniversityFilterOptionsDto> GetFilterOptionsAsync(CancellationToken ct = default);
}
