using System.Text.Json;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin;

/// <summary>Dùng chung cho các màn admin sửa dữ liệu trường và học bổng.</summary>
internal static class AdminEditHelpers
{
    public static readonly string[] SourceReliabilities = ["official", "aggregated", "self_reported"];

    public static void AddChange<T>(Dictionary<string, object?[]> changes, string field, T oldValue, T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
            changes[field] = [oldValue, newValue];
    }

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static AuditLog AuditEntry(Guid actorUserId, string action, string entityType, Guid entityId, object detail) => new()
    {
        ActorUserId = actorUserId,
        Action = action,
        EntityType = entityType,
        EntityId = entityId.ToString(),
        DetailJson = JsonSerializer.Serialize(detail),
    };

    /// <summary>Mọi số liệu phải có nguồn http(s) và ngày lấy, không ở tương lai.</summary>
    public static void ValidateSource(string? sourceUrl, DateOnly? retrievedAt, string? sourceReliability, Dictionary<string, string> errors)
    {
        if (sourceUrl is null)
            errors["sourceUrl"] = "Cần nhập đường dẫn nguồn.";
        else if (sourceUrl.Length > 512 || !IsHttpUrl(sourceUrl))
            errors["sourceUrl"] = "Nguồn phải là đường dẫn http(s), tối đa 512 ký tự.";

        // ponytail: +1 ngày để giờ Việt Nam (UTC+7) nhập "hôm nay" không bị coi là tương lai.
        if (retrievedAt is null)
            errors["retrievedAt"] = "Cần nhập ngày lấy dữ liệu.";
        else if (retrievedAt > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            errors["retrievedAt"] = "Ngày lấy dữ liệu không được ở tương lai.";

        if (sourceReliability is not null && !SourceReliabilities.Contains(sourceReliability))
            errors["sourceReliability"] = "Mức tin cậy chỉ nhận official, aggregated hoặc self_reported.";
    }

    /// <summary>Ẩn bắt buộc có lý do; hiện lại thì lý do không bắt buộc.</summary>
    public static Dictionary<string, string> ValidateVisibilityReason(bool isActive, string? reason)
    {
        var errors = new Dictionary<string, string>();
        if (!isActive && reason is null)
            errors["reason"] = "Cần nhập lý do ẩn.";
        else if (reason is { Length: > 500 })
            errors["reason"] = "Lý do tối đa 500 ký tự.";
        return errors;
    }
}
