using System.Text.Json;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

// Ưu tiên phụ huynh đã lưu. HasResult: đã có danh sách theo cha mẹ tính sẵn chưa (con chưa nhận gợi ý thì chưa có).
public record ParentPreferenceDto(
    IReadOnlyList<int> Pref, long? BudgetVnd, decimal? VndPerUsd, decimal? BudgetUsd, DateTime UpdatedAt, bool HasResult);

/// <summary>[UC-76] Phụ huynh lưu 3 mức ưu tiên và ngân sách VND cho từng con có liên kết thật.
/// Con managed, liên kết chưa hiệu lực hoặc đã thu hồi, con của phụ huynh khác: đều trả NotFound (404), không lộ con có tồn tại hay không.</summary>
public class ParentPreferenceService(
    IParentLinkRepository links, IUserRepository users, IRecommendationRunner runner, IExchangeRateProvider rates)
{
    public const long MinBudgetVnd = 100_000_000;       // 100 triệu
    public const long MaxBudgetVnd = 10_000_000_000;    // 10 tỷ

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    // Cách lưu trong cột jsonb. Result: ItemsJson của SimulateAsync, cùng khuôn danh sách của con.
    // BasedOnRecommendationId: lần gợi ý của con mà Result được tính theo. Stale: lần tính gần nhất bị lỗi, trang so sánh sẽ tính bù.
    public record Stored(
        IReadOnlyList<int> Pref, long? BudgetVnd, decimal? VndPerUsd, decimal? BudgetUsd, DateTime UpdatedAt,
        JsonElement? Result, Guid? BasedOnRecommendationId, DateTime? ComputedAt, bool Stale);

    public async Task<ParentLinkResult<ParentPreferenceDto?>> GetAsync(Guid parentId, Guid childId, CancellationToken ct)
    {
        var link = await FindLinkAsync(parentId, childId, ct);
        if (link is null)
            return ParentLinkResult<ParentPreferenceDto?>.Fail(ParentLinkError.NotFound, "Không tìm thấy liên kết với con này.");
        var stored = Read(link.ParentPreferenceJson);
        return ParentLinkResult<ParentPreferenceDto?>.Ok(stored is null ? null : ToDto(stored));   // null: chưa lưu lần nào
    }

    public async Task<ParentLinkResult<ParentPreferenceDto>> SaveAsync(
        Guid parentId, Guid childId, IReadOnlyList<int>? pref, long? budgetVnd, CancellationToken ct)
    {
        if (pref is null || pref.Count != 3 || pref.Any(v => v is < 0 or > 4))
            return Fail(ParentLinkError.Validation, "Cần đủ 3 mức ưu tiên, mỗi mức từ 0 đến 4.");
        if (budgetVnd is { } b && (b < MinBudgetVnd || b > MaxBudgetVnd))
            return Fail(ParentLinkError.Validation, "Ngân sách phải từ 100 triệu đến 10 tỷ đồng mỗi năm.");

        var link = await FindLinkAsync(parentId, childId, ct);
        if (link is null)
            return Fail(ParentLinkError.NotFound, "Không tìm thấy liên kết với con này.");

        // Chốt tỷ giá lúc Lưu: sau này tỷ giá đổi thì số USD dùng để so sánh vẫn giữ nguyên
        decimal? vndPerUsd = null, budgetUsd = null;
        if (budgetVnd is { } vnd)
        {
            vndPerUsd = await rates.GetVndPerUsdAsync(ct);
            budgetUsd = Math.Round(vnd / vndPerUsd.Value, 0);
        }

        var now = DateTime.UtcNow;
        JsonElement? result = null;
        Guid? basedOn = null;
        DateTime? computedAt = null;
        var stale = false;

        // Con đã nhận gợi ý thì tính luôn danh sách theo cha mẹ trên hồ sơ của con. Chưa có thì để trống, chờ con chạy gợi ý (2a').
        var latest = await runner.GetLatestAsync(childId, ct);
        if (latest is not null)
        {
            try
            {
                var overrides = new SimulationOverrides(null, null, null, null, budgetUsd, null, null);
                var sim = await runner.SimulateAsync(childId, overrides, pref, ct);
                result = JsonSerializer.Deserialize<JsonElement>(sim.ItemsJson);
                basedOn = latest.Id;
                computedAt = now;
            }
            catch (InvalidOperationException)
            {
                stale = true;   // hồ sơ con thiếu / lỗi: ý của cha mẹ vẫn được lưu, trang so sánh tính bù sau
            }
        }

        var stored = new Stored(pref.ToArray(), budgetVnd, vndPerUsd, budgetUsd, now, result, basedOn, computedAt, stale);
        link.ParentPreferenceJson = JsonSerializer.Serialize(stored, WebJson);
        try
        {
            await links.UpdateAsync(link, ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Fail(ParentLinkError.Conflict, "Liên kết vừa thay đổi. Hãy tải lại trang rồi lưu lại.");
        }
        return ParentLinkResult<ParentPreferenceDto>.Ok(ToDto(stored));
    }

    // Liên kết đang hiệu lực giữa đúng phụ huynh này và con thật (không phải con managed)
    private async Task<ParentStudentLink?> FindLinkAsync(Guid parentId, Guid childId, CancellationToken ct)
    {
        var link = (await links.GetActiveByParentAsync(parentId, ct)).FirstOrDefault(l => l.StudentUserId == childId);
        if (link is null) return null;
        var child = await users.GetUserById(childId, ct);
        return child is null || AgeAccess.IsOperatedByParent(child) ? null : link;
    }

    // public để 2a' (refresher), 2c (so sánh) và test đọc cùng một khuôn
    public static Stored? Read(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<Stored>(json, WebJson);

    private static ParentPreferenceDto ToDto(Stored s) =>
        new(s.Pref, s.BudgetVnd, s.VndPerUsd, s.BudgetUsd, s.UpdatedAt, s.Result is not null);

    private static ParentLinkResult<ParentPreferenceDto> Fail(ParentLinkError error, string message) =>
        ParentLinkResult<ParentPreferenceDto>.Fail(error, message);
}
