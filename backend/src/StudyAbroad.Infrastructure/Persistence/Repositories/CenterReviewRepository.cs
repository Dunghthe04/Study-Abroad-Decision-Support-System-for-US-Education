using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class CenterReviewRepository(AppDbContext db) : ICenterReviewRepository
{
    private const string VisibleStatus = "visible";

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
            join u in db.Users.AsNoTracking() on r.UserId equals u.Id into usersJoin
            from u in usersJoin.DefaultIfEmpty()
            orderby r.CreatedAt descending, r.Id descending
            select new
            {
                r.Id,
                FullName = u != null ? u.FullName : null,
                Role = u != null ? u.Role : null,
                UserStatus = u != null ? u.Status : null,
                r.IsVerified,
                r.CreatedAt,
                r.Rating,
                r.Comment,
                r.CenterReplyContent,
                r.CenterReplyCreatedAt,
                r.CenterReplyEditedAt,
                r.CenterReplyStatus,
            })
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        var items = rows
            .Select(x => new CenterReviewDto(
                x.Id,
                FormatReviewerName(x.FullName, x.UserStatus),
                x.Role == UserRoles.Parent ? UserRoles.Parent : UserRoles.Student,
                x.IsVerified,
                x.CreatedAt,
                x.Rating,
                x.Comment,
                BuildPublicReply(x.CenterReplyStatus, x.CenterReplyContent, x.CenterReplyCreatedAt, x.CenterReplyEditedAt)))
            .ToList();

        return new CenterReviewsDto(stats.Count, CenterRatingMath.Round(stats.Average), items);
    }

    public static CenterReviewReplyDto? BuildPublicReply(string? status, string? content, DateTime? createdAt, DateTime? editedAt) =>
        string.Equals(status, "visible", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(content)
        && createdAt.HasValue
            ? new CenterReviewReplyDto(content, createdAt.Value, editedAt)
            : null;

    public const string DeletedReviewerLabel = "Người dùng đã xóa tài khoản";

    /// <summary>[UC-41] Khi tác giả đã xóa tài khoản (hoặc chờ xóa), giữ đánh giá nhưng hiển thị nhãn ẩn danh chuẩn.</summary>
    public static string FormatReviewerName(string? fullName, string? userStatus) =>
        fullName is null || string.Equals(userStatus, UserStatuses.Deleted, StringComparison.OrdinalIgnoreCase)
            ? DeletedReviewerLabel
            : MaskName(fullName);

    /// <summary>Rút gọn tên để bảo vệ người đánh giá: giữ họ, các từ còn lại chỉ lấy chữ cái đầu.</summary>
    public static string MaskName(string? fullName)
    {
        var parts = (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "Người dùng ẩn danh";
        if (parts.Length == 1) return parts[0];
        return parts[0] + " " + string.Join(" ", parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + "."));
    }
}
