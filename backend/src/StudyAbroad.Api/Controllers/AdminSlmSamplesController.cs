using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Quản lý dữ liệu huấn luyện SLM: xem, tạo, sửa, xóa mềm, duyệt theo lô, nhập từ file xlsx.
/// Moderator và Admin đều dùng được; riêng xuất file chỉ Admin. Dữ liệu sai trả 400 kèm lý do, trùng mẫu trả 409.
/// </summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Moderator},{UserRoles.Admin}")]
[Route("api/v1/admin/slm-samples")]
public class AdminSlmSamplesController(ISlmSampleService samples, ISlmTemplateWriter templateWriter) : ControllerBase
{
    private const long MaxImportBytes = 10 * 1024 * 1024;
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Danh sách một trang, mới nhất trước. Bộ lọc bỏ trống là không lọc; pageSize mặc định 20, tối đa 100.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<SlmSampleDto>>> GetPage(
        [FromQuery] string? sampleType, [FromQuery] string? split, [FromQuery] string? reviewStatus, [FromQuery] string? search,
        [FromQuery] Guid? createdBy = null, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        [FromQuery] string? source = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var filter = new SlmSampleFilter(
                sampleType, split, reviewStatus, search, createdBy,
                StartOfDayUtc(from),
                StartOfDayUtc(to?.AddDays(1)),
                source);
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

    /// <summary>Số dòng theo trạng thái duyệt và theo loại mẫu, cho các tab của trang.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<SlmSampleSummaryDto>> GetSummary(CancellationToken ct) =>
        Ok(await samples.GetSummaryAsync(ct));

    [HttpGet("creators")]
    public async Task<ActionResult<IReadOnlyList<SlmSampleCreatorDto>>> GetCreators(CancellationToken ct) =>
        Ok(await samples.GetCreatorsAsync(ct));

    /// <summary>Chi tiết một mẫu kèm lịch sử sửa và duyệt.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SlmSampleDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var sample = await samples.GetDetailAsync(id, ct);
        return sample is null ? NotFound() : Ok(sample);
    }

    /// <summary>Tạo một dòng ở trạng thái draft, hoặc pending nếu submit.</summary>
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
        catch (SlmDuplicateSampleException ex)
        {
            return Conflict(new { error = ex.Message, existingId = ex.ExistingId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Sửa một dòng. Dòng approved đổi nội dung thì về pending; submit gửi dòng nháp đi duyệt.</summary>
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
        catch (SlmDuplicateSampleException ex)
        {
            return Conflict(new { error = ex.Message, existingId = ex.ExistingId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Xóa mềm: dòng không còn hiện và không được xuất, lịch sử vẫn giữ.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();
        return await samples.DeleteAsync(id, actorId, ct) ? NoContent() : NotFound();
    }

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
        var fileError = CheckWorkbookFile(file);
        if (fileError is not null)
            return BadRequest(new { error = fileError });

        try
        {
            await using var stream = file!.OpenReadStream();
            return Ok(await samples.ImportAsync(stream, actorId, ct));
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

    /// <summary>UC-102: xuất mẫu approved chưa xóa (format=jsonl hoặc xlsx). Chỉ Admin; mỗi lần xuất ghi nhật ký.</summary>
    [HttpGet("export")]
    [Authorize(Roles = UserRoles.Admin)]
    public async Task<IActionResult> Export([FromQuery] string? format, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();
        try
        {
            var file = await samples.ExportAsync(
                format, actorId, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
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
