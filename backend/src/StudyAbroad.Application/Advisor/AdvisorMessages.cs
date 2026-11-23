namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Các câu cố định báo "chưa làm được" khi chat không dựng được lời trả lời: lỗi kết nối, kết quả hỏng, không có kết quả.
/// Đây là lời dự phòng cuối cùng nên do code viết, không gọi Claude (xem shared/answer_policy.json, cột fallback).
/// Gom ở một chỗ để sửa giọng văn một lần và biết hết các câu báo lỗi học sinh có thể nhận.
/// Lời dự phòng dựng động từ dữ liệu (ChatRecommendation.Summarize, ChatGoalSeek.Summarize, ChatSimulation.Fallback) nằm cạnh
/// code tạo dữ kiện vì cần cùng dữ liệu đó.
/// </summary>
public static class AdvisorMessages
{
    // ---- Gợi ý trường
    public const string RecommendationBusy =
        "Phần gợi ý trường đang bận, bạn thử lại sau ít phút hoặc mở trang Gợi ý trường nhé.";
    public const string RecommendationInvalid = "Mình chưa tạo được danh sách trường hợp lệ, bạn thử lại sau ít phút nhé.";
    public const string RecommendationEmpty =
        "Mình chưa tìm được trường nào khớp với hồ sơ hiện tại. Bạn mở trang Gợi ý trường để xem lý do và điều chỉnh tiêu chí nhé.";

    // ---- Giải thích một trường
    public const string ExplanationBusy = "Phần tư vấn trường đang bận, bạn thử lại sau ít phút nhé.";
    public const string ExplanationFailed = "Mình chưa viết được lời tư vấn cho trường này, bạn thử lại sau ít phút nhé.";

    // ---- Goal-seeking (cần gì để vào trường X)
    public const string GoalSeekFailed = "Mình chưa tính được phần này, bạn thử lại sau ít phút nhé.";

    // ---- What-if
    public const string SimulationFailed = "Mình chưa thử được kịch bản này, bạn thử lại sau ít phút nhé.";

    // ---- Tra cứu số liệu một trường
    public const string SchoolInfoUnavailable =
        "Mình chưa tra được số liệu của trường này lúc này, bạn thử lại sau ít phút nhé.";
}
