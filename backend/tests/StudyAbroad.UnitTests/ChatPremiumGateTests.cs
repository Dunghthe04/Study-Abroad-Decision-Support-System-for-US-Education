using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.UnitTests;

// Hàm thuần của cổng Premium trong chat (billing-plan-v2: G4, G5, G8, G9, G12). Chạy qua ChatService nằm ở ChatServiceTests.
public class ChatPremiumGateTests
{
    private static readonly DateTime ResetsUtc = new(2026, 11, 30, 17, 0, 0, DateTimeKind.Utc);   // 00:00 ngày 1/12 giờ Việt Nam
    private static readonly JsonElement State = JsonDocument.Parse("""{"currentStep":"B1","questionId":"D2"}""").RootElement.Clone();

    private static ChatCharge Free(bool exhausted = false) =>
        new(false, false, null, "free", PlanKinds.Free, PremiumIntents.FreeAllowed, exhausted, ResetsUtc);

    private static readonly ChatCharge PremiumWithTokens =
        new(false, true, Guid.NewGuid(), "personal-1m", PlanKinds.Personal, null, false, ResetsUtc);

    private static readonly ChatCharge PremiumSpent =
        new(false, true, null, "free", PlanKinds.Free, PremiumIntents.FreeAllowed, false, ResetsUtc);

    // Cố ý đặt mọi cờ khóa lên nhân viên: cổng vẫn phải bỏ qua
    private static readonly ChatCharge Staff =
        new(true, true, null, null, PlanKinds.Free, PremiumIntents.FreeAllowed, true, ResetsUtc);

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);   // cấu hình JSON mặc định của MVC

    private static ChatAction? Button(string? type) => type is null ? null : new ChatAction(type, "188429");

    [Fact]
    public void PremiumWithFreeFallbackAlsoUsedUp_GetsTokensExhaustedMessage_NotTheFreeQuotaOne()
    {
        var spent = PremiumSpent with { FreeExhausted = true };

        var turn = ChatPremiumGate.Check(spent, null, State)!;

        Assert.Equal(("Đã hết token, hãy mua thêm gói", ChatLockReasons.PremiumTokensExhausted, (DateTimeOffset?)null),
            (turn.Reply.Answer, turn.Lock.Reason, turn.Lock.ResetsAt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("school_info")]
    [InlineData("school_compare")]
    [InlineData("school_search")]
    [InlineData("explain")]
    [InlineData("goal_seek")]
    public void G8_FreeUsedUp_LocksEveryTypedMessageAndButton_WithTheResetDateAndTheStatePassedThrough(string? type)
    {
        var turn = ChatPremiumGate.Check(Free(exhausted: true), Button(type), State)!;

        Assert.Equal(ChatLockReasons.QuotaExhausted, turn.Lock.Reason);
        Assert.Equal(CycleRules.ToVietnamTime(ResetsUtc), turn.Lock.ResetsAt);
        Assert.Equal(TimeSpan.FromHours(7), turn.Lock.ResetsAt!.Value.Offset);
        Assert.Equal(ChatPremiumGate.QuotaExhaustedAnswer, turn.Reply.Answer);
        Assert.Equal(ChatLockReasons.QuotaExhausted, turn.Reply.Action);   // để meta_json của tin ghi lý do
        Assert.Equal(State.GetRawText(), turn.Reply.State!.Value.GetRawText());   // không reset buổi nhập hồ sơ
        Assert.Empty(turn.Reply.Citations);
        Assert.Null(turn.Reply.Disclaimer);
        var plans = Assert.Single(turn.Reply.QuickReplies!);
        Assert.Equal(("Xem gói", "/plans"), (plans.Label, plans.Href));
    }

    [Theory]
    [InlineData("explain", true)]
    [InlineData("goal_seek", true)]
    [InlineData("school_info", false)]
    [InlineData("school_compare", false)]
    [InlineData("school_search", false)]
    [InlineData(null, false)]       // tin gõ đi tiếp sang advisor, advisor tự lọc theo AllowedIntents
    [InlineData("unknown", false)]
    public void G4_G5_Free_OnlyThePremiumButtonsAreLocked(string? type, bool locked)
    {
        var turn = ChatPremiumGate.Check(Free(), Button(type), State);

        Assert.Equal(locked, turn is not null);
        if (turn is null) return;
        Assert.Equal(new ChatLock(ChatLockReasons.PremiumRequired, null), turn.Lock);
        Assert.Equal(ChatPremiumGate.PremiumRequiredAnswer, turn.Reply.Answer);
        Assert.Equal(ChatLockReasons.PremiumRequired, turn.Reply.Action);
        Assert.Equal(State.GetRawText(), turn.Reply.State!.Value.GetRawText());
    }

    [Fact]
    public void FreeButtons_AreExactlyTheOnesTheMatrixAllows()
    {
        // Mọi nút đã biết phải thuộc đúng một nhóm: thêm nút mới mà quên phân loại thì test này nhắc
        Assert.Empty(PremiumIntents.FreeButtons.Intersect(PremiumIntents.PremiumButtons));
        foreach (var type in PremiumIntents.FreeButtons)
            Assert.Null(ChatPremiumGate.Check(Free(), Button(type), State));
    }

    [Fact]
    public void G9_PremiumWithEveryPoolUsedUp_PremiumButtonsAreLocked_TypedGoesOnWithTheFreeList()
    {
        var explain = ChatPremiumGate.Check(PremiumSpent, Button("explain"), State)!;
        var goalSeek = ChatPremiumGate.Check(PremiumSpent, Button("goal_seek"), State)!;
        Assert.Equal((ChatLockReasons.PremiumTokensExhausted, ChatPremiumGate.PremiumTokensExhaustedAnswer), (explain.Lock.Reason, explain.Reply.Answer));
        Assert.Equal((ChatLockReasons.PremiumTokensExhausted, ChatPremiumGate.PremiumTokensExhaustedAnswer), (goalSeek.Lock.Reason, goalSeek.Reply.Answer));
        Assert.Null(ChatPremiumGate.Check(PremiumSpent, Button("school_info"), State));
        Assert.Null(ChatPremiumGate.Check(PremiumSpent, null, State));
        Assert.Equal(PremiumIntents.FreeAllowed, ChatPremiumGate.AllowedIntentsFor(PremiumSpent));
    }

    [Fact]
    public void PremiumWithTokens_IsNeverLocked_AndAllIntentsAreAllowed()
    {
        foreach (var type in new[] { null, "explain", "goal_seek", "school_info" })
            Assert.Null(ChatPremiumGate.Check(PremiumWithTokens, Button(type), State));
        Assert.Null(ChatPremiumGate.AllowedIntentsFor(PremiumWithTokens));
    }

    [Fact]
    public void G12_Staff_IsNeverGated_WhateverTheFlagsSay()
    {
        foreach (var type in new[] { null, "explain", "goal_seek", "school_info" })
            Assert.Null(ChatPremiumGate.Check(Staff, Button(type), State));
        Assert.Null(ChatPremiumGate.AllowedIntentsFor(Staff));
        Assert.Null(ChatPremiumGate.LockOf(Staff, new AdvisorChatResponse("x", [], null, Action: ChatLockReasons.PremiumRequired, LockedIntents: ["scenario"])));
    }

    [Fact]
    public void WithoutAnEntitlementService_NothingIsGated_AndAllIntentsAreAllowed()
    {
        Assert.Null(ChatPremiumGate.Check(null, Button("explain"), State));
        Assert.Null(ChatPremiumGate.AllowedIntentsFor(null));
        Assert.Null(ChatPremiumGate.LockOf(null, new AdvisorChatResponse("x", [], null, Action: ChatLockReasons.PremiumRequired)));
    }

    [Fact]
    public void AllowedIntents_ForFree_IsTheKnowledgeListOnly()
    {
        Assert.Equal(["knowledge", "out_of_scope"], ChatPremiumGate.AllowedIntentsFor(Free())!);
        Assert.Equal(["knowledge", "out_of_scope"], ChatPremiumGate.AllowedIntentsFor(Free(exhausted: true))!);
    }

    [Fact]
    public void LockOf_ReadsTheAdvisorsAction_AndItsLockedIntents()
    {
        var premium = new ChatLock(ChatLockReasons.PremiumRequired, null);

        Assert.Equal(premium, ChatPremiumGate.LockOf(Free(), new AdvisorChatResponse("x", [], null, Action: "premium_required")));
        Assert.Equal(premium, ChatPremiumGate.LockOf(Free(), new AdvisorChatResponse("x", [], null, Action: "school_info", LockedIntents: ["scenario"])));
        Assert.Null(ChatPremiumGate.LockOf(Free(), new AdvisorChatResponse("x", [], null, Action: "school_info", LockedIntents: [])));
        Assert.Null(ChatPremiumGate.LockOf(Free(), new AdvisorChatResponse("x", [], null)));
    }

    [Fact]
    public void ChatLock_SerializesAsTheFrontendContract()
    {
        var quota = new ChatLock(ChatLockReasons.QuotaExhausted, CycleRules.ToVietnamTime(ResetsUtc));
        var premium = new ChatLock(ChatLockReasons.PremiumRequired, null);

        Assert.Equal("""{"reason":"quota_exhausted","resetsAt":"2026-12-01T00:00:00+07:00"}""", JsonSerializer.Serialize(quota, Web));
        Assert.Equal("""{"reason":"premium_required","resetsAt":null}""", JsonSerializer.Serialize(premium, Web));
    }
}
