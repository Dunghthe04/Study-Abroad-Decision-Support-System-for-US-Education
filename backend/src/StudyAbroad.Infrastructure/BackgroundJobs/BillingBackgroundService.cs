using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Billing.Jobs;

namespace StudyAbroad.Infrastructure.BackgroundJobs;

/// <summary>
/// Job nền thanh toán: mỗi <see cref="BillingOptions.JobIntervalSeconds"/> giây chạy lần lượt năm bước của <see cref="IBillingJobService"/>
/// (đối soát đơn, chuyển kỳ, hết hạn lời mời Gia đình, nhắc gia hạn, thông báo dùng hạn mức). Mỗi bước có scope DI và try/catch riêng nên
/// một bước lỗi (kể cả lỗi lưu làm hỏng DbContext) không kéo các bước sau hỏng theo.
/// </summary>
public class BillingBackgroundService(
    IServiceScopeFactory scopeFactory,
    BillingOptions options,
    ILogger<BillingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.JobIntervalSeconds));
        logger.LogInformation("BillingBackgroundService đã khởi động: chạy mỗi {Interval}.", interval);

        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RunTickAsync(DateTime.UtcNow, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        logger.LogInformation("BillingBackgroundService đang dừng.");
    }

    /// <summary>Một lượt: năm bước theo thứ tự, mỗi bước một scope mới. Lỗi một bước chỉ ghi log. Công khai để test.</summary>
    public async Task RunTickAsync(DateTime nowUtc, CancellationToken ct)
    {
        await RunStepAsync("đối soát đơn", (job, t) => job.ReconcilePendingOrdersAsync(nowUtc, t), ct);
        await RunStepAsync("chuyển kỳ", (job, t) => job.AdvanceDueLanesAsync(nowUtc, t), ct);
        await RunStepAsync("hết hạn lời mời Gia đình", (job, t) => job.ExpireFamilyInvitesAsync(nowUtc, t), ct);
        await RunStepAsync("nhắc gia hạn", (job, t) => job.SendRenewalRemindersAsync(nowUtc, t), ct);
        await RunStepAsync("thông báo hạn mức", (job, t) => job.SendUsageNotificationsAsync(nowUtc, t), ct);
    }

    private async Task RunStepAsync(string name, Func<IBillingJobService, CancellationToken, Task<int>> step, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var handled = await step(scope.ServiceProvider.GetRequiredService<IBillingJobService>(), ct);
            if (handled > 0)
                logger.LogInformation("[Billing job] {Step}: {Handled} mục.", name, handled);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Background Job Error] Bước {Step} của job thanh toán lỗi: {Message}", name, ex.Message);
        }
    }
}
