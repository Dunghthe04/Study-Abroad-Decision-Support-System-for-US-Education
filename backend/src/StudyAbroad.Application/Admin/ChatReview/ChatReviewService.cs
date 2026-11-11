using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Admin.ChatReview;

public interface IChatReviewService
{
    /// <summary>
    /// Hàng đợi câu cần xem, mới nhất trước. status mặc định open (open, resolved, ignored); outcome bỏ trống là cả no_answer lẫn failed.
    /// from, to bỏ trống là không giới hạn ngày (tính cả hai đầu, theo ngày UTC). page từ 1; pageSize bị giới hạn 1 đến 100.
    /// Không có nội dung chat. Tham số sai (trạng thái hay kết quả lạ, khoảng ngày sai) thì ném ArgumentException.
    /// </summary>
    Task<PagedResult<ChatReviewItemDto>> GetQueueAsync(
        string? status, string? outcome, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Số câu theo trạng thái xử lý trong khoảng (bỏ trống là không giới hạn ngày). Khoảng sai thì ném ArgumentException.</summary>
    Task<ChatReviewSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
}

public class ChatReviewService(IChatReviewRepository repository) : IChatReviewService
{
    public const int MaxPageSize = 100;

    // Chỉ hai kết quả đáng đưa vào hàng đợi: câu trả lời được (answered) không có trạng thái xử lý
    private static readonly string[] QueueOutcomes = [ChatOutcomes.NoAnswer, ChatOutcomes.Failed];
    private static readonly string[] Statuses = [ChatReviewStatuses.Open, ChatReviewStatuses.Resolved, ChatReviewStatuses.Ignored];

    public async Task<PagedResult<ChatReviewItemDto>> GetQueueAsync(
        string? status, string? outcome, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default)
    {
        status = string.IsNullOrWhiteSpace(status) ? ChatReviewStatuses.Open : status.Trim();
        if (!Statuses.Contains(status))
            throw new ArgumentException($"Trạng thái phải là một trong: {string.Join(", ", Statuses)}.");
        outcome = string.IsNullOrWhiteSpace(outcome) ? null : outcome.Trim();
        if (outcome is not null && !QueueOutcomes.Contains(outcome))
            throw new ArgumentException($"Kết quả phải là một trong: {string.Join(", ", QueueOutcomes)}.");
        var (fromUtc, toUtcExclusive) = Range(from, to);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (rows, total) = await repository.QueuePageAsync(status, outcome, fromUtc, toUtcExclusive, (page - 1) * pageSize, pageSize, ct);
        var items = rows.Select(ToItem).ToList();
        return new PagedResult<ChatReviewItemDto>(items, page, pageSize, total);
    }

    public async Task<ChatReviewSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var (fromUtc, toUtcExclusive) = Range(from, to);
        var counts = await repository.CountsAsync(fromUtc, toUtcExclusive, ct);

        int Sum(string status, string? outcome = null) =>
            counts.Where(c => c.ReviewStatus == status && (outcome is null || c.Outcome == outcome)).Sum(c => c.Count);

        return new ChatReviewSummaryDto(
            from, to,
            Sum(ChatReviewStatuses.Open),
            Sum(ChatReviewStatuses.Resolved),
            Sum(ChatReviewStatuses.Ignored),
            Sum(ChatReviewStatuses.Open, ChatOutcomes.NoAnswer),
            Sum(ChatReviewStatuses.Open, ChatOutcomes.Failed));
    }

    private static ChatReviewItemDto ToItem(ChatReviewRow row)
    {
        var (failedSteps, turnMs, error) = ChatReviewMetaReader.ReadReply(row.ReplyMetaJson);
        return new ChatReviewItemDto(
            row.MessageId, row.SessionId, row.UserId, row.CreatedAt, row.Role, row.Outcome, row.ReviewStatus,
            row.QuestionMessageId, ChatReviewMetaReader.ReadModel(row.QuestionMetaJson), failedSteps, turnMs, error);
    }

    // Khoảng ngày tính cả hai đầu theo UTC; bỏ trống đầu nào thì không giới hạn đầu đó. Không có mặc định 30 ngày vì hàng đợi là việc tồn đọng
    private static (DateTime? FromUtc, DateTime? ToUtcExclusive) Range(DateOnly? from, DateOnly? to)
    {
        if (from is { } start && to is { } end && start > end)
            throw new ArgumentException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        return (
            from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            to?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }
}
