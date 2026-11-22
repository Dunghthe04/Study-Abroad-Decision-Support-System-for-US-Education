using System.Text.Json;
using StudyAbroad.Application.Profile.Academic;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Application.Advisor;

public record ChatMessage(string Role, string Content);

/// <summary>
/// Một tin khi advisor trả lời theo từng ý (advisor: POST /api/v1/chat/stream).
/// Type: "progress" (Answers = số câu sắp trả lời), "partial" (Text = câu trả lời của một ý, đúng thứ tự hỏi)
/// hoặc "card" (Widget = thẻ kết quả do code dựng, gửi trước khi Gemini viết xong lời để học sinh thấy đáp án ngay).
/// </summary>
public record AdvisorStreamEvent(string Type, int? Answers = null, string? Text = null, Widget? Widget = null);

/// <param name="StudyLevel">Level the user asks about (see Domain.Constants.StudyLevels); null = all levels.</param>
/// <param name="Profile">Hồ sơ hiện tại (khóa chat → {value, status}) do .NET gửi mỗi lượt; null = không gửi.</param>
/// <param name="Schools">Trường của lần gợi ý gần nhất, chỉ gửi khi đã có kết quả; skill_explain dùng để biết học sinh nói trường nào.</param>
/// <param name="Transcript">Học bạ theo năm (ChatTranscript), gửi riêng ngoài Profile vì chat chỉ đọc; null = nhập GPA tổng.</param>
/// <param name="Benchmarks">Mốc GPA / tiếng Anh từ dữ liệu trường (ChatBenchmarks) cho skill ho_so; null = chưa có dữ liệu.</param>
/// <param name="HasCommittedProfile">Học sinh đã từng lưu hồ sơ chính thức (ProfileFlowState.HasCommittedProfile): advisor chỉ hỏi
/// mục bắt buộc còn thiếu; false thì hỏi đủ như form; null = advisor tự đoán theo các ô hồ sơ như cũ.</param>
/// <param name="FromForm">Tin do form gửi ngay sau khi lưu hồ sơ chính thức (frontend đặt, .NET chuyển nguyên sang advisor):
/// form đã là bước rà soát nên advisor bỏ bảng xác nhận X1 đang treo và trả lời luôn câu học sinh hỏi.</param>
/// <param name="ExactBudget">UC-94: phụ huynh đang nhập hộ con managed (controller đặt theo token, không lấy từ frontend):
/// câu ngân sách F1 hỏi số tiền tối đa/năm chính xác bằng VNĐ như bước 4 của form; null/false = hỏi chọn mức như cũ.</param>
/// <param name="AllowedIntents">Gói của tài khoản cho chạy những loại ý nào trong chat (ChatPremiumGate); null = tất cả (Premium còn token, nhân viên).
/// Cùng record này được nhận từ trình duyệt nên ChatService LUÔN ghi đè bằng giá trị của server, không bao giờ chuyển giá trị trình duyệt gửi.</param>
public record AdvisorChatRequest(
    IReadOnlyList<ChatMessage> Messages,
    string? SessionId = null,
    string? StudyLevel = null,
    JsonElement? State = null,
    JsonElement? Profile = null,
    IReadOnlyList<ChatSchool>? Schools = null,
    JsonElement? Transcript = null,
    JsonElement? Benchmarks = null,
    ChatAction? Action = null,
    IReadOnlyList<ChatSchool>? OtherSchools = null,
    SimulationOverrides? Simulation = null,
    IReadOnlyList<GradeScaleRuleDto>? GradeScale = null,
    bool? HasCommittedProfile = null,
    bool FromForm = false,
    bool? ExactBudget = null,
    IReadOnlyList<string>? AllowedIntents = null);

/// <summary>
/// Hành động có cấu trúc từ một nút trên giao diện (không qua SLM hiểu câu): Type là goal_seek hoặc explain,
/// SchoolCode là mã trường, Target là nhóm đích (match | safety) chỉ dùng cho goal_seek. Basis = "real" thì giải thích theo hồ sơ thật
/// dù trang đang xem giả định (nút "Xem theo hồ sơ thật"); bỏ trống thì theo cái đang xem. Simulation (ở yêu cầu) là giá trị giả định đang kéo trên trang.
/// </summary>
public record ChatAction(
    string Type, string? SchoolCode = null, string? Target = null, string? Basis = null, string? Question = null,
    IReadOnlyList<string>? SchoolCodes = null, IReadOnlyList<string>? Fields = null,
    // Chỉ cho ô chọn trường: các trường nên hiện sẵn dạng nút bấm (vd các trường đầu danh sách gợi ý)
    IReadOnlyList<string>? Suggested = null,
    // Chỉ cho school_search (nút lọc lại sau tìm trường): dữ liệu tìm dựng sẵn {question, filters, sort, model_sort},
    // cùng dạng advisor gửi, để tìm lại thẳng không qua SLM
    JsonElement? Search = null);

/// <summary>Một trường trong danh sách gợi ý. Group là mã nhóm đã chấm (R, RF, M, S, NA, O), advisor đổi sang chữ.</summary>
public record ChatSchool(string Code, string Name, string? Group);
public record Citation(string Title, string? Url, string? Snippet, DateOnly? RetrievedAt = null);

/// <summary>
/// Nút gợi ý: Label là chữ trên nút, Value là chữ gửi đi như tin nhắn của học sinh khi bấm.
/// Có Action thì nút chạy thẳng hành động có cấu trúc (giải thích, goal-seeking) bằng mã trường, không qua SLM hiểu câu nên không nhầm tên trường.
/// Có Href thì nút chuyển sang trang đó (ví dụ "Sửa hồ sơ"), không gửi tin nhắn.
/// </summary>
public record QuickReply(string Label, string Value, ChatAction? Action = null, string? Href = null);

/// <summary>Công cụ nhập ngay trong chat. Type: major | review | budget | scholarship; Data tùy loại.</summary>
public record Widget(string Type, JsonElement? Data = null);

/// <summary>
/// Gửi advisor (POST api/v1/chat/pending-question) để dựng lại nút và công cụ của câu hồ sơ đang chờ khi mở lại phiên chat cũ.
/// State là trạng thái đã lưu của phiên; các trường còn lại là thông tin .NET đọc mới từ server, giống hệt một lượt chat
/// (ChatService.BuildAdvisorRequestAsync), để bảng rà soát hiện đúng hồ sơ hiện tại.
/// </summary>
public record AdvisorPendingQuestionRequest(
    JsonElement State,
    JsonElement? Profile = null,
    IReadOnlyList<ChatSchool>? Schools = null,
    IReadOnlyList<ChatSchool>? OtherSchools = null,
    JsonElement? Transcript = null,
    JsonElement? Benchmarks = null,
    IReadOnlyList<GradeScaleRuleDto>? GradeScale = null,
    bool? HasCommittedProfile = null,
    bool? ExactBudget = null);

/// <summary>
/// Câu hồ sơ đang chờ học sinh trả lời trong một phiên cũ, kèm nút và công cụ dựng lại (tin nhắn lưu chỉ có chữ).
/// Pending = false: không có câu hồ sơ nào đang chờ (phiên hỏi đáp thường, hoặc câu đang chờ chỉ là câu gợi ý).
/// </summary>
public record PendingQuestionDto(bool Pending, string? QuestionId, string Question, IReadOnlyList<QuickReply> QuickReplies, Widget? Widget)
{
    public static PendingQuestionDto None { get; } = new(false, null, "", [], null);
}

/// <param name="Profile">Hồ sơ sau lượt này (nội bộ, .NET dùng để ghi vào bản nháp).</param>
/// <param name="ProfileChanged">Khóa hồ sơ đổi trong lượt này.</param>
/// <param name="FormStep">Trang form (1–6) của câu chat đang hỏi; null = câu không thuộc trang nào.</param>
/// <param name="FormGoals">Việc học sinh đã hỏi khi đang điền hồ sơ trong chat (danh_gia, lo_trinh, goi_y_truong); form tích sẵn ô nhiệm vụ. Rỗng hoặc null = không đổi.</param>
public record AdvisorChatResponse(
    string Answer,
    IReadOnlyList<Citation> Citations,
    string? Disclaimer,
    JsonElement? State = null,
    string? SessionId = null,
    IReadOnlyList<QuickReply>? QuickReplies = null,
    Widget? Widget = null,
    JsonElement? Profile = null,
    IReadOnlyList<string>? ProfileChanged = null,
    int? FormStep = null,
    IReadOnlyList<string>? FormGoals = null,
    IReadOnlyList<string>? Skills = null,
    // Việc .NET cần làm sau lượt này: "recommend" = chạy gợi ý trường (ChatRecommendation),
    // "explain" = viết lời tư vấn một trường (ChatExplanation, mã trường nằm ở ActionData.code). Không trả cho trình duyệt
    string? Action = null,
    JsonElement? ActionData = null,
    // Số đo từng lần gọi AI của lượt này, để ghi nhật ký ai_calls. Nội bộ: không trả cho trình duyệt
    IReadOnlyList<AiUsageDto>? AiUsage = null,
    // SLM đã hiểu tin nhắn ra sao (nhãn model gán, độ tin cậy, có dùng kết quả dự phòng không), để đánh dấu câu chưa trả lời được.
    // Có chữ của học sinh nên nội bộ: không trả cho trình duyệt
    AdvisorSlmDto? Slm = null,
    // Lượt này vừa lưu hồ sơ chính thức (bấm "Đúng rồi" ở bảng rà soát): trang Gợi ý trường hiện nút cập nhật danh sách.
    // Chỉ .NET đặt, advisor không gửi
    bool? ProfileSaved = null,
    // Loại ý advisor bỏ vì gói không cho (allowedIntents). Nội bộ: ChatService đổi thành Locked rồi bỏ trước khi trả trình duyệt
    IReadOnlyList<string>? LockedIntents = null,
    // Lượt bị khóa vì gói (cần Premium hoặc hết hạn mức Free): giao diện hiện thẻ nâng cấp. Chỉ .NET đặt, trả cho trình duyệt
    ChatLock? Locked = null,
    // Câu trả lời này nói "chưa có thông tin": kho kiến thức không có dòng phù hợp và cơ sở dữ liệu trường cũng không có số liệu học sinh hỏi.
    // Chỉ .NET đặt (ở bước hỏi thông tin trường), advisor không gửi; dùng để đánh dấu lượt là no_answer, không trả cho trình duyệt
    bool? NoBasis = null);
