using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.AiQuality;

public interface IAiQualitySampleService
{
    /// <summary>
    /// Một trang hội thoại mẫu của nhóm có vấn đề (no_answer, refused, failed), mới nhất trước, nội dung ĐÃ CHE thông tin cá nhân.
    /// Nhóm answered không có ở đây (dùng chức năng duyệt mẫu). Hội thoại của người dưới 16 tuổi chưa có đồng ý phù hợp bị ẩn nội dung
    /// và chỉ được đếm vào HiddenCount. Ghi nhật ký người xem (audit_logs) TRƯỚC khi trả nội dung; ghi lỗi thì ném lỗi chứ không trả nội dung.
    /// Nhật ký chỉ có mã và số đếm, không có nội dung chat. outcome sai hoặc khoảng ngày sai thì ném ArgumentException.
    /// </summary>
    Task<AiQualitySamplePage> GetPageAsync(
        Guid actorUserId, DateOnly? from, DateOnly? to, string outcome, int page, int pageSize,
        string? ipAddress, string? userAgent, CancellationToken ct = default);
}

public class AiQualitySampleService(
    IAiQualitySampleRepository repository, IAuditLogRepository auditLogs, TimeProvider? clock = null) : IAiQualitySampleService
{
    public const int MaxPageSize = 20;
    public const int ContextMessages = 2;
    public const string ViewSamplesAction = "ai_quality.view_samples";

    /// <summary>Các loại được xem nội dung. Nhóm answered không nằm ở đây: đã có chức năng duyệt mẫu (review-batches).</summary>
    public static readonly string[] ProblemOutcomes = [ChatOutcomes.NoAnswer, ChatOutcomes.Refused, ChatOutcomes.Failed];

    public async Task<AiQualitySamplePage> GetPageAsync(
        Guid actorUserId, DateOnly? from, DateOnly? to, string outcome, int page, int pageSize,
        string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("Thiếu người xem: không ghi được nhật ký nên không trả nội dung.");
        if (!ProblemOutcomes.Contains(outcome))
            throw new ArgumentException(
                $"Chỉ xem được nội dung của nhóm có vấn đề ({string.Join(", ", ProblemOutcomes)}). Nhóm đã trả lời được thì dùng chức năng duyệt mẫu.");
        var (start, end) = AiQualityRange.Resolve(from, to, clock);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        // Bước một: chỉ thông tin để quyết định, chưa có chữ nào của học sinh
        var candidates = await repository.CandidatesAsync(AiQualityRange.FromUtc(start), AiQualityRange.ToUtcExclusive(end), outcome, ct);
        var visible = candidates.Where(c => AiQualityContentGate.CanReadContent(c)).ToList();
        var hidden = candidates.Count - visible.Count;
        var pageIds = visible.Skip((page - 1) * pageSize).Take(pageSize).Select(c => c.MessageId).ToList();
        if (pageIds.Count == 0)
            return new AiQualitySamplePage([], page, pageSize, visible.Count, hidden);   // không có gì để xem nên cũng không ghi nhật ký

        // Ghi nhật ký TRƯỚC khi lấy nội dung. Không bắt lỗi: ghi lỗi thì ném lên, nội dung chưa được tải
        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = ViewSamplesAction,
            EntityType = "chat_message",
            // Chỉ mã và số đếm, tuyệt đối không có nội dung chat (không nhân bản dữ liệu cá nhân sang bảng nhật ký)
            DetailJson = JsonSerializer.Serialize(new
            {
                from = start, to = end, outcome, page, pageSize, count = pageIds.Count, hiddenCount = hidden, messageIds = pageIds,
            }),
            IpAddress = Clip(ipAddress, 64),
            UserAgent = Clip(userAgent, 512),
        }, ct);

        // Bước hai: nội dung của đúng các lượt được xem, giữ thứ tự mới nhất trước
        var rows = (await repository.RowsAsync(pageIds, ContextMessages, ct)).ToDictionary(r => r.MessageId);
        var items = pageIds.Where(rows.ContainsKey).Select(id => ToDto(rows[id], outcome)).ToList();
        return new AiQualitySamplePage(items, page, pageSize, visible.Count, hidden);
    }

    // Che mọi chữ của học sinh (tin hỏi, tin trả lời, ngữ cảnh) bằng bộ che chung, kèm họ tên chủ phiên; bỏ mọi mã định danh
    private static AiQualitySampleDto ToDto(AiQualitySampleRow row, string requestedOutcome)
    {
        string[] names = row.OwnerFullName is { } name ? [name] : [];
        var meta = AiQualityMetaReader.ToTurn(row.CreatedAt, row.Outcome, row.ReplyMetaJson, row.QuestionMetaJson, false);
        return new AiQualitySampleDto(
            row.CreatedAt,
            row.Outcome ?? requestedOutcome,
            meta.Feature,
            meta.TurnMs,
            row.Question is null ? null : ChatTextMasker.Mask(row.Question, names),
            ChatTextMasker.Mask(row.Answer, names),
            row.Context.Select(c => new AiQualitySampleMessage(c.Role, ChatTextMasker.Mask(c.Content, names))).ToList());
    }

    private static string? Clip(string? text, int max) =>
        string.IsNullOrEmpty(text) ? null : text.Length <= max ? text : text[..max];
}
