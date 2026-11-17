using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[USAS-2079 / UC-57] Moderator gỡ đánh giá trung tâm vi phạm quy tắc.</summary>
public class CenterReviewModerationTests
{
    private sealed class FakeModerationRepo : ICenterReviewModerationRepository
    {
        public List<CenterReview> Reviews { get; } = [];
        public int Saves { get; private set; }

        public Task<CenterReview?> GetForUpdateAsync(Guid centerId, Guid reviewId, CancellationToken ct = default) =>
            Task.FromResult(Reviews.FirstOrDefault(r => r.Id == reviewId && r.StudyCenterId == centerId));

        public Task SaveAsync(CenterReview review, CancellationToken ct = default)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAudit : IAuditLogRepository
    {
        public List<AuditLog> Logs { get; } = [];
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
        {
            Logs.Add(entry);
            return Task.CompletedTask;
        }
        public Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private static (CenterReviewModerationService Service, FakeModerationRepo Repo, FakeAudit Audit) Create()
    {
        var repo = new FakeModerationRepo();
        var audit = new FakeAudit();
        return (new CenterReviewModerationService(repo, audit), repo, audit);
    }

    private static CenterReview NewReview(Guid centerId, string status = "visible") => new()
    {
        StudyCenterId = centerId,
        UserId = Guid.NewGuid(),
        Rating = 1,
        Comment = "Nội dung vi phạm",
        Status = status,
    };

    [Fact]
    public async Task Remove_VisibleReview_MarksRemovedKeepsOriginalAndLogsAudit()
    {
        var (service, repo, audit) = Create();
        var centerId = Guid.NewGuid();
        var review = NewReview(centerId);
        repo.Reviews.Add(review);
        var moderatorId = Guid.NewGuid();

        var result = await service.RemoveAsync(moderatorId, centerId, review.Id, "  Ngôn từ xúc phạm  ");

        Assert.Equal("removed", review.Status);
        Assert.Equal("Nội dung vi phạm", review.Comment); // bản gốc vẫn được giữ
        Assert.NotNull(review.UpdatedAt);
        Assert.Equal(new CenterReviewRemovalDto(review.Id, centerId, "removed"), result);
        var log = Assert.Single(audit.Logs);
        Assert.Equal("center_review.remove", log.Action);
        Assert.Equal(moderatorId, log.ActorUserId);
        Assert.Equal(review.Id.ToString(), log.EntityId);
        using var detail = JsonDocument.Parse(log.DetailJson!);
        Assert.Equal("Ngôn từ xúc phạm", detail.RootElement.GetProperty("reason").GetString());
        Assert.Equal("visible", detail.RootElement.GetProperty("previousStatus").GetString());
        Assert.Equal(1, detail.RootElement.GetProperty("rating").GetInt32());
    }

    [Fact]
    public async Task Remove_HiddenReview_AlsoBecomesRemoved()
    {
        var (service, repo, _) = Create();
        var centerId = Guid.NewGuid();
        var review = NewReview(centerId, "hidden");
        repo.Reviews.Add(review);

        await service.RemoveAsync(Guid.NewGuid(), centerId, review.Id, "Vi phạm quy tắc");

        Assert.Equal("removed", review.Status);
    }

    [Fact]
    public async Task Remove_AlreadyRemoved_IsIdempotentWithoutSecondAuditOrSave()
    {
        var (service, repo, audit) = Create();
        var centerId = Guid.NewGuid();
        var review = NewReview(centerId, "removed");
        repo.Reviews.Add(review);

        var result = await service.RemoveAsync(Guid.NewGuid(), centerId, review.Id, "Vi phạm quy tắc");

        Assert.Equal("removed", result.Status);
        Assert.Empty(audit.Logs);
        Assert.Equal(0, repo.Saves);
    }

    [Fact]
    public async Task Remove_ReviewOfAnotherCenter_ThrowsKeyNotFoundAndStaysVisible()
    {
        var (service, repo, audit) = Create();
        var review = NewReview(Guid.NewGuid());
        repo.Reviews.Add(review);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RemoveAsync(Guid.NewGuid(), Guid.NewGuid(), review.Id, "Vi phạm quy tắc"));

        Assert.Equal("visible", review.Status);
        Assert.Empty(audit.Logs);
    }

    [Fact]
    public async Task Remove_UnknownReview_ThrowsKeyNotFound()
    {
        var (service, _, _) = Create();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RemoveAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Vi phạm quy tắc"));
    }

    [Fact]
    public async Task Remove_EmptyIdsOrTooLongReason_ThrowsArgumentExceptionAndChangesNothing()
    {
        var (service, repo, _) = Create();
        var centerId = Guid.NewGuid();
        var review = NewReview(centerId);
        repo.Reviews.Add(review);

        await Assert.ThrowsAsync<ArgumentException>(() => service.RemoveAsync(Guid.NewGuid(), Guid.Empty, review.Id, "Vi phạm quy tắc"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RemoveAsync(Guid.NewGuid(), centerId, Guid.Empty, "Vi phạm quy tắc"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RemoveAsync(Guid.NewGuid(), centerId, review.Id, new string('a', CenterReviewModerationService.MaxReasonLength + 1)));
        Assert.Equal("visible", review.Status);
    }

    [Fact]
    public async Task Remove_MissingOrBlankReason_IsRejectedAndChangesNothing()
    {
        var (service, repo, audit) = Create();
        var centerId = Guid.NewGuid();
        var review = NewReview(centerId);
        repo.Reviews.Add(review);

        foreach (var reason in new string?[] { null, "", "   " })
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.RemoveAsync(Guid.NewGuid(), centerId, review.Id, reason));
            Assert.Contains("lý do", ex.Message);
        }

        Assert.Equal("visible", review.Status);
        Assert.Empty(audit.Logs);
    }

    // ---- controller

    private sealed class FakeService : ICenterReviewModerationService
    {
        public Exception? ToThrow { get; set; }
        public (Guid ModeratorId, Guid CenterId, Guid ReviewId, string? Reason)? LastRemove { get; private set; }

        public Task<CenterReviewRemovalDto> RemoveAsync(Guid moderatorUserId, Guid centerId, Guid reviewId, string? reason, CancellationToken ct = default)
        {
            if (ToThrow is not null) throw ToThrow;
            LastRemove = (moderatorUserId, centerId, reviewId, reason);
            return Task.FromResult(new CenterReviewRemovalDto(reviewId, centerId, "removed"));
        }
    }

    private static (CenterReviewModerationController Controller, FakeService Service) CreateController(Guid userId, bool authenticated = true)
    {
        var service = new FakeService();
        var identity = authenticated
            ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, UserRoles.Moderator)], "TestAuth")
            : new ClaimsIdentity();
        var controller = new CenterReviewModerationController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } }
        };
        return (controller, service);
    }

    [Fact]
    public async Task Controller_Remove_ReturnsOkAndPassesArguments()
    {
        var moderatorId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var (controller, service) = CreateController(moderatorId);

        var result = await controller.Remove(centerId, reviewId, new RemoveCenterReviewRequest("Spam"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("removed", Assert.IsType<CenterReviewRemovalDto>(ok.Value).Status);
        Assert.Equal((moderatorId, centerId, reviewId, "Spam"), service.LastRemove);
    }

    [Fact]
    public async Task Controller_Remove_WithoutBody_PassesNullReason()
    {
        var (controller, service) = CreateController(Guid.NewGuid());

        await controller.Remove(Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None);

        Assert.Null(service.LastRemove!.Value.Reason);
    }

    [Fact]
    public async Task Controller_Remove_MapsErrors()
    {
        var (controller, service) = CreateController(Guid.NewGuid());

        service.ToThrow = new KeyNotFoundException("Không tìm thấy đánh giá.");
        Assert.IsType<NotFoundObjectResult>(await controller.Remove(Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None));

        service.ToThrow = new ArgumentException("Lý do gỡ quá dài.");
        Assert.IsAssignableFrom<ProblemDetails>(Assert.IsType<ObjectResult>(await controller.Remove(Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None)).Value);

        var (anonymous, _) = CreateController(Guid.Empty, authenticated: false);
        Assert.IsType<UnauthorizedObjectResult>(await anonymous.Remove(Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None));
    }

    [Fact]
    public void Controller_IsRestrictedToModerator()
    {
        var roles = typeof(CenterReviewModerationController).GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single().Roles;

        Assert.Equal(UserRoles.Moderator, roles);
    }
}
