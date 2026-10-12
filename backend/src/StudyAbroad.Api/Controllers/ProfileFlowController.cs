using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// [USAS-640] API quản lý luồng nhập hồ sơ một cửa (Unified Profile Flow 6 bước):
/// - Tự động lưu nháp (auto-save draft)
/// - Khôi phục đúng bước đang làm dở khi quay lại
/// - Tải file bảng điểm nháp (xem trước / điền nháp)
/// - Xác nhận lưu chính thức (Commit)
/// Áp dụng cơ chế bảo mật chống IDOR: UserId lấy từ HttpOnly Cookie / JWT Token claims.
/// </summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Parent}")]
[Route("api/v1/profile/flow")]
public class ProfileFlowController(IProfileFlowService flowService) : ControllerBase
{
    /// <summary>
    /// Lấy trạng thái hiện tại của luồng hồ sơ (bước đang làm, dữ liệu nháp các bước).
    /// Bỏ dở giữa chừng thì quay lại đúng bước đang làm.
    /// </summary>
    [HttpGet("state")]
    [ProducesResponseType(typeof(ProfileFlowStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFlowState(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Không tìm thấy thông tin xác thực của người dùng." });
        }

        var state = await flowService.GetFlowStateAsync(userId.Value, ct);
        return Ok(state);
    }

    /// <summary>
    /// Lưu nháp tự động dữ liệu từng bước trong luồng khi người dùng nhập liệu hoặc đổi bước.
    /// </summary>
    [HttpPut("draft")]
    [ProducesResponseType(typeof(ProfileFlowStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveDraft([FromBody] SaveProfileDraftRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Không tìm thấy thông tin xác thực của người dùng." });
        }

        if (request.CurrentStep < 1 || request.CurrentStep > 6)
        {
            return BadRequest(new { error = "Bước hiện tại phải từ 1 đến 6." });
        }

        var updatedState = await flowService.SaveDraftAsync(userId.Value, request, ct);
        return Ok(updatedState);
    }

    /// <summary>
    /// Tải tệp bảng điểm (PDF hoặc ảnh) lên dạng nháp để xem trước.
    /// Không ghi đè hồ sơ chính thức cho đến khi bấm xác nhận ở bước cuối.
    /// </summary>
    [HttpPost("draft/upload-transcript")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DraftTranscriptInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadDraftTranscript(IFormFile? file, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Không tìm thấy thông tin xác thực của người dùng." });
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "Vui lòng chọn tệp bảng điểm hợp lệ để tải lên." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var draftInfo = await flowService.UploadDraftTranscriptAsync(
                userId.Value, stream, file.FileName, file.ContentType, file.Length, ct);

            return Ok(draftInfo);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Xác nhận lưu chính thức toàn bộ 5 phần hồ sơ vào CSDL sau khi kiểm tra ở Bước 6.
    /// Bóc tách đổ vào các cột thực, chuyển file nháp thành chính thức, xóa bản nháp.
    /// </summary>
    [HttpPost("commit")]
    [ProducesResponseType(typeof(CommitProfileFlowResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CommitFlow([FromBody] CommitProfileFlowRequest? request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Không tìm thấy thông tin xác thực của người dùng." });
        }

        try
        {
            var result = await flowService.CommitFlowAsync(userId.Value, request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Xóa toàn bộ dữ liệu nháp của luồng hồ sơ để người dùng bắt đầu lại từ đầu.
    /// </summary>
    [HttpDelete("draft")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ClearDraft(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Không tìm thấy thông tin xác thực của người dùng." });
        }

        await flowService.ClearDraftAsync(userId.Value, ct);
        return NoContent();
    }
}
