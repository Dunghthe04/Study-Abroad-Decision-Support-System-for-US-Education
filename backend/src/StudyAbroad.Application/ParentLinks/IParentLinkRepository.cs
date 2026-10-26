using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

/// <summary>Lời mời / liên kết phụ huynh–học sinh (bảng app.parent_student_links) và bản ghi đồng ý đi kèm.</summary>
public interface IParentLinkRepository
{
    Task AddAsync(ParentStudentLink link, CancellationToken ct = default);

    /// <summary>Ném <see cref="Common.ConcurrencyConflictException"/> khi request khác đã sửa link (xmin).</summary>
    Task UpdateAsync(ParentStudentLink link, CancellationToken ct = default);

    Task<ParentStudentLink?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<ParentStudentLink?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Mọi link của học sinh, mới nhất trước.</summary>
    Task<List<ParentStudentLink>> GetByStudentAsync(Guid studentUserId, CancellationToken ct = default);

    /// <summary>Các link đang hiệu lực của phụ huynh (con đã liên kết).</summary>
    Task<List<ParentStudentLink>> GetActiveByParentAsync(Guid parentUserId, CancellationToken ct = default);

    Task<Consent?> GetConsentAsync(Guid consentId, CancellationToken ct = default);

    /// <summary>Phụ huynh chấp nhận: thêm consent và cập nhật link trong cùng một lần lưu (một transaction).
    /// Ném <see cref="Common.ConcurrencyConflictException"/> khi link vừa bị sửa.</summary>
    Task SaveAcceptAsync(ParentStudentLink link, Consent consent, CancellationToken ct = default);

    /// <summary>Rút đồng ý / hủy liên kết: cập nhật link và consent (nếu có) trong cùng một lần lưu.</summary>
    Task SaveRevokeAsync(ParentStudentLink link, Consent? consent, CancellationToken ct = default);
}
