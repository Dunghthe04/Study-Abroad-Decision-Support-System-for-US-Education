using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[USAS-2070 / UC-45] Học sinh / phụ huynh đã gửi yêu cầu liên hệ thì đánh giá trung tâm 1 đến 5 sao.</summary>
public class CenterReviewSubmissionTests
{
    private sealed class FakeSubmissionRepo : ICenterReviewSubmissionRepository
    {
        public List<Lead> Leads { get; } = [];
        public List<CenterReview> Reviews { get; } = [];

        private static readonly string[] AcceptedStatuses = [LeadStatuses.Accepted, LeadStatuses.Contacted, LeadStatuses.Closed];

        public Task<Lead?> FindLatestLeadAsync(Guid userId, Guid centerId, CancellationToken ct = default) =>
            Task.FromResult(Leads
                .Where(l => l.StudyCenterId == centerId && (l.UserId == userId || l.SubmittedByUserId == userId))
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefault());

        public Task<Lead?> FindLatestAcceptedLeadAsync(Guid userId, Guid centerId, CancellationToken ct = default) =>
            Task.FromResult(Leads
                .Where(l => l.StudyCenterId == centerId && (l.UserId == userId || l.SubmittedByUserId == userId))
                .Where(l => AcceptedStatuses.Contains(l.Status))
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefault());

        public Task<Lead?> FindLatestUnreviewedLeadAsync(Guid userId, Guid centerId, CancellationToken ct = default) =>
            Task.FromResult(Leads
                .Where(l => l.StudyCenterId == centerId && (l.UserId == userId || l.SubmittedByUserId == userId))
                .Where(l => AcceptedStatuses.Contains(l.Status))
                .Where(l => !Reviews.Any(r => r.LeadId == l.Id))
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefault());

        /// <summary>Giả lập lượt gửi khác vừa chiếm mất yêu cầu liên hệ (DB từ chối vì index duy nhất).</summary>
        public bool SimulateConcurrentDuplicate { get; set; }

        public Task<bool> TryAddAsync(CenterReview review, CancellationToken ct = default)
        {
            if (SimulateConcurrentDuplicate || (review.LeadId is not null && Reviews.Any(r => r.LeadId == review.LeadId)))
                return Task.FromResult(false);
            Reviews.Add(review);
            return Task.FromResult(true);
        }

        public Task<CenterReviewDto?> GetPublicDtoAsync(Guid reviewId, CancellationToken ct = default) =>
            Task.FromResult(Reviews.Where(r => r.Id == reviewId)
                .Select(r => (CenterReviewDto?)new CenterReviewDto(r.Id, "Nguyễn V. A.", UserRoles.Student, r.IsVerified, r.CreatedAt, r.Rating, r.Comment))
                .FirstOrDefault());
    }

    private sealed class FakeCenterRepo : IStudyCenterRepository
    {
        public List<StudyCenter> Centers { get; } = [];
        public Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));
        public Task<StudyCenter?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));
        public Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(StudyCenter center, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(StudyCenter center, CancellationToken ct = default) => Task.CompletedTask;
        public Task<StudyCenter?> GetByOwnerOrEmailAsync(Guid ownerUserId, string? email, CancellationToken ct = default) => throw new NotImplementedException();
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

    private sealed class Setup
    {
        public FakeSubmissionRepo Repo { get; } = new();
        public FakeCenterRepo Centers { get; } = new();
        public FakeUserRepository Users { get; } = new();
        public FakeAudit Audit { get; } = new();
        public StudyCenter Center { get; } = new() { Id = Guid.NewGuid(), Name = "Trung tâm A", IsActive = true, VerificationStatus = CenterVerificationStatuses.Verified };
        public CenterReviewService Service { get; }

        public Setup()
        {
            Centers.Centers.Add(Center);
            Service = new CenterReviewService(Repo, Centers, Users, Audit);
        }

        public User AddUser(string role = UserRoles.Student)
        {
            var user = new User { Id = Guid.NewGuid(), FullName = "Nguyễn Văn An", Role = role };
            Users.Users.Add(user);
            return user;
        }

        public Lead AddLead(Guid studentId, Guid? submitterId = null, string status = LeadStatuses.Accepted)
        {
            var lead = new Lead { UserId = studentId, SubmittedByUserId = submitterId ?? studentId, StudyCenterId = Center.Id, Status = status };
            Repo.Leads.Add(lead);
            return lead;
        }
    }

    [Fact]
    public async Task Create_AfterContactRequest_SavesVerifiedVisibleReviewLinkedToLeadAndLogsAudit()
    {
        var s = new Setup();
        var student = s.AddUser();
        var lead = s.AddLead(student.Id);

        var dto = await s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(5, "  Tư vấn nhiệt tình  "));

        var saved = Assert.Single(s.Repo.Reviews);
        Assert.Equal(5, saved.Rating);
        Assert.Equal("Tư vấn nhiệt tình", saved.Comment);
        Assert.True(saved.IsVerified);
        Assert.Equal("visible", saved.Status);
        Assert.Equal(lead.Id, saved.LeadId);
        Assert.Equal(s.Center.Id, saved.StudyCenterId);
        Assert.Equal(saved.Id, dto.Id);
        Assert.Equal("center_review.create", Assert.Single(s.Audit.Logs).Action);
    }

    [Fact]
    public async Task Create_WithoutComment_StoresNullComment()
    {
        var s = new Setup();
        var student = s.AddUser();
        s.AddLead(student.Id);

        await s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(3, "   "));

        Assert.Null(Assert.Single(s.Repo.Reviews).Comment);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task Create_RatingOutOfRange_IsRejected(int rating)
    {
        var s = new Setup();
        var student = s.AddUser();
        s.AddLead(student.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(rating)));
        Assert.Empty(s.Repo.Reviews);
    }

    [Fact]
    public async Task Create_CommentTooLong_IsRejected()
    {
        var s = new Setup();
        var student = s.AddUser();
        s.AddLead(student.Id);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(4, new string('a', CenterReviewService.MaxCommentLength + 1))));
        Assert.Empty(s.Repo.Reviews);
    }

    [Fact]
    public async Task Create_WithoutContactRequest_IsRejectedWithReason()
    {
        var s = new Setup();
        var student = s.AddUser();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(5)));

        Assert.Contains("gửi yêu cầu liên hệ", ex.Message);
        Assert.Empty(s.Repo.Reviews);
    }

    [Fact]
    public async Task Create_ConcurrentDuplicateRejectedByDatabase_IsReportedAsAlreadyReviewedAndNotAudited()
    {
        var s = new Setup();
        var student = s.AddUser();
        s.AddLead(student.Id);
        s.Repo.SimulateConcurrentDuplicate = true;

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(4)));

        Assert.Equal(CenterReviewService.AlreadyReviewedMessage, ex.Message);
        Assert.Empty(s.Repo.Reviews);
        Assert.Empty(s.Audit.Logs);
    }

    [Fact]
    public async Task Create_SecondReviewForSameLead_IsRejected()
    {
        var s = new Setup();
        var student = s.AddUser();
        s.AddLead(student.Id);
        await s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(5));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(1)));

        Assert.Contains("đã được đánh giá", ex.Message);
        Assert.Single(s.Repo.Reviews);
    }

    [Fact]
    public async Task Create_ParentWhoSentTheRequest_CanReview_ButStudentOfSameLeadCannotReviewAgain()
    {
        var s = new Setup();
        var student = s.AddUser();
        var parent = s.AddUser(UserRoles.Parent);
        var lead = s.AddLead(student.Id, submitterId: parent.Id);

        await s.Service.CreateAsync(parent.Id, s.Center.Id, new CreateCenterReviewRequest(4));

        // một yêu cầu một đánh giá: học sinh của yêu cầu đó không đánh giá thêm được
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(5)));
        Assert.Equal(lead.Id, Assert.Single(s.Repo.Reviews).LeadId);
    }

    [Fact]
    public async Task Create_NewLeadToSameCenterAfterReview_AllowsAnotherReviewForThatLead()
    {
        var s = new Setup();
        var student = s.AddUser();
        var first = s.AddLead(student.Id);
        first.CreatedAt = DateTime.UtcNow.AddDays(-10);
        await s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(3));
        Assert.False((await s.Service.GetEligibilityAsync(student.Id, s.Center.Id)).CanReview);

        var second = s.AddLead(student.Id);
        Assert.True((await s.Service.GetEligibilityAsync(student.Id, s.Center.Id)).CanReview);
        await s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(5));

        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(x => x), s.Repo.Reviews.Select(r => r.LeadId!.Value).OrderBy(x => x));
    }

    [Fact]
    public async Task Create_RemovedReviewStillConsumesTheLeadsOneReview()
    {
        var s = new Setup();
        var student = s.AddUser();
        s.AddLead(student.Id);
        await s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(1));
        s.Repo.Reviews[0].Status = "removed"; // Moderator gỡ (UC-57)

        var eligibility = await s.Service.GetEligibilityAsync(student.Id, s.Center.Id);

        Assert.False(eligibility.CanReview);
        Assert.True(eligibility.HasReviewed);
    }

    [Fact]
    public async Task Create_LeadNotAccepted_CannotReview()
    {
        var s = new Setup();
        var student = s.AddUser();
        s.AddLead(student.Id, status: LeadStatuses.New);

        var eligibility = await s.Service.GetEligibilityAsync(student.Id, s.Center.Id);
        Assert.False(eligibility.CanReview);
        Assert.False(eligibility.HasReviewed);
        Assert.Contains("tiếp nhận", eligibility.Reason);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(5)));
        Assert.Contains("tiếp nhận", ex.Message);
    }

    [Fact]
    public async Task Create_LeadSentToAnotherCenter_DoesNotCount()
    {
        var s = new Setup();
        var student = s.AddUser();
        s.Repo.Leads.Add(new Lead { UserId = student.Id, SubmittedByUserId = student.Id, StudyCenterId = Guid.NewGuid() });

        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(5)));
    }

    [Fact]
    public async Task Create_NonStudentOrParentRole_IsRejected()
    {
        var s = new Setup();
        var moderator = s.AddUser(UserRoles.Moderator);
        s.AddLead(moderator.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.CreateAsync(moderator.Id, s.Center.Id, new CreateCenterReviewRequest(5)));
    }

    [Theory]
    [InlineData("unverified", true)]
    [InlineData("suspended", true)]
    [InlineData("verified", false)]
    public async Task Create_CenterNotVerifiedOrInactive_IsNotFound(string status, bool expectNotFound)
    {
        var s = new Setup();
        var student = s.AddUser();
        s.AddLead(student.Id);
        s.Center.VerificationStatus = status;

        if (expectNotFound)
            await Assert.ThrowsAsync<KeyNotFoundException>(() => s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(5)));
        else
            await s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(5));
    }

    [Fact]
    public async Task Create_UnknownCenter_IsNotFound()
    {
        var s = new Setup();
        var student = s.AddUser();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => s.Service.CreateAsync(student.Id, Guid.NewGuid(), new CreateCenterReviewRequest(5)));
    }

    [Fact]
    public async Task Eligibility_ReflectsContactRequestAndExistingReview()
    {
        var s = new Setup();
        var student = s.AddUser();

        var before = await s.Service.GetEligibilityAsync(student.Id, s.Center.Id);
        Assert.False(before.CanReview);
        Assert.False(before.HasReviewed);

        s.AddLead(student.Id);
        var eligible = await s.Service.GetEligibilityAsync(student.Id, s.Center.Id);
        Assert.True(eligible.CanReview);
        Assert.Null(eligible.Reason);

        await s.Service.CreateAsync(student.Id, s.Center.Id, new CreateCenterReviewRequest(5));
        var after = await s.Service.GetEligibilityAsync(student.Id, s.Center.Id);
        Assert.False(after.CanReview);
        Assert.True(after.HasReviewed);
    }

    // ---- controller

    private sealed class FakeService : ICenterReviewService
    {
        public Exception? ToThrow { get; set; }
        public (Guid UserId, Guid CenterId, int Rating, string? Comment)? LastCreate { get; private set; }

        public Task<CenterReviewEligibilityDto> GetEligibilityAsync(Guid userId, Guid centerId, CancellationToken ct = default)
        {
            if (ToThrow is not null) throw ToThrow;
            return Task.FromResult(new CenterReviewEligibilityDto(true, false, null));
        }

        public Task<CenterReviewDto> CreateAsync(Guid userId, Guid centerId, CreateCenterReviewRequest request, CancellationToken ct = default)
        {
            if (ToThrow is not null) throw ToThrow;
            LastCreate = (userId, centerId, request.Rating, request.Comment);
            return Task.FromResult(new CenterReviewDto(Guid.NewGuid(), "Nguyễn V. A.", UserRoles.Student, true, DateTime.UtcNow, request.Rating, request.Comment));
        }
    }

    private static (CenterReviewsController Controller, FakeService Service) Create(Guid userId)
    {
        var service = new FakeService();
        var controller = new CenterReviewsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, UserRoles.Student),
                    ], "TestAuth"))
                }
            }
        };
        return (controller, service);
    }

    [Fact]
    public async Task Controller_Create_Returns201AndPassesArguments()
    {
        var userId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var (controller, service) = Create(userId);

        var result = await controller.Create(centerId, new CreateCenterReviewRequest(4, "Tốt"), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, obj.StatusCode);
        Assert.Equal((userId, centerId, 4, "Tốt"), service.LastCreate);
    }

    [Fact]
    public async Task Controller_RuleViolation_ReturnsValidationProblemAndMissingCenter_Returns404()
    {
        var (controller, service) = Create(Guid.NewGuid());
        var request = new CreateCenterReviewRequest(5);

        service.ToThrow = new ArgumentException("Bạn đã đánh giá trung tâm này.");
        Assert.IsAssignableFrom<ProblemDetails>(Assert.IsType<ObjectResult>(await controller.Create(Guid.NewGuid(), request, CancellationToken.None)).Value);

        service.ToThrow = new KeyNotFoundException("Trung tâm không tồn tại.");
        Assert.IsType<NotFoundObjectResult>(await controller.Create(Guid.NewGuid(), request, CancellationToken.None));
        Assert.IsType<NotFoundObjectResult>(await controller.GetEligibility(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Controller_Eligibility_ReturnsOk()
    {
        var (controller, _) = Create(Guid.NewGuid());

        var result = await controller.GetEligibility(Guid.NewGuid(), CancellationToken.None);

        Assert.True(Assert.IsType<CenterReviewEligibilityDto>(Assert.IsType<OkObjectResult>(result).Value).CanReview);
    }

    [Fact]
    public void Controller_IsRestrictedToStudentAndParent()
    {
        var roles = typeof(CenterReviewsController).GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single().Roles;

        Assert.Equal($"{UserRoles.Student},{UserRoles.Parent}", roles);
    }
}
