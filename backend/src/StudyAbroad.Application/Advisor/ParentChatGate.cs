using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Chức năng 1f: phụ huynh đã liên kết không có hồ sơ riêng (hồ sơ do con nhập, phụ huynh xem ở "Con của tôi").
/// Lượt chat mà SLM hiểu là việc của hồ sơ (nhập hồ sơ, rà soát, kịch bản, gợi ý trường) thì thay bằng câu báo kèm nút sang "Con của tôi".
/// Câu hỏi kiến thức, thông tin / tìm / so sánh trường, trung tâm, chào hỏi... vẫn trả lời bình thường.
/// Đọc nhãn SLM có sẵn, không huấn luyện lại. ChatService gọi trước khi lưu nháp, nên lượt bị thay không ghi gì vào hồ sơ.
/// </summary>
public static class ParentChatGate
{
    public const string ChildrenHref = "/parent/children";

    public const string Answer =
        "Tài khoản phụ huynh không có hồ sơ riêng. Bạn xem hồ sơ, trường gợi ý và trường mục tiêu của con ở mục Con của tôi nhé.";

    public static QuickReply ViewChildren { get; } = new("Xem ở Con của tôi", "Xem ở Con của tôi", Href: ChildrenHref);

    // Nhãn SLM của việc cần hồ sơ riêng (advisor/app: collect_profile, profile_review, scenario, recommend)
    private static readonly HashSet<string> ProfileIntents = ["collect_profile", "profile_review", "scenario", "recommend"];

    // Hành động .NET chạy trên hồ sơ riêng; bấm nút thì không qua SLM nên phải xét cả hành động
    private static readonly HashSet<string> ProfileActions = [ChatRecommendation.Action, ChatSimulation.Action];

    /// <summary>Lượt này có đụng tới hồ sơ riêng không.</summary>
    public static bool TouchesOwnProfile(AdvisorChatResponse response) =>
        (response.Action is { } action && ProfileActions.Contains(action))
        || (response.Slm?.Intents?.Any(ProfileIntents.Contains) ?? false);

    /// <summary>Lượt đụng tới hồ sơ thì trả câu thay thế, giữ trạng thái chat của lượt trước để chat không kẹt ở câu hỏi hồ sơ;
    /// không thì trả nguyên response.</summary>
    public static AdvisorChatResponse Apply(AdvisorChatResponse response, JsonElement? previousState) =>
        !TouchesOwnProfile(response)
            ? response
            : response with
            {
                Answer = Answer,
                Citations = [],
                Disclaimer = null,
                State = previousState,
                QuickReplies = [ViewChildren],
                Widget = null,
                Profile = null,
                ProfileChanged = null,
                FormStep = null,
                FormGoals = null,
                Skills = null,
                Action = null,
                ActionData = null,
            };
}
