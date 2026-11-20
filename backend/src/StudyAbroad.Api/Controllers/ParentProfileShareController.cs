using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Hồ sơ học thuật giữa phụ huynh và con: phụ huynh xem hồ sơ con (chỉ đọc) và chọn con managed nào là con thật vừa liên kết;
/// học sinh chọn dùng bản phụ huynh nhập thay hay giữ bản của mình. UserId lấy từ JWT, không nhận từ client.
/// </summary>
[ApiController]
[Route("api/v1/profile/academic")]
public class ParentProfileShareController(IParentProfileShareService shareService) : ControllerBase
{
    /// <summary>Phụ huynh: hồ sơ các con đã liên kết và các con thật đang chờ ghép với con managed.</summary>
    [HttpGet("children")]
    [Authorize(Roles = UserRoles.Parent)]
    public async Task<IActionResult> GetChildren(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return ToResult(await shareService.GetParentOverviewAsync(userId, ct));
    }

    /// <summary>[1.5d] Phụ huynh ghép con thật (linkId) với con managed đã nhập, hoặc managedChildId null là không phải ai.</summary>
    [HttpPost("match")]
    [Authorize(Roles = UserRoles.Parent)]
    public async Task<IActionResult> Match([FromBody] MatchManagedChildRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return ToResult(await shareService.MatchAsync(userId, request, ct));
    }

    /// <summary>Học sinh: các đề nghị dùng hồ sơ phụ huynh đang chờ chọn.</summary>
    [HttpGet("parent-offers")]
    [Authorize(Roles = UserRoles.Student)]
    public async Task<IActionResult> GetOffers(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return ToResult(await shareService.GetStudentOffersAsync(userId, ct));
    }

    [HttpPost("parent-offers/{linkId:guid}/accept")]
    [Authorize(Roles = UserRoles.Student)]
    public async Task<IActionResult> AcceptOffer(Guid linkId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return ToResult(await shareService.DecideOfferAsync(userId, linkId, accept: true, ct));
    }

    [HttpPost("parent-offers/{linkId:guid}/decline")]
    [Authorize(Roles = UserRoles.Student)]
    public async Task<IActionResult> DeclineOffer(Guid linkId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Unauthorized();
        return ToResult(await shareService.DecideOfferAsync(userId, linkId, accept: false, ct));
    }

    private IActionResult ToResult<T>(ParentLinkResult<T> result) => result.Error switch
    {
        ParentLinkError.None => Ok(new { message = result.Message, data = result.Value }),
        ParentLinkError.NotFound => Problem(result.Message, statusCode: StatusCodes.Status404NotFound),
        ParentLinkError.Forbidden => Problem(result.Message, statusCode: StatusCodes.Status403Forbidden),
        ParentLinkError.Conflict => Problem(result.Message, statusCode: StatusCodes.Status409Conflict),
        _ => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest),
    };
}
