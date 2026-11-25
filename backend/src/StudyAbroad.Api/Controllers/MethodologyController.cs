using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Admin.Settings;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-89] Căn cứ tham số của thuật toán gợi ý trường: chỉ xem, không có thao tác sửa.
/// Công khai như trang /methodology (không có dữ liệu cá nhân). Giá trị đọc trực tiếp từ hằng số đang chạy trong code,
/// nên chỉ đổi khi phát hành bản mới (D6, BR-10).
/// </summary>
[ApiController]
[Route("api/v1/methodology")]
public class MethodologyController(ICostParametersSettingService costParameters) : ControllerBase
{
    public record ParametersResponse(string AlgorithmVersion, IReadOnlyList<AlgorithmParameter> Parameters);

    /// <summary>Phiên bản thuật toán và danh mục tham số đang chạy (gồm tham số chi phí sống từ app_settings).</summary>
    [HttpGet("parameters")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParametersResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ParametersResponse>> GetParameters(CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";
        var cost = await costParameters.GetCurrentParametersAsync(ct);
        return new ParametersResponse(AlgorithmParameters.Version, AlgorithmParameters.Build(cost));
    }
}
