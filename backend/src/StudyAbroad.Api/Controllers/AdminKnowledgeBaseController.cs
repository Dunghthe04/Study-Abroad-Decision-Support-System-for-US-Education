using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Application.Admin.KnowledgeBase;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Kho tri thức (UC-118..125): tra cứu, xem, thêm, sửa, xóa, nhập từ xlsx.
/// Moderator và Admin đều dùng được. Dữ liệu sai trả 400 kèm lý do, trùng entry_no trả 409 kèm existingId.
/// </summary>
[ApiController]
[Authorize(Roles = $"{UserRoles.Moderator},{UserRoles.Admin}")]
[Route("api/v1/admin/knowledge-base")]
public class AdminKnowledgeBaseController(IKnowledgeBaseService knowledgeBase, IKnowledgeTemplateWriter templateWriter) : ControllerBase
{
    private const long MaxImportBytes = 10 * 1024 * 1024;
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Danh sách một trang. sort: newest (mặc định), oldest, entryNo, updated.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<KnowledgeEntryDto>>> GetPage(
        [FromQuery] string? search, [FromQuery] string? docType, [FromQuery] string? label, [FromQuery] string? topicCode,
        [FromQuery] string? sort = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var filter = new KnowledgeEntryFilter(search, docType, label, topicCode, sort);
            return Ok(await knowledgeBase.GetPageAsync(filter, page, pageSize, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KnowledgeEntryDto>> Get(Guid id, CancellationToken ct)
    {
        var entry = await knowledgeBase.GetAsync(id, ct);
        return entry is null ? NotFound() : Ok(entry);
    }

    [HttpPost]
    public async Task<ActionResult<KnowledgeEntryDto>> Create([FromBody] SaveKnowledgeEntryRequest request, CancellationToken ct)
    {
        try
        {
            var created = await knowledgeBase.CreateAsync(request, ct);
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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KnowledgeEntryDto>> Update(Guid id, [FromBody] SaveKnowledgeEntryRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await knowledgeBase.UpdateAsync(id, request, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await knowledgeBase.DeleteAsync(id, ct) ? NoContent() : NotFound();

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

    /// <summary>Nhập file xlsx (trường form tên file, tối đa 10 MB). Thêm dòng mới, cập nhật dòng đã có theo id.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(MaxImportBytes)]
    public async Task<ActionResult<KnowledgeImportResultDto>> Import(IFormFile? file, CancellationToken ct)
    {
        var fileError = CheckWorkbookFile(file);
        if (fileError is not null)
            return BadRequest(new { error = fileError });

        try
        {
            await using var stream = file!.OpenReadStream();
            return Ok(await knowledgeBase.ImportAsync(stream, ct));
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