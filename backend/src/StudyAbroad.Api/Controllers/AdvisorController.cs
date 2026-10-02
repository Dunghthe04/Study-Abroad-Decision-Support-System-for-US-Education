using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>Proxies chat requests to the Python advisor service so the browser only talks to this API.</summary>
[ApiController]
[Route("api/v1/advisor")]
public class AdvisorController(IAdvisorClient advisor, ILogger<AdvisorController> logger) : ControllerBase
{
    [HttpPost("chat")]
    public async Task<ActionResult<AdvisorChatResponse>> Chat(AdvisorChatRequest request, CancellationToken ct)
    {
        if (request.Messages.Count == 0)
            return ValidationProblem("Messages must not be empty.");
        if (request.StudyLevel is not null && !StudyLevels.IsValid(request.StudyLevel))
            return ValidationProblem($"Unknown study level '{request.StudyLevel}'.");

        try
        {
            return Ok(await advisor.ChatAsync(request, ct));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Advisor service call failed");
            return Problem("Advisor service is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
