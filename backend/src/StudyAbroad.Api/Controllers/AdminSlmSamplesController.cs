using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Quản lý dữ liệu huấn luyện SLM (UC-89..96): tra cứu, xem chi tiết &amp; phiên bản, thêm, sửa, xóa mềm, khôi phục, nhập từ xlsx, xuất file.
/// Moderator và Admin đều dùng được; riêng xuất file (UC-96) chỉ Admin. Dữ liệu sai trả 400 kèm lý do, trùng mẫu trả 409.
/// </summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Moderator},{UserRoles.Admin}")]
[Route("api/v1/admin/slm-samples")]
public class AdminSlmSamplesController(ISlmSampleService samples, ISlmTemplateWriter templateWriter) : ControllerBase
{
    private const long MaxImportBytes = 10 * 1024 * 1024;
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>UC-89: danh sách một trang, mới nhất trước. deleted = true lọc mẫu đã xóa; pageSize mặc định 20, tối đa 100.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<SlmSampleDto>>> GetPage(
        [FromQuery] string? sampleType, [FromQuery] string? split, [FromQuery] string? search,
        [FromQuery] Guid? createdBy = null, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        [FromQuery] string? source = null, [FromQuery] bool deleted = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var filter = new SlmSampleFilter(
                sampleType, split, search, createdBy,
                StartOfDayUtc(from),
                StartOfDayUtc(to?.AddDays(1)),
                source,
                deleted);
            return Ok(await samples.GetPageAsync(filter, page, pageSize, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private static DateTime? StartOfDayUtc(DateOnly? day) =>
        day is { } d ? DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue) - VietnamOffset, DateTimeKind.Utc) : null;

    /// <summary>Số dòng đang dùng, đã xóa và theo loại mẫu, cho trang admin.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<SlmSampleSummaryDto>> GetSummary(CancellationToken ct) =>
        Ok(await samples.GetSummaryAsync(ct));

    [HttpGet("creators")]
    public async Task<ActionResult<IReadOnlyList<SlmSampleCreatorDto>>> GetCreators(CancellationToken ct) =>
        Ok(await samples.GetCreatorsAsync(ct));

    /// <summary>Danh sách nguồn (manual, excel và các nguồn tùy chỉnh đang có) cho bộ lọc UC-89.</summary>
    [HttpGet("sources")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetSources(CancellationToken ct) =>
        Ok(await samples.GetSourcesAsync(ct));

    /// <summary>UC-90: chi tiết một mẫu (kể cả đã xóa) kèm lịch sử phiên bản.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SlmSampleDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var sample = await samples.GetDetailAsync(id, ct);
        return sample is null ? NotFound() : Ok(sample);
    }

    /// <summary>UC-92: tạo một mẫu mới ở phiên bản 1.</summary>
    [HttpPost]
    public async Task<ActionResult<SlmSampleDto>> Create([FromBody] SaveSlmSampleRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var created = await samples.CreateAsync(request, actorId, RemoteIp(), UserAgent(), ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (SlmDuplicateSampleException ex)
        {
            return Conflict(new { error = ex.Message, existingId = ex.ExistingId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>UC-93: sửa một mẫu đang dùng và tạo phiên bản mới nếu có thay đổi.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SlmSampleDto>> Update(Guid id, [FromBody] SaveSlmSampleRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var updated = await samples.UpdateAsync(id, request, actorId, RemoteIp(), UserAgent(), ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (SlmDuplicateSampleException ex)
        {
            return Conflict(new { error = ex.Message, existingId = ex.ExistingId });
        }
        catch (SlmSampleDeletedException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ConcurrencyConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>UC-94: xóa mềm mẫu; dòng không còn hiện ở Đang dùng và không được xuất.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();
        return await samples.DeleteAsync(id, actorId, RemoteIp(), UserAgent(), ct) ? NoContent() : NotFound();
    }

    /// <summary>UC-94 3a: khôi phục mẫu đã xóa mềm.</summary>
    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<SlmSampleDto>> Restore(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var restored = await samples.RestoreAsync(id, actorId, RemoteIp(), UserAgent(), ct);
            return restored is null ? NotFound() : Ok(restored);
        }
        catch (SlmDuplicateSampleException ex)
        {
            return Conflict(new { error = ex.Message, existingId = ex.ExistingId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>UC-90 2a: khôi phục một phiên bản cũ bằng cách tạo phiên bản mới có nội dung đó.</summary>
    [HttpPost("{id:guid}/versions/{version:int}/restore")]
    public async Task<ActionResult<SlmSampleDto>> RestoreVersion(Guid id, int version, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var restored = await samples.RestoreVersionAsync(id, version, actorId, RemoteIp(), UserAgent(), ct);
            return restored is null ? NotFound() : Ok(restored);
        }
        catch (SlmDuplicateSampleException ex)
        {
            return Conflict(new { error = ex.Message, existingId = ex.ExistingId });
        }
        catch (SlmSampleDeletedException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ConcurrencyConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// UC-95: nhập file xlsx (trường form tên file, tối đa 10 MB). Dòng hợp lệ và chưa trùng với mẫu chưa xóa được thêm;
    /// dòng lỗi được liệt kê kèm lý do và không được thêm.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(MaxImportBytes)]
    public async Task<ActionResult<SlmImportResultDto>> Import(IFormFile? file, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();
        var fileError = CheckWorkbookFile(file);
        if (fileError is not null)
            return BadRequest(new { error = fileError });

        try
        {
            await using var stream = file!.OpenReadStream();
            return Ok(await samples.ImportAsync(stream, actorId, RemoteIp(), UserAgent(), ct));
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("preview")]
    [RequestSizeLimit(MaxImportBytes)]
    public async Task<ActionResult<SlmImportPreviewDto>> Preview(IFormFile? file, CancellationToken ct)
    {
        var fileError = CheckWorkbookFile(file);
        if (fileError is not null)
            return BadRequest(new { error = fileError });

        try
        {
            await using var stream = file!.OpenReadStream();
            return Ok(await samples.PreviewAsync(stream, ct));
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>UC-96: xuất toàn bộ mẫu chưa xóa (format=jsonl hoặc xlsx). Chỉ Admin; mỗi lần xuất ghi nhật ký truy cập (UC-91).</summary>
    [HttpGet("export")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> Export([FromQuery] string? format, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();
        try
        {
            var file = await samples.ExportAsync(format, actorId, RemoteIp(), UserAgent(), ct);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("template")]
    public IActionResult Template() =>
        File(templateWriter.Build(), XlsxContentType, "slm-training-template.xlsx");

    private string? RemoteIp() => HttpContext?.Connection?.RemoteIpAddress?.ToString();
    private string? UserAgent() => HttpContext?.Request?.Headers?.UserAgent.ToString();

    private static string? CheckWorkbookFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return "Cần chọn một file xlsx.";
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return "Chỉ nhận file .xlsx.";
        if (file.Length > MaxImportBytes)
            return "File tối đa 10 MB.";
        return null;
    }
}
