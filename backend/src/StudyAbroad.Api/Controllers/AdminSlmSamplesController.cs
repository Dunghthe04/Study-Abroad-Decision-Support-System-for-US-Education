using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Quản lý dữ liệu huấn luyện SLM: xem, tạo, sửa, xóa, duyệt theo lô, nhập từ file xlsx và xuất JSONL các dòng đã duyệt.
/// Moderator và Admin đều dùng được. Dữ liệu sai trả 400 kèm lý do.
/// </summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Moderator},{UserRoles.Admin}")]
[Route("api/v1/admin/slm-samples")]
public class AdminSlmSamplesController(ISlmSampleService samples) : ControllerBase
{
    private const long MaxImportBytes = 10 * 1024 * 1024;

    /// <summary>Danh sách một trang, mới nhất trước. Bộ lọc bỏ trống là không lọc; pageSize mặc định 20, tối đa 100.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<SlmSampleDto>>> GetPage(
        [FromQuery] string? sampleType, [FromQuery] string? split, [FromQuery] string? reviewStatus, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var filter = new SlmSampleFilter(sampleType, split, reviewStatus, search);
            return Ok(await samples.GetPageAsync(filter, page, pageSize, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Số dòng theo trạng thái duyệt và theo loại mẫu, cho các tab của trang.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<SlmSampleSummaryDto>> GetSummary(CancellationToken ct) =>
        Ok(await samples.GetSummaryAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SlmSampleDto>> Get(Guid id, CancellationToken ct)
    {
        var sample = await samples.GetAsync(id, ct);
        return sample is null ? NotFound() : Ok(sample);
    }

    /// <summary>Tạo một dòng ở trạng thái draft.</summary>
    [HttpPost]
    public async Task<ActionResult<SlmSampleDto>> Create([FromBody] SaveSlmSampleRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var created = await samples.CreateAsync(request, actorId, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Sửa một dòng. Nếu câu hỏi hoặc nhãn đổi thì dòng quay về draft và mất kết quả duyệt cũ.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SlmSampleDto>> Update(Guid id, [FromBody] SaveSlmSampleRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var updated = await samples.UpdateAsync(id, request, actorId, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await samples.DeleteAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>Duyệt một hoặc nhiều dòng cùng lúc (tối đa 500): đổi sang cùng một trạng thái và cùng một ghi chú.</summary>
    [HttpPost("review")]
    public async Task<ActionResult<ReviewSlmSamplesResultDto>> Review([FromBody] ReviewSlmSamplesRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            return Ok(await samples.ReviewAsync(request, actorId, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Nhập file xlsx (trường form tên file, tối đa 10 MB). Dòng hợp lệ và chưa trùng được thêm ở trạng thái draft;
    /// dòng lỗi được liệt kê kèm lý do và không được thêm.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(MaxImportBytes)]
    public async Task<ActionResult<SlmImportResultDto>> Import(IFormFile? file, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Cần chọn một file xlsx." });
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Chỉ nhận file .xlsx." });

        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await samples.ImportAsync(stream, actorId, ct));
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Tải về các dòng đã duyệt (approved) dạng JSONL, mỗi dòng một JSON. split bỏ trống là cả train, val và test.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string? split, CancellationToken ct)
    {
        try
        {
            var content = await samples.ExportJsonlAsync(split, ct);
            var name = string.IsNullOrWhiteSpace(split) ? "slm_training.jsonl" : $"slm_training_{split.Trim()}.jsonl";
            return File(System.Text.Encoding.UTF8.GetBytes(content), "application/x-ndjson", name);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
