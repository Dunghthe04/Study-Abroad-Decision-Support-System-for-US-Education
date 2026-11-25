using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class CenterReviewRepository(AppDbContext db) : ICenterReviewRepository
{
    private const string VisibleStatus = "visible";

    /// <summary>[UC-45 / UC-75 lớp 3] Tên người đánh giá đã bị xóa hẳn tài khoản.</summary>
    public const string DeletedReviewerName = "Người dùng đã xóa tài khoản";

    public async Task<CenterReviewsDto> GetVisibleAsync(Guid centerId, int page, int pageSize, CancellationToken ct = default)
    {
        var (safePage, safePageSize) = CenterReviewPaging.Normalize(page, pageSize);

        var visible = db.CenterReviews.AsNoTracking()
            .Where(r => r.StudyCenterId == centerId && r.Status == VisibleStatus);

        // Một truy vấn cho cả số lượng và điểm trung bình để hai số liệu luôn cùng một thời điểm.
        var stats = await visible
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Average = g.Average(r => (double)r.Rating) })
            .FirstOrDefaultAsync(ct);
        if (stats is null || stats.Count == 0)
            return new CenterReviewsDto(0, null, []);

        // Id là tie-breaker để thứ tự ổn định khi nhiều đánh giá cùng thời điểm, tránh lặp hoặc sót khi sang trang.
        var rows = await (
            from r in visible
            // [UC-45 / UC-75 lớp 3] Nối trái: đánh giá của người đã bị xóa hẳn (UserId null) vẫn hiện
            join u in db.Users.AsNoTracking() on r.UserId equals (Guid?)u.Id into reviewers
            from u in reviewers.DefaultIfEmpty()
            orderby r.CreatedAt descending, r.Id descending
            select new { r.Id, FullName = u != null ? u.FullName : null, Role = u != null ? u.Role : null, Deleted = r.UserId == null, r.IsVerified, r.CreatedAt, r.Rating, r.Comment })
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        var items = rows
            .Select(x => new CenterReviewDto(
                x.Id,
                x.Deleted ? DeletedReviewerName : MaskName(x.FullName),
                x.Deleted ? null : x.Role == UserRoles.Parent ? UserRoles.Parent : UserRoles.Student,
                x.IsVerified,
                x.CreatedAt,
                x.Rating,
                x.Comment))
            .ToList();

        return new CenterReviewsDto(stats.Count, CenterRatingMath.Round(stats.Average), items);
    }

    /// <summary>Rút gọn tên để bảo vệ người đánh giá: giữ họ, các từ còn lại chỉ lấy chữ cái đầu.</summary>
    public static string MaskName(string? fullName)
    {
        var parts = (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "Người dùng ẩn danh";
        if (parts.Length == 1) return parts[0];
        return parts[0] + " " + string.Join(" ", parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + "."));
    }
}
