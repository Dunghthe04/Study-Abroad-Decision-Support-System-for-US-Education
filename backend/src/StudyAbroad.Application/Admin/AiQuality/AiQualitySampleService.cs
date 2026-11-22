using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.AiQuality;

public interface IAiQualitySampleService
{
    /// <summary>
    /// Một trang hội thoại mẫu của nhóm có vấn đề (no_answer, refused, failed), mới nhất trước, nội dung ĐÃ CHE thông tin cá nhân.
    /// Nhóm answered không có ở đây (dùng chức năng duyệt mẫu). Hội thoại học sinh đã xóa, và của người dưới 16 tuổi chưa có đồng ý phù hợp,
    /// thành DÒNG GIỮ CHỖ ở đúng vị trí: có thời điểm, loại kết quả, tính năng, thời gian phản hồi và lý do ẩn, không có nội dung (nội dung không
    /// bao giờ được tải). Ghi nhật ký người xem (audit_logs) TRƯỚC khi trả nội dung; ghi lỗi thì ném lỗi chứ không trả nội dung.
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
        var reasons = candidates.Select(c => (Candidate: c, Reason: AiQualityContentGate.HiddenReason(c))).ToList();
        var hiddenDeleted = reasons.Count(r => r.Reason == AiQualityHiddenReason.DeletedByUser);
        var hiddenMinor = reasons.Count(r => r.Reason == AiQualityHiddenReason.MinorWithoutConsent);
        var hidden = hiddenDeleted + hiddenMinor;

        // Phân trang trên MỌI lượt (lượt bị ẩn thành dòng giữ chỗ ở đúng vị trí thời gian), nên tổng khớp số trên thẻ của báo cáo
        var onPage = reasons.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var pageIds = onPage.Where(r => r.Reason is null).Select(r => r.Candidate.MessageId).ToList();
        var hiddenIds = onPage.Where(r => r.Reason is not null).Select(r => r.Candidate.MessageId).ToList();
        if (onPage.Count == 0)
            return new AiQualitySamplePage([], page, pageSize, reasons.Count, hidden, hiddenDeleted, hiddenMinor);

        // Dòng giữ chỗ chỉ có số liệu, không có nội dung: tải được mà không cần ghi nhật ký
        var hiddenRows = (await repository.HiddenRowsAsync(hiddenIds, ct)).ToDictionary(r => r.MessageId);
        if (pageIds.Count == 0)   // trang toàn dòng giữ chỗ: không có nội dung nào rời đi nên không ghi nhật ký
            return new AiQualitySamplePage(
                Assemble(onPage, new Dictionary<Guid, AiQualitySampleRow>(), hiddenRows, outcome), page, pageSize, reasons.Count, hidden,
                hiddenDeleted, hiddenMinor);

        // Ghi nhật ký TRƯỚC khi lấy nội dung. Không bắt lỗi: ghi lỗi thì ném lên, nội dung chưa được tải
        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = ViewSamplesAction,
            EntityType = "chat_message",
            // Chỉ mã và số đếm, tuyệt đối không có nội dung chat (không nhân bản dữ liệu cá nhân sang bảng nhật ký)
            DetailJson = JsonSerializer.Serialize(new
            {
                from = start, to = end, outcome, page, pageSize, count = pageIds.Count, hiddenCount = hidden,
                hiddenDeletedCount = hiddenDeleted, hiddenMinorCount = hiddenMinor,
                // Tách hai nhóm: lượt đã được đọc nội dung, và lượt chỉ hiện dòng giữ chỗ (không ai đọc được chữ)
                messageIds = pageIds, placeholderMessageIds = hiddenIds,
            }),
            IpAddress = Clip(ipAddress, 64),
            UserAgent = Clip(userAgent, 512),
        }, ct);

        // Bước hai: nội dung của đúng các lượt được xem, giữ thứ tự mới nhất trước
        var rows = (await repository.RowsAsync(pageIds, ContextMessages, ct)).ToDictionary(r => r.MessageId);
        return new AiQualitySamplePage(Assemble(onPage, rows, hiddenRows, outcome), page, pageSize, reasons.Count, hidden, hiddenDeleted, hiddenMinor);
    }

    // Ghép theo đúng thứ tự của trang (mới nhất trước); lượt không còn tồn tại thì bỏ qua
    private static List<AiQualitySampleDto> Assemble(
        IEnumerable<(AiQualityCandidate Candidate, AiQualityHiddenReason? Reason)> onPage,
        IReadOnlyDictionary<Guid, AiQualitySampleRow> rows, IReadOnlyDictionary<Guid, AiQualityHiddenRow> hiddenRows, string outcome)
    {
        var items = new List<AiQualitySampleDto>();
        foreach (var (candidate, reason) in onPage)
        {
            var id = candidate.MessageId;
            if (reason is { } why)
            {
                if (hiddenRows.TryGetValue(id, out var hiddenRow))
                    items.Add(Placeholder(hiddenRow, why, outcome));
            }
            else if (rows.TryGetValue(id, out var row))
                items.Add(ToDto(row, outcome));
        }
        return items;
    }

    // Dòng giữ chỗ: chỉ số liệu của lượt và lý do ẩn, không có câu hỏi, câu trả lời hay ngữ cảnh
    private static AiQualitySampleDto Placeholder(AiQualityHiddenRow row, AiQualityHiddenReason reason, string requestedOutcome)
    {
        var meta = AiQualityMetaReader.ToTurn(row.CreatedAt, row.Outcome, row.ReplyMetaJson, row.SlmFallback, false);
        return new AiQualitySampleDto(
            row.CreatedAt, row.Outcome ?? requestedOutcome, meta.Feature, meta.TurnMs, null, null, [], AiQualityHiddenReasons.Code(reason));
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
