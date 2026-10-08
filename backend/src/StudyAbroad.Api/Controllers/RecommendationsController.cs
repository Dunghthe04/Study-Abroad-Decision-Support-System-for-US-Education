using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/recommendations")]
    public class RecommendationsController(IRecommendationService service) : ControllerBase
    {
        //Người dùng lấy từ cookie đăng nhập, không nhận userId từ request: không chạy được gợi ý cho hồ sơ người khác
        [HttpPost]
        public async Task<ActionResult<RecommendationResultDto>> Create(CancellationToken ct)
        {
            if (User.GetUserId() is not { } userId) return Unauthorized();
            var result = await service.CreateAsync(userId, ct);
            return result is null
                ? Problem("Người dùng chưa có hồ sơ học sinh, hãy tạo hồ sơ trước.", statusCode: StatusCodes.Status404NotFound)
                : Ok(result);
        }

        [HttpGet("latest")]
        public async Task<ActionResult<RecommendationResultDto>> GetLatest(CancellationToken ct)
        {
            if (User.GetUserId() is not { } userId) return Unauthorized();
            var result = await service.GetLatestAsync(userId, ct);
            return result is null ? NotFound() : Ok(result);
        }
    }
}
