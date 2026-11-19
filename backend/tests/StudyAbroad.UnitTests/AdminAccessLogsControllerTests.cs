using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

public class AdminAccessLogsControllerTests
{
    private sealed class FakeAccessLogService : IAccessLogService
    {
        public AccessLogFilter? LastFilter { get; private set; }
        public PagedResult<AccessLogDto> LogsResult { get; set; } = new([], 1, 20, 0);
        public AccessLogSummaryDto SummaryResult { get; set; } = new(0, 0, 0, 0);

        public Task<PagedResult<AccessLogDto>> GetAccessLogsAsync(
            string? category = null,
            string? action = null,
            string? search = null,
            Guid? actorUserId = null,
            string? actorRole = null,
            DateOnly? from = null,
            DateOnly? to = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default)
        {
            LastFilter = new AccessLogFilter(category, action, search, actorUserId, actorRole, null, null, page, pageSize);
            return Task.FromResult(LogsResult);
        }

        public Task<AccessLogSummaryDto> GetSummaryAsync(
            DateOnly? from = null,
            DateOnly? to = null,
            CancellationToken ct = default)
        {
            return Task.FromResult(SummaryResult);
        }
    }

    [Fact]
    public void Controller_MustRequireAdminRoleOnly()
    {
        var authAttr = typeof(AdminAccessLogsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(UserRoles.Admin, authAttr.Roles);
    }

    [Fact]
    public void Controller_MustOnlyHaveGetMethods_RuleImmutableLogs()
    {
        var methods = typeof(AdminAccessLogsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName);

        foreach (var method in methods)
        {
            var httpGet = method.GetCustomAttribute<HttpGetAttribute>();
            var httpPost = method.GetCustomAttribute<HttpPostAttribute>();
            var httpPut = method.GetCustomAttribute<HttpPutAttribute>();
            var httpDelete = method.GetCustomAttribute<HttpDeleteAttribute>();
            var httpPatch = method.GetCustomAttribute<HttpPatchAttribute>();

            Assert.NotNull(httpGet);
            Assert.Null(httpPost);
            Assert.Null(httpPut);
            Assert.Null(httpDelete);
            Assert.Null(httpPatch);
        }
    }

    [Fact]
    public async Task GetLogs_ReturnsOkWithPagedResult()
    {
        var fake = new FakeAccessLogService();
        var sampleDto = new AccessLogDto(
            Id: Guid.NewGuid(),
            CreatedAt: DateTime.UtcNow,
            ActorUserId: Guid.NewGuid(),
            ActorFullName: "Admin Test",
            ActorEmail: "admin@test.com",
            ActorRole: UserRoles.Admin,
            Action: AuditLogConstants.CenterDocumentViewed,
            ActionCategory: AuditLogConstants.CategoryCenterDocument,
            ActionDescription: "Xem tài liệu xác minh trung tâm",
            EntityType: "CenterVerificationDocument",
            EntityId: Guid.NewGuid().ToString(),
            DetailJson: "{}",
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0"
        );
        fake.LogsResult = new PagedResult<AccessLogDto>([sampleDto], 1, 20, 1);

        var controller = new AdminAccessLogsController(fake);
        var actionResult = await controller.GetLogs(
            category: "center_document",
            action: null,
            search: "admin",
            actorUserId: null,
            actorRole: null,
            from: new DateOnly(2026, 11, 1),
            to: new DateOnly(2026, 11, 20),
            page: 1,
            pageSize: 20
        );

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var paged = Assert.IsType<PagedResult<AccessLogDto>>(ok.Value);
        Assert.Single(paged.Items);
        Assert.Equal(AuditLogConstants.CenterDocumentViewed, paged.Items[0].Action);
        Assert.Equal("center_document", fake.LastFilter?.Category);
    }

    [Fact]
    public async Task GetSummary_ReturnsOkWithSummaryDto()
    {
        var fake = new FakeAccessLogService
        {
            SummaryResult = new AccessLogSummaryDto(15, 5, 8, 2)
        };

        var controller = new AdminAccessLogsController(fake);
        var actionResult = await controller.GetSummary(
            from: new DateOnly(2026, 11, 1),
            to: new DateOnly(2026, 11, 20)
        );

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var summary = Assert.IsType<AccessLogSummaryDto>(ok.Value);
        Assert.Equal(15, summary.TotalSensitiveCount);
        Assert.Equal(5, summary.CenterDocumentViews);
        Assert.Equal(8, summary.ReportedMessageViews);
        Assert.Equal(2, summary.SlmSampleExports);
    }
}
