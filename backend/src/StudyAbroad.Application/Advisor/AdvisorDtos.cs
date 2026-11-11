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
    IReadOnlyList<GradeScaleRuleDto>? GradeScale = null);

/// <summary>
/// Hành động có cấu trúc từ một nút trên giao diện (không qua SLM hiểu câu): Type là goal_seek hoặc explain,
/// SchoolCode là mã trường, Target là nhóm đích (match | safety) chỉ dùng cho goal_seek. Basis = "real" thì giải thích theo hồ sơ thật
/// dù trang đang xem giả định (nút "Xem theo hồ sơ thật"); bỏ trống thì theo cái đang xem. Simulation (ở yêu cầu) là giá trị giả định đang kéo trên trang.
/// </summary>
public record ChatAction(
    string Type, string? SchoolCode = null, string? Target = null, string? Basis = null, string? Question = null,
    IReadOnlyList<string>? SchoolCodes = null, IReadOnlyList<string>? Fields = null,
    // Chỉ cho ô chọn trường: các trường nên hiện sẵn dạng nút bấm (vd các trường đầu danh sách gợi ý)
    IReadOnlyList<string>? Suggested = null);

/// <summary>Một trường trong danh sách gợi ý. Group là mã nhóm đã chấm (R, RF, M, S, NA, O), advisor đổi sang chữ.</summary>
public record ChatSchool(string Code, string Name, string? Group);
public record Citation(string Title, string? Url, string? Snippet);

/// <summary>
/// Nút gợi ý: Label là chữ trên nút, Value là chữ gửi đi như tin nhắn của học sinh khi bấm.
/// Có Action thì nút chạy thẳng hành động có cấu trúc (giải thích, goal-seeking) bằng mã trường, không qua SLM hiểu câu nên không nhầm tên trường.
/// Có Href thì nút chuyển sang trang đó (ví dụ "Sửa hồ sơ"), không gửi tin nhắn.
/// </summary>
public record QuickReply(string Label, string Value, ChatAction? Action = null, string? Href = null);

/// <summary>Công cụ nhập ngay trong chat. Type: major | review | budget | scholarship; Data tùy loại.</summary>
public record Widget(string Type, JsonElement? Data = null);

/// <param name="Profile">Hồ sơ sau lượt này (nội bộ, .NET dùng để ghi vào bản nháp).</param>
/// <param name="ProfileChanged">Khóa hồ sơ đổi trong lượt này.</param>
/// <param name="FormStep">Trang form (1–6) của câu chat đang hỏi; null = câu không thuộc trang nào.</param>
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
    IReadOnlyList<string>? Skills = null,
    // Việc .NET cần làm sau lượt này: "recommend" = chạy gợi ý trường (ChatRecommendation),
    // "explain" = viết lời tư vấn một trường (ChatExplanation, mã trường nằm ở ActionData.code). Không trả cho trình duyệt
    string? Action = null,
    JsonElement? ActionData = null,
    // Số đo từng lần gọi AI của lượt này, để ghi nhật ký ai_calls. Nội bộ: không trả cho trình duyệt
    IReadOnlyList<AiUsageDto>? AiUsage = null,
    // SLM đã hiểu tin nhắn ra sao (nhãn model gán, độ tin cậy, có dùng kết quả dự phòng không), để đánh dấu câu chưa trả lời được.
    // Có chữ của học sinh nên nội bộ: không trả cho trình duyệt
    AdvisorSlmDto? Slm = null);
