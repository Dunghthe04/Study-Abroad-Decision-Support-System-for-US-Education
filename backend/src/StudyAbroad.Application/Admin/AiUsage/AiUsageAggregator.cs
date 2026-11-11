using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.AiUsage;

/// <summary>
/// Truy vấn gộp ai_calls theo nhóm. Viết trên IQueryable nên chạy được hai nơi: EF dịch thành SQL (GROUP BY ở database,
/// không kéo cả bảng về), và test chạy trên danh sách trong bộ nhớ để kiểm số liệu. Token trống (null) tính là 0.
/// Chỉ dùng các biểu thức EF dịch được (Count, Sum, GroupBy theo cột); đã có test kiểm tra dịch ra SQL.
/// userId: chỉ tính các lần gọi của người đó; bỏ trống là tính tất cả.
/// </summary>
public static class AiUsageAggregator
{
    // Cùng giá trị với AiCall.Kind của lần gọi Claude; hằng số để EF dịch thành tham số SQL
    private const string ClaudeKind = "claude";

    /// <summary>Các lần gọi trong khoảng [fromUtc, toUtcExclusive), của một người nếu có userId.</summary>
    public static IQueryable<AiCall> InWindow(IQueryable<AiCall> calls, DateTime fromUtc, DateTime toUtcExclusive, Guid? userId = null)
    {
        var inWindow = calls.Where(c => c.CreatedAt >= fromUtc && c.CreatedAt < toUtcExclusive);
        return userId is { } id ? inWindow.Where(c => c.UserId == id) : inWindow;
    }

    /// <summary>Gộp theo (nguồn, tính năng): nền để tính tổng và tổng theo nguồn.</summary>
    public static IQueryable<AiUsageGroupRow> ByKindAndPurpose(
        IQueryable<AiCall> calls, DateTime fromUtc, DateTime toUtcExclusive, Guid? userId = null) =>
        InWindow(calls, fromUtc, toUtcExclusive, userId)
            .GroupBy(c => new { c.Kind, c.Purpose })
            .Select(g => new AiUsageGroupRow(
                g.Key.Kind,
                g.Key.Purpose,
                null,
                g.Count(),
                g.Count(c => !c.Success),
                g.Sum(c => (long)(c.PromptTokens ?? 0)),
                g.Sum(c => (long)(c.CompletionTokens ?? 0)),
                g.Sum(c => (long)(c.CachedTokens ?? 0)),
                g.Sum(c => (long)(c.LatencyMs ?? 0)),
                g.Sum(c => c.CostUsd ?? 0m),
                g.Count(c => c.Kind == ClaudeKind && c.CostUsd == null)));

    /// <summary>Gộp theo (ngày UTC, nguồn): để vẽ biểu đồ theo ngày.</summary>
    public static IQueryable<AiUsageGroupRow> ByDayAndKind(
        IQueryable<AiCall> calls, DateTime fromUtc, DateTime toUtcExclusive, Guid? userId = null) =>
        InWindow(calls, fromUtc, toUtcExclusive, userId)
            .GroupBy(c => new { Day = c.CreatedAt.Date, c.Kind })
            .Select(g => new AiUsageGroupRow(
                g.Key.Kind,
                null,
                g.Key.Day,
                g.Count(),
                g.Count(c => !c.Success),
                g.Sum(c => (long)(c.PromptTokens ?? 0)),
                g.Sum(c => (long)(c.CompletionTokens ?? 0)),
                g.Sum(c => (long)(c.CachedTokens ?? 0)),
                g.Sum(c => (long)(c.LatencyMs ?? 0)),
                g.Sum(c => c.CostUsd ?? 0m),
                g.Count(c => c.Kind == ClaudeKind && c.CostUsd == null)));

    /// <summary>
    /// Một trang xếp hạng người dùng theo tổng token (vào + ra) giảm dần; bằng nhau thì theo mã người dùng để các trang không lặp hay sót.
    /// Bỏ qua các lần gọi không có người dùng (ngoài phiên đăng nhập).
    /// </summary>
    public static IQueryable<AiUsageUserRow> ByUserPage(
        IQueryable<AiCall> calls, DateTime fromUtc, DateTime toUtcExclusive, int skip, int take) =>
        InWindow(calls, fromUtc, toUtcExclusive)
            .Where(c => c.UserId != null)
            .GroupBy(c => c.UserId!.Value)
            // Sắp xếp và phân trang trên các NHÓM, trước khi dựng bản ghi: sắp sau khi dựng bản ghi bằng hàm khởi tạo thì EF
            // không dịch được (không biết thuộc tính nào ứng với phép cộng nào). Test dịch SQL canh đúng chỗ này
            .OrderByDescending(g => g.Sum(c => (long)(c.PromptTokens ?? 0)) + g.Sum(c => (long)(c.CompletionTokens ?? 0)))
            .ThenBy(g => g.Key)
            .Skip(skip)
            .Take(take)
            .Select(g => new AiUsageUserRow(
                g.Key,
                g.Count(),
                g.Count(c => !c.Success),
                g.Sum(c => (long)(c.PromptTokens ?? 0)),
                g.Sum(c => (long)(c.CompletionTokens ?? 0)),
                g.Sum(c => (long)(c.CachedTokens ?? 0)),
                g.Sum(c => (long)(c.LatencyMs ?? 0)),
                g.Sum(c => c.CostUsd ?? 0m),
                g.Count(c => c.Kind == ClaudeKind && c.CostUsd == null)));

    // Tin nhắn không phải của học sinh (trợ lý hoặc dòng lỗi hệ thống) thuộc về người dùng, trong khoảng thời gian
    private static IQueryable<ChatMessage> TurnsOf(
        IQueryable<ChatMessage> messages, IQueryable<ChatSession> sessions, Guid userId, DateTime fromUtc, DateTime toUtcExclusive) =>
        messages
            .Join(sessions, m => m.ChatSessionId, s => s.Id, (m, s) => new { Message = m, s.UserId })
            .Where(x => x.UserId == userId && x.Message.Role != "user"
                        && x.Message.CreatedAt >= fromUtc && x.Message.CreatedAt < toUtcExclusive)
            .Select(x => x.Message);

    /// <summary>
    /// Một trang các lượt chat của một người, mới nhất trước, kèm số đo cộng từ các lần gọi AI của từng lượt (nối bằng chat_message_id).
    /// Phân trang trước, rồi mới tính số đo cho các lượt của trang đó (dùng chỉ mục chat_message_id của ai_calls).
    /// </summary>
    public static IQueryable<AiUsageTurnRow> TurnsPage(
        IQueryable<ChatMessage> messages, IQueryable<ChatSession> sessions, IQueryable<AiCall> calls,
        Guid userId, DateTime fromUtc, DateTime toUtcExclusive, int skip, int take) =>
        TurnsOf(messages, sessions, userId, fromUtc, toUtcExclusive)
            .OrderByDescending(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Skip(skip)
            .Take(take)
            .Select(m => new AiUsageTurnRow(
                m.Id,
                m.ChatSessionId,
                m.CreatedAt,
                m.Role,
                m.Outcome,
                calls.Count(a => a.ChatMessageId == m.Id),
                calls.Count(a => a.ChatMessageId == m.Id && !a.Success),
                calls.Where(a => a.ChatMessageId == m.Id).Sum(a => (long)(a.PromptTokens ?? 0)),
                calls.Where(a => a.ChatMessageId == m.Id).Sum(a => (long)(a.CompletionTokens ?? 0)),
                calls.Where(a => a.ChatMessageId == m.Id).Sum(a => (long)(a.CachedTokens ?? 0)),
                calls.Where(a => a.ChatMessageId == m.Id).Sum(a => (long)(a.LatencyMs ?? 0)),
                calls.Where(a => a.ChatMessageId == m.Id).Sum(a => a.CostUsd ?? 0m),
                calls.Count(a => a.ChatMessageId == m.Id && a.Kind == ClaudeKind && a.CostUsd == null)));

    /// <summary>Mã các lượt chat của người trong khoảng, để đếm tổng số lượt cho phân trang.</summary>
    public static IQueryable<Guid> TurnIds(
        IQueryable<ChatMessage> messages, IQueryable<ChatSession> sessions, Guid userId, DateTime fromUtc, DateTime toUtcExclusive) =>
        TurnsOf(messages, sessions, userId, fromUtc, toUtcExclusive).Select(m => m.Id);

    /// <summary>Thông tin chung của một lượt (kèm người sở hữu qua phiên chat). Rỗng nếu không có tin nhắn đó.</summary>
    public static IQueryable<AiUsageTurnHeader> TurnHeader(IQueryable<ChatMessage> messages, IQueryable<ChatSession> sessions, Guid messageId) =>
        messages
            .Where(m => m.Id == messageId)
            .Join(sessions, m => m.ChatSessionId, s => s.Id,
                (m, s) => new AiUsageTurnHeader(m.Id, m.ChatSessionId, s.UserId, m.CreatedAt, m.Role, m.Outcome));

    /// <summary>Các lần gọi AI của một lượt theo thứ tự thời gian.</summary>
    public static IQueryable<AiUsageCallDetailDto> CallsOfTurn(IQueryable<AiCall> calls, Guid messageId) =>
        calls
            .Where(a => a.ChatMessageId == messageId)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Select(a => new AiUsageCallDetailDto(
                a.Id,
                a.CreatedAt,
                a.Kind,
                a.Purpose,
                a.SkillVersion,
                a.Model,
                a.PromptTokens ?? 0,
                a.CompletionTokens ?? 0,
                a.CachedTokens ?? 0,
                a.LatencyMs ?? 0,
                a.Success,
                a.Error,
                a.CostUsd));

    /// <summary>Các người dùng có ít nhất một lần gọi trong khoảng (mỗi người một lần), để đếm tổng số người cho phân trang.</summary>
    public static IQueryable<Guid> UsersInWindow(IQueryable<AiCall> calls, DateTime fromUtc, DateTime toUtcExclusive) =>
        InWindow(calls, fromUtc, toUtcExclusive)
            .Where(c => c.UserId != null)
            .Select(c => c.UserId!.Value)
            .Distinct();
}
