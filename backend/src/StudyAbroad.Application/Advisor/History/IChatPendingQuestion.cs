namespace StudyAbroad.Application.Advisor.History;

/// <summary>
/// Dựng lại nút và công cụ của câu hồ sơ đang chờ khi mở lại một phiên chat cũ (lịch sử chat). ChatService làm việc này vì cần đúng
/// cách dựng thông tin gửi advisor như một lượt chat; tách thành interface để controller không phụ thuộc cả ChatService.
/// </summary>
public interface IChatPendingQuestion
{
    /// <summary>
    /// Câu hồ sơ đang chờ của phiên sessionId thuộc userId. Null nếu không có phiên đó hoặc phiên của người khác (bên gọi trả 404).
    /// exactBudget: phụ huynh đang nhập hộ con (đọc từ token), để câu ngân sách hỏi số tiền chính xác như trong chat.
    /// Chỉ đọc: không đổi state của phiên, không lưu tin, không gọi AI, không tính lượt.
    /// </summary>
    Task<PendingQuestionDto?> PendingQuestionAsync(Guid userId, Guid sessionId, bool exactBudget, CancellationToken ct = default);
}
