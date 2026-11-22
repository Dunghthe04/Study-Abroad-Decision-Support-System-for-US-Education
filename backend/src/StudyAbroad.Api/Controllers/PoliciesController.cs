using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Admin.Policies;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-103] Văn bản chính sách và điều khoản đang hiệu lực, ai cũng đọc được (trang /terms, /privacy, hộp thoại đồng ý của UC-104).
/// Chỉ có bản ĐANG HIỆU LỰC; bản nháp và bản cũ không lộ ra đây. Không kèm thông tin người soạn.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/policies")]
public class PoliciesController(IPolicyService policies) : ControllerBase
{
    /// <summary>
    /// Bản đang hiệu lực của một loại: terms_of_use, privacy_policy, child_data_consent, forum_rules.
    /// 404 nếu loại không hợp lệ hoặc chưa có bản nào được công bố. Không lưu bộ nhớ đệm để bản vừa công bố hiện ngay.
    /// </summary>
    [HttpGet("{kind}")]
    [ProducesResponseType(typeof(PublicPolicyDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicPolicyDto>> GetCurrent(string kind, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-cache";
        var doc = await policies.GetPublishedAsync(kind, ct);
        return doc is null ? NotFound() : Ok(doc);
    }
}
