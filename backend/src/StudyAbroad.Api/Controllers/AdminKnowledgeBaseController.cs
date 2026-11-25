using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.KnowledgeBase;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Kho tri thức (UC-112..117): tra cứu, xem chi tiết &amp; phiên bản, thêm, sửa, xóa mềm, khôi phục, nhập từ xlsx.
/// Moderator và Admin đều dùng được. Dữ liệu sai trả 400 kèm lý do, trùng entry_no hoặc mục đã xóa trả 409.
/// </summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Moderator},{UserRoles.Admin}")]
[Route("api/v1/admin/knowledge-base")]
public class AdminKnowledgeBaseController(IKnowledgeBaseService knowledgeBase, IKnowledgeTemplateWriter templateWriter) : ControllerBase
{
    private const long MaxImportBytes = 10 * 1024 * 1024;
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>UC-112: danh sách một trang. sort: newest (mặc định), oldest, entryNo, updated. deleted = true lọc mục đã xóa.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<KnowledgeEntryDto>>> GetPage(
        [FromQuery] string? search, [FromQuery] string? docType, [FromQuery] string? label, [FromQuery] string? topicCode,
        [FromQuery] string? sort = null, [FromQuery] bool deleted = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var filter = new KnowledgeEntryFilter(search, docType, label, topicCode, sort, deleted);
            return Ok(await knowledgeBase.GetPageAsync(filter, page, pageSize, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>UC-113: chi tiết một mục tri thức (kể cả đã xóa - chỉ đọc).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KnowledgeEntryDto>> Get(Guid id, CancellationToken ct)
    {
        var entry = await knowledgeBase.GetAsync(id, ct);
        return entry is null ? NotFound() : Ok(entry);
    }

    /// <summary>UC-113: danh sách phiên bản bất biến của một mục tri thức.</summary>
    [HttpGet("{id:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<KnowledgeEntryVersionDto>>> GetVersions(Guid id, CancellationToken ct)
    {
        var versions = await knowledgeBase.GetVersionsAsync(id, ct);
        return versions is null ? NotFound() : Ok(versions);
    }

    /// <summary>UC-114: thêm mục tri thức mới ở phiên bản 1.</summary>
    [HttpPost]
    public async Task<ActionResult<KnowledgeEntryDto>> Create([FromBody] SaveKnowledgeEntryRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var created = await knowledgeBase.CreateAsync(request, actorId, RemoteIp(), UserAgent(), ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (KnowledgeDuplicateEntryException ex)
        {
            return Conflict(new { error = ex.Message, existingId = ex.ExistingId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>UC-115: sửa mục tri thức đang dùng, tạo phiên bản mới nếu có thay đổi.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KnowledgeEntryDto>> Update(Guid id, [FromBody] SaveKnowledgeEntryRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var updated = await knowledgeBase.UpdateAsync(id, request, actorId, RemoteIp(), UserAgent(), ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (KnowledgeEntryDeletedException ex)
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

    /// <summary>UC-116: xóa mềm mục tri thức.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();
        return await knowledgeBase.DeleteAsync(id, actorId, RemoteIp(), UserAgent(), ct) ? NoContent() : NotFound();
    }

    /// <summary>UC-116 3a: khôi phục mục tri thức đã xóa mềm.</summary>
    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<KnowledgeEntryDto>> Restore(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();
        var restored = await knowledgeBase.RestoreAsync(id, actorId, RemoteIp(), UserAgent(), ct);
        return restored is null ? NotFound() : Ok(restored);
    }

    /// <summary>UC-113 3a: khôi phục phiên bản cũ bằng cách tạo phiên bản mới với nội dung đó.</summary>
    [HttpPost("{id:guid}/versions/{version:int}/restore")]
    public async Task<ActionResult<KnowledgeEntryDto>> RestoreVersion(Guid id, int version, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var restored = await knowledgeBase.RestoreVersionAsync(id, version, actorId, RemoteIp(), UserAgent(), ct);
            return restored is null ? NotFound() : Ok(restored);
        }
        catch (KnowledgeEntryDeletedException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ConcurrencyConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("preview")]
    [RequestSizeLimit(MaxImportBytes)]
    public async Task<ActionResult<KnowledgeImportPreviewDto>> Preview(IFormFile? file, CancellationToken ct)
    {
        var fileError = CheckWorkbookFile(file);
        if (fileError is not null)
            return BadRequest(new { error = fileError });

        try
        {
            await using var stream = file!.OpenReadStream();
            return Ok(await knowledgeBase.PreviewAsync(stream, ct));
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>UC-117: nhập file xlsx (trường form tên file, tối đa 10 MB). Thêm dòng mới, cập nhật dòng đã có theo id.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(MaxImportBytes)]
    public async Task<ActionResult<KnowledgeImportResultDto>> Import(IFormFile? file, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var fileError = CheckWorkbookFile(file);
        if (fileError is not null)
            return BadRequest(new { error = fileError });

        try
        {
            await using var stream = file!.OpenReadStream();
            return Ok(await knowledgeBase.ImportAsync(stream, actorId, RemoteIp(), UserAgent(), ct));
        }
        catch (ConcurrencyConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Tiến độ tạo vector: pending là số dòng chat chưa thấy. Advisor tắt thì available = false (vẫn 200).</summary>
    [HttpGet("embed-status")]
    public async Task<ActionResult<KnowledgeEmbedStatusDto>> EmbedStatus(CancellationToken ct) =>
        Ok(await knowledgeBase.GetEmbedStatusAsync(ct));

    /// <summary>Nhờ advisor tạo vector cho dòng còn thiếu (nút "Thử lại" khi lần nhập trước chưa nhờ được).</summary>
    [HttpPost("embed")]
    public async Task<ActionResult<KnowledgeEmbedStatusDto>> StartEmbed(CancellationToken ct) =>
        Ok(await knowledgeBase.StartEmbedAsync(ct));

    [HttpGet("template")]
    public IActionResult Template() =>
        File(templateWriter.Build(), XlsxContentType, "knowledge-base-template.xlsx");

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
