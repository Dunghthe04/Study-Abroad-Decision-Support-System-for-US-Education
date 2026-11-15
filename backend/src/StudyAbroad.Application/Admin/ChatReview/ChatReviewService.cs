using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

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

    /// <summary>
    /// Chi tiết một câu CÓ NỘI DUNG chat. Chỉ xem được tin thuộc hàng đợi (tin khác trả null). Ghi nhật ký người xem (audit_logs) TRƯỚC khi trả
    /// nội dung; nếu ghi nhật ký lỗi thì ném lỗi chứ không trả nội dung (không có dấu vết thì không được đọc). Nhật ký không chứa nội dung chat.
    /// </summary>
    Task<ChatReviewDetailDto?> GetDetailAsync(
        Guid messageId, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default);

    /// <summary>
    /// Đổi trạng thái xử lý của một câu trong hàng đợi (open, resolved hoặc ignored; đổi qua lại thoải mái, ví dụ mở lại khi lỡ tay).
    /// Trả null nếu mã không phải tin của hàng đợi. Trạng thái mới trùng trạng thái cũ thì thành công mà không ghi gì. Mỗi lần đổi thật
    /// được ghi nhật ký (audit_logs, chỉ mã và trạng thái, không có nội dung chat) cùng lúc với thay đổi. Trạng thái lạ hoặc thiếu người
    /// thực hiện thì ném ArgumentException.
    /// </summary>
    Task<ChatReviewStatusDto?> SetStatusAsync(
        Guid messageId, string? status, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default);
}

public class ChatReviewService(IChatReviewRepository repository, IAuditLogRepository auditLogs) : IChatReviewService
{
    public const int MaxPageSize = 100;
    public const int ContextMessages = 6;
    public const string ViewAction = "chat_review.view";   // hành động ghi vào audit_logs.action khi một người xem nội dung chat
    public const string StatusAction = "chat_review.status";   // hành động ghi vào audit_logs.action khi một người đổi trạng thái xử lý

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

    public async Task<ChatReviewDetailDto?> GetDetailAsync(
        Guid messageId, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("Thiếu người xem: không ghi được nhật ký nên không trả nội dung.");

        var rows = await repository.FindDetailAsync(messageId, ContextMessages, ct);
        if (rows is null)
            return null;   // không phải tin của hàng đợi: không có gì để xem và cũng không ghi nhật ký

        // Ghi nhật ký TRƯỚC khi trả nội dung. Không bắt lỗi: ghi lỗi thì ném lên, nội dung không rời khỏi hàm này
        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = ViewAction,
            EntityType = "chat_message",
            EntityId = rows.Reply.MessageId.ToString(),
            // Chỉ mã và trạng thái, tuyệt đối không có nội dung chat (không nhân bản dữ liệu cá nhân sang bảng nhật ký)
            DetailJson = JsonSerializer.Serialize(new
            {
                sessionId = rows.Reply.SessionId,
                ownerUserId = rows.Reply.UserId,
                outcome = rows.Reply.Outcome,
                reviewStatus = rows.Reply.ReviewStatus,
            }),
            IpAddress = Clip(ipAddress, 64),
            UserAgent = Clip(userAgent, 512),
        }, ct);

        var reply = rows.Reply;
        var (failedSteps, turnMs, error) = ChatReviewMetaReader.ReadReply(reply.MetaJson);
        return new ChatReviewDetailDto(
            reply.MessageId, reply.SessionId, reply.UserId, reply.CreatedAt, reply.Role, reply.Outcome, reply.ReviewStatus,
            failedSteps, turnMs, error,
            rows.Question is { } question
                ? new ChatReviewQuestionDto(question.MessageId, question.CreatedAt, question.Content, ChatReviewMetaReader.ReadModelDetail(question.MetaJson))
                : null,
            new ChatReviewMessageDto(reply.MessageId, reply.Role, reply.CreatedAt, reply.Content),
            rows.Context
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.MessageId)
                .Select(c => new ChatReviewMessageDto(c.MessageId, c.Role, c.CreatedAt, c.Content))
                .ToList());
    }

    public async Task<ChatReviewStatusDto?> SetStatusAsync(
        Guid messageId, string? status, Guid actorUserId, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        status = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        if (status is null || !Statuses.Contains(status))
            throw new ArgumentException($"Trạng thái phải là một trong: {string.Join(", ", Statuses)}.");
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("Thiếu người thực hiện: không ghi được nhật ký nên không đổi trạng thái.");

        var newStatus = status;
        var previous = await repository.SetStatusAsync(messageId, newStatus, from => new AuditLog
        {
            ActorUserId = actorUserId,
            Action = StatusAction,
            EntityType = "chat_message",
            EntityId = messageId.ToString(),
            // Chỉ trạng thái trước và sau, không có nội dung chat
            DetailJson = JsonSerializer.Serialize(new { from, to = newStatus }),
            IpAddress = Clip(ipAddress, 64),
            UserAgent = Clip(userAgent, 512),
        }, ct);

        return previous is null ? null : new ChatReviewStatusDto(messageId, newStatus, previous);
    }

    // Cắt theo độ rộng cột để một giá trị dài bất thường (trình duyệt lạ) không làm hỏng việc ghi nhật ký
    private static string? Clip(string? text, int max) =>
        string.IsNullOrEmpty(text) ? null : text.Length <= max ? text : text[..max];

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
