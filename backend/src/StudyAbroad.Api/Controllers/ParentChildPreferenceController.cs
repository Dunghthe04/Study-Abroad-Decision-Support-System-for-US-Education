using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

public record SaveParentPreferenceRequest(int[]? Pref, long? BudgetVnd);
public record SaveParentBudgetRequest(long? BudgetVnd);

/// <summary>[UC-76] Phụ huynh chọn ưu tiên cho con có liên kết thật và xem so sánh với danh sách của con.
/// Tách khỏi ParentChildrenController để không đổi constructor của controller đó.
/// Con managed / không liên kết: 404.</summary>
[ApiController]
[Authorize(Roles = UserRoles.Parent)]
[Route("api/v1/parent/children/{studentId:guid}/preference")]
public class ParentChildPreferenceController(ParentPreferenceService preferences) : ControllerBase
{
    // GET: ý đã lưu (data null nếu chưa lưu lần nào)
    [HttpGet]
    public async Task<IActionResult> Get(Guid studentId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } parentId) return Unauthorized(new { message = "Vui lòng đăng nhập." });
        return PreferenceResults.ToResult(this, await preferences.GetAsync(parentId, studentId, ct));
    }

    // PUT: lưu ý, chốt tỷ giá, tính sẵn danh sách theo cha mẹ, báo cho con
    [HttpPut]
    public async Task<IActionResult> Save(Guid studentId, [FromBody] SaveParentPreferenceRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } parentId) return Unauthorized(new { message = "Vui lòng đăng nhập." });
        return PreferenceResults.ToResult(this, await preferences.SaveAsync(parentId, studentId, request.Pref, request.BudgetVnd, ct));
    }

    // PUT budget: [UC-94] chỉ lưu số tiền tối đa/năm từ dashboard, không đụng ưu tiên, không báo cho con
    [HttpPut("budget")]
    public async Task<IActionResult> SaveBudget(Guid studentId, [FromBody] SaveParentBudgetRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } parentId) return Unauthorized(new { message = "Vui lòng đăng nhập." });
        return PreferenceResults.ToResult(this, await preferences.SaveBudgetAsync(parentId, studentId, request.BudgetVnd, ct));
    }

    // GET compare?pref=4,2,0&budgetVnd=...: có tham số là xem thử (không lưu), không có là so với ý đã lưu
    [HttpGet("compare")]
    public async Task<IActionResult> Compare(Guid studentId, [FromQuery] string? pref, [FromQuery] long? budgetVnd, CancellationToken ct)
    {
        if (User.GetUserId() is not { } parentId) return Unauthorized(new { message = "Vui lòng đăng nhập." });
        IReadOnlyList<int>? parsed = null;
        if (!string.IsNullOrWhiteSpace(pref))
        {
            var parts = pref.Split(',');
            var values = new List<int>();
            foreach (var p in parts)
            {
                if (!int.TryParse(p.Trim(), out var v))
                    return Problem("Cần đủ 3 mức ưu tiên, mỗi mức từ 0 đến 4.", statusCode: StatusCodes.Status400BadRequest);
                values.Add(v);
            }
            parsed = values;
        }
        return PreferenceResults.ToResult(this, await preferences.CompareForParentAsync(parentId, studentId, parsed, budgetVnd, ct));
    }
}

/// <summary>[UC-76] Con xem danh sách theo ưu tiên cha mẹ, chỉ đọc. parentId bỏ trống: cha/mẹ đầu tiên đã chọn ưu tiên.</summary>
[ApiController]
[Authorize(Roles = UserRoles.Student)]
[Route("api/v1/parent-preferences")]
public class ParentPreferencesController(ParentPreferenceService preferences) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid? parentId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } childId) return Unauthorized(new { message = "Vui lòng đăng nhập." });
        return PreferenceResults.ToResult(this, await preferences.GetForChildAsync(childId, parentId, ct));
    }
}

// Cùng cách đổi lỗi ra mã HTTP như ParentLinksController
internal static class PreferenceResults
{
    public static IActionResult ToResult<T>(ControllerBase c, ParentLinkResult<T> result) => result.Error switch
    {
        ParentLinkError.None => c.Ok(new { message = result.Message, data = result.Value }),
        ParentLinkError.NotFound => c.Problem(result.Message, statusCode: StatusCodes.Status404NotFound),
        ParentLinkError.Conflict => c.Problem(result.Message, statusCode: StatusCodes.Status409Conflict),
        _ => c.Problem(result.Message, statusCode: StatusCodes.Status400BadRequest),
    };
}
