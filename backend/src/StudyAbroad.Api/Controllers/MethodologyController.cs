using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Costs;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [UC-83] Căn cứ tham số của gợi ý trường và ước tính chi phí: chỉ xem, không có thao tác sửa.
/// Công khai như trang /methodology (không có dữ liệu cá nhân). Tham số thuật toán đọc trực tiếp từ hằng số đang chạy trong code,
/// nên chỉ đổi khi phát hành bản mới (D6, BR-10). Tham số chi phí (phí visa, mức ngân sách, tỉ giá...) là dữ liệu đang chạy, đọc lúc gọi.
/// </summary>
[ApiController]
[Route("api/v1/methodology")]
public class MethodologyController(ICostParameterCatalog costParameters) : ControllerBase
{
    public record ParametersResponse(string AlgorithmVersion, IReadOnlyList<AlgorithmParameter> Parameters);

    /// <summary>Phiên bản thuật toán và danh mục tham số đang chạy (thuật toán gợi ý, rồi ước tính chi phí).</summary>
    [HttpGet("parameters")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ParametersResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ParametersResponse>> GetParameters(CancellationToken ct)
    {
        // Tham số chi phí do Moderator sửa trong app_settings: không cache để trang hiện giá trị mới ngay
        Response.Headers.CacheControl = "no-store";
        var cost = await costParameters.ListAsync(ct);
        return new ParametersResponse(AlgorithmParameters.Version, [.. AlgorithmParameters.All, .. cost]);
    }
}
