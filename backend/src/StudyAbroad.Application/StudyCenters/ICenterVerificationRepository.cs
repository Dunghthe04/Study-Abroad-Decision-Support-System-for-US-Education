using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.StudyCenters;

public record CenterVerificationQuery(
    string? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

public interface ICenterVerificationRepository
{
    Task<CenterVerificationRequest?> GetCurrentByCenterIdAsync(Guid centerId, CancellationToken ct = default);
    Task<CenterVerificationRequest?> GetByIdWithDocumentsAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CenterVerificationRequest>> SearchAsync(CenterVerificationQuery query, CancellationToken ct = default);
    Task AddRequestAsync(CenterVerificationRequest request, CancellationToken ct = default);
    Task UpdateRequestAsync(CenterVerificationRequest request, CancellationToken ct = default);
    Task<CenterVerificationDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default);
    Task<bool> ExistsVerifiedTaxCodeAsync(string taxCode, Guid? excludeCenterId = null, CancellationToken ct = default);
}

