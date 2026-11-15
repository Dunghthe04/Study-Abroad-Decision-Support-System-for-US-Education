namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Lưu tin nhắn của cuộc chat vào bảng chat_messages để admin xem câu nào hệ thống không trả lời được và cải thiện dữ liệu.
/// Nội dung chat là dữ liệu cá nhân: chỉ admin và moderator được đọc. Bật hoặc tắt bằng cấu hình Chat:StoreMessages (không đăng ký thì ChatService không lưu).
/// </summary>
public interface IChatMessageRepository
{
    /// <summary>Lưu các tin của một lượt (tin học sinh rồi tin trợ lý). Phiên chat phải đã có trong database. Lỗi thì ném, bên gọi quyết định.</summary>
    Task AddRangeAsync(IReadOnlyList<StudyAbroad.Domain.Entities.ChatMessage> messages, CancellationToken ct = default);
}
