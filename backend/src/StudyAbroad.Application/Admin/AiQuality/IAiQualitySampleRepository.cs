namespace StudyAbroad.Application.Admin.AiQuality;

/// <summary>
/// Đọc các lượt chat cho danh sách mẫu hội thoại (UC-90), làm HAI bước để nội dung của lượt bị ẩn không bao giờ được tải lên:
/// bước một chỉ lấy thông tin để quyết định (không có chữ nào của học sinh), bước hai mới lấy nội dung của đúng các lượt được xem.
/// </summary>
public interface IAiQualitySampleRepository
{
    /// <summary>
    /// Các lượt có kết quả outcome trong [fromUtc, toUtcExclusive), mới nhất trước, CHƯA có nội dung.
    /// Kèm ngày sinh của chủ phiên và phiên bản chính sách phụ huynh đã đồng ý, để cổng quyết định.
    /// </summary>
    Task<IReadOnlyList<AiQualityCandidate>> CandidatesAsync(
        DateTime fromUtc, DateTime toUtcExclusive, string outcome, CancellationToken ct = default);

    /// <summary>
    /// Nội dung THÔ của các lượt được hỏi (tin trả lời, tin hỏi, tối đa contextMessages tin ngay trước câu hỏi, họ tên chủ phiên).
    /// Lượt không còn tồn tại thì không có trong kết quả. Thứ tự kết quả không quan trọng.
    /// </summary>
    Task<IReadOnlyList<AiQualitySampleRow>> RowsAsync(
        IReadOnlyCollection<Guid> messageIds, int contextMessages, CancellationToken ct = default);

    /// <summary>
    /// Thông tin cho dòng giữ chỗ của các lượt bị ẩn: chỉ thời điểm, loại kết quả, meta của tin trả lời và cờ SLM dự phòng.
    /// KHÔNG đọc cột nội dung nào và không tải meta của tin hỏi (có đoạn chữ học sinh viết). Thứ tự kết quả không quan trọng.
    /// </summary>
    Task<IReadOnlyList<AiQualityHiddenRow>> HiddenRowsAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken ct = default);
}
