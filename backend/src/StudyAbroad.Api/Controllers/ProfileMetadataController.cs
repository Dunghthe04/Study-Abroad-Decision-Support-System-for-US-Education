using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Profile.Flow;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [USAS-640] API cung cấp siêu dữ liệu (Metadata) cấu hình cho luồng nhập hồ sơ.
/// Tuyệt đối không fix cứng các dropdown / mức ngân sách trong code.
/// Endpoint này cho phép truy cập công khai (hoặc người dùng đã đăng nhập) để frontend render form động.
/// </summary>
[ApiController]
[Route("api/v1/profile/metadata")]
public class ProfileMetadataController(IProfileMetadataService metadataService) : ControllerBase
{
    /// <summary>
    /// Lấy toàn bộ siêu dữ liệu động cho biểu mẫu hồ sơ (bậc học, 4 mức tiền, form điểm theo bậc, ngoại khóa...).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProfileMetadataResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetadata(CancellationToken ct)
    {
        var metadata = await metadataService.GetMetadataAsync(ct);
        Response.Headers.CacheControl = "public, max-age=300";
        return Ok(metadata);
    }
}
