using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudyAbroad.Api.RateLimiting;
using StudyAbroad.Application.Leads;

namespace StudyAbroad.Api.Controllers
{
    // [UC-116] Phụ huynh mở link trong mail, không cần tài khoản: đồng ý / từ chối gửi yêu cầu liên hệ của con dưới 16.
    // Token trong body để không nằm trong log URL. Lỗi: 404 link sai, 410 hết hạn, 409 đã có phụ huynh trả lời.
    [ApiController]
    [AllowAnonymous]
    [Route("api/v1/leads/parent-confirm")]
    public class LeadParentConfirmController(ILeadParentConfirmService service) : ControllerBase
    {
        [HttpPost("lookup")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public Task<IActionResult> Lookup(LeadParentTokenRequest request, CancellationToken ct) =>
            Run(() => service.LookupAsync(request.Token, ct));

        [HttpPost("accept")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public Task<IActionResult> Accept(LeadParentTokenRequest request, CancellationToken ct) =>
            Run(() => service.AcceptAsync(request.Token, ClientIp(), ct));

        [HttpPost("reject")]
        [EnableRateLimiting(AuthRateLimit.PolicyName)]
        public Task<IActionResult> Reject(LeadParentTokenRequest request, CancellationToken ct) =>
            Run(() => service.RejectAsync(request.Token, ClientIp(), ct));

        private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

        private async Task<IActionResult> Run(Func<Task<LeadParentConfirmInfo>> action)
        {
            try
            {
                return Ok(new { message = (string?)null, data = await action() });
            }
            catch (LeadRuleException ex)
            {
                return Problem(ex.Message, statusCode: ex.Status);
            }
        }
    }
}
