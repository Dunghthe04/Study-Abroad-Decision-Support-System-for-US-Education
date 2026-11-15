namespace StudyAbroad.Domain.Constants;

/// <summary>Kết quả của một lượt chat (cột chat_messages.outcome, đặt ở tin trả lời của trợ lý). Admin lọc theo đây để xem câu nào chưa trả lời được.</summary>
public static class ChatOutcomes
{
    /// <summary>Hệ thống trả lời bằng dữ liệu hoặc kiến thức có nguồn.</summary>
    public const string Answered = "answered";
    /// <summary>Hệ thống không hiểu hoặc không có dữ liệu để trả lời (SLM dùng kết quả dự phòng, ý ngoài phạm vi hoặc mơ hồ, kho kiến thức không có nguồn).</summary>
    public const string NoAnswer = "no_answer";
    /// <summary>Có bước AI hoặc dịch vụ bị lỗi trong lượt (hết thời gian, bị từ chối, trả sai khuôn).</summary>
    public const string Failed = "failed";
}
