namespace StudyAbroad.Application.Leads;

/// <summary>
/// UC-42: vi phạm quy tắc khi gửi yêu cầu liên hệ. Controller trả <c>{ code, message }</c> với <see cref="Status"/>.
/// <paramref name="RetryAfterUtc"/>: lúc được gửi lại, chỉ có khi lỗi 429 center_cooldown.
/// </summary>
public sealed class LeadRuleException(string code, int status, string message, DateTime? retryAfterUtc = null)
    : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public DateTime? RetryAfterUtc { get; } = retryAfterUtc;
}
