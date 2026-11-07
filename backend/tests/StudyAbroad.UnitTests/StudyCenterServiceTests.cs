using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class StudyCenterServiceTests
{
    private sealed class FakeRepository : IStudyCenterRepository
    {
        public List<StudyCenter> Centers { get; } = [];

        public FakeRepository(params StudyCenter[] initial)
        {
            Centers.AddRange(initial);
        }

        public StudyCenterQuery? LastQuery { get; private set; }

        public Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default)
        {
            LastQuery = query;
            return Task.FromResult(new PagedResult<StudyCenter>(Centers, query.Page, query.PageSize, Centers.Count));
        }

        public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));

        public Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StudyCenter>>(Centers);

        public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(Centers.Any(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(StudyCenter center, CancellationToken ct = default)
        {
            Centers.Add(center);
            return Task.CompletedTask;
        }
    }

    private readonly FakeAuditLogRepository _auditLogs = new();

    [Fact]
    public async Task SearchAsync_ClampsPagingValues()
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);

        await service.SearchAsync(new StudyCenterQuery(Page: 0, PageSize: 1000));

        Assert.Equal(1, repo.LastQuery!.Page);
        Assert.Equal(100, repo.LastQuery.PageSize);
    }

    [Fact]
    public async Task GetByIdAsync_MapsEntityToDto()
    {
        var center = new StudyCenter { Code = "CTY_001", Name = "Test", Services = ["CHON_TRUONG"] };
        var service = new StudyCenterService(new FakeRepository(center), _auditLogs);

        var dto = await service.GetByIdAsync(center.Id);

        Assert.NotNull(dto);
        Assert.Equal("CTY_001", dto.Code);
        Assert.Contains("CHON_TRUONG", dto.Services);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        var service = new StudyCenterService(new FakeRepository(), _auditLogs);
        Assert.Null(await service.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesCenterAndWritesAudit()
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);
        var actorId = Guid.NewGuid();

        var request = new CreateStudyCenterRequest(
            Code: "CTY_NEW",
            Name: "Trung tâm Du học Mới",
            City: "Hà Nội",
            Address: "123 Cầu Giấy",
            Website: "https://example.com",
            Phone: "0912345678",
            Services: ["CHON_TRUONG", "LUYEN_PHONG_VAN"],
            StudyLevels: ["dai_hoc", "thac_si"]);

        var result = await service.CreateAsync(request, actorId);

        Assert.True(result.Success);
        Assert.NotNull(result.Center);
        Assert.Equal("CTY_NEW", result.Center.Code);
        Assert.Equal("Trung tâm Du học Mới", result.Center.Name);
        Assert.Equal("unverified", result.Center.VerificationStatus);
        Assert.True(result.Center.IsActive);

        var log = Assert.Single(_auditLogs.Entries);
        Assert.Equal("study_center.create", log.Action);
        Assert.Equal(actorId, log.ActorUserId);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_ReturnsFail()
    {
        var existing = new StudyCenter { Code = "DUPLICATE_CODE", Name = "Trung tâm cũ" };
        var repo = new FakeRepository(existing);
        var service = new StudyCenterService(repo, _auditLogs);

        var request = new CreateStudyCenterRequest(
            Code: "duplicate_code",
            Name: "Trung tâm trùng mã");

        var result = await service.CreateAsync(request, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains("đã tồn tại", result.ErrorMessage);
        Assert.Empty(_auditLogs.Entries);
    }

    [Theory]
    [InlineData("", "Tên hợp lệ", "Mã trung tâm")]
    [InlineData("CODE", "", "Tên trung tâm")]
    public async Task CreateAsync_EmptyRequiredFields_ReturnsFail(string code, string name, string expectedError)
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);

        var result = await service.CreateAsync(new CreateStudyCenterRequest(code, name), Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains(expectedError, result.ErrorMessage);
        Assert.Empty(_auditLogs.Entries);
    }

    [Fact]
    public async Task CreateAsync_WithEnhancedFields_MapsPropertiesCorrectly()
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);
        var actorId = Guid.NewGuid();

        var request = new CreateStudyCenterRequest(
            Code: "CTY_013",
            Name: "Summit Education Services",
            City: "Hà Nội",
            District: "Đống Đa",
            Address: "101 Láng Hạ",
            Website: "https://summit.edu.vn",
            Phone: "0901702889",
            Email: "contact@summit.edu.vn",
            CenterType: "hybrid",
            LogoUrl: "https://example.com/logo.png",
            CoverImageUrl: "https://example.com/cover.png",
            GalleryImages: ["https://example.com/img1.png", "https://example.com/img2.png"],
            StaffCount: 65,
            EstablishedYear: 2010,
            LicenseNumber: "GPHĐ-GD-2010/HN-088",
            Accreditations: ["AIRC", "ICEF"],
            VisaSuccessRate: 99.2,
            Services: ["LUYEN_THI_SAT", "SAN_HOC_BONG"]);

        var result = await service.CreateAsync(request, actorId);

        Assert.True(result.Success);
        Assert.NotNull(result.Center);
        Assert.Equal("hybrid", result.Center.CenterType);
        Assert.Equal("Đống Đa", result.Center.District);
        Assert.Equal(65, result.Center.StaffCount);
        Assert.Equal(2010, result.Center.EstablishedYear);
        Assert.Equal("GPHĐ-GD-2010/HN-088", result.Center.LicenseNumber);
        Assert.Equal(99.2, result.Center.VisaSuccessRate);
        Assert.Equal(2, result.Center.GalleryImages?.Count);
        Assert.Equal(2, result.Center.Accreditations?.Count);
    }

    [Fact]
    public void StudyCentersController_ActionAttributes_MatchRbacPolicy()
    {
        // UC-71: Thêm trung tâm vào danh bạ - Admin hoặc Moderator được phép
        var createAttr = typeof(StudyCentersController).GetMethod(nameof(StudyCentersController.Create))?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(createAttr);
        Assert.Equal($"{UserRoles.Admin},{UserRoles.Moderator}", createAttr.Roles);

        // UC-64: Tạo tài khoản đại diện trung tâm - Admin hoặc Moderator được phép
        var createAccountAttr = typeof(StudyCentersController).GetMethod(nameof(StudyCentersController.CreateCenterAccount))?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(createAccountAttr);
        Assert.Equal($"{UserRoles.Admin},{UserRoles.Moderator}", createAccountAttr.Roles);
    }
}
