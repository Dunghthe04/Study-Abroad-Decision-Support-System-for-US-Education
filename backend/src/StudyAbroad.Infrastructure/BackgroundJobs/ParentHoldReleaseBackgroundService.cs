using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Leads;

namespace StudyAbroad.Infrastructure.BackgroundJobs;

/// <summary>
/// [UC-110 / UC-150] Chạy lúc khởi động rồi mỗi ngày: học sinh vừa đủ 16 thì yêu cầu liên hệ còn chờ phụ huynh chuyển Mới gửi
/// (yêu cầu đang giữ trở lại trạng thái cũ); yêu cầu đang giữ quá 7 ngày chưa có phụ huynh xác nhận thì thu hồi, báo học sinh và trung tâm.
/// Lỗi cả lượt thì thử lại sau 5 phút.
/// </summary>
public class ParentHoldReleaseBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ParentHoldReleaseBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ParentHoldReleaseBackgroundService đã khởi động (chu kỳ {Interval}).", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = Interval;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ILeadParentHoldService>();
                var summary = await service.RunDailyAsync(DateTime.UtcNow, stoppingToken);
                if (summary.Released + summary.Withdrawn > 0)
                    logger.LogInformation("[Background Job] Yêu cầu liên hệ chờ phụ huynh: {Released} đã trả trung tâm, {Withdrawn} đã thu hồi.",
                        summary.Released, summary.Withdrawn);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Background Job Error] Lượt xử lý yêu cầu liên hệ chờ phụ huynh lỗi: {Message}", ex.Message);
                delay = TimeSpan.FromMinutes(5);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
