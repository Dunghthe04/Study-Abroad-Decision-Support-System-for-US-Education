using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

[ApiController]
[Route("api/v1/study-centers")]
public class StudyCentersController(IStudyCenterService service) : ControllerBase
{
    /// <summary>[UC-40] Ẩn SĐT, email liên hệ trực tiếp và tỷ lệ đỗ visa khỏi API công khai (chỉ Moderator thấy).</summary>
    private static StudyCenterDto SanitizeForPublic(StudyCenterDto dto) =>
        dto with { Phone = null, Email = null, VisaSuccessRate = null };

    [HttpGet]
    public async Task<PagedResult<StudyCenterDto>> Search([FromQuery] StudyCenterQuery query, CancellationToken ct)
    {
        // BR-C1: Chỉ trung tâm ở trạng thái "Đã xác minh" mới hiển thị trong danh bạ công khai
        var isAdminOrMod = User.IsInRole(UserRoles.Moderator);
        if (!isAdminOrMod)
        {
            // Người dùng thường không được xem trung tâm đã ẩn.
            query = query with { VerifiedOnly = true, IncludeInactive = false, OnlyInactive = false };
        }
        var result = await service.SearchAsync(query, ct);
        if (isAdminOrMod) return result;
        return new PagedResult<StudyCenterDto>(
            result.Items.Select(SanitizeForPublic).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StudyCenterDto>> GetById(Guid id, CancellationToken ct)
    {
        var center = await service.GetByIdAsync(id, ct);
        if (center is null) return NotFound();

        var isAdminOrMod = User.IsInRole(UserRoles.Moderator);
        if (!isAdminOrMod && (center.VerificationStatus != CenterReferral.Verified || !center.IsActive))
        {
            return NotFound();
        }

        return Ok(isAdminOrMod ? center : SanitizeForPublic(center));
    }

    /// <summary>[UC-40 / UC-44] Thẻ "Nên hỏi trung tâm" (CENTER_ASK) và "Dấu hiệu cảnh giác" (CENTER_WARNING) từ Kho tri thức.</summary>
    [HttpGet("guidance-cards")]
    public Task<CenterGuidanceCardsDto> GetGuidanceCards(CancellationToken ct) =>
        service.GetGuidanceCardsAsync(ct);

    /// <summary>
    /// Đánh giá công khai của trung tâm (UC-41): lấy từ dữ liệu thật, chỉ gồm đánh giá đang hiển thị.
    /// Điểm trung bình và số lượng tính từ toàn bộ đánh giá hiển thị; danh sách được phân trang (mới nhất trước).
    /// </summary>
    [HttpGet("{id:guid}/reviews")]
    public async Task<ActionResult<CenterReviewsDto>> GetReviews(
        Guid id,
        [FromServices] ICenterReviewRepository reviews,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = CenterReviewPaging.DefaultPageSize)
    {
        var center = await service.GetByIdAsync(id, ct);
        if (center is null) return NotFound();

        var isModerator = User.IsInRole(UserRoles.Moderator);
        if (!isModerator && (center.VerificationStatus != CenterReferral.Verified || !center.IsActive))
            return NotFound();

        return Ok(await reviews.GetVisibleAsync(id, page, pageSize, ct));
    }

    // Trung tâm tự đăng ký (/register/center) và chờ moderator duyệt hồ sơ (UC-59);
    // moderator không còn thêm trung tâm hay cấp tài khoản trung tâm.

    /// <summary>[UC-59] Xác minh hoặc hủy xác minh một trung tâm tư vấn (chỉ Moderator).</summary>
    [HttpPut("{id:guid}/verify")]
    [Authorize(Roles = UserRoles.Moderator)]
    public async Task<ActionResult<StudyCenterDto>> VerifyCenter(
        Guid id,
        [FromBody] VerifyStudyCenterRequest request,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var result = await service.VerifyCenterAsync(id, request.IsVerified, actorId, request.VerificationNote, ct);
        if (!result.Success || result.Center is null)
            return ValidationProblem(result.ErrorMessage);

        return Ok(result.Center);
    }

    // [UC-72] Moderator chỉ ẩn/hiện trung tâm (quyết định bản 14/11). Sửa nội dung do trung tâm tự làm qua PUT /center/profile (UC-92).

    /// <summary>[UC-72] Thay đổi trạng thái hoạt động (ẩn/hiện khỏi danh bạ và gợi ý) của trung tâm (chỉ Moderator).</summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = UserRoles.Moderator)]
    public async Task<ActionResult<StudyCenterDto>> SetActiveStatus(
        Guid id,
        [FromBody] SetStudyCenterActiveRequest request,
        CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var result = await service.SetCenterActiveAsync(id, request.IsActive, actorId, ct);
        if (!result.Success || result.Center is null)
            return ValidationProblem(result.ErrorMessage);

        return Ok(result.Center);
    }

    /// <summary>Chat (B6) lấy các trung tâm đã xác minh cho một dịch vụ (tối đa 20); nêu thành phố thì chỉ lấy ở thành phố đó.</summary>
    [HttpGet("referrals")]
    public async Task<ActionResult<IReadOnlyList<StudyCenterDto>>> Refer(
        [FromQuery] CenterReferralQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Service))
            return BadRequest("service is required");
        var list = await service.ReferAsync(query, ct);
        return Ok(User.IsInRole(UserRoles.Moderator) ? list : list.Select(SanitizeForPublic).ToList());
    }

    /// <summary>Các thành phố đang có trung tâm gợi ý được (chat đưa cho AI chọn thành phố của học sinh).</summary>
    [HttpGet("referral-cities")]
    public Task<IReadOnlyList<string>> ReferralCities(CancellationToken ct) => service.GetReferralCitiesAsync(ct);

    /// <summary>Thành phố có trung tâm đúng dịch vụ kèm số trung tâm (chat đưa nút chọn thành phố cho học sinh).</summary>
    [HttpGet("referral-city-counts")]
    public async Task<ActionResult<IReadOnlyList<CityCount>>> ReferralCityCounts(
        [FromQuery] CenterReferralQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Service))
            return BadRequest("service is required");
        return Ok(await service.GetReferralCityCountsAsync(query, ct));
    }

}
