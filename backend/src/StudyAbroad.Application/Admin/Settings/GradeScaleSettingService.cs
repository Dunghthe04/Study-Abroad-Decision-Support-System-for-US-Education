using System.Text.Json;
using StudyAbroad.Application.Profile.Academic;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.Settings;

public interface IGradeScaleSettingService
{
    Task<GradeScaleSettingDto> GetAsync(CancellationToken ct = default);
    Task<GradeScaleUpdateResult> UpdateAsync(UpdateGradeScaleRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<GradeScaleHistoryItemDto>> GetHistoryAsync(int limit, CancellationToken ct = default);
}

/// <summary>
/// Moderator sửa bảng quy đổi grade_scale.10, admin xem (quyền đặt ở controller).
/// Lưu: kiểm tra luật (GradeScale.Validate) → so Version để chặn ghi đè bảng người khác vừa sửa (409)
/// → lưu kèm audit_logs (bảng cũ / mới) → xoá cache để chỗ tính GPA dùng bảng mới ngay.
/// </summary>
public class GradeScaleSettingService(
    IGradeScaleSettingRepository repository,
    IGradeScaleProvider provider) : IGradeScaleSettingService
{
    public const string AuditAction = "grade_scale.update";
    public const int MaxLetterGradeLength = 4;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<GradeScaleSettingDto> GetAsync(CancellationToken ct = default) =>
        ToDto(await repository.GetAsync(ct));

    public async Task<GradeScaleUpdateResult> UpdateAsync(
        UpdateGradeScaleRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var config = BuildConfig(request, out var inputErrors);
        var errors = inputErrors.Concat(GradeScale.Validate(config)).ToList();
        if (errors.Count > 0) return GradeScaleUpdateResult.Invalid(errors);

        var setting = await repository.GetAsync(ct);
        if (!SameVersion(VersionOf(setting), request.Version))
            return GradeScaleUpdateResult.Conflict(ToDto(setting));

        var oldConfig = GradeScale.Parse(setting?.ValueJson);
        var isNew = setting is null;
        setting ??= new AppSetting
        {
            Key = AcademicConstants.GradeScale10AppSettingKey,
            Description = "Bảng quy đổi điểm thang 10 sang GPA thang 4 (tra cận dưới MinScore), moderator sửa trên trang quản trị.",
        };
        setting.ValueJson = JsonSerializer.Serialize(config, JsonOptions);

        var audit = new AuditLog
        {
            ActorUserId = actorUserId,
            Action = AuditAction,
            EntityType = "app_setting",
            EntityId = AcademicConstants.GradeScale10AppSettingKey,
            DetailJson = JsonSerializer.Serialize(new { old = oldConfig, @new = config }, JsonOptions),
        };
        await repository.SaveAsync(setting, isNew, audit, ct);
        provider.Invalidate();

        return GradeScaleUpdateResult.Ok(ToDto(setting));
    }

    public async Task<IReadOnlyList<GradeScaleHistoryItemDto>> GetHistoryAsync(int limit, CancellationToken ct = default)
    {
        var rows = await repository.GetHistoryAsync(Math.Clamp(limit, 1, 100), ct);
        return rows.Select(r =>
        {
            var (oldConfig, newConfig) = ReadDetail(r.Log.DetailJson);
            return new GradeScaleHistoryItemDto(r.Log.CreatedAt, r.Log.ActorUserId, r.ActorName, oldConfig, newConfig);
        }).ToList();
    }

    // Sắp theo mốc tăng dần, MaxScore = mốc kế tiếp − 0.01 (khoảng cao nhất tới 10), chỉ để hiển thị: tra GPA chỉ dùng MinScore
    private static GradeScaleConfigDto BuildConfig(UpdateGradeScaleRequest request, out List<string> errors)
    {
        errors = [];
        var inputs = (request.Rules ?? []).OrderBy(r => r.MinScore).ToList();
        var rules = new List<GradeScaleRuleDto>();
        for (var i = 0; i < inputs.Count; i++)
        {
            var letter = inputs[i].LetterGrade?.Trim() ?? string.Empty;
            if (letter.Length == 0 || letter.Length > MaxLetterGradeLength)
                errors.Add($"Điểm chữ của mốc {inputs[i].MinScore} phải có 1–{MaxLetterGradeLength} ký tự.");
            var max = i + 1 < inputs.Count ? inputs[i + 1].MinScore - 0.01m : 10m;
            rules.Add(new GradeScaleRuleDto(inputs[i].MinScore, Math.Max(max, inputs[i].MinScore), inputs[i].Gpa4, letter));
        }

        var sourceName = string.IsNullOrWhiteSpace(request.SourceName)
            ? AcademicAnalysisService.GetDefaultWesScaleConfig().SourceName
            : request.SourceName.Trim();
        return new GradeScaleConfigDto(AcademicConstants.GradeScale10AppSettingKey, sourceName, "table", rules);
    }

    private static GradeScaleSettingDto ToDto(AppSetting? setting)
    {
        var parsed = GradeScale.Parse(setting?.ValueJson);
        return new GradeScaleSettingDto(
            parsed ?? AcademicAnalysisService.GetDefaultWesScaleConfig(), VersionOf(setting), parsed is null);
    }

    private static DateTime? VersionOf(AppSetting? setting) => setting is null ? null : setting.UpdatedAt ?? setting.CreatedAt;

    // Postgres lưu tới micro giây, .NET tới 100 ns: so ở mức micro giây để Version vừa trả sau khi lưu vẫn khớp lần sau
    private static bool SameVersion(DateTime? current, DateTime? sent) =>
        current is null || sent is null
            ? current is null && sent is null
            : current.Value.ToUniversalTime().Ticks / 10 == sent.Value.ToUniversalTime().Ticks / 10;

    private static (GradeScaleConfigDto? Old, GradeScaleConfigDto? New) ReadDetail(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(json);
            return (Read(doc.RootElement, "old"), Read(doc.RootElement, "new"));
        }
        catch (JsonException)
        {
            return (null, null);
        }

        static GradeScaleConfigDto? Read(JsonElement root, string name) =>
            root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Object
                ? e.Deserialize<GradeScaleConfigDto>(JsonOptions)
                : null;
    }
}
