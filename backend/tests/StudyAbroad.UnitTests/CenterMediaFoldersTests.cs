using StudyAbroad.Application.StudyCenters;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>Chỉ ảnh trung tâm tự tải lên (thư mục riêng của tài khoản) mới được xóa trên CDN.</summary>
public class CenterMediaFoldersTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Other = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private const string Cloud = "https://res.cloudinary.com/demo/image/upload/v1/usas";

    [Fact]
    public void For_BuildsTheFolderFromTheOwnerId() =>
        Assert.Equal("centers/11111111222233334444555555555555", CenterMediaFolders.For(Owner));

    [Theory]
    [InlineData("centers/11111111222233334444555555555555/abc.png")]
    [InlineData("centers/11111111222233334444555555555555/abc.webp?_a=x")]
    [InlineData("centers/11111111222233334444555555555555/sub/abc.png")]
    public void IsOwnedBy_FileInTheOwnersFolder_IsTrue(string path) =>
        Assert.True(CenterMediaFolders.IsOwnedBy(Owner, $"{Cloud}/{path}"));

    [Fact]
    public void IsOwnedBy_UrlWithTransformationAndVersion_StillMatches() =>
        Assert.True(CenterMediaFolders.IsOwnedBy(Owner,
            "https://res.cloudinary.com/demo/image/upload/c_fill,w_200/v1/usas/centers/11111111222233334444555555555555/abc.png"));

    [Theory]
    [InlineData("universities/harvard.png")]
    [InlineData("centers/99999999888877776666555555555555/abc.png")]
    [InlineData("lead-chat/abc.png")]
    [InlineData("centers/11111111222233334444555555555555")]
    [InlineData("centers/11111111222233334444555555555555/")]
    public void IsOwnedBy_AnythingElse_IsFalse(string path) =>
        Assert.False(CenterMediaFolders.IsOwnedBy(Owner, $"{Cloud}/{path}"));

    [Fact]
    public void IsOwnedBy_OwnersFolderOnlyInTheQueryOrFragment_IsFalse()
    {
        Assert.False(CenterMediaFolders.IsOwnedBy(Owner, $"{Cloud}/universities/harvard.png?x=/centers/{Owner:N}/a.png"));
        Assert.False(CenterMediaFolders.IsOwnedBy(Owner, $"{Cloud}/universities/harvard.png#/centers/{Owner:N}/a.png"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    public void IsOwnedBy_NotAUrl_IsFalse(string? url) => Assert.False(CenterMediaFolders.IsOwnedBy(Owner, url));

    [Fact]
    public void IsOwnedBy_AnotherUsersFolder_IsNotMine() =>
        Assert.False(CenterMediaFolders.IsOwnedBy(Other, $"{Cloud}/centers/{Owner:N}/a.png"));
}
