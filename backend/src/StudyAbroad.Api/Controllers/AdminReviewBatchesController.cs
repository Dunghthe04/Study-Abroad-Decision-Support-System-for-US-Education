using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application.Admin.ReviewBatches;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Controllers;

/// <summary>
/// Đợt duyệt mẫu: bốc ngẫu nhiên một số câu hệ thống đã trả lời được để người duyệt đọc và chấm đúng sai (đo tỷ lệ lỗi mà hàng đợi không thấy).
/// Chỉ Admin. Danh sách đợt và danh sách câu KHÔNG có nội dung chat; nội dung một câu chỉ trả cho người được giao duyệt đợt đó và mỗi lần xem
/// được ghi nhật ký. Người không được giao (kể cả người tạo đợt) nhận 404, không phân biệt với đợt không tồn tại.
/// </summary>
[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/v1/admin/review-batches")]
public class AdminReviewBatchesController(IReviewBatchService batches) : ControllerBase
{
    /// <summary>
    /// Tạo đợt: bốc tối đa SizePerStratum câu cho mỗi nhóm skill trong khoảng ngày (không giới hạn nếu bỏ trống), giao cho 1 đến 5 Admin duyệt.
    /// Seed bỏ trống thì tự sinh. Tham số sai (hoặc không có câu nào để bốc) trả 400 kèm lý do và không tạo gì.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ReviewBatchCreatedDto>> Create([FromBody] CreateReviewBatchRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var created = await batches.CreateAsync(
                request, actorId, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Các đợt của người dùng hiện tại (đã tạo hoặc được giao duyệt), mới nhất trước, kèm tiến độ. Không có nội dung chat.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReviewBatchListItemDto>>> List(CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        return Ok(await batches.ListAsync(actorId, ct));
    }

    /// <summary>Các câu của một đợt theo thứ tự đọc, chưa có nội dung. 404 nếu đợt không có hoặc người gọi không được giao duyệt.</summary>
    [HttpGet("{batchId:guid}/items")]
    public async Task<ActionResult<ReviewItemListDto>> GetItems(Guid batchId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var items = await batches.GetItemsAsync(batchId, actorId, ct);
        return items is null ? NotFound() : Ok(items);
    }

    /// <summary>
    /// Một câu để chấm: câu hỏi kèm nhãn SLM gán, câu hệ thống trả lời, vài tin trước đó, và kết luận của chính người gọi (nếu có).
    /// Đây là dữ liệu cá nhân của học sinh: mỗi lần xem được ghi vào nhật ký (ai, lúc nào, từ đâu), ghi lỗi thì không trả nội dung.
    /// Không có mã học sinh. 404 nếu không được giao đợt hoặc câu không thuộc đợt.
    /// </summary>
    [HttpGet("{batchId:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<ReviewItemDetailDto>> GetItem(Guid batchId, Guid itemId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        var detail = await batches.GetItemDetailAsync(
            batchId, itemId, actorId, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>
    /// Lưu kết luận của người gọi cho một câu (ghi đè nếu đã chấm). Nhãn đúng chỉ khi nhãn SLM sai; nguyên nhân bắt buộc khi câu trả lời sai
    /// và chỉ khi đó. Sai quy tắc trả 400; không được giao đợt hoặc câu không thuộc đợt trả 404.
    /// </summary>
    [HttpPut("{batchId:guid}/items/{itemId:guid}/verdict")]
    public async Task<ActionResult<ReviewVerdictDto>> SaveVerdict(
        Guid batchId, Guid itemId, [FromBody] SaveReviewVerdictRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } actorId)
            return Unauthorized();

        try
        {
            var saved = await batches.SaveVerdictAsync(batchId, itemId, request, actorId, ct);
            return saved is null ? NotFound() : Ok(saved);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
