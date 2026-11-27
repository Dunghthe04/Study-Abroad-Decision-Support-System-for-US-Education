using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyAbroad.Infrastructure.Auth;

namespace StudyAbroad.Infrastructure.BackgroundJobs;

/// <summary>
/// Gửi mail trong email_outbox: có tín hiệu thì gửi ngay, không thì 30 giây quét một lần.
/// Tối đa 2 mail/giây; SMTP báo giới hạn thì cả job dừng 1 phút.
/// </summary>
public sealed class EmailOutboxBackgroundService(
    EmailOutboxProcessor processor,
    EmailOutboxSignal signal,
    ILogger<EmailOutboxBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan BetweenMails = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan IdleScan = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AfterDbError = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("EmailOutboxBackgroundService đã khởi động.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await processor.ProcessNextAsync(DateTime.UtcNow, stoppingToken);
                switch (result)
                {
                    case OutboxStepResult.Idle:
                        await signal.WaitAsync(IdleScan, stoppingToken);
                        break;
                    case OutboxStepResult.Throttled:
                        logger.LogWarning("SMTP đang giới hạn tốc độ, tạm dừng gửi mail {Seconds} giây", EmailOutboxProcessor.ThrottlePause.TotalSeconds);
                        await Task.Delay(EmailOutboxProcessor.ThrottlePause, stoppingToken);
                        break;
                    default:
                        await Task.Delay(BetweenMails, stoppingToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Thường là DB chập: mail vẫn nằm trong bảng, lát quét lại. Chỉ ghi loại lỗi
                logger.LogError("Job gửi mail lỗi ({Error}), thử lại sau {Seconds} giây", ex.GetType().Name, AfterDbError.TotalSeconds);
                try { await Task.Delay(AfterDbError, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
        logger.LogInformation("EmailOutboxBackgroundService đang dừng.");
    }
}
