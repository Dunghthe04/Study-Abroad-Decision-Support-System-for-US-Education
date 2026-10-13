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
    public async Task<IActionResult> SaveDraft([FromBody] SaveProfileDraftRequest? request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Không tìm thấy thông tin xác thực của người dùng." });
        }

        var req = request ?? new SaveProfileDraftRequest(CurrentStep: 1);
        if (req.CurrentStep < 1 || req.CurrentStep > 6)
        {
            return BadRequest(new { error = "Bước hiện tại phải từ 1 đến 6." });
        }

        var updatedState = await flowService.SaveDraftAsync(userId.Value, req, ct);
        return Ok(updatedState);
    }

    /// <summary>
    /// Tải tệp bảng điểm (PDF hoặc ảnh) lên dạng nháp để xem trước và tự động OCR đọc điểm các môn học.
    /// Không ghi đè hồ sơ chính thức cho đến khi bấm xác nhận ở bước cuối.
    /// </summary>
    [HttpPost("draft/upload-transcript")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadDraftTranscriptResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadDraftTranscript(
        List<IFormFile>? files,
        IFormFile? file,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "Không tìm thấy thông tin xác thực của người dùng." });
        }

        var fileList = (files != null && files.Count > 0) ? files : (file != null ? [file] : new List<IFormFile>());
        fileList = fileList.Where(f => f.Length > 0).ToList();

        if (fileList.Count == 0)
        {
            return BadRequest(new { error = "Vui lòng chọn ít nhất một tệp bảng điểm hợp lệ để tải lên." });
        }

        try
        {
            var primaryFile = fileList[0];
            await using var stream = primaryFile.OpenReadStream();
            var draftInfo = await flowService.UploadDraftTranscriptAsync(
                userId.Value, stream, primaryFile.FileName, primaryFile.ContentType, primaryFile.Length, ct);

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
