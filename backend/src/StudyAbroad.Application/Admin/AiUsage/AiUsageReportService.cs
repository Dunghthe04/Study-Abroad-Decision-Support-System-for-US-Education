using StudyAbroad.Application.Common;

namespace StudyAbroad.Application.Admin.AiUsage;

public interface IAiUsageReportService
{
    /// <summary>
    /// Tổng quan token cả hệ thống trong khoảng from đến to (tính cả hai đầu, theo ngày UTC). Bỏ trống to thì lấy hôm nay,
    /// bỏ trống from thì lấy 30 ngày kết thúc ở to. Khoảng sai (from sau to, hoặc dài quá 366 ngày) thì ném ArgumentException.
    /// </summary>
    Task<AiUsageOverviewDto> GetOverviewAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);

    /// <summary>
    /// Bảng xếp hạng người dùng theo token (vào + ra) giảm dần trong khoảng. page từ 1; pageSize bị giới hạn 1 đến 100.
    /// Không gồm các lần gọi không có người dùng (ngoài phiên đăng nhập). Khoảng sai thì ném ArgumentException.
    /// </summary>
    Task<PagedResult<AiUsageUserRankDto>> GetUsersAsync(DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Tổng quan token riêng của một người. Trả null nếu không có người dùng đó. Khoảng sai thì ném ArgumentException.</summary>
    Task<AiUsageUserOverviewDto?> GetUserOverviewAsync(Guid userId, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    /// <summary>
    /// Các lượt chat của một người (mới nhất trước) kèm số đo, phân trang như GetUsersAsync. Trả null nếu không có người dùng đó.
    /// Không có nội dung chat. Khoảng sai thì ném ArgumentException.
    /// </summary>
    Task<PagedResult<AiUsageTurnRowDto>?> GetUserTurnsAsync(
        Guid userId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Chi tiết một lượt chat: tổng và từng lần gọi AI. Trả null nếu không có tin nhắn đó. Không có nội dung chat.</summary>
    Task<AiUsageTurnDetailDto?> GetTurnAsync(Guid messageId, CancellationToken ct = default);
}

public class AiUsageReportService(IAiUsageReportRepository repository, TimeProvider? clock = null) : IAiUsageReportService
{
    public const int DefaultDays = 30;
    public const int MaxDays = 366;
    public const int MaxPageSize = 100;

    public Task<AiUsageOverviewDto> GetOverviewAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default) =>
        BuildOverviewAsync(from, to, null, ct);

    public async Task<PagedResult<AiUsageUserRankDto>> GetUsersAsync(
        DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default)
    {
        var (start, end) = Range(from, to);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (rows, total) = await repository.ByUserPageAsync(
            FromUtc(start), ToUtcExclusive(end), (page - 1) * pageSize, pageSize, ct);
        var users = (await repository.UsersByIdAsync(rows.Select(row => row.UserId).ToList(), ct)).ToDictionary(user => user.Id);

        var items = rows
            .Select(row => new AiUsageUserRankDto(
                row.UserId,
                users.GetValueOrDefault(row.UserId)?.FullName,
                users.GetValueOrDefault(row.UserId)?.Email,
                Metrics([Row(row)])))
            .ToList();
        return new PagedResult<AiUsageUserRankDto>(items, page, pageSize, total);
    }

    public async Task<AiUsageUserOverviewDto?> GetUserOverviewAsync(Guid userId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        // Kiểm khoảng ngày trước để khoảng sai luôn báo lỗi, dù người dùng có tồn tại hay không
        Range(from, to);
        var user = (await repository.UsersByIdAsync([userId], ct)).FirstOrDefault();
        if (user is null)
            return null;
        return new AiUsageUserOverviewDto(user.Id, user.FullName, user.Email, await BuildOverviewAsync(from, to, userId, ct));
    }

    public async Task<PagedResult<AiUsageTurnRowDto>?> GetUserTurnsAsync(
        Guid userId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct = default)
    {
        var (start, end) = Range(from, to);   // khoảng sai luôn báo lỗi, dù người dùng có tồn tại hay không
        if ((await repository.UsersByIdAsync([userId], ct)).Count == 0)
            return null;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (rows, total) = await repository.TurnsPageAsync(userId, FromUtc(start), ToUtcExclusive(end), (page - 1) * pageSize, pageSize, ct);
        var items = rows
            .Select(row => new AiUsageTurnRowDto(row.MessageId, row.SessionId, row.CreatedAt, row.Role, row.Outcome, Metrics([Row(row)])))
            .ToList();
        return new PagedResult<AiUsageTurnRowDto>(items, page, pageSize, total);
    }

    public async Task<AiUsageTurnDetailDto?> GetTurnAsync(Guid messageId, CancellationToken ct = default)
    {
        var header = await repository.FindTurnAsync(messageId, ct);
        if (header is null)
            return null;
        var calls = await repository.CallsOfTurnAsync(messageId, ct);
        return new AiUsageTurnDetailDto(
            header.MessageId, header.SessionId, header.UserId, header.CreatedAt, header.Role, header.Outcome,
            Metrics(calls.Select(Row)),
            calls);
    }

    private async Task<AiUsageOverviewDto> BuildOverviewAsync(DateOnly? from, DateOnly? to, Guid? userId, CancellationToken ct)
    {
        var (start, end) = Range(from, to);
        var fromUtc = FromUtc(start);
        var toUtcExclusive = ToUtcExclusive(end);
        var kindPurpose = await repository.ByKindAndPurposeAsync(fromUtc, toUtcExclusive, userId, ct);
        var dayKind = await repository.ByDayAndKindAsync(fromUtc, toUtcExclusive, userId, ct);

        return new AiUsageOverviewDto(
            start,
            end,
            Metrics(kindPurpose),
            kindPurpose
                .GroupBy(row => row.Kind)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new AiUsageByKind(group.Key, Metrics(group)))
                .ToList(),
            kindPurpose
                .OrderByDescending(row => row.InputTokens + row.OutputTokens)
                .ThenBy(row => row.Kind, StringComparer.Ordinal)
                .ThenBy(row => row.Purpose, StringComparer.Ordinal)
                .Select(row => new AiUsageByPurpose(row.Kind, row.Purpose, Metrics([row])))
                .ToList(),
            dayKind
                .OrderBy(row => row.Day)
                .ThenBy(row => row.Kind, StringComparer.Ordinal)
                .Select(row => new AiUsageByDay(DateOnly.FromDateTime(row.Day!.Value), row.Kind, Metrics([row])))
                .ToList());
    }

    // Khoảng ngày tính cả hai đầu theo UTC; bỏ trống thì mặc định 30 ngày kết thúc hôm nay
    private (DateOnly Start, DateOnly End) Range(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);
        var end = to ?? today;
        var start = from ?? end.AddDays(1 - DefaultDays);
        if (start > end)
            throw new ArgumentException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        if (end.DayNumber - start.DayNumber + 1 > MaxDays)
            throw new ArgumentException($"Khoảng thời gian tối đa {MaxDays} ngày.");
        return (start, end);
    }

    private static DateTime FromUtc(DateOnly start) => start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    private static DateTime ToUtcExclusive(DateOnly end) => end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    // Dòng theo người có cùng các số đo với dòng theo nhóm, đổi sang một kiểu để dùng chung hàm cộng dồn
    private static AiUsageGroupRow Row(AiUsageUserRow row) =>
        new("", null, null, row.Calls, row.FailedCalls, row.InputTokens, row.OutputTokens, row.CachedTokens, row.LatencySumMs,
            row.CostUsd, row.UnpricedCalls);

    // Dòng của một lượt và một lần gọi AI cũng đổi sang cùng kiểu dòng thô để dùng chung hàm cộng dồn
    private static AiUsageGroupRow Row(AiUsageTurnRow row) =>
        new("", null, null, row.Calls, row.FailedCalls, row.InputTokens, row.OutputTokens, row.CachedTokens, row.LatencySumMs,
            row.CostUsd, row.UnpricedCalls);

    private static AiUsageGroupRow Row(AiUsageCallDetailDto call) =>
        new(call.Kind, call.Purpose, null, 1, call.Success ? 0 : 1, call.InputTokens, call.OutputTokens, call.CachedTokens, call.LatencyMs,
            call.CostUsd ?? 0m, call.CostUsd is null && call.Kind == "claude" ? 1 : 0);

    // Cộng các dòng thô rồi mới chia: trung bình thời gian của cả nhóm = tổng thời gian chia tổng số lần gọi
    private static AiUsageMetrics Metrics(IEnumerable<AiUsageGroupRow> rows)
    {
        var list = rows as IReadOnlyCollection<AiUsageGroupRow> ?? rows.ToList();
        var calls = list.Sum(row => row.Calls);
        return new AiUsageMetrics(
            calls,
            list.Sum(row => row.FailedCalls),
            list.Sum(row => row.InputTokens),
            list.Sum(row => row.OutputTokens),
            list.Sum(row => row.CachedTokens),
            calls == 0 ? 0 : Math.Round((double)list.Sum(row => row.LatencySumMs) / calls, 1),
            list.Sum(row => row.CostUsd),
            list.Sum(row => row.UnpricedCalls));
    }
}
