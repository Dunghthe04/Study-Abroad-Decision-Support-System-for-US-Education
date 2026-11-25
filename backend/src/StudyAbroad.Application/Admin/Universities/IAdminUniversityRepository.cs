using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.Universities;

public interface IAdminUniversityRepository
{
    Task<PagedResult<AdminUniversityListItemDto>> SearchAsync(AdminUniversityQuery query, CancellationToken ct = default);

    Task<bool> ExistsCodeAsync(string code, CancellationToken ct = default);

    /// <summary>So tên không phân biệt hoa thường.</summary>
    Task<bool> ExistsNameAsync(string name, Guid? excludeUniversityId = null, CancellationToken ct = default);

    void AddUniversity(University university);

    /// <summary>Trường có tracking để sửa; null nếu không có.</summary>
    Task<University?> GetUniversityAsync(Guid universityId, CancellationToken ct = default);

    void AddOffering(UniversityOffering offering);

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

    /// <summary>Thêm dòng audit vào cùng lần SaveChangesAsync với thay đổi dữ liệu.</summary>
    void AddAudit(AuditLog entry);

    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Như SaveChangesAsync nhưng trả false (không ném lỗi) khi vi phạm ràng buộc unique.</summary>
    Task<bool> TrySaveAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Media>> GetUniversityMediaAsync(Guid universityId, CancellationToken ct = default);
    Task SaveUniversityMediaAsync(Guid universityId, string? logoUrl, string? coverUrl, CancellationToken ct = default);
}
