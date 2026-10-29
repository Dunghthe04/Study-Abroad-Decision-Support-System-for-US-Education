using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

public interface IStudyCenterService
{
    Task<PagedResult<StudyCenterDto>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default);
    Task<StudyCenterDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StudyCenterDto>> ReferAsync(CenterReferralQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetReferralCitiesAsync(CancellationToken ct = default);
}

public class StudyCenterService(IStudyCenterRepository repository) : IStudyCenterService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<StudyCenterDto>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default)
    {
        var normalized = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, MaxPageSize),
        };

        var result = await repository.SearchAsync(normalized, ct);
        return new PagedResult<StudyCenterDto>(
            result.Items.Select(ToDto).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<StudyCenterDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var center = await repository.GetByIdAsync(id, ct);
        return center is null ? null : ToDto(center);
    }

    // Tối đa 3 trung tâm cho chat gợi ý (B6); luật chọn nằm ở CenterReferral
    public async Task<IReadOnlyList<StudyCenterDto>> ReferAsync(CenterReferralQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.Service))
            throw new ArgumentException("Service is required.", nameof(query));
        var centers = await repository.ListReferableAsync(ct);
        return CenterReferral.Pick(centers, query.Service, query.StudyLevel, query.City).Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<string>> GetReferralCitiesAsync(CancellationToken ct = default) =>
        CenterReferral.Cities(await repository.ListReferableAsync(ct));

    internal static StudyCenterDto ToDto(StudyCenter c) =>
        new(c.Id, c.Code, c.Name, c.Website, c.Address, c.City, c.StudyLevels, c.Services, c.SurveyedAt);
}
