using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

/// <summary>[UC-40] Chỉ số phản hồi 90 ngày gần nhất của trung tâm (chỉ hiển thị khi SampleCount >= 5).</summary>
public record CenterResponseMetrics(double? MedianResponseHours, double? ResponseRatePercent, int SampleCount);

public interface IStudyCenterRepository
{
    Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default);
    Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StudyCenter?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default);
    Task AddAsync(StudyCenter center, CancellationToken ct = default);
    Task UpdateAsync(StudyCenter center, CancellationToken ct = default);
    Task<StudyCenter?> GetByOwnerOrEmailAsync(Guid ownerUserId, string? email, CancellationToken ct = default);

    Task<Dictionary<Guid, CenterResponseMetrics>> GetResponseMetrics90dAsync(
        IReadOnlyCollection<Guid> centerIds,
        CancellationToken ct = default) =>
        Task.FromResult(new Dictionary<Guid, CenterResponseMetrics>());

    Task<CenterGuidanceCardsDto> GetGuidanceCardsAsync(CancellationToken ct = default) =>
        Task.FromResult(new CenterGuidanceCardsDto(null, null));
}
