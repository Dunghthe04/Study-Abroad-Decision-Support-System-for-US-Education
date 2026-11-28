using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyAbroad.Application.BirthDateChanges;

namespace StudyAbroad.Infrastructure.BackgroundJobs;

/// <summary>
/// [UC-107 / UC-150] Mỗi ngày một lần (lần đầu 10 phút sau khởi động): xóa minh chứng ngày sinh trên Cloudinary
/// không còn thuộc yêu cầu Chờ duyệt nào. Lỗi cả lượt thì thử lại sau 1 giờ. Nhiều bản api cùng chạy cũng không sao:
/// xóa tệp đã mất không báo lỗi.
/// </summary>
public class BirthDateEvidenceSweepBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<BirthDateEvidenceSweepBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan FirstDelay = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = FirstDelay;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            delay = Interval;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sweeper = scope.ServiceProvider.GetRequiredService<IBirthDateEvidenceSweeper>();
                var result = await sweeper.SweepAsync(DateTime.UtcNow, stoppingToken);
                if (result.Deleted > 0)
                    logger.LogInformation("[Background Job] Dọn minh chứng ngày sinh: xóa {Deleted}/{Listed} tệp không thuộc yêu cầu nào.",
                        result.Deleted, result.Listed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Background Job] Lượt dọn minh chứng ngày sinh lỗi, thử lại sau 1 giờ: {Message}", ex.Message);
                delay = RetryDelay;
            }
        }
    }
}
