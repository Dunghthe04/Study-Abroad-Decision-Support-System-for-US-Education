using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;

namespace StudyAbroad.UnitTests;

// Chat hỏi backend một chữ có phải tên tỉnh / thành không, để tên gọi khác chỉ khai ở một chỗ (VietnamProvinces)
public class ProvinceResolveEndpointTests
{
    private static string? Resolve(string? name)
    {
        var result = new RoadmapController(null!).ResolveProvince(name);
        return Assert.IsType<ResolvedProvince>(Assert.IsType<OkObjectResult>(result.Result).Value).Province;
    }

    [Theory]
    [InlineData("Hà Nội", "Hà Nội")]
    [InlineData("sài gòn", "TP. Hồ Chí Minh")]
    [InlineData("TP. HCM", "TP. Hồ Chí Minh")]
    [InlineData("da nang", "Đà Nẵng")]
    public void Resolve_ReturnsTheOfficialName(string typed, string official) => Assert.Equal(official, Resolve(typed));

    [Theory]
    [InlineData("3.6 nhé")]
    [InlineData("gợi ý trường cho em")]
    [InlineData("")]
    [InlineData(null)]
    public void Resolve_ReturnsNullWhenItIsNotAProvince(string? typed) => Assert.Null(Resolve(typed));

    // Advisor gọi không kèm đăng nhập, như API danh sách tỉnh
    [Fact]
    public void Resolve_DoesNotNeedSignIn() =>
        Assert.NotNull(typeof(RoadmapController).GetMethod(nameof(RoadmapController.ResolveProvince))!
            .GetCustomAttribute<AllowAnonymousAttribute>());
}
