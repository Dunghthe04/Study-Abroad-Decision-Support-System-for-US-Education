using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Auth;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// [Scheduled: Daily at 3:00 AM]
    /// Tác vụ nền tự động dọn dẹp các mã OTP đã hết hạn khỏi cơ sở dữ liệu.
    /// </summary>
    public class OtpCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OtpCleanupBackgroundService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("OtpCleanupBackgroundService đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Tính thời gian chờ tới 3:00 AM tiếp theo
                    var now = DateTime.UtcNow;
                    var nextRun = now.Date.AddHours(3);
                    if (now >= nextRun)
                    {
                        nextRun = nextRun.AddDays(1);
                    }

                    var delay = nextRun - now;
                    logger.LogInformation("[Background Job] OtpCleanupBackgroundService sẽ chạy lần tiếp theo lúc {NextRun:yyyy-MM-dd HH:mm:ss} UTC (sau {DelayTotalHours:F1} giờ).", nextRun, delay.TotalHours);

                    await Task.Delay(delay, stoppingToken);

                    using var scope = scopeFactory.CreateScope();
                    var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

                    var deletedCount = await users.CleanupExpiredOtpsAsync(stoppingToken);
                    logger.LogInformation("[Background Job] Dọn dẹp định kỳ 3:00 AM: Đã xóa {Count} mã OTP đã hết hạn.", deletedCount);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[Background Job Error] Lỗi khi dọn dẹp OTP hết hạn: {Message}", ex.Message);
                    // Chờ 5 phút rồi thử lại nếu gặp lỗi
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }

            logger.LogInformation("OtpCleanupBackgroundService đang dừng.");
        }
    }
}
