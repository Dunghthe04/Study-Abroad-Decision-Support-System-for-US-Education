using StudyAbroad.Application.Billing;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.UnitTests;

// IEntitlementService.GetExpiredCyclesAsync: chỉ kỳ đã kết thúc của chính mình, hết hạn gần nhất trước
public class ExpiredCyclesTests
{
    private static readonly DateTime Now = BillingStore.Now;
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    [Fact]
    public async Task ReturnsOwnEndedCycles_NewestExpiredFirst_ExcludingActiveQueuedAndOthers()
    {
        var store = new BillingStore();
        var older = store.AddActive(Me, "personal-1m", startsAt: Now.AddDays(-100));
        var newer = store.AddActive(Me, "family-1m", startsAt: Now.AddDays(-45));
        foreach (var c in new[] { older, newer }) c.Status = CycleStatuses.Ended;
        store.AddActive(Me, "personal-1m");                                              // đang chạy
        var queued = store.AddActive(Me, "personal-3m"); queued.Status = CycleStatuses.Ended; queued.StartsAt = null;   // chưa bắt đầu
        var theirs = store.AddActive(Other, "personal-1m", startsAt: Now.AddDays(-60)); theirs.Status = CycleStatuses.Ended;

        var list = await new EntitlementService(store).GetExpiredCyclesAsync(Me);

        Assert.Equal([newer.Id, older.Id], list.Select(x => x.Id));
        Assert.Equal(["family-1m", "personal-1m"], list.Select(x => x.PlanCode));
        Assert.Equal(PlanKinds.Family, list[0].Kind);
    }
}
