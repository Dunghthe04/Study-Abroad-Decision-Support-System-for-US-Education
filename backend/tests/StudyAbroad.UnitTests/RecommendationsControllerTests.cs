using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class RecommendationsControllerTests
{
    // Service giả: ghi lại userId được gọi
    private sealed class FakeService : IRecommendationService
    {
        public List<Guid> Calls { get; } = [];

        public Task<RecommendationResultDto?> CreateAsync(Guid userId, CancellationToken ct = default)
        {
            Calls.Add(userId);
            return Task.FromResult<RecommendationResultDto?>(new(Guid.NewGuid(), DateTime.UtcNow, "undergraduate", [], []));
        }

        public Task<RecommendationResultDto?> GetLatestAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<RecommendationResultDto?>(null);
    }

    private static RecommendationsController NewController(FakeService service, Guid? userId)
    {
        var claims = userId is { } id ? new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) } : [];
        return new RecommendationsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
            }
        };
    }

    [Fact]
    public void Controller_RequiresLogin() =>
        Assert.NotNull(typeof(RecommendationsController).GetCustomAttribute<AuthorizeAttribute>());

    [Fact]
    public void Create_DoesNotAcceptUserIdFromRequest()
    {
        // Chống IDOR: userId chỉ lấy từ cookie đăng nhập, không có tham số nào để client tự truyền
        var parameters = typeof(RecommendationsController).GetMethod(nameof(RecommendationsController.Create))!.GetParameters();
        Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(Guid));
    }

    [Fact]
    public async Task Create_UsesUserIdFromToken()
    {
        var service = new FakeService();
        var userId = Guid.NewGuid();

        var result = await NewController(service, userId).Create(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal([userId], service.Calls);
    }

    [Fact]
    public async Task Create_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var service = new FakeService();

        var result = await NewController(service, userId: null).Create(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Empty(service.Calls);
    }
}
