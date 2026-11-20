namespace StudyAbroad.Domain.Constants;

/// <summary>Loại gói. Loại quyết định làn (lane) và chỗ ngồi gia đình nên không đổi được sau khi tạo.</summary>
public static class PlanKinds
{
    public const string Free = "free";
    public const string Personal = "personal";
    public const string Family = "family";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Free, Personal, Family };
}

/// <summary>Trạng thái một kỳ (user_subscriptions). Mỗi làn tối đa 1 active và 1 queued.</summary>
public static class CycleStatuses
{
    public const string Queued = "queued";
    public const string Active = "active";
    public const string Ended = "ended";
}

public static class CycleEndReasons
{
    public const string Expired = "expired";
    public const string ExhaustedRollover = "exhausted_rollover";
    public const string OwnerPurged = "owner_purged";
    public const string FamilyLapsed = "family_lapsed";
}

/// <summary>Trạng thái đơn hàng (payment_transactions).</summary>
public static class OrderStatuses
{
    public const string Pending = "pending";
    public const string Paid = "paid";
    public const string Mismatch = "mismatch";
    public const string Expired = "expired";
    public const string Cancelled = "cancelled";
}

public static class FamilyMemberStatuses
{
    public const string Pending = "pending";
    public const string Active = "active";
    public const string Declined = "declined";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";
    public const string Removed = "removed";
    public const string Left = "left";
    public const string Ended = "ended";
}

public static class FamilyMemberEndReasons
{
    public const string MemberDeleted = "member_deleted";
    public const string FamilyLapsed = "family_lapsed";
    public const string OwnerPurged = "owner_purged";
    public const string JoinedOwnFamily = "joined_own_family";
}

public static class FamilyGroupStatuses
{
    public const string Active = "active";
    public const string Ended = "ended";
}

/// <summary>Chỗ ngồi gói Gia đình: chủ gói không chiếm chỗ.</summary>
public static class FamilySeats
{
    public const int MaxStudents = 1;
    public const int MaxParents = 2;
    public const string Student = "student";
    public const string Parent = "parent";
    /// <summary>Chỉ dùng trong phản hồi (danh sách thành viên), không lưu trong family_members.</summary>
    public const string Owner = "owner";
}

/// <summary>Tài khoản Free chỉ được các ý định / nút này trong chat.</summary>
public static class PremiumIntents
{
    public static readonly IReadOnlyList<string> FreeAllowed = ["knowledge", "out_of_scope"];
    public static readonly IReadOnlySet<string> FreeButtons = new HashSet<string> { "school_info", "school_compare", "school_search" };
    public static readonly IReadOnlySet<string> PremiumButtons = new HashSet<string> { "goal_seek", "explain" };
}

public static class BillingRules
{
    public const int LowQuotaUsedPercent = 80;
    public const int ExpiringSoonDays = 7;
    public const int InviteExpiryDays = 7;
    public const int InviteResendCooldownSeconds = 60;
    public const int InviteMaxSendsPer24h = 5;
    /// <summary>ai_calls.kind của lần gọi Claude, loại duy nhất bị tính vào hạn mức (SLM, embedding, Ollama miễn phí).</summary>
    public const string MeteredAiKind = "claude";
}
