using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyAbroad.Application.ParentLinks;

namespace StudyAbroad.Infrastructure.BackgroundJobs;

/// <summary>
/// [1.5d] Chạy một lần lúc khởi động: phụ huynh còn hồ sơ riêng từ trước 1.5d thì chuyển sang con managed "Con của bạn"
/// rồi xóa hồ sơ của phụ huynh. Không còn ai thì không làm gì, nên để chạy mỗi lần khởi động cũng không sao.
/// </summary>
public class ParentProfileMigrationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ParentProfileMigrationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IParentProfileShareService>();
            var migrated = await service.MigrateLegacyParentProfilesAsync(stoppingToken);
            if (migrated > 0)
                logger.LogInformation("[Background Job] Đã chuyển hồ sơ riêng của {Count} phụ huynh sang con managed.", migrated);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // Lỗi thì lần khởi động sau chạy lại; phụ huynh chưa chuyển vẫn giữ hồ sơ
            logger.LogError(ex, "[Background Job Error] Chuyển hồ sơ riêng của phụ huynh lỗi: {Message}", ex.Message);
        }
    }
}
