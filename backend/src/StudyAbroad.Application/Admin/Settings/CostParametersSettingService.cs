using System.Text.Json;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Costs;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.Settings;

public record CostParametersSettingDto(CostParameters Values, DateTime? Version, bool IsDefault, CostParameters Defaults);

public record UpdateCostParametersRequest(
    decimal VisaIntegrityFeeUsd,
    decimal AnnualEscalationRate,
    decimal BankSellRateSpread,
    int UndergraduateYears,
    DateTime? Version);

public enum CostParametersUpdateStatus { Ok, Invalid, Conflict }

public record CostParametersUpdateResult(
    CostParametersUpdateStatus Status, CostParametersSettingDto? Value, IReadOnlyList<string> Errors)
{
    public static CostParametersUpdateResult Ok(CostParametersSettingDto value) =>
        new(CostParametersUpdateStatus.Ok, value, []);
    public static CostParametersUpdateResult Invalid(IReadOnlyList<string> errors) =>
        new(CostParametersUpdateStatus.Invalid, null, errors);
    public static CostParametersUpdateResult Conflict(CostParametersSettingDto current) =>
        new(CostParametersUpdateStatus.Conflict, current, ["Tham số chi phí vừa được người khác sửa. Hãy tải lại rồi lưu lại."]);
}

public interface ICostParametersSettingRepository
{
    Task<AppSetting?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(AppSetting setting, bool isNew, AuditLog audit, CancellationToken ct = default);
}

public interface ICostParametersSettingService
{
    Task<CostParametersSettingDto> GetAsync(CancellationToken ct = default);
    Task<CostParameters> GetCurrentParametersAsync(CancellationToken ct = default);
    Task<CostParametersUpdateResult> UpdateAsync(UpdateCostParametersRequest request, Guid actorUserId, CancellationToken ct = default);
}

/// <summary>Đọc/sửa 4 tham số ước tính chi phí UC-26 trong app_settings (cost.parameters) kèm optimistic concurrency và audit_logs.</summary>
public class CostParametersSettingService(ICostParametersSettingRepository repository) : ICostParametersSettingService
{
    public const string AuditAction = "cost_parameters.update";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<CostParametersSettingDto> GetAsync(CancellationToken ct = default) =>
        ToDto(await repository.GetAsync(ct));

    public async Task<CostParameters> GetCurrentParametersAsync(CancellationToken ct = default) =>
        CostParameters.Parse((await repository.GetAsync(ct))?.ValueJson);

    public async Task<CostParametersUpdateResult> UpdateAsync(
        UpdateCostParametersRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var next = new CostParameters(
            request.VisaIntegrityFeeUsd,
            request.AnnualEscalationRate,
            request.BankSellRateSpread,
            request.UndergraduateYears);
        var errors = CostParameters.Validate(next);
        if (errors.Count > 0) return CostParametersUpdateResult.Invalid(errors);

        var setting = await repository.GetAsync(ct);
        if (!SameVersion(VersionOf(setting), request.Version))
            return CostParametersUpdateResult.Conflict(ToDto(setting));

        var oldValues = string.IsNullOrWhiteSpace(setting?.ValueJson) ? null : CostParameters.Parse(setting.ValueJson);
        var isNew = setting is null;
        setting ??= new AppSetting
        {
            Key = CostConstants.CostParametersAppSettingKey,
            Description = "Tham số ước tính chi phí UC-26 (Visa Integrity Fee, tỷ lệ tăng chi phí hằng năm, biên độ giá bán ra ngân hàng, số năm học bậc đại học).",
        };
        setting.ValueJson = JsonSerializer.Serialize(next, JsonOptions);

        var audit = new AuditLog
        {
            ActorUserId = actorUserId,
            Action = AuditAction,
            EntityType = "app_setting",
            EntityId = CostConstants.CostParametersAppSettingKey,
            DetailJson = JsonSerializer.Serialize(new { old = oldValues, @new = next }, JsonOptions),
        };
        try
        {
            await repository.SaveAsync(setting, isNew, audit, ct);
        }
        catch (ConcurrencyConflictException)
        {
            return CostParametersUpdateResult.Conflict(ToDto(await repository.GetAsync(ct)));
        }
        return CostParametersUpdateResult.Ok(ToDto(setting));
    }

    private static CostParametersSettingDto ToDto(AppSetting? setting)
    {
        var hasStored = !string.IsNullOrWhiteSpace(setting?.ValueJson);
        return new CostParametersSettingDto(
            CostParameters.Parse(setting?.ValueJson),
            VersionOf(setting),
            !hasStored,
            CostParameters.Default);
    }

    private static DateTime? VersionOf(AppSetting? setting) => setting is null ? null : setting.UpdatedAt ?? setting.CreatedAt;

    // Postgres lưu tới micro giây, .NET tới 100 ns: so ở mức micro giây giống GradeScaleSettingService
    private static bool SameVersion(DateTime? current, DateTime? sent) =>
        current is null || sent is null
            ? current is null && sent is null
            : current.Value.ToUniversalTime().Ticks / 10 == sent.Value.ToUniversalTime().Ticks / 10;
}
