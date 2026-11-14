using StudyAbroad.Infrastructure.Persistence.Repositories;

namespace StudyAbroad.UnitTests;

/// <summary>UC-41: rút gọn tên người đánh giá trước khi hiển thị công khai.</summary>
public class CenterReviewRepositoryTests
{
    [Theory]
    [InlineData("Nguyễn Văn An", "Nguyễn V. A.")]
    [InlineData("  Trần   Thị  Bích  ", "Trần T. B.")]
    [InlineData("lê minh", "lê M.")]
    [InlineData("Phạm", "Phạm")]
    public void MaskName_KeepsFirstWordAndInitialsOfTheRest(string input, string expected) =>
        Assert.Equal(expected, CenterReviewRepository.MaskName(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskName_BlankName_FallsBackToAnonymous(string? input) =>
        Assert.Equal("Người dùng ẩn danh", CenterReviewRepository.MaskName(input));

    [Fact]
    public void MaskName_NeverExposesFullGivenNames()
    {
        var masked = CenterReviewRepository.MaskName("Nguyễn Hoàng Long");
        Assert.DoesNotContain("Hoàng", masked);
        Assert.DoesNotContain("Long", masked);
    }
}
