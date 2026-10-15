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

        //AI tư vấn 1 trường trong lần gợi ý gần nhất của chính người dùng (gọi khi mở "Chi tiết")
        [HttpPost("latest/schools/{offeringId:guid}/advice")]
        public async Task<ActionResult<SchoolAdviceDto>> Advice(Guid offeringId, CancellationToken ct)
        {
            if (User.GetUserId() is not { } userId) return Unauthorized();
            try
            {
                var advice = await service.ExplainSchoolAsync(userId, offeringId, ct);
                return advice is null ? NotFound() : Ok(advice);
            }
            catch (RecommendationAiException)
            {
                return Problem("AI tư vấn tạm thời chưa phản hồi, hãy thử lại sau.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }
    }
}
