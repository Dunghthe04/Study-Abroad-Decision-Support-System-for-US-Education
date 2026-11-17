using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Auth;

namespace StudyAbroad.Infrastructure.Auth
{
    /// <summary>
    /// [UC-91] Bộ gửi SMS tạm thời khi chưa có nhà cung cấp SMS: không gửi tin thật, chỉ ghi mã OTP ra log ở môi trường phát triển
    /// để thử luồng. Ngoài môi trường phát triển, <see cref="CanDeliver"/> là false nên không ai nhận được mã và hệ thống báo
    /// tính năng chưa sẵn sàng thay vì giả vờ đã gửi. Thay lớp này bằng cài đặt gọi nhà cung cấp SMS khi có.
    /// </summary>
    public class LoggingSmsSender(IHostEnvironment environment, ILogger<LoggingSmsSender> logger) : ISmsSender
    {
        public bool CanDeliver => environment.IsDevelopment();

        public Task SendOtpAsync(string phoneNumber, string otpCode, int expiryMinutes, CancellationToken ct = default)
        {
            if (!CanDeliver)
                throw new InvalidOperationException("Chưa cấu hình nhà cung cấp SMS.");

            // Chỉ ở môi trường phát triển: mã OTP nằm trong log để người phát triển thử luồng
            logger.LogInformation("[SMS giả lập] Mã OTP xác thực số điện thoại {Phone}: {Code} (hiệu lực {Minutes} phút)",
                phoneNumber, otpCode, expiryMinutes);
            return Task.CompletedTask;
        }
    }
}
