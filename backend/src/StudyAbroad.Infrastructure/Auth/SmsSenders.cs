using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Auth;

namespace StudyAbroad.Infrastructure.Auth;

/// <summary>
/// [UC-91] Bản gửi SMS cho môi trường phát triển: chỉ ghi mã OTP vào log máy chủ, không gửi tin thật và không trả mã qua API.
/// TODO(UC-91): thay bằng nhà cung cấp SMS thật khi có hợp đồng (eSMS, SpeedSMS, Twilio...).
/// </summary>
public sealed class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task SendOtpAsync(string phone, string code, int expiryMinutes, CancellationToken ct = default)
    {
        logger.LogWarning("[DEV SMS] OTP {Code} cho số {Phone} (hiệu lực {Minutes} phút). Chưa cấu hình nhà cung cấp SMS thật.", code, phone, expiryMinutes);
        return Task.CompletedTask;
    }
}

/// <summary>[UC-91] Dùng ở Production khi chưa có nhà cung cấp SMS: báo kênh chưa sẵn sàng thay vì âm thầm bỏ qua.</summary>
public sealed class UnavailableSmsSender : ISmsSender
{
    public Task SendOtpAsync(string phone, string code, int expiryMinutes, CancellationToken ct = default) =>
        throw new InvalidOperationException("Chưa cấu hình nhà cung cấp SMS.");
}
