using StudyAbroad.Application.StudyCenters;

namespace StudyAbroad.UnitTests;

/// <summary>UC-41: phân trang danh sách đánh giá và cách làm tròn điểm trung bình dùng chung.</summary>
public class CenterReviewRulesTests
{
    [Theory]
    [InlineData(1, 20, 1, 20)]
    [InlineData(3, 10, 3, 10)]
    [InlineData(0, 20, 1, 20)]
    [InlineData(-5, 20, 1, 20)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, -1, 1, 1)]
    [InlineData(2, 500, 2, CenterReviewPaging.MaxPageSize)]
    public void Normalize_ClampsPageAndPageSize(int page, int pageSize, int expectedPage, int expectedPageSize) =>
        Assert.Equal((expectedPage, expectedPageSize), CenterReviewPaging.Normalize(page, pageSize));

    [Fact]
    public void DefaultPageSize_IsWithinMax() =>
        Assert.InRange(CenterReviewPaging.DefaultPageSize, 1, CenterReviewPaging.MaxPageSize);

    [Theory]
    [InlineData(4.25, 4.25)]
    [InlineData(4.125, 4.13)] // banker's rounding sẽ ra 4.12
    [InlineData(4.135, 4.14)]
    [InlineData(5.0, 5.0)]
    [InlineData(3.0 + 1.0 / 3.0, 3.33)]
    public void Round_RoundsHalfAwayFromZeroToTwoDecimals(double input, double expected) =>
        Assert.Equal(expected, CenterRatingMath.Round(input));

    [Fact]
    public void Round_SharedValueForListAndDetailIsTheSame()
    {
        // Danh sách và trang chi tiết cùng nhận một giá trị 2 chữ số, nên cùng hiển thị 4,3 (không lệch 4,2 / 4,3).
        var shared = CenterRatingMath.Round((4 + 4 + 4 + 5) / 4.0);
        Assert.Equal(4.25, shared);
    }
}
