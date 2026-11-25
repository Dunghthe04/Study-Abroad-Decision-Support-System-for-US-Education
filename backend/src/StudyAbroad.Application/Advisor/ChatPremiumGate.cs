using System.Text.Json;
using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Advisor;

/// <summary>Thẻ khóa trên phản hồi chat (trình duyệt đọc <c>locked</c>): vì sao lượt này không chạy. ResetsAt chỉ có khi hết hạn mức Free (giờ Việt Nam).</summary>
public sealed record ChatLock(string Reason, DateTimeOffset? ResetsAt);

public static class ChatLockReasons
{
    public const string PremiumRequired = "premium_required";
    public const string PremiumTokensExhausted = "premium_tokens_exhausted";
    public const string QuotaExhausted = "quota_exhausted";
}

/// <summary>
/// Cổng Premium của chat, chạy sau cổng tuổi và ParentChatGate, trước khi gọi advisor (billing-plan-v2, "Chat gate").
/// Hàm thuần: ChatService gọi, không đọc DB. Chỉ áp cho tài khoản bị tính (charge.Ungated = false); nhân viên không bao giờ bị chặn.
/// </summary>
public static class ChatPremiumGate
{
    public const string PremiumRequiredAction = ChatLockReasons.PremiumRequired;
    public const string PremiumRequiredAnswer = "Tính năng này cần tài khoản USDSS Premium.";
    public const string PremiumTokensExhaustedAnswer = "Đã hết token, hãy mua thêm gói";
    public const string QuotaExhaustedAnswer = "Bạn đã dùng hết lượt hỏi miễn phí của tháng này.";
    public const string EmptyProfileReviewAnswer = "Bạn muốn mình hỗ trợ việc nào với hồ sơ của bạn?";
    public const string PlansHref = "/plans";
    private const string ProfileReviewIntent = "profile_review";

    public static QuickReply ViewPlans { get; } = new("Xem gói", "Xem gói", Href: PlansHref);

    /// <summary>[UC-127 / Mục 6] Ba nút chọn khi người dùng nói chung chung ("tư vấn giúp em" — profile_review không có task), mở cho mọi tài khoản.</summary>
    public static IReadOnlyList<QuickReply> EmptyProfileReviewChoices { get; } =
    [
        new("Nhận xét hồ sơ", "Đánh giá hồ sơ của em"),
        new("Danh sách trường", "Gợi ý trường cho em"),
        new("Lộ trình", "Lên lộ trình cho em"),
    ];

    /// <summary>Lượt do .NET tự trả lời (không gọi advisor): Reply là phản hồi dựng sẵn, Lock là thẻ khóa gửi trình duyệt.</summary>
    public sealed record LockedTurn(AdvisorChatResponse Reply, ChatLock Lock);

    /// <summary>Có chặn được lượt này không (tài khoản bị tính). charge null = chưa nối dịch vụ gói (test cũ), không chặn.</summary>
    public static bool Applies(ChatCharge? charge) => charge is { Ungated: false };

    /// <summary>Ý định gửi advisor. Luôn lấy từ server (ghi đè giá trị trình duyệt gửi); null = tất cả.</summary>
    public static IReadOnlyList<string>? AllowedIntentsFor(ChatCharge? charge) => Applies(charge) ? charge!.AllowedIntents : null;

    /// <summary>
    /// Lượt phải khóa ngay, trước mọi lần gọi advisor hay đọc dữ liệu gợi ý; null thì lượt đi tiếp.
    /// 1) Hạn mức Free tháng này đã hết: khóa mọi tin gõ và mọi nút (quota_exhausted, kèm ngày làm mới).
    /// 2) Nút Premium (goal_seek, explain) khi gói không cho hết ý định: khóa (premium_tokens_exhausted nếu đang Premium hết token, ngược lại premium_required).
    /// state là trạng thái hội thoại của DB, giữ nguyên trong phản hồi để lượt khóa không làm mất câu hỏi đang dở.
    /// </summary>
    public static LockedTurn? Check(ChatCharge? charge, ChatAction? action, JsonElement? state)
    {
        if (charge is not { Ungated: false })
            return null;

        // Premium hết token rồi lại hết cả hạn mức Free dự phòng: vẫn là "hết token" (UC-127 1a), không phải thông báo của Free
        if (charge.PoolKind == PlanKinds.Free && charge.FreeExhausted && charge.IsPremium)
            return Build(PremiumTokensExhaustedAnswer, ChatLockReasons.PremiumTokensExhausted, null, state);

        if (charge.PoolKind == PlanKinds.Free && charge.FreeExhausted)
            return Build(QuotaExhaustedAnswer, ChatLockReasons.QuotaExhausted, CycleRules.ToVietnamTime(charge.FreeResetsAtUtc), state);

        if (action is { Type: { } type } && PremiumIntents.PremiumButtons.Contains(type) && charge.AllowedIntents is not null)
            return Build(LockAnswerFor(charge), LockReasonFor(charge), null, state);

        return null;
    }

    /// <summary>
    /// [UC-127 1a, Mục 6] Hậu xử lý phản hồi bị advisor khóa:
    /// 1) Nếu ý duy nhất bị khóa là profile_review KHÔNG có task (vd. "tư vấn giúp em"): mở khóa và trả 3 nút chọn (Nhận xét hồ sơ, Danh sách trường, Lộ trình) cho mọi tài khoản.
    /// 2) Nếu tài khoản đang là Premium đã hết token mà bị advisor khóa: đổi lời từ "cần USDSS Premium" sang "Đã hết token, hãy mua thêm gói".
    /// </summary>
    public static AdvisorChatResponse AdjustAdvisorLock(ChatCharge? charge, AdvisorChatResponse response, JsonElement? state)
    {
        if (!Applies(charge))
            return response;

        if (IsEmptyTaskProfileReviewOnlyLock(response))
        {
            if (response.Action == ChatLockReasons.PremiumRequired)
            {
                return response with
                {
                    Answer = EmptyProfileReviewAnswer,
                    QuickReplies = EmptyProfileReviewChoices,
                    Action = null,
                    LockedIntents = null,
                    State = state ?? response.State,
                };
            }
            // Advisor đã trả lời các ý khác, chỉ khóa ý profile_review rỗng: vẫn đưa 3 nút chọn thay vì bỏ khóa trong im lặng
            return response with
            {
                LockedIntents = null,
                Answer = $"{response.Answer}\n\n{EmptyProfileReviewAnswer}",
                QuickReplies = [.. response.QuickReplies ?? [], .. EmptyProfileReviewChoices],
            };
        }

        if (charge!.IsPremium && response.Action == ChatLockReasons.PremiumRequired)
            return response with { Answer = PremiumTokensExhaustedAnswer };

        return response;
    }

    /// <summary>
    /// Advisor tự khóa (action premium_required: mọi ý bị khóa) hoặc bỏ một phần ý (lockedIntents: phần còn lại đã trả lời kèm thẻ).
    /// Không áp cho nhân viên hay khi chưa nối dịch vụ gói.
    /// </summary>
    public static ChatLock? LockOf(ChatCharge? charge, AdvisorChatResponse response) =>
        Applies(charge) && (response.Action == ChatLockReasons.PremiumRequired || response.LockedIntents is { Count: > 0 })
            ? new ChatLock(LockReasonFor(charge!), null)
            : null;

    // Hành động .NET chạy sau advisor mà chỉ gói Premium được dùng (gợi ý trường, giải thích, mô phỏng, goal-seeking)
    private static readonly IReadOnlySet<string> PremiumDispatchActions = new HashSet<string>
    {
        ChatRecommendation.Action, ChatExplanation.Action, ChatSimulation.Action, ChatGoalSeek.Action, ChatFitCompare.Action,
    };

    /// <summary>
    /// Lớp phòng thủ thứ hai: tài khoản bị giới hạn ý định (allowedIntents khác null) mà advisor vẫn trả hành động Premium
    /// (advisor bản cũ chưa biết allowedIntents). Trả thẻ khóa như cổng đầu lượt để ChatService bỏ lượt đó, không chạy nhánh nào, state của DB giữ nguyên.
    /// null thì advisor đã tuân thủ (hoặc tài khoản không bị giới hạn).
    /// </summary>
    public static LockedTurn? BlockPremiumAction(ChatCharge? charge, AdvisorChatResponse response, JsonElement? state) =>
        AllowedIntentsFor(charge) is not null && response.Action is { } action && PremiumDispatchActions.Contains(action)
            ? Build(LockAnswerFor(charge!), LockReasonFor(charge!), null, state)
            : null;

    private static bool IsEmptyTaskProfileReviewOnlyLock(AdvisorChatResponse response)
    {
        var locked = response.LockedIntents;
        if (locked is not { Count: > 0 } || locked.Any(i => i != ProfileReviewIntent))
            return false;
        var modelIntents = response.Slm?.ModelIntents;
        if (modelIntents is null)
            return false;
        var profileReviews = modelIntents.Where(i => i.Type == ProfileReviewIntent).ToList();
        if (profileReviews.Count == 0)
            return false;
        return profileReviews.All(HasNoTasksOrSchools);
    }

    // Cùng danh sách với SCHOOL_LIST_WORDS của advisor (branches.py)
    private static readonly string[] SchoolListWords =
    [
        "truong nao", "chon truong", "nhung truong", "danh sach truong", "goi y truong", "de xuat truong", "nop truong",
        "apply truong", "which universit", "which school", "which college", "universities should", "schools should",
        "colleges should", "recommend universit", "recommend school", "recommend college",
    ];

    private static bool HasNoTasksOrSchools(AdvisorSlmIntentDto intent)
    {
        // Câu xin danh sách trường ("gợi ý trường cho em") có thể tới dưới dạng profile_review không có task: vẫn là việc Premium,
        // nếu mở khóa thành 3 nút thì nút "Danh sách trường" gửi lại đúng câu này và lặp mãi
        var plain = ChatSchoolInfo.Plain(intent.Span);
        if (SchoolListWords.Any(plain.Contains))
            return false;
        if (intent.Entities is not { ValueKind: JsonValueKind.Object } entities)
            return true;
        return IsEmptyArrayOrMissing(entities, "tasks") && IsEmptyArrayOrMissing(entities, "schools");
    }

    private static bool IsEmptyArrayOrMissing(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var value))
            return true;
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => true,
            JsonValueKind.Array => value.GetArrayLength() == 0,
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
            _ => false,
        };
    }

    private static string LockReasonFor(ChatCharge charge) =>
        charge.IsPremium ? ChatLockReasons.PremiumTokensExhausted : ChatLockReasons.PremiumRequired;

    private static string LockAnswerFor(ChatCharge charge) =>
        charge.IsPremium ? PremiumTokensExhaustedAnswer : PremiumRequiredAnswer;

    // Action ghi đúng lý do để meta_json của tin trả lời cho admin thấy lượt này bị khóa; Action không bao giờ tới trình duyệt
    private static LockedTurn Build(string text, string reason, DateTimeOffset? resetsAt, JsonElement? state) =>
        new(new AdvisorChatResponse(text, [], null, state, QuickReplies: [ViewPlans], Action: reason), new ChatLock(reason, resetsAt));
}
