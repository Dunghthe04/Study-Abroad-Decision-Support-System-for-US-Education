namespace StudyAbroad.Application.Advisor.History;

/// <summary>
/// Một phiên trong danh sách lịch sử. Chỉ là tóm tắt, không có nội dung tin nhắn.
/// LastActivityAt: lần trao đổi gần nhất (dùng để xếp, mới nhất trước). MessageCount: số tin đã lưu của phiên.
/// </summary>
public record ChatSessionSummaryDto(Guid Id, string Title, DateTime CreatedAt, DateTime LastActivityAt, int MessageCount);

/// <summary>Một trang danh sách phiên. NextCursor: đưa vào lần gọi sau để lấy các phiên cũ hơn; null là đã hết.</summary>
public record ChatSessionPageDto(IReadOnlyList<ChatSessionSummaryDto> Items, string? NextCursor);

/// <summary>
/// Một tin trong lịch sử của phiên: chỉ có chữ (nút, công cụ, thẻ số liệu không được lưu nên không hiện lại).
/// Role: user (học sinh), assistant (trợ lý), system (lượt hệ thống không phản hồi). Citations: nguồn trích dẫn nếu có (kèm entryNo và version - UC-36).
/// </summary>
public record ChatHistoryMessageDto(Guid Id, string Role, string Content, DateTime CreatedAt, IReadOnlyList<Citation> Citations);

/// <summary>
/// Các tin của một phiên, cũ trước mới sau. Mặc định là các tin MỚI NHẤT của phiên; OlderCursor đưa vào lần gọi sau để lấy
/// các tin cũ hơn nữa (null là đã tới tin đầu tiên).
/// </summary>
public record ChatHistoryMessagesDto(Guid SessionId, string Title, IReadOnlyList<ChatHistoryMessageDto> Items, string? OlderCursor);

/// <summary>Đổi tên phiên: Title là tên mới (gom khoảng trắng, tối đa ChatSessionTitle.MaxUserLength ký tự).</summary>
public record RenameChatSessionRequest(string? Title);

/// <summary>Kết quả đổi tên: tên đã chuẩn hóa mà hệ thống lưu.</summary>
public record RenamedChatSessionDto(Guid Id, string Title);

// ---- dòng thô từ database

public record ChatSessionRow(Guid Id, string? Title, DateTime CreatedAt, DateTime LastActivityAt, int MessageCount);

public record ChatHistoryMessageRow(Guid Id, string Role, string Content, DateTime CreatedAt, string? SourcesJson);
