using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Api.Controllers
{
    [ApiController]
    [Route("api/v1/recommendations")]
    public class RecommendationsController(IRecommendationService service) : ControllerBase
    {
        // TODO(#17): khi có đăng nhập JWT thì lấy userId từ token, bỏ tham số query.
        [HttpPost]
        public async Task<ActionResult<RecommendationResultDto>> Create([FromQuery] Guid userId, CancellationToken ct)
        {
            var result = await service.CreateAsync(userId, ct);
            return result is null
                ? Problem("Người dùng chưa có hồ sơ học sinh, hãy tạo hồ sơ trước.", statusCode: StatusCodes.Status404NotFound)
                : Ok(result);
        }

        [HttpGet("latest")]
        public async Task<ActionResult<RecommendationResultDto>> GetLatest([FromQuery] Guid userId, CancellationToken ct)
        {
            var result = await service.GetLatestAsync(userId, ct);
            return result is null ? NotFound() : Ok(result);
        }
    }
}
