namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Lượt xác nhận hồ sơ chạy hai bước. Lộ trình gửi advisor (đạt / chưa đạt mốc trường mục tiêu, các mốc thời gian) tính theo hồ sơ
/// ĐÃ LƯU, mà hồ sơ mới chỉ được lưu sau khi advisor trả lời. Nên advisor chỉ báo đã xác nhận (ResumeNeeded), .NET lưu hồ sơ rồi
/// gọi lại với lộ trình mới để trả lời câu đang chờ, ghép hai phần thành một tin.
/// </summary>
public static class ChatResume
{
    // Chữ advisor nhận để trả lời lại câu đang chờ. Phải khớp RESUME_VALUE bên advisor (app/services/replies.py)
    public const string RetryValue = "Trả lời giúp câu em vừa hỏi";

    public static QuickReply RetryButton { get; } = new("Trả lời câu vừa hỏi", RetryValue);

    public const string FailedNote =
        "Mình chưa trả lời được câu bạn hỏi lúc nãy. Hồ sơ đã lưu rồi, bạn bấm nút bên dưới để mình trả lời lại nhé.";

    public static string Join(string first, string second) =>
        string.Join("\n\n", new[] { first, second }.Where(text => !string.IsNullOrEmpty(text)));

    // Ghép lời báo đã lưu (lần đầu) với câu trả lời (lần hai) thành một lượt: việc cần làm tiếp, state, nút lấy theo lần hai;
    // phần hồ sơ vừa đổi và nhãn SLM chỉ lần đầu mới có
    public static AdvisorChatResponse Merge(AdvisorChatResponse saved, AdvisorChatResponse next) => next with
    {
        Answer = Join(saved.Answer, next.Answer),
        Citations = [.. saved.Citations ?? [], .. next.Citations ?? []],
        Disclaimer = next.Disclaimer ?? saved.Disclaimer,
        State = next.State ?? saved.State,
        QuickReplies = next.QuickReplies is { Count: > 0 } ? next.QuickReplies : saved.QuickReplies,
        Widget = next.Widget ?? saved.Widget,
        Profile = saved.Profile,
        ProfileChanged = saved.ProfileChanged,
        FormGoals = next.FormGoals ?? saved.FormGoals,
        Skills = [.. saved.Skills ?? [], .. next.Skills ?? []],
        Slm = saved.Slm,
        AiUsage = [.. saved.AiUsage ?? [], .. next.AiUsage ?? []],
        ResumeNeeded = null,
    };
}
