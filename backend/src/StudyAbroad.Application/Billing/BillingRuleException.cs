namespace StudyAbroad.Application.Billing;

/// <summary>
/// Vi phạm quy tắc thanh toán / gói dịch vụ. Api trả <c>{ code, message, ...extra }</c> với <see cref="StatusCode"/>.
/// <paramref name="extra"/>: các trường kèm theo trong body (khóa dùng nguyên văn, nên truyền camelCase như <c>orderCode</c>).
/// </summary>
public sealed class BillingRuleException(string code, int statusCode, string message, IReadOnlyDictionary<string, object?>? extra = null)
    : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public IReadOnlyDictionary<string, object?> Extra { get; } = extra ?? new Dictionary<string, object?>();
}
