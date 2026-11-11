using System.Text.Json;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Một ý do model SLM đọc ra (advisor/app/schemas/chat.py: SlmSummaryIntent): loại, đoạn chữ học sinh viết (nguyên văn) và thực thể.
/// Có chữ của học sinh nên chỉ lưu vào chat_messages.meta_json (bảng dữ liệu cá nhân), không trả cho trình duyệt.
/// </summary>
public record AdvisorSlmIntentDto(string Type, string Span = "", JsonElement? Entities = null);

/// <summary>
/// SLM đã hiểu tin nhắn ra sao ở lượt này (advisor/app/schemas/chat.py: SlmSummary).
/// ModelIntents: đầu ra nguyên văn của model, trước các luật code đổi ý (rỗng khi bấm nút hoặc model hỏng).
/// Intents: loại các ý hệ thống thực sự đã chạy, sau khi luật code hậu xử lý.
/// OutOfScope: loại con của ý ngoài phạm vi (exception | sensitive | farewell | ambiguous | about_system | human_handoff).
/// Confidence: 0 đến 1, null khi không gọi SLM. Fallback: SLM hai lần đều sai, phải dùng kết quả dự phòng.
/// </summary>
public record AdvisorSlmDto(
    IReadOnlyList<AdvisorSlmIntentDto>? ModelIntents = null,
    IReadOnlyList<string>? Intents = null,
    string? OutOfScope = null,
    double? Confidence = null,
    bool Fallback = false);
