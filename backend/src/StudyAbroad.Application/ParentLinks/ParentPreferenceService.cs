using System.Text.Json;
using System.Text.Json.Nodes;
using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

// Ưu tiên phụ huynh đã lưu. HasResult: đã có danh sách theo cha mẹ tính sẵn chưa (con chưa nhận gợi ý thì chưa có).
// HasPref=false: phụ huynh mới lưu số tiền (UC-94 popup), chưa chọn ưu tiên; Pref khi đó là mặc định [2,2,2].
public record ParentPreferenceDto(
    IReadOnlyList<int> Pref, long? BudgetVnd, decimal? VndPerUsd, decimal? BudgetUsd, DateTime UpdatedAt, bool HasResult,
    bool HasPref = true);

// [UC-76 2c] Màn so sánh. StudentHasRun=false: con chưa nhận gợi ý, Comparison null.
// IsPreview: danh sách cha mẹ vừa tính từ giá trị chưa lưu (kéo thanh, hoặc cha mẹ chưa lưu lần nào → [2,2,2]).
// ProfileChangedAfterRun: con sửa hồ sơ sau lần nhận gợi ý mới nhất, hai bên có thể lệch vì hồ sơ chứ không vì ưu tiên.
// StudentItems / ParentItems: hai danh sách cùng khuôn ItemsJson, chỉ giữ số liệu các trường được xếp nhóm, để frontend vẽ thẻ trường như trang Gợi ý.
// StudentBudgetMinUsd / MaxUsd: khoảng ngân sách trong hồ sơ con (theo mức đã chọn), để đặt cạnh ngân sách cha mẹ và nêu chỗ khác (LNK-019/022/033).
public record PreferenceCompareDto(
    bool StudentHasRun, IReadOnlyList<int>? StudentPref, IReadOnlyList<int> ParentPref,
    long? ParentBudgetVnd, decimal? ParentBudgetUsd, decimal? VndPerUsd,
    bool ParentHasPref, bool IsPreview, bool ProfileChangedAfterRun,
    DateTime? StudentRunAt, DateTime? SavedAt, DateTime? ComputedAt,
    PreferenceComparison? Comparison, string? Message,
    JsonElement? StudentItems = null, JsonElement? ParentItems = null,
    decimal? StudentBudgetMinUsd = null, decimal? StudentBudgetMaxUsd = null);

// Trang của con: các cha/mẹ liên kết thật (ô chọn Bố / Mẹ) và so sánh của người đang chọn
public record ParentOption(Guid Id, string Name, bool HasPref);
public record ChildPreferenceView(IReadOnlyList<ParentOption> Parents, Guid? SelectedParentId, PreferenceCompareDto? Compare);

/// <summary>[UC-76] Phụ huynh lưu 3 mức ưu tiên và ngân sách VND cho từng con có liên kết thật.
/// Con managed, liên kết chưa hiệu lực hoặc đã thu hồi, con của phụ huynh khác: đều trả NotFound (404), không lộ con có tồn tại hay không.</summary>
public class ParentPreferenceService(
    IParentLinkRepository links, IUserRepository users, IRecommendationRunner runner, IExchangeRateProvider rates,
    IAcademicProfileRepository profiles, INotificationService notifications)
{
    public const long MinBudgetVnd = 100_000_000;       // 100 triệu
    public const long MaxBudgetVnd = 10_000_000_000;    // 10 tỷ
    public const string NotificationType = "parent_preference_saved";   // hằng riêng, không thêm vào NotificationTypes (không phải file của Việt)

    private static readonly int[] DefaultPref = [2, 2, 2];
    private const string NotRunMessage = "Con chưa nhận gợi ý trường, chưa so được.";
    private const string NoPrefMessage = "Bố/Mẹ chưa chọn ưu tiên.";
    private const string PrefError = "Cần đủ 3 mức ưu tiên, mỗi mức từ 0 đến 4.";
    private const string BudgetError = "Ngân sách phải từ 100 triệu đến 10 tỷ đồng mỗi năm.";
    private const string NoLinkError = "Không tìm thấy liên kết với con này.";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    // Cách lưu trong cột jsonb. Result: ItemsJson của SimulateAsync, cùng khuôn danh sách của con.
    // BasedOnRecommendationId: lần gợi ý của con mà Result được tính theo. Stale: lần tính gần nhất bị lỗi, trang so sánh sẽ tính bù.
    // Rule: phiên bản luật lọc lúc tính Result. Khác CurrentRule (bản lưu trước khi đổi luật thì thiếu, đọc ra 0) thì tính lại.
    // Pref null: phụ huynh mới lưu số tiền tối đa từ dashboard (UC-94), chưa chọn ưu tiên → coi như chưa chọn, con không thấy.
    public record Stored(
        IReadOnlyList<int>? Pref, long? BudgetVnd, decimal? VndPerUsd, decimal? BudgetUsd, DateTime UpdatedAt,
        JsonElement? Result, Guid? BasedOnRecommendationId, DateTime? ComputedAt, bool Stale, int Rule = 0);

    // 2: ngân sách cha mẹ không có mức dư 10%, vượt là sang khối cần học bổng (UC-76 2j-1)
    public const int CurrentRule = 2;

    public async Task<ParentLinkResult<ParentPreferenceDto?>> GetAsync(Guid parentId, Guid childId, CancellationToken ct)
    {
        var link = await FindLinkAsync(parentId, childId, ct);
        if (link is null)
            return ParentLinkResult<ParentPreferenceDto?>.Fail(ParentLinkError.NotFound, NoLinkError);
        var stored = Read(link.ParentPreferenceJson);
        return ParentLinkResult<ParentPreferenceDto?>.Ok(stored is null ? null : ToDto(stored));   // null: chưa lưu lần nào
    }

    public async Task<ParentLinkResult<ParentPreferenceDto>> SaveAsync(
        Guid parentId, Guid childId, IReadOnlyList<int>? pref, long? budgetVnd, CancellationToken ct)
    {
        if (pref is null || !ValidPref(pref))
            return Fail(ParentLinkError.Validation, PrefError);
        if (!ValidBudget(budgetVnd))
            return Fail(ParentLinkError.Validation, BudgetError);

        var link = await FindLinkAsync(parentId, childId, ct);
        if (link is null)
            return Fail(ParentLinkError.NotFound, NoLinkError);

        // Chốt tỷ giá lúc Lưu: sau này tỷ giá đổi thì số USD dùng để so sánh vẫn giữ nguyên
        var (vndPerUsd, budgetUsd) = await ToUsdAsync(budgetVnd, ct);

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
                result = await SimulateAsync(childId, pref, budgetUsd, ct);
                basedOn = latest.Id;
                computedAt = now;
            }
            catch (InvalidOperationException)
            {
                stale = true;   // hồ sơ con thiếu / lỗi: ý của cha mẹ vẫn được lưu, trang so sánh tính bù sau
            }
        }

        var stored = new Stored(pref.ToArray(), budgetVnd, vndPerUsd, budgetUsd, now, result, basedOn, computedAt, stale, CurrentRule);
        link.ParentPreferenceJson = JsonSerializer.Serialize(stored, WebJson);
        try
        {
            await links.UpdateAsync(link, ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Fail(ParentLinkError.Conflict, "Liên kết vừa thay đổi. Hãy tải lại trang rồi lưu lại.");
        }

        // Chỉ Lưu mới báo cho con (xem thử thì không). NotifyAsync không ném lỗi.
        await notifications.NotifyAsync(childId, NotificationType, "Phụ huynh đã chọn ưu tiên chọn trường",
            "Xem danh sách trường theo ý cha mẹ so với danh sách của bạn.", $"/parent-preferences?parentId={parentId}", ct);
        return ParentLinkResult<ParentPreferenceDto>.Ok(ToDto(stored));
    }

    /// <summary>[UC-94] Phụ huynh chỉ lưu số tiền tối đa chi trả được/năm từ dashboard. Không đụng ưu tiên, không báo cho con.
    /// Đã có ưu tiên thì đánh dấu Stale để trang so sánh tính lại danh sách theo số tiền mới.</summary>
    public async Task<ParentLinkResult<ParentPreferenceDto>> SaveBudgetAsync(
        Guid parentId, Guid childId, long? budgetVnd, CancellationToken ct)
    {
        if (budgetVnd is null || !ValidBudget(budgetVnd))
            return Fail(ParentLinkError.Validation, BudgetError);

        var link = await FindLinkAsync(parentId, childId, ct);
        if (link is null)
            return Fail(ParentLinkError.NotFound, NoLinkError);

        var (vndPerUsd, budgetUsd) = await ToUsdAsync(budgetVnd, ct);
        var now = DateTime.UtcNow;
        var old = Read(link.ParentPreferenceJson);
        var stored = old is null
            ? new Stored(null, budgetVnd, vndPerUsd, budgetUsd, now, null, null, null, false, CurrentRule)
            : old with { BudgetVnd = budgetVnd, VndPerUsd = vndPerUsd, BudgetUsd = budgetUsd, UpdatedAt = now, Stale = old.Pref is not null };

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

    /// <summary>[2c] Phụ huynh xem so sánh. Có pref hoặc budgetVnd: xem thử, tính bằng giá trị chưa lưu và không ghi DB
    /// (budgetVnd bỏ trống = ngân sách trong hồ sơ con). Không có: dùng ý đã lưu; chưa lưu lần nào thì xem thử với [2,2,2].</summary>
    public async Task<ParentLinkResult<PreferenceCompareDto>> CompareForParentAsync(
        Guid parentId, Guid childId, IReadOnlyList<int>? pref, long? budgetVnd, CancellationToken ct)
    {
        if (pref is not null && !ValidPref(pref))
            return FailCompare(ParentLinkError.Validation, PrefError);
        if (!ValidBudget(budgetVnd))
            return FailCompare(ParentLinkError.Validation, BudgetError);

        var link = await FindLinkAsync(parentId, childId, ct);
        if (link is null)
            return FailCompare(ParentLinkError.NotFound, NoLinkError);

        var stored = Read(link.ParentPreferenceJson);
        if (pref is not null || budgetVnd is not null)
            return await PreviewAsync(childId, pref ?? stored?.Pref ?? DefaultPref, budgetVnd, stored, ct);
        if (stored?.Pref is null)   // chưa chọn ưu tiên (có thể đã lưu số tiền): xem thử [2,2,2] với số tiền đã lưu
            return await PreviewAsync(childId, DefaultPref, stored?.BudgetVnd, stored, ct);
        return await CompareSavedAsync(link, childId, stored, ct);
    }

    /// <summary>[2c] Con xem so sánh với ý đã lưu của cha/mẹ. Con không gửi được pref/ngân sách (chỉ đọc).
    /// parentId null: chọn người đầu tiên đã lưu ý; parentId không phải cha/mẹ liên kết thật của con: 404.</summary>
    public async Task<ParentLinkResult<ChildPreferenceView>> GetForChildAsync(Guid childId, Guid? parentId, CancellationToken ct)
    {
        var child = await users.GetUserById(childId, ct);
        if (child is null || AgeAccess.IsOperatedByParent(child))
            return ParentLinkResult<ChildPreferenceView>.Fail(ParentLinkError.NotFound, "Không tìm thấy.");

        var active = (await links.GetByStudentAsync(childId, ct))
            .Where(l => l.Status == Domain.Constants.ParentLinkStatuses.Active && l.ParentUserId is not null)
            .ToList();
        if (active.Count == 0 && parentId is null)
            return ParentLinkResult<ChildPreferenceView>.Ok(new ChildPreferenceView([], null, null));

        var options = new List<ParentOption>();
        foreach (var l in active)
        {
            var parent = await users.GetUserById(l.ParentUserId!.Value, ct);
            var name = string.IsNullOrWhiteSpace(parent?.FullName) ? l.ParentEmail : parent!.FullName!;
            options.Add(new ParentOption(l.ParentUserId.Value, name, Read(l.ParentPreferenceJson)?.Pref is not null));
        }

        var selected = parentId is { } pid
            ? active.FirstOrDefault(l => l.ParentUserId == pid)
            : active.FirstOrDefault(l => Read(l.ParentPreferenceJson)?.Pref is not null) ?? active.First();
        if (selected is null)
            return ParentLinkResult<ChildPreferenceView>.Fail(ParentLinkError.NotFound, "Không tìm thấy phụ huynh này.");

        var stored = Read(selected.ParentPreferenceJson);
        PreferenceCompareDto compare;
        if (stored?.Pref is null)   // chỉ lưu số tiền: con chưa thấy gì
        {
            var latest = await runner.GetLatestAsync(childId, ct);
            compare = new PreferenceCompareDto(latest is not null,
                latest is null ? null : ChatSimulation.BasePref(latest.CriteriaJson), DefaultPref, null, null, null,
                false, false, false, latest?.CreatedAt, null, null, null, NoPrefMessage);
        }
        else
        {
            var r = await CompareSavedAsync(selected, childId, stored, ct);
            if (r.Error != ParentLinkError.None)
                return ParentLinkResult<ChildPreferenceView>.Fail(r.Error, r.Message ?? "");
            compare = r.Value!;
        }
        return ParentLinkResult<ChildPreferenceView>.Ok(new ChildPreferenceView(options, selected.ParentUserId, compare));
    }

    // Xem thử: tính danh sách cha mẹ bằng giá trị chưa lưu, tỷ giá hôm nay; không ghi DB
    private async Task<ParentLinkResult<PreferenceCompareDto>> PreviewAsync(
        Guid childId, IReadOnlyList<int> pref, long? budgetVnd, Stored? stored, CancellationToken ct)
    {
        var latest = await runner.GetLatestAsync(childId, ct);
        if (latest is null)
            return ParentLinkResult<PreferenceCompareDto>.Ok(
                NotRun(pref, budgetVnd, null, null, stored?.Pref is not null, true, stored?.UpdatedAt));

        var (vndPerUsd, budgetUsd) = await ToUsdAsync(budgetVnd, ct);
        JsonElement parentItems;
        try
        {
            parentItems = await SimulateAsync(childId, pref, budgetUsd, ct);
        }
        catch (InvalidOperationException ex)
        {
            return FailCompare(ParentLinkError.Conflict, ProfileNotReady(ex));
        }
        var dto = await BuildAsync(childId, latest, pref, budgetVnd, budgetUsd, vndPerUsd,
            stored?.Pref is not null, true, stored?.UpdatedAt, null, parentItems, ct);
        return ParentLinkResult<PreferenceCompareDto>.Ok(dto);
    }

    // Ý đã lưu (Pref có giá trị): đọc danh sách đã tính sẵn. Chỉ tính bù (rồi lưu) khi chưa có, bị đánh dấu stale, theo lần gợi ý cũ của con, hoặc theo luật lọc cũ.
    private async Task<ParentLinkResult<PreferenceCompareDto>> CompareSavedAsync(
        ParentStudentLink link, Guid childId, Stored stored, CancellationToken ct)
    {
        var latest = await runner.GetLatestAsync(childId, ct);
        if (latest is null)
            return ParentLinkResult<PreferenceCompareDto>.Ok(
                NotRun(stored.Pref!, stored.BudgetVnd, stored.BudgetUsd, stored.VndPerUsd, true, false, stored.UpdatedAt));

        var current = stored;
        if (stored.Result is null || stored.Stale || stored.BasedOnRecommendationId != latest.Id || stored.Rule != CurrentRule)
        {
            try
            {
                var items = await SimulateAsync(childId, stored.Pref!, stored.BudgetUsd, ct);
                current = stored with { Result = items, BasedOnRecommendationId = latest.Id, ComputedAt = DateTime.UtcNow, Stale = false, Rule = CurrentRule };
            }
            catch (InvalidOperationException ex)
            {
                return FailCompare(ParentLinkError.Conflict, ProfileNotReady(ex));
            }

            link.ParentPreferenceJson = JsonSerializer.Serialize(current, WebJson);
            try
            {
                await links.UpdateAsync(link, ct);
            }
            catch (ConcurrencyConflictException)
            {
                // Cha mẹ vừa lưu ý mới cùng lúc: vẫn hiện bản vừa tính, lần mở sau đọc lại từ DB
            }
        }

        var dto = await BuildAsync(childId, latest, current.Pref!, current.BudgetVnd, current.BudgetUsd, current.VndPerUsd,
            true, false, current.UpdatedAt, current.ComputedAt, current.Result!.Value, ct);
        return ParentLinkResult<PreferenceCompareDto>.Ok(dto);
    }

    private async Task<(decimal? VndPerUsd, decimal? BudgetUsd)> ToUsdAsync(long? budgetVnd, CancellationToken ct)
    {
        if (budgetVnd is not { } vnd) return (null, null);
        var rate = await rates.GetVndPerUsdAsync(ct);
        return (rate, Math.Round(vnd / rate, 0));
    }

    /// <summary>Giả định để tính danh sách theo cha mẹ. Cha mẹ có nhập ngân sách thì đó là con số chốt: mức dư 0%,
    /// vượt dù 1 USD là sang khối cần học bổng (trường không có học bổng thì loại). Bỏ trống thì dùng ngân sách của con kèm mức dư như con.</summary>
    public static SimulationOverrides OverridesFor(decimal? budgetUsd) =>
        new(null, null, null, null, budgetUsd, null, null, BudgetTolerance: budgetUsd is null ? null : 0m);

    private async Task<JsonElement> SimulateAsync(Guid childId, IReadOnlyList<int> pref, decimal? budgetUsd, CancellationToken ct)
    {
        var sim = await runner.SimulateAsync(childId, OverridesFor(budgetUsd), pref, ct);
        return JsonSerializer.Deserialize<JsonElement>(sim.ItemsJson);
    }

    private async Task<PreferenceCompareDto> BuildAsync(
        Guid childId, Recommendation latest, IReadOnlyList<int> parentPref, long? budgetVnd, decimal? budgetUsd, decimal? vndPerUsd,
        bool parentHasPref, bool isPreview, DateTime? savedAt, DateTime? computedAt, JsonElement parentItems, CancellationToken ct)
    {
        var studentPref = ChatSimulation.BasePref(latest.CriteriaJson);
        JsonElement studentItems;
        try
        {
            studentItems = JsonSerializer.Deserialize<JsonElement>(latest.ItemsJson);
        }
        catch (JsonException)
        {
            studentItems = JsonSerializer.Deserialize<JsonElement>("{}");   // ItemsJson hỏng: coi như danh sách rỗng
        }

        // Không thêm cột: so lúc sửa hồ sơ với lúc nhận gợi ý
        var profile = await profiles.GetByUserIdAsync(childId, ct);
        var changed = profile?.UpdatedAt is { } u && u > latest.CreatedAt;

        var comparison = PreferenceComparer.Compare(studentItems, parentItems, studentPref, parentPref, budgetUsd);
        return new PreferenceCompareDto(true, studentPref, parentPref, budgetVnd, budgetUsd, vndPerUsd,
            parentHasPref, isPreview, changed, latest.CreatedAt, savedAt, computedAt, comparison, null,
            Picked(studentItems), Picked(parentItems), profile?.BudgetMinUsd, profile?.BudgetMaxUsd);
    }

    private static readonly string[] Blocks = ["reach", "match", "safety", "aid", "other"];

    /// <summary>Giữ 5 khối và số liệu (scored) của đúng các trường có trong khối; bỏ phần scored còn lại cho gọn.
    /// public để test đọc.</summary>
    public static JsonElement Picked(JsonElement items)
    {
        var result = new JsonObject();
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (items.ValueKind == JsonValueKind.Object)
        {
            foreach (var block in Blocks)
            {
                if (!items.TryGetProperty(block, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
                result[block] = JsonNode.Parse(arr.GetRawText());
                foreach (var p in arr.EnumerateArray())
                    if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("code", out var c) && c.GetString() is { } code)
                        codes.Add(code);
            }
        }
        var scored = new JsonArray();
        if (items.ValueKind == JsonValueKind.Object && items.TryGetProperty("scored", out var all) && all.ValueKind == JsonValueKind.Array)
            foreach (var e in all.EnumerateArray())
                if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("code", out var c) && c.GetString() is { } code && codes.Contains(code))
                    scored.Add(JsonNode.Parse(e.GetRawText()));
        result["scored"] = scored;
        foreach (var block in Blocks) result[block] ??= new JsonArray();
        return JsonSerializer.Deserialize<JsonElement>(result.ToJsonString());
    }

    private static PreferenceCompareDto NotRun(IReadOnlyList<int> parentPref, long? budgetVnd, decimal? budgetUsd, decimal? vndPerUsd,
        bool parentHasPref, bool isPreview, DateTime? savedAt) =>
        new(false, null, parentPref, budgetVnd, budgetUsd, vndPerUsd, parentHasPref, isPreview, false, null, savedAt, null, null, NotRunMessage);

    private static string ProfileNotReady(InvalidOperationException ex) =>
        $"Hồ sơ của con chưa đủ để tính danh sách: {ex.Message}";

    private static bool ValidPref(IReadOnlyList<int> pref) => pref.Count == 3 && pref.All(v => v is >= 0 and <= 4);
    private static bool ValidBudget(long? vnd) => vnd is not { } b || (b >= MinBudgetVnd && b <= MaxBudgetVnd);

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
        new(s.Pref ?? DefaultPref, s.BudgetVnd, s.VndPerUsd, s.BudgetUsd, s.UpdatedAt, s.Result is not null, s.Pref is not null);

    private static ParentLinkResult<ParentPreferenceDto> Fail(ParentLinkError error, string message) =>
        ParentLinkResult<ParentPreferenceDto>.Fail(error, message);

    private static ParentLinkResult<PreferenceCompareDto> FailCompare(ParentLinkError error, string message) =>
        ParentLinkResult<PreferenceCompareDto>.Fail(error, message);
}
