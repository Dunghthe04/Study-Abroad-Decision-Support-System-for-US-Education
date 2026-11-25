using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Auth;

namespace StudyAbroad.Infrastructure.BackgroundJobs;

/// <summary>
/// [UC-80] Chạy mỗi CheckInterval (mặc định 1 giờ): xóa hẳn tài khoản học sinh, phụ huynh
/// đã chờ xóa quá RetentionPeriod (mặc định 30 ngày), rồi dọn dữ liệu giữ lại đã tới hạn (UC-75). Lỗi cả lượt thì thử lại sau 5 phút.
/// </summary>
public class AccountPurgeBackgroundService(
    IServiceScopeFactory scopeFactory,
    AccountDeletionOptions options,
    ILogger<AccountPurgeBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AccountPurgeBackgroundService đã khởi động: chờ xóa {Retention}, chạy mỗi {Interval}.",
            options.RetentionPeriod, options.CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = options.CheckInterval;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAccountPurgeService>();
                var summary = await service.PurgeDueAccountsAsync(DateTime.UtcNow, stoppingToken);

                foreach (var failure in summary.Failures)
                    logger.LogError(failure.Error, "[Background Job Error] Xóa hẳn tài khoản {UserId} lỗi: {Message}",
                        failure.UserId, failure.Error.Message);

                foreach (var failure in summary.FileCleanupFailures)
                    logger.LogWarning(failure.Error, "[Background Job] Đã xóa tài khoản {UserId} nhưng xóa tệp trong uploads lỗi, cần dọn tay: {Message}",
                        failure.UserId, failure.Error.Message);

                if (summary.Purged > 0 || summary.Skipped > 0 || summary.Failures.Count > 0)
                    logger.LogInformation("[Background Job] Xóa hẳn tài khoản: {Purged} đã xóa, {Skipped} bỏ qua, {Failed} lỗi.",
                        summary.Purged, summary.Skipped, summary.Failures.Count);

                // [UC-75] Dữ liệu giữ lại của tài khoản đã xóa hẳn: tin nhắn quá 90 ngày, đơn hàng quá 10 năm
                var retained = await service.PurgeExpiredRetainedDataAsync(DateTime.UtcNow, stoppingToken);
                if (retained.Messages + retained.Leads + retained.Orders + retained.Buyers > 0)
                    logger.LogInformation("[Background Job] Dọn dữ liệu giữ lại: {Messages} tin, {Leads} yêu cầu liên hệ, {Orders} đơn, {Buyers} người mua.",
                        retained.Messages, retained.Leads, retained.Orders, retained.Buyers);

                // [UC-93] Trung tâm Admin đã duyệt xóa, tới hạn thì xóa hẳn (cùng thời hạn chờ)
                var centerService = scope.ServiceProvider.GetRequiredService<StudyAbroad.Application.StudyCenters.ICenterDeletionService>();
                var centerSummary = await centerService.PurgeDueAsync(DateTime.UtcNow, stoppingToken);
                foreach (var failure in centerSummary.Failures)
                    logger.LogError(failure.Error, "[Background Job Error] Xóa hẳn trung tâm theo yêu cầu {RequestId} lỗi: {Message}",
                        failure.RequestId, failure.Error.Message);
                if (centerSummary.Purged > 0)
                    logger.LogInformation("[Background Job] Xóa hẳn trung tâm: {Purged} đã xóa.", centerSummary.Purged);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Background Job Error] Lượt xóa hẳn tài khoản lỗi: {Message}", ex.Message);
                delay = TimeSpan.FromMinutes(5);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("AccountPurgeBackgroundService đang dừng.");
    }
}
