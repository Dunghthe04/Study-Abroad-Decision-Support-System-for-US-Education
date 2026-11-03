using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Profile.Academic;
using StudyAbroad.Application.Profile.Financial;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>
/// [USAS-364 / USAS-365] Kiểm thử bảo mật cho FinancialProfileController và AcademicAnalysisController:
/// - Đảm bảo Controller được bảo vệ bởi [Authorize(Roles = "student,parent")]
/// - Đảm bảo Controller có Policy bảo vệ trẻ em [Authorize(Policy = AgeFeatures.ProcessStudentData)]
/// - Không còn backdoor chấp nhận header X-User-Id khi chưa đăng nhập
/// </summary>
public class LegacyProfileControllersSecurityTests
{
    [Fact]
    public void FinancialProfileController_HasCorrectAuthorizeAttributes()
    {
        var authAttrs = typeof(FinancialProfileController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .ToList();

        Assert.NotEmpty(authAttrs);
        Assert.Contains(authAttrs, a => a.Roles == $"{UserRoles.Student},{UserRoles.Parent}");
        Assert.Contains(authAttrs, a => a.Policy == AgeFeatures.ProcessStudentData);
    }

    [Fact]
    public void AcademicAnalysisController_HasCorrectAuthorizeAttributes()
    {
        var authAttrs = typeof(AcademicAnalysisController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .ToList();

        Assert.NotEmpty(authAttrs);
        Assert.Contains(authAttrs, a => a.Roles == $"{UserRoles.Student},{UserRoles.Parent}");
        Assert.Contains(authAttrs, a => a.Policy == AgeFeatures.ProcessStudentData);
    }

    [Fact]
    public async Task FinancialProfileController_IgnoresXUserIdHeader_AndReturnsUnauthorized_WhenNoClaims()
    {
        var fakeService = new FakeFinancialProfileService();
        var controller = new FinancialProfileController(fakeService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        // Kẻ tấn công cố tình truyền header X-User-Id
        controller.HttpContext.Request.Headers["X-User-Id"] = Guid.NewGuid().ToString();

        var result = await controller.GetFinancialProfile(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task AcademicAnalysisController_IgnoresXUserIdHeader_AndReturnsUnauthorized_WhenNoClaims()
    {
        var fakeService = new FakeAcademicAnalysisService();
        var controller = new AcademicAnalysisController(fakeService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        // Kẻ tấn công cố tình truyền header X-User-Id
        controller.HttpContext.Request.Headers["X-User-Id"] = Guid.NewGuid().ToString();

        var result = await controller.GetScores(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    private sealed class FakeFinancialProfileService : IFinancialProfileService
    {
        private static readonly FinancialProfileDto SampleDto = new(
            Guid.NewGuid(), Guid.NewGuid(), 25000m, "family", false, 20000m, "USD", null, DateTime.UtcNow, null);

        public Task<FinancialProfileDto?> GetFinancialProfileAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<FinancialProfileDto?>(SampleDto);

        public Task<FinancialProfileDto> SaveFinancialProfileAsync(Guid userId, SaveFinancialProfileRequest request, CancellationToken ct = default)
            => Task.FromResult(SampleDto);

        public Task<IReadOnlyList<ExtracurricularActivityDto>> GetActivitiesAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ExtracurricularActivityDto>>(Array.Empty<ExtracurricularActivityDto>());

        public Task<ExtracurricularActivityDto> AddActivityAsync(Guid userId, CreateExtracurricularRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ExtracurricularActivityDto?> UpdateActivityAsync(Guid userId, Guid activityId, UpdateExtracurricularRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<bool> DeleteActivityAsync(Guid userId, Guid activityId, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<StudentAchievementDto>> GetAchievementsAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StudentAchievementDto>>(Array.Empty<StudentAchievementDto>());

        public Task<StudentAchievementDto> AddAchievementAsync(Guid userId, CreateAchievementRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<bool> DeleteAchievementAsync(Guid userId, Guid achievementId, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<StudentProfileSummaryDto> GetFullProfileSummaryAsync(Guid userId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeAcademicAnalysisService : IAcademicAnalysisService
    {
        public Task<IReadOnlyList<TranscriptScoreDto>> GetScoresAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TranscriptScoreDto>>(Array.Empty<TranscriptScoreDto>());

        public Task<IReadOnlyList<TranscriptScoreDto>> SaveScoresAsync(Guid userId, BatchTranscriptScoresRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task DeleteScoreAsync(Guid userId, Guid scoreId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<AcademicAnalysisResponseDto> AnalyzeAcademicProfileAsync(Guid userId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<AcademicAnalysisResponseDto?> GetLatestAnalysisAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<AcademicAnalysisResponseDto?>(null);

        public Task<GradeScaleConfigDto> GetGradeScaleConfigAsync(CancellationToken ct = default)
            => Task.FromResult(new GradeScaleConfigDto("scale", "name", "table", new List<GradeScaleRuleDto>()));
    }
}
