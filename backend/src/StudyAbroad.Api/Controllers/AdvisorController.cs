using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Domain.Constants;
using System;

namespace StudyAbroad.Api.Controllers;

/// <summary>Proxies chat requests to the Python advisor service so the browser only talks to this API.</summary>
[ApiController]
[Authorize]
[Route("api/v1/advisor")]
public class AdvisorController(ChatService chat, ILogger<AdvisorController> logger) : ControllerBase
{
    [Authorize(Policy = AgeFeatures.ProcessStudentData)] // [Q11] dưới 16 cần phụ huynh đồng ý
    [HttpPost("chat")]
    public async Task<ActionResult<AdvisorChatResponse>> Chat(AdvisorChatRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { message = "Vui lòng đăng nhập." });
        if (request.Messages.Count == 0)
            return ValidationProblem("Messages must not be empty.");
        if (request.StudyLevel is not null && !StudyLevels.IsValid(request.StudyLevel))
            return ValidationProblem($"Unknown study level '{request.StudyLevel}'.");

        try
        {
            return Ok(await chat.ChatAsync(userId.Value, request, ct));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Advisor service call failed");
            return Problem("Advisor service is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
