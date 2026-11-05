using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.Universities;

public interface IAdminUniversityRepository
{
    Task<PagedResult<AdminUniversityListItemDto>> SearchAsync(AdminUniversityQuery query, CancellationToken ct = default);

    Task<bool> ExistsCodeAsync(string code, CancellationToken ct = default);

    void AddUniversity(University university);

    /// <summary>Trường có tracking để sửa; null nếu không có.</summary>
    Task<University?> GetUniversityAsync(Guid universityId, CancellationToken ct = default);

    Task<IReadOnlyList<UniversityOffering>> ListOfferingsAsync(Guid universityId, CancellationToken ct = default);

    /// <summary>Offering có tracking để sửa; null nếu không có hoặc không thuộc trường này.</summary>
    Task<UniversityOffering?> GetOfferingAsync(Guid universityId, Guid offeringId, CancellationToken ct = default);

    Task<IReadOnlyList<AdminOfferingMajorRow>> ListMajorRowsAsync(IReadOnlyList<Guid> offeringIds, CancellationToken ct = default);

    /// <summary>Liên kết ngành của một offering, có tracking để thêm/xóa/sửa.</summary>
    Task<List<UniversityOfferingMajor>> GetMajorLinksAsync(Guid offeringId, CancellationToken ct = default);

    Task<IReadOnlyList<Major>> ListMajorsAsync(CancellationToken ct = default);

    /// <summary>Tên hiển thị của người sửa; null nếu không có id hoặc tài khoản đã bị xóa.</summary>
    Task<string?> GetUserDisplayNameAsync(Guid? userId, CancellationToken ct = default);

    void AddMajorLink(UniversityOfferingMajor link);

    void RemoveMajorLink(UniversityOfferingMajor link);

    Task SaveChangesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Media>> GetUniversityMediaAsync(Guid universityId, CancellationToken ct = default);
    Task SaveUniversityMediaAsync(Guid universityId, string? logoUrl, string? coverUrl, CancellationToken ct = default);
}
