using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;
using Xunit;

namespace StudyAbroad.UnitTests;

public class AccessLogServiceTests
{
    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public AccessLogFilter? LastFilter { get; private set; }
        public DateTime? LastSummaryFrom { get; private set; }
        public DateTime? LastSummaryTo { get; private set; }

        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<PagedResult<AccessLogDto>> GetAccessLogsAsync(AccessLogFilter filter, CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            return Task.FromResult(new PagedResult<AccessLogDto>([], filter.Page, filter.PageSize, 0));
        }

        public Task<AccessLogSummaryDto> GetAccessLogSummaryAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
        {
            LastSummaryFrom = fromUtc;
            LastSummaryTo = toUtc;
            return Task.FromResult(new AccessLogSummaryDto(10, 3, 5, 2));
        }
    }

    [Fact]
    public async Task GetAccessLogsAsync_NormalizesPagingAndDefaultsCategory()
    {
        var repo = new FakeAuditLogRepository();
        var service = new AccessLogService(repo);

        await service.GetAccessLogsAsync(
            category: null,
            action: null,
            search: "  admin test  ",
            actorUserId: null,
            actorRole: null,
            from: null,
            to: null,
            page: 0,
            pageSize: 500
        );

        Assert.NotNull(repo.LastFilter);
        Assert.Equal(1, repo.LastFilter.Page);
        Assert.Equal(100, repo.LastFilter.PageSize);
        Assert.Equal(AuditLogConstants.CategoryAllSensitive, repo.LastFilter.Category);
        Assert.Equal("admin test", repo.LastFilter.Search);
    }

    [Fact]
    public async Task GetAccessLogsAsync_ComputesVietnamOffsetDateRange()
    {
        var repo = new FakeAuditLogRepository();
        var service = new AccessLogService(repo);

        var fromDay = new DateOnly(2026, 11, 10);
        var toDay = new DateOnly(2026, 11, 15);

        await service.GetAccessLogsAsync(
            category: "slm_export",
            from: fromDay,
            to: toDay,
            page: 1,
            pageSize: 20
        );

        Assert.NotNull(repo.LastFilter);
        Assert.Equal("slm_export", repo.LastFilter.Category);
        Assert.NotNull(repo.LastFilter.FromUtc);
        Assert.NotNull(repo.LastFilter.ToUtc);

        // 2026-11-10 00:00:00 VN = 2026-11-09 17:00:00 UTC
        var expectedFromUtc = new DateTime(2026, 11, 9, 17, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedFromUtc, repo.LastFilter.FromUtc.Value);

        // to: 2026-11-15 + 1 day = 2026-11-16 00:00:00 VN = 2026-11-15 17:00:00 UTC
        var expectedToUtc = new DateTime(2026, 11, 15, 17, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedToUtc, repo.LastFilter.ToUtc.Value);
    }

    [Fact]
    public async Task GetSummaryAsync_ComputesVietnamOffsetAndCallsRepo()
    {
        var repo = new FakeAuditLogRepository();
        var service = new AccessLogService(repo);

        var fromDay = new DateOnly(2026, 11, 1);
        var toDay = new DateOnly(2026, 11, 20);

        var summary = await service.GetSummaryAsync(fromDay, toDay);

        Assert.Equal(10, summary.TotalSensitiveCount);
        Assert.Equal(3, summary.CenterDocumentViews);
        Assert.Equal(5, summary.ReportedMessageViews);
        Assert.Equal(2, summary.SlmSampleExports);

        var expectedFromUtc = new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc);
        var expectedToUtc = new DateTime(2026, 11, 20, 17, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedFromUtc, repo.LastSummaryFrom);
        Assert.Equal(expectedToUtc, repo.LastSummaryTo);
    }

    [Fact]
    public void Repository_AuditLogQuery_TranslatesToSql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);

        var query = from log in db.AuditLogs.AsNoTracking()
                    join u in db.Users.AsNoTracking() on log.ActorUserId equals (Guid?)u.Id into userGroup
                    from actor in userGroup.DefaultIfEmpty()
                    where AuditLogConstants.SensitiveActions.Contains(log.Action)
                    select new { log, actor };

        var sql = query.ToQueryString();
        Assert.Contains("audit_logs", sql);
        Assert.Contains("users", sql);
        Assert.Contains("action", sql);
    }

    [Fact]
    public async Task GetAccessLogsAsync_ClampsMaxPageToPreventIntegerOverflow()
    {
        var repo = new FakeAuditLogRepository();
        var service = new AccessLogService(repo);

        await service.GetAccessLogsAsync(page: int.MaxValue, pageSize: 50);

        Assert.NotNull(repo.LastFilter);
        Assert.Equal(100_000, repo.LastFilter.Page);
    }

    [Fact]
    public async Task GetAccessLogsAsync_SwapsReversedDateRange()
    {
        var repo = new FakeAuditLogRepository();
        var service = new AccessLogService(repo);

        var earlierDay = new DateOnly(2026, 11, 1);
        var laterDay = new DateOnly(2026, 11, 20);

        // Truyền ngược: from = 20/11, to = 01/11
        await service.GetAccessLogsAsync(from: laterDay, to: earlierDay);

        Assert.NotNull(repo.LastFilter);
        Assert.True(repo.LastFilter.FromUtc <= repo.LastFilter.ToUtc);
    }
}
