namespace StudyAbroad.Domain.Constants;

/// <summary>Kết quả của một lượt chat (cột chat_messages.outcome, đặt ở tin trả lời của trợ lý). Admin lọc theo đây để xem câu nào chưa trả lời được.</summary>
public static class ChatOutcomes
{
    /// <summary>Hệ thống trả lời bằng dữ liệu hoặc kiến thức có nguồn.</summary>
    public const string Answered = "answered";
    /// <summary>Hệ thống MUỐN trả lời nhưng không thể: không hiểu câu hỏi (mơ hồ) hoặc không có dữ liệu. Đây là khoảng trống cần admin bổ sung.</summary>
    public const string NoAnswer = "no_answer";
    /// <summary>Hệ thống hiểu câu hỏi và CỐ Ý không trả lời: ngoài chuyện du học hoặc chủ đề nhạy cảm. Đúng thiết kế, không phải khoảng trống nên không vào hàng đợi.</summary>
    public const string Refused = "refused";
    /// <summary>Có bước AI hoặc dịch vụ bị lỗi trong lượt (hết thời gian, bị từ chối, trả sai khuôn).</summary>
    public const string Failed = "failed";
}
