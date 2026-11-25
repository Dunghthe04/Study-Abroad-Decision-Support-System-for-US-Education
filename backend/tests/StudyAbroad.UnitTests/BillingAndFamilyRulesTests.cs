using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using StudyAbroad.Api.Authorization;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Admin.AiUsage;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Billing.Admin;
using StudyAbroad.Application.Billing.Family;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

/// <summary>Focused unit tests for Group B UC spec alignment rules (B1..B21).</summary>
public class BillingAndFamilyRulesTests
{
    private static readonly DateTime Now = BillingStore.Now;

    // ---- B1: Admin AI usage reports current-cycle quota usage via IEntitlementService (same filter: LLM + SLM chat only)
    [Fact]
    public async Task EntitlementCurrentQuotaUsage_CountsClaudeAndOllamaChatOnly_AndMatchesAdminAiUsage()
    {
        var store = new BillingStore();
        var userId = Guid.NewGuid();
        var cycle = store.AddActive(userId, "personal-1m", Now.AddDays(-1));
        store.Calls.Add(new AiCall { UserId = userId, UserSubscriptionId = cycle.Id, PlanCode = "personal-1m", Kind = "claude", TotalTokens = 10_000, CreatedAt = Now });
        store.Calls.Add(new AiCall { UserId = userId, UserSubscriptionId = cycle.Id, PlanCode = "personal-1m", Kind = "ollama", TotalTokens = 2_500, CreatedAt = Now });
        store.Calls.Add(new AiCall { UserId = userId, UserSubscriptionId = cycle.Id, PlanCode = "personal-1m", Kind = "embedding", TotalTokens = 9_999, CreatedAt = Now });
        store.Calls.Add(new AiCall { UserId = userId, UserSubscriptionId = null, PlanCode = null, Kind = "claude", TotalTokens = 50_000, CreatedAt = Now }); // OCR non-chat

        var ent = new EntitlementService(store);
        var usage = await ent.GetCurrentQuotaUsageAsync(userId, Now);

        Assert.Equal((12_500L, 620_000L, AccountTiers.Premium), (usage.UsedTokens, usage.QuotaTokens, usage.Tier));
    }

    // ---- B5 & B6: Admin orders date range (from/to), buyer FullName search, GatewayTransactionId search including deleted buyer
    [Fact]
    public async Task AdminOrders_FilterByDateRange_SearchFullName_AndGatewayTransactionIdIncludingDeleted()
    {
        var j = new JobHarness();
        var buyer1 = j.AddUser(name: "Nguyễn Văn An", email: "an@test.vn");
        var buyer2 = j.AddUser(name: "Trần Thị Bình", email: "binh@test.vn");
        var o1 = await j.H.CreateOrder(buyer1, "personal-1m", Now.AddDays(-5));
        var o2 = await j.H.CreateOrder(buyer2, "personal-1m", Now);
        var raw2 = j.H.Order(o2.OrderCode);
        raw2.GatewayTransactionId = "payos-tx-deleted-999";
        raw2.UserId = null; // deleted account
        var retention = new DeletedBuyerRetention
        {
            BuyerCode = "DEL-1A2B3C4D", FullName = "Trần Thị Bình", Email = "binh@test.vn",
            RetainUntilUtc = AccountPurgeService.ComputeRetentionExpiryUtc(raw2.CreatedAt),
        };
        j.Store.Retentions[retention.Id] = retention;
        raw2.DeletedBuyerRetentionId = retention.Id;

        // Filter by FullName
        var byName = await j.Admin.ListAsync(null, "Văn An", 1, 20);
        Assert.Equal(o1.OrderCode, Assert.Single(byName.Items).OrderCode);

        // Filter by date range (VN date)
        var day2 = DateOnly.FromDateTime(Now + CycleRules.VietnamOffset);
        var byDate = await j.Admin.ListAsync(null, null, 1, 20, from: day2, to: day2);
        Assert.Equal(o2.OrderCode, Assert.Single(byDate.Items).OrderCode);

        // Search by GatewayTransactionId for deleted account + random buyer code displayed without leaking PII
        var byTx = await j.Admin.ListAsync(null, "payos-tx-deleted-999", 1, 20);
        var found = Assert.Single(byTx.Items);
        Assert.Equal(("payos-tx-deleted-999", "DEL-1A2B3C4D", (string?)null, (string?)null),
            (found.GatewayTransactionId, found.DeletedBuyerCode, found.BuyerName, found.BuyerEmail));
    }

    // ---- B7: 10-year post-fiscal-year retention calculation + RevealBuyer writes UC-91 access audit log
    [Fact]
    public async Task RetentionExpiryIs10YearsAfterFiscalYearEnd_AndRevealBuyerLogsUC91Audit()
    {
        // Order on 2026-11-20 VN → fiscal year ends 2027-01-01 00:00 +07:00 → +10 years = 2037-01-01 00:00 +07:00 = 2036-12-31 17:00 UTC
        var expiry = AccountPurgeService.ComputeRetentionExpiryUtc(new DateTime(2026, 11, 20, 3, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2036, 12, 31, 17, 0, 0, DateTimeKind.Utc), expiry);

        var j = new JobHarness();
        var (buyer, admin) = (j.AddUser(name: "Lê Minh", email: "minh@test.vn"), j.AddAdmin());
        var order = await j.H.CreateOrder(buyer, "personal-1m");
        var raw = j.H.Order(order.OrderCode);
        raw.UserId = null;
        var retention = new DeletedBuyerRetention { BuyerCode = "DEL-CAFEBABE", FullName = "Lê Minh", Email = "minh@test.vn", RetainUntilUtc = expiry };
        j.Store.Retentions[retention.Id] = retention;
        raw.DeletedBuyerRetentionId = retention.Id;

        var revealed = await j.Admin.RevealBuyerAsync(admin.Id, order.OrderCode, "127.0.0.1", "TestAgent");

        Assert.Equal(("DEL-CAFEBABE", "Lê Minh", "minh@test.vn", expiry), (revealed.BuyerCode, revealed.FullName, revealed.Email, revealed.RetainUntilUtc));
        var log = Assert.Single(j.Audit.Entries);
        Assert.Equal((AuditLogConstants.OrderDeletedBuyerViewed, admin.Id, "127.0.0.1"), (log.Action, log.ActorUserId, log.IpAddress));
    }

    // ---- B8: ReceivedAmountVnd persisted on mismatch & surfaced in AdminOrderDto
    [Fact]
    public async Task MismatchOrder_PersistsReceivedAmountVnd()
    {
        var j = new JobHarness();
        var buyer = j.AddUser();
        var order = await j.H.CreateOrder(buyer, "personal-1m");

        await j.H.Orders.ApplyPaymentAsync(order.OrderCode, 45_000, null, Now.AddMinutes(1));
        var detail = await j.Admin.GetAsync(order.OrderCode);

        Assert.Equal((OrderStatuses.Mismatch, 79_000L, (long?)45_000L), (detail.Status, detail.AmountVnd, detail.ReceivedAmountVnd));
    }

    // ---- B11: UC-71 endpoints require PremiumPolicies.ParentPreferenceCompare; UC-94 SaveBudget remains open
    [Fact]
    public void ParentChildPreferenceController_GatesUC71WithPremiumPolicy_LeavesSaveBudgetOpen()
    {
        var ctrl = typeof(ParentChildPreferenceController);
        string? PolicyOf(string method) => ctrl.GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>()?.Policy;

        Assert.Equal(PremiumPolicies.ParentPreferenceCompare, PolicyOf(nameof(ParentChildPreferenceController.Get)));
        Assert.Equal(PremiumPolicies.ParentPreferenceCompare, PolicyOf(nameof(ParentChildPreferenceController.Save)));
        Assert.Equal(PremiumPolicies.ParentPreferenceCompare, PolicyOf(nameof(ParentChildPreferenceController.Compare)));
        Assert.Null(PolicyOf(nameof(ParentChildPreferenceController.SaveBudget)));
    }

    // ---- B13: Empty-task profile_review ("tư vấn giúp em") returns 3 choice buttons for Free accounts instead of locking
    [Fact]
    public void EmptyTaskProfileReview_IsNotLockedForFree_Returns3ChoiceButtons()
    {
        var freeCharge = new ChatCharge(false, false, null, PlanKinds.Free, PlanKinds.Free, PremiumIntents.FreeAllowed, false, Now.AddDays(10));
        var emptyEntities = JsonDocument.Parse("""{"tasks":[]}""").RootElement.Clone();
        var emptySlm = new AdvisorSlmDto(ModelIntents: [new AdvisorSlmIntentDto("profile_review", "tư vấn giúp em", emptyEntities)]);
        var lockedResp = new AdvisorChatResponse("locked", [], null, Action: ChatLockReasons.PremiumRequired, LockedIntents: ["profile_review"], Slm: emptySlm);

        var adjusted = ChatPremiumGate.AdjustAdvisorLock(freeCharge, lockedResp, null);

        Assert.Null(adjusted.Action);
        Assert.Null(ChatPremiumGate.LockOf(freeCharge, adjusted));
        Assert.Equal(3, adjusted.QuickReplies!.Count);
        Assert.Equal(["Nhận xét hồ sơ", "Danh sách trường", "Lộ trình"], adjusted.QuickReplies.Select(q => q.Label));

        // Non-empty task profile_review remains locked for Free!
        var taskEntities = JsonDocument.Parse("""{"tasks":["assessment"]}""").RootElement.Clone();
        var taskSlm = new AdvisorSlmDto(ModelIntents: [new AdvisorSlmIntentDto("profile_review", "đánh giá hồ sơ", taskEntities)]);
        var lockedTask = new AdvisorChatResponse("locked", [], null, Action: ChatLockReasons.PremiumRequired, LockedIntents: ["profile_review"], Slm: taskSlm);

        var stillLocked = ChatPremiumGate.AdjustAdvisorLock(freeCharge, lockedTask, null);
        Assert.Equal(ChatLockReasons.PremiumRequired, ChatPremiumGate.LockOf(freeCharge, stillLocked)!.Reason);
    }

    // ---- B14: Plans with DurationDays <= 7 remind 1 day before (not 7 days before)
    [Fact]
    public async Task ShortPlan7Days_Reminds1DayBefore_Not7DaysBefore()
    {
        var j = new JobHarness();
        var buyer = j.AddUser();
        var shortPlan = new SubscriptionPlan { Code = "personal-7d", Name = "Cá nhân 7 ngày", Kind = PlanKinds.Personal, PriceVnd = 29_000, DurationDays = 7, TokenQuota = 100_000 };
        j.Store.Plans.Add(shortPlan);
        // 3 days left on a 7-day plan → should NOT remind yet (> 1 day left)
        j.Store.Cycles.Add(new UserSubscription
        {
            UserId = buyer.Id, SubscriptionPlanId = shortPlan.Id, Kind = PlanKinds.Personal, Status = CycleStatuses.Active,
            StartsAt = Now.AddDays(-4), EndsAt = Now.AddDays(3), DurationDays = 7, TokenQuota = 100_000, PlanName = shortPlan.Name,
        });
        Assert.Equal(0, await j.Jobs.SendRenewalRemindersAsync(Now));
        Assert.Empty(j.Notes.Sent);

        // 20 hours left → reminds once
        Assert.Equal(1, await j.Jobs.SendRenewalRemindersAsync(Now.AddDays(2).AddHours(4)));
        Assert.Single(j.Notes.Sent, n => n.UserId == buyer.Id && n.Type == NotificationTypes.BillingExpiring);
    }

    // ---- B15: Family expired notification goes to owner AND active members
    [Fact]
    public async Task FamilyExpired_NotifiesOwnerAndActiveMembers()
    {
        var j = new JobHarness();
        var (owner, member) = (j.AddUser(UserRoles.Parent, 40), j.AddUser());
        var cycle = j.Store.AddFamily(owner.Id, member.Id, Now.AddDays(-31));
        Assert.True(cycle.EndsAt <= Now);

        await j.H.Entitlements.EndExpiredCyclesAsync(owner.Id, PlanKinds.Family, Now);

        Assert.Contains(j.Notes.Sent, n => n.UserId == owner.Id && n.Type == NotificationTypes.BillingExpired);
        Assert.Contains(j.Notes.Sent, n => n.UserId == member.Id && n.Type == NotificationTypes.BillingExpired);
    }

    // ---- B16: Order expiration emits BillingOrderExpired in-app notification
    [Fact]
    public async Task OrderExpired_EmitsInAppNotification()
    {
        var j = new JobHarness();
        var buyer = j.AddUser();
        var order = await j.H.CreateOrder(buyer, "personal-1m");
        j.H.Gateway.Statuses[order.OrderCode] = new GatewayStatus(GatewayStatuses.Expired, 0);

        await j.Reconciler.ReconcileAsync(order.OrderCode, Now.AddMinutes(5));

        Assert.Equal(OrderStatuses.Expired, j.H.Order(order.OrderCode).Status);
        Assert.Single(j.Notes.Sent, n => n.UserId == buyer.Id && n.Type == NotificationTypes.BillingOrderExpired);
    }
}
