using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Application.ParentLinks;

/// <summary>[UC-76] Được RecommendationService gọi ngay sau khi lưu một lượt gợi ý của con.</summary>
public interface IParentPreferenceRefresher
{
    /// <param name="runner">Chính RecommendationService đang chạy (truyền vào thay vì inject để tránh vòng phụ thuộc DI).</param>
    Task OnChildRunAsync(IRecommendationRunner runner, Guid childId, Guid recommendationId, CancellationToken ct);
}

/// <summary>Với mỗi cha/mẹ có liên kết thật đang hiệu lực và đã lưu ưu tiên, tính lại danh sách theo ý đó trên lần gợi ý mới của con.
/// Không bao giờ ném lỗi ra ngoài: lỗi của một cha/mẹ thì giữ danh sách cũ, bật stale (trang so sánh tính bù), lượt gợi ý của con vẫn lưu.</summary>
public class ParentPreferenceRefresher(
    IParentLinkRepository links, IUserRepository users, ILogger<ParentPreferenceRefresher>? logger = null) : IParentPreferenceRefresher
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public async Task OnChildRunAsync(IRecommendationRunner runner, Guid childId, Guid recommendationId, CancellationToken ct)
    {
        try
        {
            var child = await users.GetUserById(childId, ct);
            if (child is null || AgeAccess.IsOperatedByParent(child)) return;   // con managed: không có so sánh

            var withPref = (await links.GetByStudentAsync(childId, ct))
                .Where(l => l.Status == Domain.Constants.ParentLinkStatuses.Active && l.ParentUserId is not null
                            && !string.IsNullOrEmpty(l.ParentPreferenceJson))
                .ToList();

            foreach (var link in withPref)
            {
                var stored = ParentPreferenceService.Read(link.ParentPreferenceJson)!;
                if (stored.Pref is null) continue;   // UC-94: mới lưu số tiền, chưa có ưu tiên nên không có danh sách để tính
                ParentPreferenceService.Stored next;
                try
                {
                    // Ý của cha mẹ giữ nguyên (pref + ngân sách USD đã chốt), chỉ tính lại trên hồ sơ / lần gợi ý mới của con
                    var sim = await runner.SimulateAsync(childId, ParentPreferenceService.OverridesFor(stored.BudgetUsd), stored.Pref, ct);
                    next = stored with
                    {
                        Result = JsonSerializer.Deserialize<JsonElement>(sim.ItemsJson),
                        BasedOnRecommendationId = recommendationId,
                        ComputedAt = DateTime.UtcNow,
                        Stale = false,
                        Rule = ParentPreferenceService.CurrentRule,
                    };
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger?.LogWarning(ex, "UC-76: không tính lại được danh sách theo cha mẹ, link {LinkId}", link.Id);
                    next = stored with { Stale = true };   // giữ result cũ
                }

                link.ParentPreferenceJson = JsonSerializer.Serialize(next, WebJson);
                try
                {
                    await links.UpdateAsync(link, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Ví dụ cha mẹ vừa lưu ý mới cùng lúc (xmin): bỏ qua, trang so sánh thấy basedOn lệch sẽ tính bù
                    logger?.LogWarning(ex, "UC-76: không ghi được danh sách theo cha mẹ, link {LinkId}", link.Id);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "UC-76: lỗi khi tính lại danh sách theo cha mẹ của con {ChildId}", childId);
        }
    }
}
