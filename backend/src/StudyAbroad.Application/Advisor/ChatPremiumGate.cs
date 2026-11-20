using System.Text.Json;
using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Advisor;

/// <summary>Thẻ khóa trên phản hồi chat (trình duyệt đọc <c>locked</c>): vì sao lượt này không chạy. ResetsAt chỉ có khi hết hạn mức Free (giờ Việt Nam).</summary>
public sealed record ChatLock(string Reason, DateTimeOffset? ResetsAt);

public static class ChatLockReasons
{
    public const string PremiumRequired = "premium_required";
    public const string QuotaExhausted = "quota_exhausted";
}

/// <summary>
/// Cổng Premium của chat, chạy sau cổng tuổi và ParentChatGate, trước khi gọi advisor (billing-plan-v2, "Chat gate").
/// Hàm thuần: ChatService gọi, không đọc DB. Chỉ áp cho tài khoản bị tính (charge.Ungated = false); nhân viên không bao giờ bị chặn.
/// </summary>
public static class ChatPremiumGate
{
    public const string PremiumRequiredAnswer = "Tính năng này cần tài khoản USDSS Premium.";
    public const string QuotaExhaustedAnswer = "Bạn đã dùng hết lượt hỏi miễn phí của tháng này.";
    public const string PlansHref = "/plans";

    public static QuickReply ViewPlans { get; } = new("Xem gói", "Xem gói", Href: PlansHref);

    /// <summary>Lượt do .NET tự trả lời (không gọi advisor): Reply là phản hồi dựng sẵn, Lock là thẻ khóa gửi trình duyệt.</summary>
    public sealed record LockedTurn(AdvisorChatResponse Reply, ChatLock Lock);

    /// <summary>Có chặn được lượt này không (tài khoản bị tính). charge null = chưa nối dịch vụ gói (test cũ), không chặn.</summary>
    public static bool Applies(ChatCharge? charge) => charge is { Ungated: false };

    /// <summary>Ý định gửi advisor. Luôn lấy từ server (ghi đè giá trị trình duyệt gửi); null = tất cả.</summary>
    public static IReadOnlyList<string>? AllowedIntentsFor(ChatCharge? charge) => Applies(charge) ? charge!.AllowedIntents : null;

    /// <summary>
    /// Lượt phải khóa ngay, trước mọi lần gọi advisor hay đọc dữ liệu gợi ý; null thì lượt đi tiếp.
    /// 1) Hạn mức Free tháng này đã hết: khóa mọi tin gõ và mọi nút (quota_exhausted, kèm ngày làm mới).
    /// 2) Nút Premium (goal_seek, explain) khi gói không cho hết ý định: khóa (premium_required).
    /// state là trạng thái hội thoại của DB, giữ nguyên trong phản hồi để lượt khóa không làm mất câu hỏi đang dở.
    /// </summary>
    public static LockedTurn? Check(ChatCharge? charge, ChatAction? action, JsonElement? state)
    {
        if (charge is not { Ungated: false })
            return null;

        if (charge.PoolKind == PlanKinds.Free && charge.FreeExhausted)
            return Build(QuotaExhaustedAnswer, ChatLockReasons.QuotaExhausted, CycleRules.ToVietnamTime(charge.FreeResetsAtUtc), state);

        if (action is { Type: { } type } && PremiumIntents.PremiumButtons.Contains(type) && charge.AllowedIntents is not null)
            return Build(PremiumRequiredAnswer, ChatLockReasons.PremiumRequired, null, state);

        return null;
    }

    /// <summary>
    /// Advisor tự khóa (action premium_required: mọi ý bị khóa) hoặc bỏ một phần ý (lockedIntents: phần còn lại đã trả lời kèm thẻ).
    /// Không áp cho nhân viên hay khi chưa nối dịch vụ gói.
    /// </summary>
    public static ChatLock? LockOf(ChatCharge? charge, AdvisorChatResponse response) =>
        Applies(charge) && (response.Action == ChatLockReasons.PremiumRequired || response.LockedIntents is { Count: > 0 })
            ? new ChatLock(ChatLockReasons.PremiumRequired, null)
            : null;

    // Action ghi đúng lý do để meta_json của tin trả lời cho admin thấy lượt này bị khóa; Action không bao giờ tới trình duyệt
    private static LockedTurn Build(string text, string reason, DateTimeOffset? resetsAt, JsonElement? state) =>
        new(new AdvisorChatResponse(text, [], null, state, QuickReplies: [ViewPlans], Action: reason), new ChatLock(reason, resetsAt));
}
