using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Auth;

namespace StudyAbroad.Api.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController(IAuthService auth) : ControllerBase
    {
        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register(RegisterRequest request, CancellationToken ct)
        {
            var result = await auth.RegisterAsync(request, ct);
            return result.Error switch
            {
                AuthError.None => StatusCode(StatusCodes.Status201Created, result.User),
                AuthError.EmailTaken => Problem(result.Message, statusCode: StatusCodes.Status409Conflict),
                _ => ValidationProblem(result.Message),
            };
        }
    }
}
