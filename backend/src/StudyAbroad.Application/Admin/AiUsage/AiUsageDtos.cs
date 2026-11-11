namespace StudyAbroad.Application.Admin.AiUsage;

/// <summary>
/// Số đo của một nhóm lần gọi AI. InputTokens chưa gồm token Claude đọc từ cache (CachedTokens tính riêng, tính tiền khác).
/// AvgLatencyMs là thời gian trung bình của MỘT lần gọi (mili giây), làm tròn một chữ số thập phân.
/// </summary>
public record AiUsageMetrics(int Calls, int FailedCalls, long InputTokens, long OutputTokens, long CachedTokens, double AvgLatencyMs);

/// <summary>Theo nguồn: ollama (AI local, không tốn tiền) hoặc claude (AI cloud, trả phí theo token).</summary>
public record AiUsageByKind(string Kind, AiUsageMetrics Metrics);

/// <summary>Theo tính năng. Purpose là tên skill (slm_intents, skill_tra_loi, transcript_ocr_read...), null với dòng cũ chưa có nhãn.</summary>
public record AiUsageByPurpose(string Kind, string? Purpose, AiUsageMetrics Metrics);

/// <summary>Theo ngày (UTC) và nguồn, để vẽ biểu đồ local so với cloud.</summary>
public record AiUsageByDay(DateOnly Date, string Kind, AiUsageMetrics Metrics);

/// <summary>Tổng quan token của admin trong khoảng From đến To (tính cả hai đầu, theo ngày UTC).</summary>
public record AiUsageOverviewDto(
    DateOnly From,
    DateOnly To,
    AiUsageMetrics Total,
    IReadOnlyList<AiUsageByKind> ByKind,
    IReadOnlyList<AiUsageByPurpose> ByPurpose,
    IReadOnlyList<AiUsageByDay> ByDay);

/// <summary>Một người trong bảng xếp hạng token. FullName và Email trống nếu tài khoản đã bị xóa (dòng nhật ký vẫn còn).</summary>
public record AiUsageUserRankDto(Guid UserId, string? FullName, string? Email, AiUsageMetrics Metrics);

/// <summary>Tổng quan token riêng của một người: cùng cấu trúc với tổng thể nhưng chỉ tính các lần gọi của người đó.</summary>
public record AiUsageUserOverviewDto(Guid UserId, string FullName, string Email, AiUsageOverviewDto Overview);

/// <summary>
/// Một lượt chat trong danh sách của một người: tin trả lời của hệ thống (Role assistant) hoặc dòng báo lỗi khi advisor sập (Role system),
/// cùng số đo cộng từ các lần gọi AI của lượt (bảng ai_calls). KHÔNG có nội dung chat: màn token chỉ cần số đo,
/// nội dung nằm ở API hàng đợi câu hỏi (có ghi lại ai đã xem).
/// </summary>
public record AiUsageTurnRowDto(Guid MessageId, Guid SessionId, DateTime CreatedAt, string Role, string? Outcome, AiUsageMetrics Metrics);

/// <summary>Một lần gọi AI trong lượt chat: nguồn, tính năng, phiên bản skill, model, token, thời gian, lỗi.</summary>
public record AiUsageCallDetailDto(
    Guid Id,
    DateTime CreatedAt,
    string Kind,
    string? Purpose,
    string? SkillVersion,
    string? Model,
    int InputTokens,
    int OutputTokens,
    int CachedTokens,
    int LatencyMs,
    bool Success,
    string? Error);

/// <summary>Chi tiết một lượt chat: tổng và từng lần gọi AI theo thứ tự thời gian. Không có nội dung chat.</summary>
public record AiUsageTurnDetailDto(
    Guid MessageId,
    Guid SessionId,
    Guid UserId,
    DateTime CreatedAt,
    string Role,
    string? Outcome,
    AiUsageMetrics Total,
    IReadOnlyList<AiUsageCallDetailDto> Calls);

/// <summary>Dòng thô của một lượt do truy vấn gộp trả về (chưa tính trung bình), cùng ý nghĩa với AiUsageGroupRow.</summary>
public record AiUsageTurnRow(
    Guid MessageId,
    Guid SessionId,
    DateTime CreatedAt,
    string Role,
    string? Outcome,
    int Calls,
    int FailedCalls,
    long InputTokens,
    long OutputTokens,
    long CachedTokens,
    long LatencySumMs);

/// <summary>Thông tin chung của một lượt chat (tin nhắn và phiên chứa nó), để dựng chi tiết một lượt.</summary>
public record AiUsageTurnHeader(Guid MessageId, Guid SessionId, Guid UserId, DateTime CreatedAt, string Role, string? Outcome);

/// <summary>Họ tên và email của người dùng, lấy từ bảng users để gắn vào số liệu.</summary>
public record AiUsageUserInfo(Guid Id, string FullName, string Email);

/// <summary>Dòng thô theo người do truy vấn gộp trả về (chưa tính trung bình), cùng ý nghĩa với AiUsageGroupRow.</summary>
public record AiUsageUserRow(
    Guid UserId,
    int Calls,
    int FailedCalls,
    long InputTokens,
    long OutputTokens,
    long CachedTokens,
    long LatencySumMs);

/// <summary>
/// Một dòng thô do truy vấn gộp trả về (chưa tính trung bình): Purpose có khi gộp theo tính năng, Day có khi gộp theo ngày.
/// LatencySumMs là tổng thời gian để cộng dồn các nhóm rồi mới chia cho số lần gọi, tránh trung bình của trung bình.
/// </summary>
public record AiUsageGroupRow(
    string Kind,
    string? Purpose,
    DateTime? Day,
    int Calls,
    int FailedCalls,
    long InputTokens,
    long OutputTokens,
    long CachedTokens,
    long LatencySumMs);
