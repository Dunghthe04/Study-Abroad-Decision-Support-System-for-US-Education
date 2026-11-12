using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using StudyAbroad.Api.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

public class DefaultErrorMessagesTests
{
    private static ProblemDetailsContext Context(int status, string? detail = null) => new()
    {
        HttpContext = new DefaultHttpContext { Response = { StatusCode = status } },
        ProblemDetails = new ProblemDetails { Status = status, Title = "Forbidden", Detail = detail },
    };

    [Fact]
    public void Loi_khong_ly_do_duoc_dien_cau_tieng_Viet()
    {
        var ctx = Context(StatusCodes.Status403Forbidden);
        DefaultErrorMessages.FillMissingDetail(ctx);
        Assert.Equal(DefaultErrorMessages.For(403), ctx.ProblemDetails.Detail);
    }

    [Fact]
    public void Ly_do_controller_da_viet_thi_giu_nguyen()
    {
        var ctx = Context(StatusCodes.Status404NotFound, "Không tìm thấy trường.");
        DefaultErrorMessages.FillMissingDetail(ctx);
        Assert.Equal("Không tìm thấy trường.", ctx.ProblemDetails.Detail);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    public void Loi_5xx_dung_chung_cau_may_chu(int status) =>
        Assert.Equal(DefaultErrorMessages.For(500), DefaultErrorMessages.For(status));

    [Fact]
    public async Task Sai_vai_tro_tra_403_kem_ly_do()
    {
        // Tài khoản không phải học sinh/phụ huynh gửi hồ sơ (như ảnh lỗi "Forbidden" ở bước 6)
        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();
        var failure = AuthorizationFailure.Failed([new RolesAuthorizationRequirement([UserRoles.Student, UserRoles.Parent])]);

        await new AgeRestrictionResultHandler().HandleAsync(
            _ => Task.CompletedTask, http, new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(),
            PolicyAuthorizationResult.Forbid(failure));

        http.Response.Body.Position = 0;
        var body = await new StreamReader(http.Response.Body).ReadToEndAsync();
        Assert.Equal(StatusCodes.Status403Forbidden, http.Response.StatusCode);
        Assert.Contains("học sinh hoặc phụ huynh", body);
    }

    [Fact]
    public void Sai_vai_tro_noi_ro_danh_cho_ai()
    {
        var msg = DefaultErrorMessages.WrongRole([UserRoles.Student, UserRoles.Parent]);
        Assert.Contains("học sinh hoặc phụ huynh", msg);
    }
}
