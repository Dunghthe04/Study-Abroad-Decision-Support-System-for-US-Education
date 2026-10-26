using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

/// <summary>Lời mời / liên kết phụ huynh–học sinh (bảng app.parent_student_links).</summary>
public interface IParentLinkRepository
{
    Task AddAsync(ParentStudentLink link, CancellationToken ct = default);

    /// <summary>Ném <see cref="Common.ConcurrencyConflictException"/> khi request khác đã sửa link (xmin).</summary>
    Task UpdateAsync(ParentStudentLink link, CancellationToken ct = default);

    /// <summary>Mọi link của học sinh, mới nhất trước.</summary>
    Task<List<ParentStudentLink>> GetByStudentAsync(Guid studentUserId, CancellationToken ct = default);
}
