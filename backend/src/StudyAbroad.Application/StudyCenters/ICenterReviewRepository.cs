namespace StudyAbroad.Application.StudyCenters;

/// <summary>Một đánh giá công khai của học sinh / phụ huynh về trung tâm (tên người đánh giá đã rút gọn).</summary>
public record CenterReviewDto(
    Guid Id,
    string ReviewerName,
    string ReviewerRole,
    bool IsVerified,
    DateTime CreatedAt,
    int Rating,
    string? Comment);

/// <summary>
/// Đánh giá hiển thị của một trung tâm. <see cref="TotalCount"/> và <see cref="AverageRating"/> tính trên toàn bộ
/// đánh giá đang hiển thị, còn <see cref="Items"/> chỉ là một trang của danh sách.
/// </summary>
public record CenterReviewsDto(
    int TotalCount,
    double? AverageRating,
    IReadOnlyList<CenterReviewDto> Items);

public interface ICenterReviewRepository
{
    /// <summary>
    /// Một trang đánh giá ở trạng thái "visible", mới nhất trước. Số lượng và điểm trung bình tính từ dữ liệu thật
    /// trên toàn bộ đánh giá hiển thị (không phụ thuộc trang).
    /// </summary>
    Task<CenterReviewsDto> GetVisibleAsync(Guid centerId, int page, int pageSize, CancellationToken ct = default);
}

/// <summary>Quy ước phân trang cho danh sách đánh giá.</summary>
public static class CenterReviewPaging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    /// <summary>Đưa page về &gt;= 1 và pageSize về khoảng 1..<see cref="MaxPageSize"/>.</summary>
    public static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize));
}

/// <summary>Cách làm tròn điểm trung bình, dùng chung cho danh sách trung tâm và trang chi tiết để hai nơi luôn khớp nhau.</summary>
public static class CenterRatingMath
{
    /// <summary>Làm tròn 2 chữ số thập phân, nửa thì làm tròn lên (4,125 thành 4,13), không dùng làm tròn kiểu banker's.</summary>
    public static double Round(double average) => Math.Round(average, 2, MidpointRounding.AwayFromZero);
}
