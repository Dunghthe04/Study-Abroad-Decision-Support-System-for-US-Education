using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.Profile.Flow;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Chức năng 1f, 1.5d: tài khoản phụ huynh không có hồ sơ riêng, kể cả khi chưa liên kết (hồ sơ của con xem / nhập thay ở "Con của tôi").
/// Lượt chat mà SLM hiểu là việc của hồ sơ (nhập hồ sơ, rà soát, kịch bản, gợi ý trường) thì thay bằng câu báo kèm nút.
/// 1.5e: có danh sách con thì hỏi "Bạn muốn làm cho con nào?": mỗi con managed một nút vào làm thay rồi chat tiếp,
/// thêm nút "Thêm con"; con đã liên kết thật thì nút "Xem ở Con của tôi" (con tự nhập hồ sơ của mình).
/// Câu hỏi kiến thức, thông tin / tìm / so sánh trường, trung tâm, chào hỏi... vẫn trả lời bình thường.
/// Đọc nhãn SLM có sẵn, không huấn luyện lại. ChatService gọi trước khi lưu nháp, nên lượt bị thay không ghi gì vào hồ sơ.
/// Đang làm thay thì userId là id con (Role student) nên không qua đây.
/// </summary>
public static class ParentChatGate
{
    public const string ChildrenHref = "/parent/children";

    // Trang mở sẵn hộp thêm con; thêm xong vào làm thay rồi mở chat (frontend app/parent/children/add)
    public const string AddChildHref = "/parent/children/add";

    // Trang vào làm thay con rồi mở chat (frontend app/parent/act/[childId])
    public static string ActHref(System.Guid childId) => $"/parent/act/{childId}";

    public const string Answer =
        "Tài khoản phụ huynh không có hồ sơ riêng. Bạn xem hồ sơ, trường gợi ý và trường mục tiêu của con ở mục Con của tôi nhé.";

    public const string NoChildAnswer =
        "Tài khoản phụ huynh không có hồ sơ riêng. Bạn muốn làm cho con nào? Bấm Thêm con để nhập tên con, rồi mình hỏi hồ sơ của con nhé.";

    public const string ChooseChildAnswer =
        "Tài khoản phụ huynh không có hồ sơ riêng. Bạn muốn làm cho con nào? Chọn con để nhập hồ sơ thay con, hoặc thêm con mới.";

    public static QuickReply ViewChildren { get; } = new("Xem ở Con của tôi", "Xem ở Con của tôi", Href: ChildrenHref);

    public static QuickReply AddChild { get; } = new("Thêm con", "Thêm con", Href: AddChildHref);

    // Nhãn SLM của việc cần hồ sơ riêng (advisor/app: collect_profile, profile_review, scenario, recommend)
    private static readonly HashSet<string> ProfileIntents = ["collect_profile", "profile_review", "scenario", "recommend"];

    // Hành động .NET chạy trên hồ sơ riêng; bấm nút thì không qua SLM nên phải xét cả hành động.
    // Gồm cả so sánh theo độ phù hợp (UC-154), giải thích và goal-seeking: cả ba tính trên hồ sơ của người đang chat,
    // phụ huynh không có hồ sơ nên phải chọn con (làm thay) trước. So sánh số liệu trường (school_compare) không cần hồ sơ nên không nằm đây.
    private static readonly HashSet<string> ProfileActions =
    [
        ChatRecommendation.Action, ChatSimulation.Action, ChatFitCompare.Action, ChatExplanation.Action, ChatGoalSeek.Action,
    ];

    /// <summary>Lượt này có đụng tới hồ sơ riêng không.</summary>
    public static bool TouchesOwnProfile(AdvisorChatResponse response) =>
        (response.Action is { } action && ProfileActions.Contains(action))
        || (response.Slm?.Intents?.Any(ProfileIntents.Contains) ?? false);

    /// <summary>Lượt đụng tới hồ sơ thì trả câu thay thế, giữ trạng thái chat của lượt trước để chat không kẹt ở câu hỏi hồ sơ;
    /// không thì trả nguyên response. children null (không đọc được danh sách con) thì chỉ có nút sang Con của tôi như 1.5d.</summary>
    public static AdvisorChatResponse Apply(
        AdvisorChatResponse response, JsonElement? previousState, IReadOnlyList<ParentChatChild>? children = null)
    {
        if (!TouchesOwnProfile(response))
            return response;

        var (answer, replies) = BuildReply(children);
        return response with
        {
            Answer = answer,
            Citations = [],
            Disclaimer = null,
            State = previousState,
            QuickReplies = replies,
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

    // Bảng chat trong tài liệu UC76: chưa có con → Thêm con; có con managed → từng con + Thêm con;
    // chỉ có con liên kết thật → Xem ở Con của tôi. Có cả hai thì đủ các nút.
    private static (string Answer, List<QuickReply> Replies) BuildReply(IReadOnlyList<ParentChatChild>? children)
    {
        if (children is null)
            return (Answer, [ViewChildren]);

        var managed = children.Where(c => c.IsManaged).ToList();
        var hasLinked = children.Any(c => !c.IsManaged);
        if (managed.Count == 0 && hasLinked)
            return (Answer, [ViewChildren]);

        var replies = managed.Select(c => new QuickReply(c.Name, c.Name, Href: ActHref(c.Id))).ToList();
        if (managed.Count < ManagedChildService.MaxManagedChildren)
            replies.Add(AddChild);
        if (hasLinked)
            replies.Add(ViewChildren);
        return (managed.Count == 0 ? NoChildAnswer : ChooseChildAnswer, replies);
    }
}
