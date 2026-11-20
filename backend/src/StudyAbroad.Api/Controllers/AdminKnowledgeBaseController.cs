using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.KnowledgeBase;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Kho tri thức (UC-118..125): tra cứu, xem, thêm, sửa, xóa mềm, khôi phục, duyệt theo lô, nhập từ xlsx.
/// Moderator và Admin đều dùng được. Dữ liệu sai trả 400 kèm lý do, trùng câu hỏi trả 409 kèm existingId.
/// </summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Moderator},{UserRoles.Admin}")]
[Route("api/v1/admin/knowledge-base")]
public class AdminKnowledgeBaseController(IKnowledgeBaseService knowledgeBase, IKnowledgeTemplateWriter templateWriter) : ControllerBase
{
    private const long MaxImportBytes = 10 * 1024 * 1024;
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Danh sách một trang. deleted=true để xem mục đã xóa. sort: newest (mặc định), oldest, entryNo, updated.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<KnowledgeEntryDto>>> GetPage(
        [FromQuery] string? search, [FromQuery] string? label, [FromQuery] string? level, [FromQuery] string? source,
        [FromQuery] string? reviewStatus, [FromQuery] string? origin, [FromQuery] Guid? createdBy = null,
        [FromQuery] bool deleted = false, [FromQuery] string? sort = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var filter = new KnowledgeEntryFilter(search, label, level, source, reviewStatus, origin, createdBy, deleted, sort);
            return Ok(await knowledgeBase.GetPageAsync(filter, page, pageSize, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("creators")]
    public async Task<ActionResult<IReadOnlyList<KnowledgeCreatorDto>>> GetCreators(CancellationToken ct) =>
        Ok(await knowledgeBase.GetCreatorsAsync(ct));

    /// <summary>Chi tiết kèm lịch sử sửa và duyệt.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KnowledgeEntryDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var detail = await knowledgeBase.GetDetailAsync(id, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Tạo ở trạng thái draft, hoặc pending nếu submit.</summary>
    [HttpPost]
    public async Task<ActionResult<KnowledgeEntryDto>> Create([FromBody] SaveKnowledgeEntryRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var created = await knowledgeBase.CreateAsync(request, actorId, ct);
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

    /// <summary>Sửa. Mục đã duyệt bị sửa thì về chờ duyệt; submit gửi mục nháp đi duyệt.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KnowledgeEntryDto>> Update(Guid id, [FromBody] SaveKnowledgeEntryRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var updated = await knowledgeBase.UpdateAsync(id, request, actorId, ct);
            return updated is null ? NotFound() : Ok(updated);
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

    /// <summary>Xóa mềm; khôi phục được, lịch sử giữ nguyên.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();
        return await knowledgeBase.DeleteAsync(id, actorId, ct) ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<KnowledgeEntryDto>> Restore(Guid id, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var restored = await knowledgeBase.RestoreAsync(id, actorId, ct);
            return restored is null ? NotFound() : Ok(restored);
        }
        catch (KnowledgeDuplicateEntryException ex)
        {
            return Conflict(new { error = ex.Message, existingId = ex.ExistingId });
        }
    }

    /// <summary>Duyệt theo lô (tối đa 500): cùng trạng thái, cùng ghi chú.</summary>
    [HttpPost("review")]
    public async Task<ActionResult<ReviewKnowledgeEntriesResultDto>> Review(
        [FromBody] ReviewKnowledgeEntriesRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            return Ok(await knowledgeBase.ReviewAsync(request, actorId, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
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

    /// <summary>Nhập file xlsx (trường form tên file, tối đa 10 MB). Dòng hợp lệ chưa trùng vào trạng thái chờ duyệt.</summary>
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
            return Ok(await knowledgeBase.ImportAsync(stream, actorId, ct));
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("template")]
    public IActionResult Template() =>
        File(templateWriter.Build(), XlsxContentType, "knowledge-base-template.xlsx");

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