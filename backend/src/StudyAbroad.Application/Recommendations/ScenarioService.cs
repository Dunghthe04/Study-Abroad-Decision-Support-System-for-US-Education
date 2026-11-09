using System.Text.Json;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Recommendations;

// Một kịch bản đã lưu: giá trị giả định lúc bấm "Lưu" và bản chụp kết quả lúc đó (số trường mỗi nhóm trước/sau, trường đổi nhóm).
public record SavedScenarioDto(
    Guid Id, string Title, DateTime CreatedAt, DateTime? UpdatedAt, string Kind,
    SimulationOverrides Overrides, IReadOnlyList<int>? Pref,
    GroupSummary? Baseline, GroupSummary? Simulated, IReadOnlyList<SchoolChange> Changes);

/// <summary>Lưu và mở lại kịch bản what-if (UC-86). Học sinh lưu, xem, đổi tên, xóa kịch bản của mình;
/// phụ huynh có liên kết đang hiệu lực chỉ được xem. Không tìm thấy hoặc không có quyền đều trả null/false (controller trả 404).</summary>
public class ScenarioService(IScenarioRepository scenarios, IRecommendationRunner simulator, IParentLinkRepository links)
{
    public const int MaxPerUser = 20;
    public const int MaxTitleLength = 256;   // khớp HasMaxLength(256) của cột title
    public const string WhatIf = "what_if";  // UC-85 (độ nhạy) sau này lưu loại khác

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    // Cách lưu vào hai cột jsonb
    private record StoredInput(string Kind, SimulationOverrides Overrides, IReadOnlyList<int>? Pref);
    private record StoredResult(GroupSummary Baseline, GroupSummary Simulated, IReadOnlyList<SchoolChange> Changes);

    public async Task<SavedScenarioDto> SaveAsync(
        Guid userId, string? title, SimulationOverrides overrides, IReadOnlyList<int>? pref, CancellationToken ct)
    {
        var name = CleanTitle(title);
        if (!RecommendationService.HasValues(overrides) && pref is null)
            throw new ArgumentException("Chưa có giá trị giả định nào để lưu.");
        if (await scenarios.CountByUserAsync(userId, ct) >= MaxPerUser)
            throw new ArgumentException($"Bạn đã có {MaxPerUser} kịch bản. Hãy xóa bớt trước khi lưu thêm.");

        // Bản chụp do backend tự tính lại với đúng giá trị đó, không nhận kết quả từ frontend
        var result = await simulator.SimulateAsync(userId, overrides, pref, ct);

        var row = new WhatIfSimulation
        {
            UserId = userId,
            Title = name,
            SimulatedProfileJson = JsonSerializer.Serialize(new StoredInput(WhatIf, overrides, pref), WebJson),
            SimulatedRecommendationsJson = JsonSerializer.Serialize(
                new StoredResult(result.Baseline, result.Simulated, result.Changes), WebJson),
        };
        await scenarios.AddAsync(row, ct);
        return ToDto(row);
    }

    // studentId bỏ trống: xem của chính mình. Có studentId của người khác: chỉ phụ huynh có liên kết đang hiệu lực.
    public async Task<IReadOnlyList<SavedScenarioDto>?> ListAsync(Guid viewerId, Guid? studentId, CancellationToken ct)
    {
        var ownerId = studentId ?? viewerId;
        if (!await CanViewAsync(viewerId, ownerId, ct))
            return null;
        var rows = await scenarios.ListByUserAsync(ownerId, ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<SavedScenarioDto?> GetAsync(Guid viewerId, Guid id, CancellationToken ct)
    {
        var row = await scenarios.GetByIdAsync(id, ct);
        return row is not null && await CanViewAsync(viewerId, row.UserId, ct) ? ToDto(row) : null;
    }

    // Chỉ chủ kịch bản đổi tên được
    public async Task<SavedScenarioDto?> RenameAsync(Guid userId, Guid id, string? title, CancellationToken ct)
    {
        var name = CleanTitle(title);
        var row = await scenarios.GetByIdAsync(id, ct);
        if (row is null || row.UserId != userId)
            return null;

        row.Title = name;
        row.UpdatedAt = DateTime.UtcNow;
        await scenarios.UpdateAsync(row, ct);
        return ToDto(row);
    }

    // Chỉ chủ kịch bản xóa được
    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var row = await scenarios.GetByIdAsync(id, ct);
        if (row is null || row.UserId != userId)
            return false;

        await scenarios.DeleteAsync(row, ct);
        return true;
    }

    private async Task<bool> CanViewAsync(Guid viewerId, Guid ownerId, CancellationToken ct) =>
        viewerId == ownerId
        || (await links.GetActiveByParentAsync(viewerId, ct)).Any(l => l.StudentUserId == ownerId);

    private static string CleanTitle(string? title)
    {
        var name = title?.Trim() ?? "";
        if (name.Length == 0)
            throw new ArgumentException("Hãy đặt tên cho kịch bản.");
        if (name.Length > MaxTitleLength)
            throw new ArgumentException($"Tên kịch bản tối đa {MaxTitleLength} ký tự.");
        return name;
    }

    private static SavedScenarioDto ToDto(WhatIfSimulation row)
    {
        var input = Read<StoredInput>(row.SimulatedProfileJson);
        var result = Read<StoredResult>(row.SimulatedRecommendationsJson);
        return new SavedScenarioDto(
            row.Id, row.Title, row.CreatedAt, row.UpdatedAt,
            input?.Kind ?? WhatIf,
            input?.Overrides ?? new SimulationOverrides(null, null, null, null, null, null, null),
            input?.Pref,
            result?.Baseline, result?.Simulated, result?.Changes ?? []);
    }

    private static T? Read<T>(string? json) where T : class =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json, WebJson);
}
