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
    /// [Scheduled: Every 15 minutes]
    /// Tác vụ nền tự động mở khóa các tài khoản bị tạm khóa sau 15 phút do nhập sai mật khẩu 5 lần liên tiếp.
    /// </summary>
    public class AccountUnlockBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AccountUnlockBackgroundService> logger) : BackgroundService
    {
        // Chạy kiểm tra mỗi 1 phút để mở khóa chính xác ngay khi tài khoản hết hạn 15 phút
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("AccountUnlockBackgroundService đã khởi động (chu kỳ kiểm tra: {Interval}).", Interval);

            using var timer = new PeriodicTimer(Interval);
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

                    var unlockedCount = await users.UnlockExpiredAccountsAsync(stoppingToken);
                    if (unlockedCount > 0)
                    {
                        logger.LogInformation("[Background Job] Đã tự động mở khóa thành công {Count} tài khoản sau 15 phút tạm khóa.", unlockedCount);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[Background Job Error] Lỗi trong quá trình tự động mở khóa tài khoản: {Message}", ex.Message);
                }
            }

            logger.LogInformation("AccountUnlockBackgroundService đang dừng.");
        }
    }
}
