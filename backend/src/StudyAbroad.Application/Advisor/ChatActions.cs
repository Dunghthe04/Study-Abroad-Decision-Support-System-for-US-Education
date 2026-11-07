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
    private static readonly string[] Targets = ["match", "safety"];   // nhóm đích hợp lệ; Khó vào là mức thấp nhất nên không là đích

    /// <summary>
    /// Lượt trả lời dựng sẵn cho hành động. Trả null nếu không có hành động hoặc hành động không hợp lệ
    /// (khi đó chat chạy như thường qua advisor). state là trạng thái hội thoại hiện tại, giữ nguyên.
    /// </summary>
    public static AdvisorChatResponse? ToResponse(ChatAction? action, JsonElement? state)
    {
        // "Cần gì để vào trường em muốn?" không kèm trường: hỏi lại bằng ô chọn trường, để học sinh tự chọn trường mình quan tâm
        if (action is { Type: ChatGoalSeek.Action } && string.IsNullOrWhiteSpace(action.SchoolCode) && action.Target is null)
            return new AdvisorChatResponse(ChatGoalSeek.AskSchool, [], null, state, Widget: ChatGoalSeek.SchoolPicker(null));
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
            default:
                return null;
        }
        return new AdvisorChatResponse(
            "", [], null, state, Action: name, ActionData: JsonSerializer.SerializeToElement(data));
    }
}
