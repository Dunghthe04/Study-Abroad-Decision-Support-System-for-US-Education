using Microsoft.EntityFrameworkCore;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>
/// Các phiên bản chính sách mà phụ huynh đã đồng ý cho con xử lý dữ liệu (consents.user_id là CON, granted_by_user_id là phụ huynh).
/// Phụ huynh rút đồng ý thì dòng đó đổi thành granted = false, nên chỉ lấy dòng còn hiệu lực.
/// Dùng chung cho mọi nơi cho admin đọc nội dung chat (mẫu hội thoại UC-90, đợt duyệt mẫu) để cùng một luật.
/// </summary>
internal static class ChildConsentVersions
{
    public static async Task<ILookup<Guid, string?>> LoadAsync(
        AppDbContext db, IReadOnlyCollection<Guid> ownerIds, CancellationToken ct = default) =>
        (await db.Consents.AsNoTracking()
            .Where(c => ownerIds.Contains(c.UserId) && c.Purpose == ConsentPurposes.ChildDataProcessing && c.Granted)
            .Select(c => new { c.UserId, c.PolicyVersion })
            .ToListAsync(ct))
        .ToLookup(c => c.UserId, c => c.PolicyVersion);
}
