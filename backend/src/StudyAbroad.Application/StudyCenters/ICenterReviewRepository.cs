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

/// <summary>Các đánh giá hiển thị của một trung tâm kèm số liệu tổng hợp tính từ chính các đánh giá đó.</summary>
public record CenterReviewsDto(
    int TotalCount,
    double? AverageRating,
    IReadOnlyList<CenterReviewDto> Items);

public interface ICenterReviewRepository
{
    /// <summary>Chỉ lấy đánh giá ở trạng thái "visible"; số lượng và điểm trung bình tính từ dữ liệu thật.</summary>
    Task<CenterReviewsDto> GetVisibleAsync(Guid centerId, int take, CancellationToken ct = default);
}
