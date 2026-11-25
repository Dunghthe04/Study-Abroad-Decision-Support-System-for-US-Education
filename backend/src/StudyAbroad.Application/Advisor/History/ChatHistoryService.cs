using System.Text.Json;

namespace StudyAbroad.Application.Advisor.History;

/// <summary>Đọc lịch sử chat của CHÍNH người dùng. Không có cách nào đọc phiên của người khác qua đây.</summary>
public interface IChatHistoryRepository
{
    /// <summary>Một trang phiên của userId (chỉ phiên có tin), mới trao đổi nhất trước.</summary>
    Task<IReadOnlyList<ChatSessionRow>> SessionsAsync(Guid userId, ChatHistoryCursor? after, int take, CancellationToken ct = default);

    /// <summary>
    /// Tiêu đề của phiên nếu phiên đó là của userId. Không có phiên hoặc phiên của người khác: Found = false (bên gọi trả 404 như nhau).
    /// </summary>
    Task<(bool Found, string? Title)> SessionTitleAsync(Guid sessionId, Guid userId, CancellationToken ct = default);

    /// <summary>take tin mới nhất của phiên (đứng trước con trỏ before nếu có), mới nhất trước. Bên gọi đã kiểm chủ phiên.</summary>
    Task<IReadOnlyList<ChatHistoryMessageRow>> MessagesAsync(Guid sessionId, ChatHistoryCursor? before, int take, CancellationToken ct = default);

    /// <summary>Đổi tên phiên còn hiệu lực của userId. False nếu không có phiên đó, của người khác, hoặc đã xóa.</summary>
    Task<bool> RenameAsync(Guid sessionId, Guid userId, string title, CancellationToken ct = default);

    /// <summary>Xóa mềm phiên còn hiệu lực của userId (ghi DeletedAt). False nếu không có phiên đó, của người khác, hoặc đã xóa trước đó.</summary>
    Task<bool> SoftDeleteAsync(Guid sessionId, Guid userId, DateTime deletedAtUtc, CancellationToken ct = default);
}

public interface IChatHistoryService
{
    /// <summary>
    /// Danh sách phiên của chính người dùng, mới trao đổi nhất trước. Chỉ phiên có ít nhất một tin. limit bị giới hạn 1 đến MaxSessions.
    /// cursor: NextCursor của lần trước (null là trang đầu). Con trỏ hỏng thì ném ArgumentException.
    /// </summary>
    Task<ChatSessionPageDto> ListSessionsAsync(Guid userId, string? cursor, int limit, CancellationToken ct = default);

    /// <summary>
    /// Các tin của một phiên của chính người dùng, cũ trước mới sau: mặc định là các tin mới nhất; before là OlderCursor của lần trước
    /// để lấy tin cũ hơn. Null nếu không có phiên đó hoặc phiên của người khác (bên gọi trả 404, không phân biệt hai trường hợp).
    /// </summary>
    Task<ChatHistoryMessagesDto?> GetMessagesAsync(Guid userId, Guid sessionId, string? before, int limit, CancellationToken ct = default);

    /// <summary>
    /// Đổi tên một phiên của chính người dùng. Không đổi thứ tự trong lịch sử (không tính là một lần trao đổi).
    /// Tên rỗng hoặc dài quá ChatSessionTitle.MaxUserLength thì ném ArgumentException. False nếu không có phiên, của người khác, hoặc đã xóa.
    /// Trả tên đã chuẩn hóa khi thành công.
    /// </summary>
    Task<string?> RenameAsync(Guid userId, Guid sessionId, string? title, CancellationToken ct = default);

    /// <summary>
    /// Xóa mềm một phiên của chính người dùng: biến khỏi lịch sử, không mở lại hay hỏi tiếp được. Tin nhắn vẫn giữ cho thống kê.
    /// False nếu không có phiên, của người khác, hoặc đã xóa trước đó.
    /// </summary>
    Task<bool> DeleteAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
}

public class ChatHistoryService(IChatHistoryRepository repository, TimeProvider? clock = null) : IChatHistoryService
{
    public const int DefaultSessions = 20;
    public const int MaxSessions = 50;
    public const int DefaultMessages = 100;
    public const int MaxMessages = 200;

    /// <summary>Chữ thay cho dòng lỗi hệ thống (vai trò system): dòng gốc ghi tên loại lỗi kỹ thuật, không có ý nghĩa với học sinh.</summary>
    public const string SystemFailureText = "Hệ thống không phản hồi lượt này. Bạn có thể hỏi lại.";

    public async Task<ChatSessionPageDto> ListSessionsAsync(Guid userId, string? cursor, int limit, CancellationToken ct = default)
    {
        var after = ChatHistoryCursor.Decode(cursor);
        limit = Math.Clamp(limit, 1, MaxSessions);
        // Lấy dư một dòng để biết còn trang sau mà không cần đếm cả bảng
        var rows = await repository.SessionsAsync(userId, after, limit + 1, ct);
        var page = rows.Take(limit).ToList();
        var next = rows.Count > limit ? new ChatHistoryCursor(page[^1].LastActivityAt, page[^1].Id).Encode() : null;
        return new ChatSessionPageDto(
            page.Select(r => new ChatSessionSummaryDto(r.Id, r.Title ?? ChatSessionTitle.Untitled, r.CreatedAt, r.LastActivityAt, r.MessageCount)).ToList(),
            next);
    }

    public async Task<ChatHistoryMessagesDto?> GetMessagesAsync(
        Guid userId, Guid sessionId, string? before, int limit, CancellationToken ct = default)
    {
        var cursor = ChatHistoryCursor.Decode(before);   // con trỏ hỏng thì báo lỗi trước, dù phiên có tồn tại hay không
        var (found, title) = await repository.SessionTitleAsync(sessionId, userId, ct);
        if (!found)
            return null;

        limit = Math.Clamp(limit, 1, MaxMessages);
        var rows = await repository.MessagesAsync(sessionId, cursor, limit + 1, ct);
        var newestFirst = rows.Take(limit).ToList();
        var older = rows.Count > limit ? new ChatHistoryCursor(newestFirst[^1].CreatedAt, newestFirst[^1].Id).Encode() : null;
        newestFirst.Reverse();   // hiện cũ trước, mới sau như khung chat
        return new ChatHistoryMessagesDto(
            sessionId,
            title ?? ChatSessionTitle.Untitled,
            newestFirst.Select(ToDto).ToList(),
            older);
    }

    public async Task<string?> RenameAsync(Guid userId, Guid sessionId, string? title, CancellationToken ct = default)
    {
        var clean = ChatSessionTitle.FromUser(title);   // tên sai thì báo lỗi trước, dù phiên có tồn tại hay không
        return await repository.RenameAsync(sessionId, userId, clean, ct) ? clean : null;
    }

    public Task<bool> DeleteAsync(Guid userId, Guid sessionId, CancellationToken ct = default) =>
        repository.SoftDeleteAsync(sessionId, userId, (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime, ct);

    private static ChatHistoryMessageDto ToDto(ChatHistoryMessageRow row) =>
        new(row.Id, row.Role, row.Role == "system" ? SystemFailureText : row.Content, row.CreatedAt, Citations(row.SourcesJson));

    // sources_json do ChatService ghi: [{ title, url, retrievedAt, entryNo, version }]. Dòng hỏng thì bỏ nguồn chứ không làm hỏng cả lịch sử
    private static IReadOnlyList<Citation> Citations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return [];
            var list = new List<Citation>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || Text(item, "title") is not { Length: > 0 } title)
                    continue;
                DateOnly? retrieved = Text(item, "retrievedAt") is { } d && DateOnly.TryParse(d, out var day) ? day : null;
                list.Add(new Citation(title, Text(item, "url"), null, retrieved, Int(item, "entryNo"), Int(item, "version")));
            }
            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? Text(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? Int(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
}
