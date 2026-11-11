using System.Text.Json;
using System.Text.Json.Nodes;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Hành động có cấu trúc từ nút trên giao diện ("Cần gì để vào?", "Xem tư vấn" trên thẻ trường).
/// Không qua SLM hiểu câu: ChatService dựng sẵn lượt trả lời như thể advisor đã hiểu, rồi chạy đúng các bước goal-seeking
/// và giải thích có sẵn. Nhanh hơn và không nhầm tên trường vì đã có mã trường.
/// </summary>
public static class ChatActions
{
    public const int MaxCodeLength = 32;
    public const int MaxQuestionLength = 300;
    // Số trường tối đa trong ô chọn đã thu hẹp (các trường vừa đổi nhóm)
    public const int MaxPickerCodes = 12;
    public const int MaxSuggested = 6;
    // So sánh nhận mã trường hoặc tên học sinh gõ (khi một trong các trường chưa được chọn), nên cho dài hơn mã
    public const int MaxCompareNameLength = 64;
    private static readonly string[] Targets = ["match", "safety"];   // nhóm đích hợp lệ; Khó vào là mức thấp nhất nên không là đích

    /// <summary>
    /// Lượt trả lời dựng sẵn cho hành động. Trả null nếu không có hành động hoặc hành động không hợp lệ
    /// (khi đó chat chạy như thường qua advisor). state là trạng thái hội thoại hiện tại, giữ nguyên.
    /// </summary>
    // Mã trường từ trình duyệt: bỏ rỗng, quá dài, trùng; không còn gì thì null
    private static List<string>? Clean(IReadOnlyList<string>? codes, int max)
    {
        var clean = (codes ?? []).Where(c => !string.IsNullOrWhiteSpace(c) && c.Length <= MaxCodeLength)
            .Select(c => c.Trim()).Distinct().Take(max).ToList();
        return clean.Count == 0 ? null : clean;
    }

    public static AdvisorChatResponse? ToResponse(ChatAction? action, JsonElement? state)
    {
        // "Cần gì để vào trường em muốn?" không kèm trường: hỏi lại bằng ô chọn trường, để học sinh tự chọn trường mình quan tâm
        if (action is { Type: ChatGoalSeek.Action } && string.IsNullOrWhiteSpace(action.SchoolCode) && action.Target is null)
            return new AdvisorChatResponse(
                ChatGoalSeek.AskSchool, [], null, state, Widget: ChatGoalSeek.SchoolPicker(null, suggested: Clean(action.Suggested, MaxSuggested)));
        // "Giải thích một trường" không kèm trường: cũng mở ô chọn trường, chọn xong thì giải thích trường đó
        // SchoolCodes (nếu có) thu hẹp ô chọn vào các trường đó, ví dụ các trường vừa đổi nhóm sau what-if
        if (action is { Type: ChatExplanation.Action } && string.IsNullOrWhiteSpace(action.SchoolCode))
        {
            var only = Clean(action.SchoolCodes, MaxPickerCodes);
            return new AdvisorChatResponse(
                ChatGoalSeek.AskExplainSchool, [], null, state,
                Widget: ChatGoalSeek.SchoolPicker(null, ChatExplanation.Action, only, Clean(action.Suggested, MaxSuggested)));
        }
        // So sánh các trường (nút "So sánh hai trường đầu" sau danh sách tìm trường): mã các trường, 2 đến MaxCompare
        if (action is { Type: ChatSchoolInfo.CompareAction })
        {
            var codes = (action.SchoolCodes ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().ToList();
            var fieldCodes = (action.Fields ?? []).Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()).ToList();
            if (codes.Count < 2 || codes.Count > ChatSchoolInfo.MaxCompare || codes.Any(c => c.Length > MaxCompareNameLength)
                || fieldCodes.Count > 12 || fieldCodes.Any(f => f.Length > MaxCodeLength)
                || action.Question is { Length: > MaxQuestionLength })
                return null;
            var compare = new JsonObject
            {
                ["question"] = action.Question?.Trim() ?? "",
                // mã trường đi vào chỗ tên: bộ khớp tên nhận trùng mã tuyệt đối
                ["schools"] = new JsonArray(codes.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray()),
                ["fields"] = new JsonArray(fieldCodes.Select(f => (JsonNode?)JsonValue.Create(f)).ToArray()),
            };
            return new AdvisorChatResponse(
                "", [], null, state, Action: ChatSchoolInfo.CompareAction, ActionData: JsonSerializer.SerializeToElement(compare));
        }
        if (action is null || string.IsNullOrWhiteSpace(action.SchoolCode) || action.SchoolCode.Length > MaxCodeLength)
            return null;
        var code = action.SchoolCode.Trim();
        var data = new JsonObject { ["code"] = code };

        string name;
        switch (action.Type)
        {
            case ChatGoalSeek.Action:
                var target = action.Target?.Trim().ToLowerInvariant();
                if (target is not null && !Targets.Contains(target))
                    return null;
                data["target"] = target;
                name = ChatGoalSeek.Action;
                break;
            case ChatExplanation.Action:
                name = ChatExplanation.Action;
                break;
            case ChatSchoolInfo.Action:
                // Câu hỏi gốc đi kèm để biết hỏi mục nào (học phí, học bổng...); không có thì tóm tắt về trường
                if (action.Question is { Length: > MaxQuestionLength })
                    return null;
                data["question"] = action.Question?.Trim();
                name = ChatSchoolInfo.Action;
                break;
            default:
                return null;
        }
        return new AdvisorChatResponse(
            "", [], null, state, Action: name, ActionData: JsonSerializer.SerializeToElement(data));
    }
}
