using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class CenterReviewRepository(AppDbContext db) : ICenterReviewRepository
{
    private const string VisibleStatus = "visible";

    public async Task<CenterReviewsDto> GetVisibleAsync(Guid centerId, int take, CancellationToken ct = default)
    {
        var visible = db.CenterReviews.AsNoTracking()
            .Where(r => r.StudyCenterId == centerId && r.Status == VisibleStatus);

        var total = await visible.CountAsync(ct);
        if (total == 0)
            return new CenterReviewsDto(0, null, []);

        var average = await visible.AverageAsync(r => (double)r.Rating, ct);

        var rows = await (
            from r in visible
            join u in db.Users.AsNoTracking() on r.UserId equals u.Id
            orderby r.CreatedAt descending
            select new { r.Id, u.FullName, u.Role, r.IsVerified, r.CreatedAt, r.Rating, r.Comment })
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);

        var items = rows
            .Select(x => new CenterReviewDto(
                x.Id,
                MaskName(x.FullName),
                x.Role == UserRoles.Parent ? UserRoles.Parent : UserRoles.Student,
                x.IsVerified,
                x.CreatedAt,
                x.Rating,
                x.Comment))
            .ToList();

        return new CenterReviewsDto(total, Math.Round(average, 1), items);
    }

    /// <summary>Rút gọn tên để bảo vệ người đánh giá: giữ họ, các từ còn lại chỉ lấy chữ cái đầu.</summary>
    private static string MaskName(string? fullName)
    {
        var parts = (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "Người dùng ẩn danh";
        if (parts.Length == 1) return parts[0];
        return parts[0] + " " + string.Join(" ", parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + "."));
    }
}
